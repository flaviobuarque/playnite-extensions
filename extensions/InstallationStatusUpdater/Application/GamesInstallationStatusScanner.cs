using InstallationStatusUpdater.Domain.Results;
using InstallationStatusUpdater.Presentation;
using Playnite.SDK;
using Playnite.SDK.Models;
using PlayniteUtilitiesCommon;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace InstallationStatusUpdater.Application
{
    public class GamesInstallationStatusScanner
    {
        private readonly IPlayniteAPI _playniteApi;
        private readonly ILogger _logger;
        private readonly InstallationStatusUpdaterSettingsViewModel _settings;
        private readonly PathsResolver _pathsResolver;
        private readonly DiskConcurrencyLimiter _diskLimiter;
        private StatusUpdateResults _lastUpdateResults;
        private StatusUpdateResults _pendingUpdateResults;

        private const string ScanSkipFeatureName = "[Status Updater] Ignore";
        public const string StatusResultsNotificationId = "InstallationStatusUpdater_ScanResults";
        private readonly ConcurrentDictionary<string, int> _disconnectedDriveSkippedGames =
            new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // How many games may be probed at once across the whole scan. This is only a ceiling:
        // DiskConcurrencyLimiter then holds each probe back until the disk it is about to read
        // has a free slot, since solid state and mechanical drives need very different amounts
        // of concurrency to run at their best.
        private const int DetectionParallelism = 32;

        public GamesInstallationStatusScanner(
            IPlayniteAPI playniteApi,
            ILogger logger,
            InstallationStatusUpdaterSettingsViewModel settings,
            PathsResolver pathResolver,
            DiskConcurrencyLimiter diskLimiter)
        {
            _playniteApi = playniteApi ?? throw new ArgumentNullException(nameof(playniteApi));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _pathsResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
            _diskLimiter = diskLimiter ?? throw new ArgumentNullException(nameof(diskLimiter));
        }

        public StatusUpdateResults DetectInstallationStatusWithProgressDialog(bool showResultsDialog)
        {
            var progressOptions = new GlobalProgressOptions(
                ResourceProvider.GetString("LOCInstallation_Status_Updater_StatusUpdaterUpdatingInstallStatusProgressMessage"))
            {
                Cancelable = true,
                IsIndeterminate = true
            };

            StatusUpdateResults results = null;
            var progressTextFormat = ResourceProvider.GetString(
                "LOCInstallation_Status_Updater_StatusUpdaterUpdatingInstallStatusOfGameProgressMessage");

            _playniteApi.Dialogs.ActivateGlobalProgress(
                args =>
                {
                    results = DetectInstallationStatusInternal(
                        args.CancelToken,
                        scopePaths: null,
                        progressReporter: (current, total, gameName) =>
                        {
                            args.IsIndeterminate = false;
                            args.ProgressMaxValue = total;
                            args.CurrentProgressValue = current;
                            args.Text = string.Format(progressTextFormat, current, total, gameName);
                        });
                },
                progressOptions);

            OpenWindowOrAddResultsNotification(showResultsDialog, results);
            return results;
        }

        public StatusUpdateResults DetectInstallationStatus(
            bool showResultsDialog,
            CancellationToken cancelToken = default,
            ICollection<string> scopePaths = null,
            ICollection<Guid> scopeGameIds = null)
        {
            var results = DetectInstallationStatusInternal(cancelToken, scopePaths, scopeGameIds: scopeGameIds);
            OpenWindowOrAddResultsNotification(showResultsDialog, results);
            return results;
        }

        /// <summary>
        /// Scans only the given games, which is what the game list context menu asks for.
        /// </summary>
        public StatusUpdateResults DetectInstallationStatusForGames(
            ICollection<Guid> gameIds,
            bool showResultsDialog = true)
        {
            if (gameIds is null || gameIds.Count == 0)
            {
                return new StatusUpdateResults();
            }

            var progressOptions = new GlobalProgressOptions(
                ResourceProvider.GetString("LOCInstallation_Status_Updater_StatusUpdaterUpdatingInstallStatusProgressMessage"))
            {
                Cancelable = true,
                IsIndeterminate = true
            };

            StatusUpdateResults results = null;
            var progressTextFormat = ResourceProvider.GetString(
                "LOCInstallation_Status_Updater_StatusUpdaterUpdatingInstallStatusOfGameProgressMessage");

            _playniteApi.Dialogs.ActivateGlobalProgress(
                args =>
                {
                    results = DetectInstallationStatusInternal(
                        args.CancelToken,
                        scopePaths: null,
                        scopeGameIds: gameIds,
                        progressReporter: (current, total, gameName) =>
                        {
                            args.IsIndeterminate = false;
                            args.ProgressMaxValue = total;
                            args.CurrentProgressValue = current;
                            args.Text = string.Format(progressTextFormat, current, total, gameName);
                        });
                },
                progressOptions);

            OpenWindowOrAddResultsNotification(showResultsDialog, results);
            return results;
        }

        public StatusUpdateResults DetectInstallationStatusInternal(
            CancellationToken cancelToken,
            ICollection<string> scopePaths = null,
            ICollection<Guid> scopeGameIds = null,
            Action<int, int, string> progressReporter = null)
        {
            _disconnectedDriveSkippedGames.Clear();
            var games = FilterGamesInScope(_playniteApi.Database.Games, scopePaths, scopeGameIds).ToArray();

            // Detection only reads from the database and the file system, so it can run in
            // parallel. The database is only written to afterwards, in a second, serialized pass.
            var detectionOutcomes = DetectOutcomes(games, cancelToken, progressReporter);

            var updateResults = new StatusUpdateResults();
            if (_settings.Settings.ConfirmStatusChangesBeforeApplying)
            {
                // Only report what would change and leave the database untouched until the user
                // applies the results.
                CollectStatusChanges(games, detectionOutcomes, updateResults, cancelToken, writeToDatabase: false);
                _pendingUpdateResults = updateResults.HasChanges ? updateResults : null;
            }
            else
            {
                using (_playniteApi.Database.BufferedUpdate())
                {
                    CollectStatusChanges(games, detectionOutcomes, updateResults, cancelToken, writeToDatabase: true);
                }

                if (updateResults.HasChanges)
                {
                    // Only scans that changed something can be reverted, so a scan without changes
                    // must not discard the results of the previous one.
                    _lastUpdateResults = updateResults;
                }

                _pendingUpdateResults = null;
            }

            return updateResults;
        }

        private void CollectStatusChanges(
            Game[] games,
            InstallationDetectionOutcome[] detectionOutcomes,
            StatusUpdateResults updateResults,
            CancellationToken cancelToken,
            bool writeToDatabase)
        {
            for (var i = 0; i < games.Length; i++)
            {
                if (cancelToken.IsCancellationRequested)
                {
                    break;
                }

                var outcome = detectionOutcomes[i];
                if (outcome is null)
                {
                    continue;
                }

                var game = games[i];
                try
                {
                    if (game.IsInstalled && outcome.Result == InstallationDetectionResult.Uninstalled)
                    {
                        if (writeToDatabase)
                        {
                            game.IsInstalled = false;
                            _playniteApi.Database.Games.Update(game);
                        }

                        updateResults.AddUninstalled(
                            game, outcome.Reason, outcome.MeasuredBytes, outcome.SizeReachedThreshold);
                    }
                    else if (!game.IsInstalled && outcome.Result == InstallationDetectionResult.Installed)
                    {
                        if (writeToDatabase)
                        {
                            game.IsInstalled = true;
                            _playniteApi.Database.Games.Update(game);
                        }

                        updateResults.AddInstalled(
                            game, outcome.Reason, outcome.MeasuredBytes, outcome.SizeReachedThreshold);
                    }
                }
                catch (Exception e)
                {
                    _logger.Error(e, $"[GameStatusScanner] Failed to update status of game '{game.Name}' (ID: {game.Id})");
                }
            }
        }

        public void ApplyPendingStatusChangesWithProgressDialog(ICollection<Guid> gameIds)
        {
            var progressOptions = new GlobalProgressOptions(
                ResourceProvider.GetString("LOCInstallation_Status_Updater_StatusUpdaterApplyingProgressMessage"))
            {
                Cancelable = true,
                IsIndeterminate = true
            };

            var appliedCount = 0;
            _playniteApi.Dialogs.ActivateGlobalProgress(
                args => appliedCount = ApplyPendingStatusChanges(gameIds, args.CancelToken),
                progressOptions);

            _playniteApi.Dialogs.ShowMessage(
                string.Format(
                    ResourceProvider.GetString("LOCInstallation_Status_Updater_StatusUpdaterAppliedMessage"),
                    appliedCount),
                "Installation Status Updater");
        }

        /// <summary>
        /// Writes the changes proposed by the last scan. Returns the number of games updated.
        /// </summary>
        public int ApplyPendingStatusChanges(
            ICollection<Guid> gameIds,
            CancellationToken cancelToken = default)
        {
            var pendingUpdateResults = _pendingUpdateResults;
            if (pendingUpdateResults is null || !pendingUpdateResults.HasChanges)
            {
                return 0;
            }

            // Games listed as installed have to be set to true and games listed as uninstalled to
            // false, which is the inverse of reverting.
            var appliedResults = new StatusUpdateResults();
            var appliedIds = new HashSet<Guid>();
            using (_playniteApi.Database.BufferedUpdate())
            {
                UpdateStatuses(pendingUpdateResults.Installed, true, gameIds, cancelToken, appliedResults, appliedIds);
                UpdateStatuses(pendingUpdateResults.Uninstalled, false, gameIds, cancelToken, appliedResults, appliedIds);
            }

            foreach (var appliedId in appliedIds)
            {
                pendingUpdateResults.Remove(appliedId);
            }

            _pendingUpdateResults = pendingUpdateResults.HasChanges ? pendingUpdateResults : null;
            if (_pendingUpdateResults is null)
            {
                _playniteApi.Notifications.Remove(StatusResultsNotificationId);
            }

            if (appliedResults.HasChanges)
            {
                _lastUpdateResults = appliedResults;
            }

            return appliedIds.Count;
        }

        public void ClearPendingStatusChanges()
        {
            _pendingUpdateResults = null;
            _playniteApi.Notifications.Remove(StatusResultsNotificationId);
        }

        private void UpdateStatuses(
            IReadOnlyList<UpdatedEntryData> entries,
            bool newIsInstalled,
            ICollection<Guid> gameIds,
            CancellationToken cancelToken,
            StatusUpdateResults updatedResults,
            HashSet<Guid> handledIds)
        {
            foreach (var entry in entries)
            {
                if (cancelToken.IsCancellationRequested)
                {
                    break;
                }

                if (gameIds != null && gameIds.Count > 0 && !gameIds.Contains(entry.Id))
                {
                    continue;
                }

                if (!handledIds.Add(entry.Id))
                {
                    continue;
                }

                var game = _playniteApi.Database.Games.Get(entry.Id);
                if (game is null)
                {
                    continue;
                }

                try
                {
                    game.IsInstalled = newIsInstalled;
                    _playniteApi.Database.Games.Update(game);
                    if (newIsInstalled)
                    {
                        updatedResults.AddInstalled(game, entry.DetectionReason);
                    }
                    else
                    {
                        updatedResults.AddUninstalled(game, entry.DetectionReason);
                    }
                }
                catch (Exception e)
                {
                    _logger.Error(e, $"[GameStatusScanner] Failed to update status of game '{game.Name}' (ID: {game.Id})");
                }
            }
        }

        public void RevertLastStatusUpdatesWithProgressDialog()
        {
            RevertStatusUpdatesWithProgressDialog(null);
        }

        /// <summary>
        /// Reverts the status of the given games, or of every game changed by the last scan when
        /// no game is given, and reports the result to the user.
        /// </summary>
        public void RevertStatusUpdatesWithProgressDialog(ICollection<Guid> gameIds)
        {
            var progressOptions = new GlobalProgressOptions(
                ResourceProvider.GetString("LOCInstallation_Status_Updater_StatusUpdaterRevertingProgressMessage"))
            {
                Cancelable = true,
                IsIndeterminate = true
            };

            var revertedCount = 0;
            _playniteApi.Dialogs.ActivateGlobalProgress(
                args => revertedCount = gameIds is null
                    ? RevertLastStatusUpdates(args.CancelToken)
                    : RevertStatusUpdates(gameIds, args.CancelToken),
                progressOptions);

            _playniteApi.Notifications.Remove(StatusResultsNotificationId);

            _playniteApi.Dialogs.ShowMessage(
                string.Format(
                    ResourceProvider.GetString("LOCInstallation_Status_Updater_StatusUpdaterRevertedMessage"),
                    revertedCount),
                "Installation Status Updater");
        }

        /// <summary>
        /// Restores the installation status the games had before the last scan that changed
        /// something. Returns the number of games that were restored.
        /// </summary>
        public int RevertLastStatusUpdates(CancellationToken cancelToken = default)
        {
            var lastUpdateResults = _lastUpdateResults;
            if (lastUpdateResults is null || !lastUpdateResults.HasChanges)
            {
                return 0;
            }

            var allGameIds = lastUpdateResults.Installed
                .Concat(lastUpdateResults.Uninstalled)
                .Select(x => x.Id)
                .ToList();

            return RevertStatusUpdates(allGameIds, cancelToken);
        }

        /// <summary>
        /// Restores the installation status of the given games, as long as they were changed by the
        /// last scan. Returns the number of games that were restored.
        /// </summary>
        public int RevertStatusUpdates(
            IEnumerable<Guid> gameIds,
            CancellationToken cancelToken = default)
        {
            var lastUpdateResults = _lastUpdateResults;
            if (lastUpdateResults is null || !lastUpdateResults.HasChanges)
            {
                return 0;
            }

            // Games set as installed had their status forced to false before the scan and games
            // set as uninstalled had it forced to true, so reverting is simply the inverse.
            var previousStates = new Dictionary<Guid, bool>();
            foreach (var gameId in gameIds)
            {
                if (previousStates.ContainsKey(gameId) ||
                    !lastUpdateResults.TryGetPreviousIsInstalled(gameId, out var previousIsInstalled))
                {
                    continue;
                }

                previousStates[gameId] = previousIsInstalled;
            }

            var revertedCount = 0;
            using (_playniteApi.Database.BufferedUpdate())
            {
                foreach (var previousState in previousStates)
                {
                    if (cancelToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (!RestorePreviousStatus(previousState.Key, previousState.Value))
                    {
                        continue;
                    }

                    revertedCount++;
                    // A reverted game is no longer pending, so the results only keep the games
                    // whose status change still stands.
                    lastUpdateResults.Remove(previousState.Key);
                }
            }

            return revertedCount;
        }

        private bool RestorePreviousStatus(Guid gameId, bool previousIsInstalled)
        {
            var game = _playniteApi.Database.Games.Get(gameId);
            if (game is null)
            {
                return false;
            }

            try
            {
                game.IsInstalled = previousIsInstalled;
                _playniteApi.Database.Games.Update(game);
                return true;
            }
            catch (Exception e)
            {
                _logger.Error(e, $"[GameStatusScanner] Failed to revert status of game '{game.Name}' (ID: {game.Id})");
                return false;
            }
        }

        private InstallationDetectionOutcome[] DetectOutcomes(
            Game[] games,
            CancellationToken cancelToken,
            Action<int, int, string> progressReporter)
        {
            var outcomes = new InstallationDetectionOutcome[games.Length];
            var processedCount = 0;

            Parallel.For(
                0,
                games.Length,
                new ParallelOptions { MaxDegreeOfParallelism = DetectionParallelism },
                (index, state) =>
                {
                    if (cancelToken.IsCancellationRequested)
                    {
                        state.Stop();
                        return;
                    }

                    var game = games[index];
                    try
                    {
                        using (_diskLimiter.Acquire(GetDiskHint(game)))
                        {
                            outcomes[index] = DetectGameInstallationStatus(game);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e, $"[GameStatusScanner] Failed to process game '{game.Name}' (ID: {game.Id})");
                    }

                    var currentCount = Interlocked.Increment(ref processedCount);
                    progressReporter?.Invoke(currentCount, games.Length, game.Name);
                });

            return outcomes;
        }

        private List<Game> FilterGamesInScope(
            IEnumerable<Game> games,
            ICollection<string> scopePaths,
            ICollection<Guid> scopeGameIds)
        {
            var gamesList = games.ToList();
            if (scopeGameIds is object && scopeGameIds.Count > 0)
            {
                // An explicit list of games is the narrowest scope there is, so no path is checked.
                return gamesList.Where(game => scopeGameIds.Contains(game.Id)).ToList();
            }

            if (scopePaths is null || scopePaths.Count == 0)
            {
                return gamesList;
            }

            return gamesList.Where(game => IsGameInScope(game, scopePaths)).ToList();
        }

        private bool IsGameInScope(Game game, ICollection<string> scopePaths)
        {
            string resolvedInstallationDirectory;
            try
            {
                resolvedInstallationDirectory = _pathsResolver.GetInstallDirForDetection(game);
            }
            catch (Exception e)
            {
                _logger.Error(e, $"[GameStatusScanner] Failed to resolve install directory of game '{game.Name}' (ID: {game.Id}).");
                // Without a usable path the game cannot be ruled in or out, so it is left to the scan.
                return true;
            }

            if (resolvedInstallationDirectory.IsNullOrEmpty())
            {
                return false;
            }

            return PathScopeMatcher.IsUnderAnyScope(resolvedInstallationDirectory, scopePaths);
        }

        private void OpenWindowOrAddResultsNotification(bool showResultsDialog, StatusUpdateResults statusUpdateResults)
        {
            if (statusUpdateResults is null)
            {
                return;
            }

            if (_disconnectedDriveSkippedGames.Count > 0)
            {
                var driveSummary = string.Join(", ", _disconnectedDriveSkippedGames.Select(x => $"{x.Key} ({x.Value})"));
                var driveMsgFormat = ResourceProvider.GetString("LOCInstallation_Status_Updater_DriveProtectionNotification");
                if (string.IsNullOrWhiteSpace(driveMsgFormat))
                {
                    driveMsgFormat = "Proteção de Unidade: Jogos em unidade desconectada foram preservados sem alteração: {0}";
                }
                _playniteApi.Notifications.Add(new NotificationMessage(
                    "InstallationStatusUpdater_DriveProtection",
                    string.Format(driveMsgFormat, driveSummary),
                    NotificationType.Info));
            }

            if (!statusUpdateResults.HasChanges)
            {
                _playniteApi.Notifications.Remove(StatusResultsNotificationId);
                if (showResultsDialog)
                {
                    ShowNoChangesPopup();
                }
                return;
            }

            var requiresConfirmation = _settings.Settings.ConfirmStatusChangesBeforeApplying;
            // Scans nobody started by hand can still be asked to show their results right away,
            // which saves the trip through the sidebar notification.
            var openAutomatically = showResultsDialog ||
                (_settings.Settings.ShowResultsWindowOnAutomaticScans && statusUpdateResults.HasChanges);

            if (openAutomatically)
            {
                _playniteApi.Notifications.Remove(StatusResultsNotificationId);
                OpenResultsWindow(statusUpdateResults, requiresConfirmation);
            }
            else if (_settings.Settings.ShowNotificationOnAutomaticScans && statusUpdateResults.HasChanges)
            {
                var notificationMessage = new NotificationMessage(
                    StatusResultsNotificationId,
                    string.Format(
                        ResourceProvider.GetString(requiresConfirmation
                            ? "LOCInstallation_Status_Updater_NotificationMessageProposedResults"
                            : "LOCInstallation_Status_Updater_NotificationMessageMarkedInstalledResults"),
                        statusUpdateResults.Installed.Count,
                        statusUpdateResults.Uninstalled.Count),
                    NotificationType.Info,
                    () =>
                    {
                        _playniteApi.Notifications.Remove(StatusResultsNotificationId);
                        OpenResultsWindow(statusUpdateResults, requiresConfirmation);
                    });
                _playniteApi.Notifications.Add(notificationMessage);
            }
            else
            {
                _playniteApi.Notifications.Remove(StatusResultsNotificationId);
            }
        }

        private void ShowNoChangesPopup()
        {
            var appWindow = _playniteApi.Dialogs.GetCurrentAppWindow();
            if (appWindow?.Dispatcher != null && !appWindow.Dispatcher.CheckAccess())
            {
                appWindow.Dispatcher.Invoke(ShowNoChangesPopup);
                return;
            }

            var window = _playniteApi.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = false
            });

            window.Title = ResourceProvider.GetString("LOCInstallation_Status_Updater_StatusUpdateResultsWindowTitle");
            window.Width = 440;
            window.Height = 230;
            window.ResizeMode = ResizeMode.NoResize;
            window.Content = new NoChangesDialogView();
            if (appWindow != null && appWindow.IsVisible)
            {
                window.Owner = appWindow;
            }
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            window.ShowDialog();
        }

        private void OpenResultsWindow(StatusUpdateResults updateResults, bool requiresConfirmation)
        {
            if (updateResults is null || !updateResults.HasChanges)
            {
                ShowNoChangesPopup();
                return;
            }

            var window = _playniteApi.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = true
            });

            window.Height = 540;
            window.Width = 860;
            window.Title = ResourceProvider.GetString(requiresConfirmation
                ? "LOCInstallation_Status_Updater_StatusUpdateResultsWindowPreviewTitle"
                : "LOCInstallation_Status_Updater_StatusUpdateResultsWindowTitle");

            var confirmAction = requiresConfirmation
                ? (Action<ICollection<Guid>>)ApplyPendingStatusChangesWithProgressDialog
                : RevertStatusUpdatesWithProgressDialog;
            var viewModel = new StatusUpdateResultsWindowViewModel(
                updateResults,
                requiresConfirmation,
                confirmAction,
                ExcludeGamesFromMinimumSizeRule,
                ClearPendingStatusChanges,
                _playniteApi,
                _pathsResolver);
            window.Content = new StatusUpdateResultsWindow();
            window.DataContext = viewModel;
            window.Owner = _playniteApi.Dialogs.GetCurrentAppWindow();
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            window.ShowDialog();
        }

        /// <summary>
        /// Puts games out of reach of the minimum folder size rule and saves the setting, which is
        /// what the results window offers when the rule flags a folder that is known to be small.
        /// </summary>
        private void ExcludeGamesFromMinimumSizeRule(ICollection<Guid> gameIds)
        {
            _settings.ExcludeGamesFromMinimumSizeRule(gameIds);
        }

        /// <summary>
        /// Best available path to tell which physical disk a game lives on, used to decide how much
        /// concurrency that disk should get. Games with no path fall back to the drive Playnite
        /// itself runs from, which keeps the guess harmless.
        /// </summary>
        private static string GetDiskHint(Game game)
        {
            if (!game.InstallDirectory.IsNullOrEmpty())
            {
                return game.InstallDirectory;
            }

            var romPath = game.Roms?.FirstOrDefault(x => !x.Path.IsNullOrEmpty())?.Path;
            return romPath.IsNullOrEmpty() ? null : romPath;
        }

        private InstallationDetectionOutcome DetectGameInstallationStatus(Game game)
        {
            if (PlayniteUtilities.GetGameHasFeature(game, ScanSkipFeatureName))
            {
                return InstallationDetectionOutcome.Skipped();
            }

            var resolvedInstallationDirectory = _pathsResolver.GetInstallDirForDetection(game);

            if (_settings.Settings.ProtectDisconnectedDrives && game.IsInstalled)
            {
                var targetDrivePath = GetTargetDrivePath(game, resolvedInstallationDirectory);
                if (!string.IsNullOrEmpty(targetDrivePath) && IsDriveDisconnected(targetDrivePath, out var driveRoot))
                {
                    _disconnectedDriveSkippedGames.AddOrUpdate(driveRoot, 1, (_, count) => count + 1);
                    return InstallationDetectionOutcome.Skipped();
                }
            }

            var detectedStatus = DetectInstallationStatusFromGamePaths(game, resolvedInstallationDirectory);
            return ApplyMinimumFolderSizeRule(game, detectedStatus, resolvedInstallationDirectory);
        }

        private static string GetTargetDrivePath(Game game, string resolvedInstallationDirectory)
        {
            if (!string.IsNullOrEmpty(game.InstallDirectory))
            {
                return game.InstallDirectory;
            }

            if (!string.IsNullOrEmpty(resolvedInstallationDirectory))
            {
                return resolvedInstallationDirectory;
            }

            var rom = game.Roms?.FirstOrDefault(r => !string.IsNullOrEmpty(r.Path))?.Path;
            if (!string.IsNullOrEmpty(rom))
            {
                return rom;
            }

            var action = game.GameActions?.FirstOrDefault(a => !string.IsNullOrEmpty(a.Path))?.Path;
            if (!string.IsNullOrEmpty(action))
            {
                return action;
            }

            return null;
        }

        private static bool IsDriveDisconnected(string path, out string driveRoot)
        {
            driveRoot = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                var root = Path.GetPathRoot(path);
                if (string.IsNullOrWhiteSpace(root))
                {
                    return false;
                }

                driveRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (driveRoot.Length >= 2 && driveRoot[1] == ':')
                {
                    var driveInfo = new DriveInfo(driveRoot);
                    return !driveInfo.IsReady;
                }

                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }

        private InstallationDetectionOutcome DetectInstallationStatusFromGamePaths(
            Game game,
            string resolvedInstallationDirectory)
        {
            if (game.GameActions?.Any() == true && _pathsResolver.IsAnyActionInstalled(game, resolvedInstallationDirectory))
            {
                return InstallationDetectionOutcome.Installed(InstallationDetectionReason.LaunchActionExists);
            }

            if (_pathsResolver.IsAnyRomInstalled(game, resolvedInstallationDirectory))
            {
                return InstallationDetectionOutcome.Installed(InstallationDetectionReason.RomExists);
            }

            var isLibraryPluginGame = game.PluginId != Guid.Empty;
            if (isLibraryPluginGame)
            {
                if (_settings.Settings.ScanGamesHandledByLibPlugins && !game.InstallDirectory.IsNullOrEmpty())
                {
                    return Directory.Exists(game.InstallDirectory)
                        ? InstallationDetectionOutcome.Installed(InstallationDetectionReason.InstallationDirectoryExists)
                        : InstallationDetectionOutcome.Uninstalled(InstallationDetectionReason.InstallationDirectoryMissing);
                }

                return InstallationDetectionOutcome.Skipped();
            }

            return InstallationDetectionOutcome.Uninstalled(InstallationDetectionReason.NoInstallationEvidence);
        }

        private InstallationDetectionOutcome ApplyMinimumFolderSizeRule(
            Game game,
            InstallationDetectionOutcome detectedStatus,
            string resolvedInstallationDirectory)
        {
            if (!_settings.Settings.UseMinimumFolderSizeDetection)
            {
                return detectedStatus;
            }

            // Rom based games are detected by the presence of the rom file itself and roms are
            // small by nature, so measuring their folder size would always flag them as uninstalled.
            if (game.Roms?.Any() == true)
            {
                return detectedStatus;
            }

            // Games and whole sources can be declared harmless, for folders the user already knows
            // to be legitimately smaller than the configured limit.
            if (IsExcludedFromMinimumSizeRule(game))
            {
                return detectedStatus;
            }

            // Games without a matching platform rule have their folder size left unchecked.
            var thresholdBytes = MinimumSizeRuleSelector.SelectThresholdBytes(
                _settings.Settings.MinimumSizeRules, game.PlatformIds);
            if (thresholdBytes == null)
            {
                return detectedStatus;
            }

            // Installation directories often point to binary subfolders instead of the game
            // folder itself, so the size has to be measured from the confirmed game root.
            var directoryToMeasure = GameFolderRootResolver.ResolveRootDirectory(
                game.Name, resolvedInstallationDirectory);

            if (directoryToMeasure.IsNullOrEmpty())
            {
                return detectedStatus;
            }

            var outcome = DirectorySizeCache.MeasureOrGet(directoryToMeasure, thresholdBytes.Value);
            var sizeBasedStatus = MinimumInstallationSizeRule.ApplyProbe(
                detectedStatus.Result, outcome.Result);
            if (sizeBasedStatus == detectedStatus.Result)
            {
                return detectedStatus;
            }

            // Only a verdict that actually changed is caused by the size rule, which is what the
            // user has to be told about.
            return detectedStatus
                .WithResult(sizeBasedStatus, InstallationDetectionReason.BelowMinimumInstallationSize)
                .WithMeasuredSize(outcome.MeasuredBytes, outcome.ReachedThreshold);
        }

        /// <summary>
        /// True when the minimum folder size rule was told to leave this game alone.
        /// </summary>
        private bool IsExcludedFromMinimumSizeRule(Game game)
        {
            var settings = _settings.Settings;
            if (settings.MinimumSizeRuleExcludedGames?.Any(x => x.GameId == game.Id) == true)
            {
                return true;
            }

            var sourceName = game.Source?.Name;
            if (sourceName.IsNullOrEmpty())
            {
                return false;
            }

            return settings.MinimumSizeRuleExcludedSources?.Any(x =>
                x.Equals(sourceName, StringComparison.OrdinalIgnoreCase)) == true;
        }
    }
}
