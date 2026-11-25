using Playnite.SDK;
using PluginsCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace InstallationStatusUpdater.Application
{
    public class DirectoryWatcherService : IDisposable
    {
        private readonly ILogger _logger;
        private readonly InstallationStatusUpdaterSettingsViewModel _settings;
        private readonly NotifyFilters _notifyFilters;
        private readonly List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();

        private readonly object _debounceLock = new object();
        private readonly HashSet<string> _debouncedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Timer _debounceTimer;
        private const int DebounceDelayMs = 600;

        /// <summary>
        /// Called when directory changes are detected. Receives the full path of the changed file or directory.
        /// </summary>
        public Action<string> OnTrigger { get; set; }

        public DirectoryWatcherService(
            ILogger logger,
            InstallationStatusUpdaterSettingsViewModel settings,
            NotifyFilters? notifyFilters = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _notifyFilters = notifyFilters ?? (NotifyFilters.Attributes
                                 | NotifyFilters.CreationTime
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.FileName
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Size);
        }

        public void StartWatching()
        {
            DisposeWatchers();
            if (!_settings.Settings.UpdateStatusOnDirChanges || !_settings.Settings.DetectionDirectories.Any())
            {
                return;
            }

            foreach (var directory in _settings.Settings.DetectionDirectories)
            {
                if (!directory.Enabled)
                {
                    continue;
                }

                if (!FileSystem.DirectoryExists(directory.DirectoryPath))
                {
                    _logger.Warn($"[DirectoryWatcher] Directory does not exist: {directory.DirectoryPath}");
                    continue;
                }

                var watcher = new FileSystemWatcher(directory.DirectoryPath)
                {
                    NotifyFilter = _notifyFilters,
                    Filter = "*.*",
                    IncludeSubdirectories = directory.ScanSubDirs,
                    EnableRaisingEvents = true
                };

                watcher.Changed += (_, e) => OnWatcherEvent(e.FullPath);
                watcher.Created += (_, e) => OnWatcherEvent(e.FullPath);
                watcher.Deleted += (_, e) => OnWatcherEvent(e.FullPath);
                watcher.Renamed += (_, e) => OnWatcherEvent(e.FullPath);

                watchers.Add(watcher);
            }
        }

        private void OnWatcherEvent(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            lock (_debounceLock)
            {
                // Coalesce: if path is a file, we can also keep the path directly
                _debouncedPaths.Add(path);

                if (_debounceTimer == null)
                {
                    _debounceTimer = new Timer(OnDebounceElapsed, null, DebounceDelayMs, Timeout.Infinite);
                }
                else
                {
                    _debounceTimer.Change(DebounceDelayMs, Timeout.Infinite);
                }
            }
        }

        private void OnDebounceElapsed(object state)
        {
            List<string> pathsToEmit;
            lock (_debounceLock)
            {
                pathsToEmit = _debouncedPaths.ToList();
                _debouncedPaths.Clear();
            }

            if (pathsToEmit.Count == 0)
            {
                return;
            }

            _logger.Debug($"[DirectoryWatcher] Debounce flushed {pathsToEmit.Count} coalesced change(s).");

            foreach (var p in pathsToEmit)
            {
                try
                {
                    OnTrigger?.Invoke(p);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"[DirectoryWatcher] Error dispatching path change: {p}");
                }
            }
        }

        public void DisposeWatchers()
        {
            lock (_debounceLock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = null;
                _debouncedPaths.Clear();
            }

            foreach (var watcher in watchers)
            {
                watcher.Dispose();
            }

            watchers.Clear();
        }

        public void Dispose()
        {
            DisposeWatchers();
        }
    }
}
