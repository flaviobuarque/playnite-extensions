using InstallationStatusUpdater.Application;
using InstallationStatusUpdater.Domain.ValueObjects;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstallationStatusUpdater
{
    public class InstallationStatusUpdaterSettings : ObservableObject
    {
        public bool UrlActionIsInstalled { get; set; } = true;
        public bool ScriptActionIsInstalled { get; set; } = true;
        public bool UseOnlyFirstRomDetection { get; set; } = false;
        public bool ScanGamesHandledByLibPlugins { get; set; } = false;
        public bool OnlyUsePlayActionsForDetection { get; set; } = true;
        public bool UpdateOnStartup { get; set; } = true;
        public bool UpdateOnLibraryUpdate { get; set; } = true;
        public bool UpdateLocTagsOnLibUpdate { get; set; } = false;
        public bool UpdateStatusOnUsbChanges { get; set; } = true;
        public bool UpdateStatusOnDirChanges { get; set; } = false;
        public bool DetectFilesFromLaunchArguments { get; set; } = true;
        public bool ShowNotificationOnAutomaticScans { get; set; } = true;
        public bool ConfirmStatusChangesBeforeApplying { get; set; } = false;
        public bool UseMinimumFolderSizeDetection { get; set; } = false;
        public bool ProtectDisconnectedDrives { get; set; } = true;

        /// <summary>
        /// Opens the results window right away on scans nobody started by hand, instead of leaving
        /// a notification in the sidebar until it is clicked.
        /// </summary>
        public bool ShowResultsWindowOnAutomaticScans { get; set; } = false;

        private int _maxScopedScanPaths = 300;
        /// <summary>
        /// How many changed paths a scan may be narrowed down to before scanning everything
        /// becomes the cheaper option.
        /// </summary>
        public int MaxScopedScanPaths
        {
            get => _maxScopedScanPaths;
            set
            {
                _maxScopedScanPaths = value;
                OnPropertyChanged();
            }
        }

        private int _parallelismSolidState = 8;
        /// <summary>
        /// How many game folders may be walked at once on solid state drives.
        /// </summary>
        public int ParallelismSolidState
        {
            get => _parallelismSolidState;
            set
            {
                _parallelismSolidState = value;
                OnPropertyChanged();
            }
        }

        private int _parallelismMechanical = 2;
        /// <summary>
        /// How many game folders may be walked at once on mechanical drives, which slow down
        /// sharply when asked for too much at the same time.
        /// </summary>
        public int ParallelismMechanical
        {
            get => _parallelismMechanical;
            set
            {
                _parallelismMechanical = value;
                OnPropertyChanged();
            }
        }

        private int _parallelismUnknown = 4;
        /// <summary>
        /// How many game folders may be walked at once on drives whose type could not be read.
        /// </summary>
        public int ParallelismUnknown
        {
            get => _parallelismUnknown;
            set
            {
                _parallelismUnknown = value;
                OnPropertyChanged();
            }
        }

        private List<PlatformMinimumSizeRule> _minimumSizeRules = new List<PlatformMinimumSizeRule>();
        public List<PlatformMinimumSizeRule> MinimumSizeRules
        {
            get => _minimumSizeRules;
            set
            {
                _minimumSizeRules = value;
                OnPropertyChanged();
            }
        }

        private List<MinimumSizeRuleGameExclusion> _minimumSizeRuleExcludedGames =
            new List<MinimumSizeRuleGameExclusion>();
        /// <summary>
        /// Games the minimum folder size rule leaves alone entirely, for folders that are known to
        /// be legitimately smaller than the configured limit.
        /// </summary>
        public List<MinimumSizeRuleGameExclusion> MinimumSizeRuleExcludedGames
        {
            get => _minimumSizeRuleExcludedGames;
            set
            {
                _minimumSizeRuleExcludedGames = value;
                OnPropertyChanged();
            }
        }

        private List<string> _minimumSizeRuleExcludedSources = new List<string>();
        /// <summary>
        /// Library sources whose games the minimum folder size rule leaves alone.
        /// </summary>
        public List<string> MinimumSizeRuleExcludedSources
        {
            get => _minimumSizeRuleExcludedSources;
            set
            {
                _minimumSizeRuleExcludedSources = value;
                OnPropertyChanged();
            }
        }

        private List<WatchedDirectory> _detectionDirectories = new List<WatchedDirectory>();
        public List<WatchedDirectory> DetectionDirectories
        {
            get => _detectionDirectories;
            set
            {
                _detectionDirectories = value;
                OnPropertyChanged();
            }
        }
    }

    public class InstallationStatusUpdaterSettingsViewModel : ObservableObject, ISettings
    {
        private readonly IPlayniteAPI _playniteApi;
        private readonly InstallationStatusUpdater _plugin;
        private InstallationStatusUpdaterSettings _editingClone;

        private InstallationStatusUpdaterSettings _settings;
        public InstallationStatusUpdaterSettings Settings
        {
            get => _settings;
            set
            {
                _settings = value;
                OnPropertyChanged();
            }
        }

        public RelayCommand AddDetectionDirectoryCommand { get; }
        public RelayCommand<IList<object>> RemoveDetectionDirectoriesCommand { get; }
        public RelayCommand AddMinimumSizeRuleCommand { get; }
        public RelayCommand<IList<object>> RemoveMinimumSizeRulesCommand { get; }
        public RelayCommand<IList<object>> RemoveMinimumSizeRuleExcludedGamesCommand { get; }
        public RelayCommand AddMinimumSizeRuleExcludedSourceCommand { get; }
        public RelayCommand<IList<object>> RemoveMinimumSizeRuleExcludedSourcesCommand { get; }
        public RelayCommand RedetectDisksCommand { get; }

        private string _newExcludedSourceName = string.Empty;
        /// <summary>
        /// Source name typed into the exclusion box before being added.
        /// </summary>
        public string NewExcludedSourceName
        {
            get => _newExcludedSourceName;
            set
            {
                _newExcludedSourceName = value;
                OnPropertyChanged();
            }
        }

        // The Playnite database is not loaded yet when the plugin constructor runs, so the
        // platform list can only be queried when the settings view actually binds to it.
        private List<Platform> _platforms = new List<Platform>();
        public List<Platform> Platforms
        {
            get
            {
                if (_platforms.Count == 0)
                {
                    _platforms = LoadPlatforms();
                }

                return _platforms;
            }
        }

        public List<SizeUnitOption> SizeUnits { get; } = new List<SizeUnitOption>
        {
            new SizeUnitOption { Unit = MinimumFolderSizeUnit.Kilobytes, DisplayName = "KB" },
            new SizeUnitOption { Unit = MinimumFolderSizeUnit.Megabytes, DisplayName = "MB" },
            new SizeUnitOption { Unit = MinimumFolderSizeUnit.Gigabytes, DisplayName = "GB" }
        };

        // Same reason as the platforms: the source list only exists once the database is loaded.
        private List<GameSource> _sources = new List<GameSource>();
        /// <summary>
        /// Library sources present in the database, offered as suggestions when excluding a
        /// source from the size rule. Names typed by hand still work for anything missing here.
        /// </summary>
        public List<GameSource> Sources
        {
            get
            {
                if (_sources.Count == 0)
                {
                    _sources = LoadSources();
                }

                return _sources;
            }
        }

        /// <summary>
        /// Handed over by the plugin, since asking the system about the disks is too expensive to
        /// be done by the settings on their own.
        /// </summary>
        internal DiskConcurrencyLimiter DiskLimiter { get; set; }

        private List<DetectedDisk> _detectedDisks = new List<DetectedDisk>();
        public List<DetectedDisk> DetectedDisks
        {
            get => _detectedDisks;
            private set
            {
                _detectedDisks = value;
                OnPropertyChanged();
            }
        }

        public InstallationStatusUpdaterSettingsViewModel(InstallationStatusUpdater plugin, IPlayniteAPI playniteApi)
        {
            _playniteApi = playniteApi ?? throw new ArgumentNullException(nameof(playniteApi));
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            var savedSettings = _plugin.LoadPluginSettings<InstallationStatusUpdaterSettings>();
            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new InstallationStatusUpdaterSettings();
            }

            AddDetectionDirectoryCommand = new RelayCommand(() => AddNewDetectionDirectory());
            RemoveDetectionDirectoriesCommand = new RelayCommand<IList<object>>(
                items => RemoveDetectionDirectories(items),
                (items) => items != null && items.Count > 0);
            AddMinimumSizeRuleCommand = new RelayCommand(() => AddNewMinimumSizeRule());
            RemoveMinimumSizeRulesCommand = new RelayCommand<IList<object>>(
                items => RemoveMinimumSizeRules(items),
                (items) => items != null && items.Count > 0);
            RemoveMinimumSizeRuleExcludedGamesCommand = new RelayCommand<IList<object>>(
                items => RemoveMinimumSizeRuleExcludedGames(items),
                (items) => items != null && items.Count > 0);
            AddMinimumSizeRuleExcludedSourceCommand = new RelayCommand(
                () => AddMinimumSizeRuleExcludedSource(),
                () => !NewExcludedSourceName.IsNullOrWhiteSpace());
            RemoveMinimumSizeRuleExcludedSourcesCommand = new RelayCommand<IList<object>>(
                items => RemoveMinimumSizeRuleExcludedSources(items),
                (items) => items != null && items.Count > 0);
            RedetectDisksCommand = new RelayCommand(() => RedetectDisks());
        }

        private List<Platform> LoadPlatforms()
        {
            var platforms = _playniteApi.Database?.Platforms;
            if (platforms == null)
            {
                return new List<Platform>();
            }

            return platforms.OrderBy(platform => platform.Name).ToList();
        }

        private void RefreshPlatforms()
        {
            _platforms = LoadPlatforms();
            OnPropertyChanged(nameof(Platforms));
        }

        private List<GameSource> LoadSources()
        {
            var sources = _playniteApi.Database?.Sources;
            if (sources == null)
            {
                return new List<GameSource>();
            }

            return sources.OrderBy(source => source.Name).ToList();
        }

        private void RefreshSources()
        {
            _sources = LoadSources();
            OnPropertyChanged(nameof(Sources));
        }

        private void RefreshDetectedDisks()
        {
            if (DiskLimiter is null)
            {
                DetectedDisks = new List<DetectedDisk>();
                return;
            }

            DetectedDisks = DiskLimiter.GetDetectedDisks().ToList();
        }

        private void RedetectDisks()
        {
            DiskLimiter?.ResetClassification();
            RefreshDetectedDisks();
        }

        public void BeginEdit()
        {
            // Code executed when settings view is opened and user starts editing values.
            // Refreshed here so that platforms and sources added after Playnite started, and
            // disks plugged in since then, are picked up.
            RefreshPlatforms();
            RefreshSources();
            RefreshDetectedDisks();
            _editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            // Code executed when user decides to cancel any changes made since BeginEdit was called.
            // This method should revert any changes made to Option1 and Option2.
            Settings = _editingClone;
        }

        public void EndEdit()
        {
            // Code executed when user decides to confirm changes made since BeginEdit was called.
            // This method should save settings made to Option1 and Option2.
            _plugin.SavePluginSettings(Settings);
            // Watching other folders, or watching any at all, has to start working now instead
            // of waiting for the next library update or for Playnite to be reopened.
            _plugin.RefreshDirectoryWatchers();
        }

        public bool VerifySettings(out List<string> errors)
        {
            // Code execute when user decides to confirm changes made since BeginEdit was called.
            // Executed before EndEdit is called and EndEdit is not called if false is returned.
            // List of errors is presented to user if verification fails.
            errors = new List<string>();
            return true;
        }

        private void AddNewDetectionDirectory()
        {
            var newDir = _playniteApi.Dialogs.SelectFolder();
            if (newDir.IsNullOrEmpty())
            {
                return;
            }

            if (Settings.DetectionDirectories
                .FirstOrDefault(s => s.DirectoryPath.Equals(newDir, StringComparison.OrdinalIgnoreCase)) != null)
            {
                return;
            }

            var newDetectionDirectory = new WatchedDirectory
            {
                DirectoryPath = newDir,
                Enabled = true,
                ScanSubDirs = false
            };

            Settings.DetectionDirectories.Add(newDetectionDirectory);
            Settings.DetectionDirectories = Serialization.GetClone(Settings.DetectionDirectories);
        }

        private void RemoveDetectionDirectories(IList<object> items)
        {
            var directories = items.OfType<WatchedDirectory>().ToList();
            foreach (var dir in directories)
            {
                Settings.DetectionDirectories.Remove(dir);
            }

            Settings.DetectionDirectories = Serialization.GetClone(Settings.DetectionDirectories);
        }

        private void AddNewMinimumSizeRule()
        {
            var newRule = new PlatformMinimumSizeRule
            {
                Enabled = true,
                SizeValue = 1,
                Unit = MinimumFolderSizeUnit.Megabytes
            };

            var firstPlatform = Platforms.FirstOrDefault();
            if (firstPlatform != null)
            {
                newRule.PlatformId = firstPlatform.Id;
            }

            Settings.MinimumSizeRules.Add(newRule);
            Settings.MinimumSizeRules = Serialization.GetClone(Settings.MinimumSizeRules);
        }

        private void RemoveMinimumSizeRules(IList<object> items)
        {
            var rules = items.OfType<PlatformMinimumSizeRule>().ToList();
            foreach (var rule in rules)
            {
                Settings.MinimumSizeRules.Remove(rule);
            }

            Settings.MinimumSizeRules = Serialization.GetClone(Settings.MinimumSizeRules);
        }

        private void RemoveMinimumSizeRuleExcludedGames(IList<object> items)
        {
            var exclusions = items.OfType<MinimumSizeRuleGameExclusion>().ToList();
            foreach (var exclusion in exclusions)
            {
                Settings.MinimumSizeRuleExcludedGames.Remove(exclusion);
            }

            Settings.MinimumSizeRuleExcludedGames =
                Serialization.GetClone(Settings.MinimumSizeRuleExcludedGames);
        }

        private void AddMinimumSizeRuleExcludedSource()
        {
            var sourceName = NewExcludedSourceName?.Trim();
            if (sourceName.IsNullOrEmpty())
            {
                return;
            }

            if (Settings.MinimumSizeRuleExcludedSources.Any(x =>
                x.Equals(sourceName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            Settings.MinimumSizeRuleExcludedSources.Add(sourceName);
            Settings.MinimumSizeRuleExcludedSources =
                Serialization.GetClone(Settings.MinimumSizeRuleExcludedSources);
            NewExcludedSourceName = string.Empty;
        }

        private void RemoveMinimumSizeRuleExcludedSources(IList<object> items)
        {
            var sources = items.OfType<string>().ToList();
            foreach (var source in sources)
            {
                Settings.MinimumSizeRuleExcludedSources.Remove(source);
            }

            Settings.MinimumSizeRuleExcludedSources =
                Serialization.GetClone(Settings.MinimumSizeRuleExcludedSources);
        }

        /// <summary>
        /// Adds games to the list the minimum folder size rule skips and saves them right away.
        /// This runs from the results window rather than from the settings dialog, where the
        /// normal save flow would never happen, so it has to persist on its own.
        /// </summary>
        public void ExcludeGamesFromMinimumSizeRule(ICollection<Guid> gameIds)
        {
            if (gameIds is null || gameIds.Count == 0)
            {
                return;
            }

            var changed = false;
            foreach (var gameId in gameIds)
            {
                if (Settings.MinimumSizeRuleExcludedGames.Any(x => x.GameId == gameId))
                {
                    continue;
                }

                var game = _playniteApi.Database?.Games?.Get(gameId);
                if (game is null)
                {
                    continue;
                }

                Settings.MinimumSizeRuleExcludedGames.Add(
                    new MinimumSizeRuleGameExclusion(gameId, game.Name));
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            Settings.MinimumSizeRuleExcludedGames =
                Serialization.GetClone(Settings.MinimumSizeRuleExcludedGames);
            _plugin.SavePluginSettings(Settings);
        }
    }
}