using InstallationStatusUpdater.Application;
using InstallationStatusUpdater.Domain.Results;
using InstallationStatusUpdater.Presentation;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using PlayniteUtilitiesCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace InstallationStatusUpdater
{
    public class InstallationStatusUpdater : GenericPlugin
    {
        private readonly DispatcherTimer _initiateStatusDetectionTimer;
        private readonly DirectoryWatcherService _dirWatcher;
        private readonly DeviceEventWatcherService _deviceWatcher;
        private readonly GamesInstallationStatusScanner _statusScanner;
        private readonly TagsUpdater _tagUpdater;
        private readonly PathsResolver _pathResolver;
        private readonly DiskConcurrencyLimiter _diskLimiter;
        private readonly ILogger _logger;
        private readonly InstallationStatusUpdaterSettingsViewModel _settings;
        private readonly HashSet<string> _pendingScanPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _pendingScanPathsLock = new object();

        public override Guid Id { get; } = Guid.Parse("ed9c467f-5ab5-478f-a09f-936146188ad0");

        public InstallationStatusUpdater(IPlayniteAPI _playniteApi) : base(_playniteApi)
        {
            _logger = LogManager.GetLogger();
            _settings = new InstallationStatusUpdaterSettingsViewModel(this, _playniteApi);
            Properties = new GenericPluginProperties { HasSettings = true };

            _initiateStatusDetectionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _initiateStatusDetectionTimer.Tick += (s, e) =>
            {
                _initiateStatusDetectionTimer.Stop();
                RunPendingStatusDetection();
            };

            // Built after the settings because it reads the concurrency limits from them, and
            // handed to both so the settings window can show how each disk was classified.
            _diskLimiter = new DiskConcurrencyLimiter(GetParallelismForDiskKind);
            _settings.DiskLimiter = _diskLimiter;

            _dirWatcher = new DirectoryWatcherService(_logger, _settings);
            _deviceWatcher = new DeviceEventWatcherService(_logger, _settings);
            _pathResolver = new PathsResolver(_playniteApi, _settings);
            _statusScanner = new GamesInstallationStatusScanner(
                _playniteApi, _logger, _settings, _pathResolver, _diskLimiter);
            _tagUpdater = new TagsUpdater(_playniteApi, _logger);

            _dirWatcher.OnTrigger = OnDirectoryChanged;
            _deviceWatcher.OnTrigger = OnStorageDeviceChanged;
            _dirWatcher.StartWatching();

            DirectorySizeCache.Load(GetCacheFilePath());
        }

        private string GetCacheFilePath() => Path.Combine(GetPluginUserDataPath(), "dir_size_cache.json");

        private int GetParallelismForDiskKind(DiskKind diskKind)
        {
            switch (diskKind)
            {
                case DiskKind.SolidState:
                    return _settings.Settings.ParallelismSolidState;
                case DiskKind.Mechanical:
                    return _settings.Settings.ParallelismMechanical;
                default:
                    return _settings.Settings.ParallelismUnknown;
            }
        }

        private void RestartTimer()
        {
            var dispatcher = _initiateStatusDetectionTimer.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                _initiateStatusDetectionTimer.Stop();
                _initiateStatusDetectionTimer.Start();
            }
            else
            {
                dispatcher.BeginInvoke(new Action(() =>
                {
                    _initiateStatusDetectionTimer.Stop();
                    _initiateStatusDetectionTimer.Start();
                }));
            }
        }

        /// <summary>
        /// Rebuilds the directory watchers so that changes made in the settings take effect right
        /// away. Without this the watchers would only be rebuilt at the next library update or at
        /// the next start of Playnite, which makes saving the settings look like it did nothing.
        /// </summary>
        public void RefreshDirectoryWatchers()
        {
            // Null while the constructor is still running, since the settings view model that
            // calls this is built before the watcher exists.
            _dirWatcher?.StartWatching();
        }

        private void OnDirectoryChanged(string changedPath)
        {
            lock (_pendingScanPathsLock)
            {
                _pendingScanPaths.Add(changedPath);
            }

            RestartTimer();
        }

        private void OnStorageDeviceChanged()
        {
            lock (_pendingScanPathsLock)
            {
                // A device change can affect games anywhere, so the next scan cannot be scoped to a path.
                _pendingScanPaths.Clear();
            }

            // A drive letter can now point at a different disk entirely, so no remembered folder
            // measurement can be trusted.
            DirectorySizeCache.Clear();
            RestartTimer();
        }

        private void RunPendingStatusDetection()
        {
            // Scoping a scan to more paths than this means the watched tree changed too much to be
            // meaningfully filtered, so a full scan is cheaper and simpler to reason about.
            var maxScopedScanPaths = _settings.Settings.MaxScopedScanPaths;

            List<string> scopePaths;
            lock (_pendingScanPathsLock)
            {
                scopePaths = _pendingScanPaths.Count == 0 || _pendingScanPaths.Count > maxScopedScanPaths
                    ? null
                    : _pendingScanPaths.ToList();

                _pendingScanPaths.Clear();
            }

            DropStaleSizeMeasurements(scopePaths);
            _statusScanner.DetectInstallationStatus(false, scopePaths: scopePaths);
        }

        /// <summary>
        /// Forgets the folder sizes that the reported changes can have made wrong. Only the
        /// affected folders are dropped, so the rest of the cache still saves the next scan from
        /// walking the whole library again.
        /// </summary>
        private static void DropStaleSizeMeasurements(List<string> scopePaths)
        {
            if (scopePaths is null)
            {
                DirectorySizeCache.Clear();
                return;
            }

            foreach (var scopePath in scopePaths)
            {
                DirectorySizeCache.Invalidate(scopePath);
            }
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            base.OnApplicationStopped(args);
            DirectorySizeCache.Save(GetCacheFilePath());
            _dirWatcher.Dispose();
            _deviceWatcher.Dispose();
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            if (System.Windows.Application.Current.MainWindow is Window mainWindow)
            {
                var interop = new WindowInteropHelper(mainWindow);
                _deviceWatcher.HookDeviceEvents(interop);
            }
            else
            {
                _logger.Debug("[InstallationStatusUpdater] Main window not found.");
            }

            if (_settings.Settings.UpdateOnStartup)
            {
                _statusScanner.DetectInstallationStatus(false);
            }
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            if (_settings.Settings.UpdateOnLibraryUpdate)
            {
                _statusScanner.DetectInstallationStatusWithProgressDialog(false);
            }

            if (_settings.Settings.UpdateLocTagsOnLibUpdate)
            {
                _tagUpdater.UpdateTagsWithInstallationRoot(PlayniteApi.Database.Games);
            }

            _dirWatcher.StartWatching();
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return _settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new InstallationStatusUpdaterSettingsView();
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {

            return new List<MainMenuItem>
            {
                new MainMenuItem
                {
                    Description = ResourceProvider.GetString("LOCInstallation_Status_Updater_MenuItemStatusUpdaterDescription"),
                    MenuSection = "@Installation Status Updater",
                    Action = _ => _statusScanner.DetectInstallationStatusWithProgressDialog(true),
                    Icon = PlayniteUtilities.GetIcoFontGlyphResource('\uEFD1')
                },
                new MainMenuItem
                {
                    Description = ResourceProvider.GetString("LOCInstallation_Status_Updater_MenuItemRevertLastScanDescription"),
                    MenuSection = "@Installation Status Updater",
                    Action = _ => _statusScanner.RevertLastStatusUpdatesWithProgressDialog(),
                    Icon = PlayniteUtilities.GetIcoFontGlyphResource('\uEFD2')
                },
                new MainMenuItem
                {
                    Description = ResourceProvider.GetString("LOCInstallation_Status_Updater_MenuUpdateDriveInstallTagDescription"),
                    MenuSection = "@Installation Status Updater",
                    Action = _ =>
                    {
                        _tagUpdater.UpdateTagsWithInstallationRoot(PlayniteApi.Database.Games);
                        PlayniteApi.Dialogs.ShowMessage(ResourceProvider.GetString("LOCInstallation_Status_Updater_StatusUpdaterUpdatingTagsFinishMessage"), "Installation Status Updater");
                    },
                    Icon = PlayniteUtilities.GetIcoFontGlyphResource('\uEFD1')
                },
                new MainMenuItem
                {
                    Description = ResourceProvider.GetString("LOCInstallation_Status_Updater_MenuScanOrphanFoldersDescription"),
                    MenuSection = "@Installation Status Updater",
                    Action = _ => ScanOrphanFolders(),
                    Icon = PlayniteUtilities.GetIcoFontGlyphResource('\uEFD1')
                }
            };
        }

        private void ScanOrphanFolders()
        {
            var enabledDirs = _settings.Settings.DetectionDirectories?.Where(d => d.Enabled).ToList();
            if (enabledDirs == null || enabledDirs.Count == 0)
            {
                var msg = ResourceProvider.GetString("LOCInstallation_Status_Updater_NoDetectionDirsConfiguredMessage");
                if (string.IsNullOrWhiteSpace(msg))
                {
                    msg = "Nenhum diretório de detecção habilitado. Configure os diretórios nas configurações da extensão.";
                }
                PlayniteApi.Dialogs.ShowMessage(msg, "Installation Status Updater");
                return;
            }

            var progressOptions = new GlobalProgressOptions(
                ResourceProvider.GetString("LOCInstallation_Status_Updater_OrphanScannerProgressTitle") ?? "Procurando pastas órfãs de jogos...")
            {
                Cancelable = true,
                IsIndeterminate = false
            };

            List<OrphanFolderData> orphanFolders = null;
            PlayniteApi.Dialogs.ActivateGlobalProgress(
                args =>
                {
                    orphanFolders = OrphanFoldersScanner.Scan(
                        PlayniteApi,
                        enabledDirs,
                        progressText => args.Text = progressText,
                        args.CancelToken);
                },
                progressOptions);

            if (orphanFolders == null || orphanFolders.Count == 0)
            {
                var noOrphansMsg = ResourceProvider.GetString("LOCInstallation_Status_Updater_NoOrphansFoundMessage");
                if (string.IsNullOrWhiteSpace(noOrphansMsg))
                {
                    noOrphansMsg = "Nenhuma pasta órfã encontrada. Todas as pastas de jogos encontradas pertencem a jogos cadastrados na sua biblioteca!";
                }
                PlayniteApi.Dialogs.ShowMessage(noOrphansMsg, "Installation Status Updater");
                return;
            }

            var window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = true
            });

            window.Height = 520;
            window.Width = 840;
            window.Title = ResourceProvider.GetString("LOCInstallation_Status_Updater_OrphanWindowTitle") ?? "Pastas Órfãs Detectadas";
            window.Content = new OrphanFoldersWindow();
            window.DataContext = new OrphanFoldersWindowViewModel(orphanFolders);
            window.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            window.ShowDialog();
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            return new List<GameMenuItem>
            {
                new GameMenuItem
                {
                    Description = ResourceProvider.GetString("LOCInstallation_Status_Updater_MenuScanSelectedGamesDescription"),
                    MenuSection = "Installation Status Updater",
                    Action = a => ScanSelectedGames(a.Games),
                    Icon = PlayniteUtilities.GetIcoFontGlyphResource('\uEFD1')
                },
                new GameMenuItem
                {
                    Description = ResourceProvider.GetString("LOCInstallation_Status_Updater_MenuAddIgnoreFeatureDescription"),
                    MenuSection = "Installation Status Updater",
                    Action = a => AddIgnoreFeature(a.Games.Distinct()),
                    Icon = PlayniteUtilities.GetIcoFontGlyphResource('\uEF22')
                },
                new GameMenuItem
                {
                    Description = ResourceProvider.GetString("LOCInstallation_Status_Updater_MenuRemoveIgnoreFeatureDescription"),
                    MenuSection = "Installation Status Updater",
                    Action = a => RemoveIgnoreFeature(a.Games.Distinct()),
                    Icon = PlayniteUtilities.GetIcoFontGlyphResource('\uEF21')
                }
            };
        }

        /// <summary>
        /// Scans only the games picked in the game list, which is the quickest way to check a
        /// handful of games without waiting for the whole library to be walked.
        /// </summary>
        private void ScanSelectedGames(IEnumerable<Game> games)
        {
            var gameIds = games?.Select(x => x.Id).Distinct().ToList();
            _statusScanner.DetectInstallationStatusForGames(gameIds);
        }

        private void AddIgnoreFeature(IEnumerable<Game> games)
        {
            var count = PlayniteUtilities.AddFeatureToGames(PlayniteApi, games, "[Status Updater] Ignore");
            PlayniteApi.Dialogs.ShowMessage(
                string.Format(ResourceProvider.GetString("LOCInstallation_Status_Updater_StatusUpdaterAddIgnoreFeatureMessage"), count),
                "Installation Status Updater");
        }

        private void RemoveIgnoreFeature(IEnumerable<Game> games)
        {
            var count = PlayniteUtilities.RemoveFeatureFromGames(PlayniteApi, games, "[Status Updater] Ignore");
            PlayniteApi.Dialogs.ShowMessage(
                string.Format(ResourceProvider.GetString("LOCInstallation_Status_Updater_StatusUpdaterRemoveIgnoreFeatureMessage"), count),
                "Installation Status Updater");
        }
    }
}
