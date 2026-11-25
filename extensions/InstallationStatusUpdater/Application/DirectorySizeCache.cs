using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;

namespace InstallationStatusUpdater.Application
{
    /// <summary>
    /// A previously measured directory, kept so that consecutive scans of the same folder do not
    /// have to read the file system again.
    /// </summary>
    public sealed class DirectorySizeCacheEntry
    {
        public DirectorySizeProbeResult Result { get; set; }
        public long MeasuredBytes { get; set; }
        public bool ReachedThreshold { get; set; }
        public long ThresholdBytes { get; set; }
        public DateTime TimestampUtc { get; set; }
        public DateTime LastWriteTimeUtc { get; set; }

        public DirectorySizeCacheEntry()
        {
        }

        public DirectorySizeCacheEntry(
            DirectorySizeProbeResult result,
            long measuredBytes,
            bool reachedThreshold,
            long thresholdBytes,
            DateTime timestampUtc,
            DateTime lastWriteTimeUtc)
        {
            Result = result;
            MeasuredBytes = measuredBytes;
            ReachedThreshold = reachedThreshold;
            ThresholdBytes = thresholdBytes;
            TimestampUtc = timestampUtc;
            LastWriteTimeUtc = lastWriteTimeUtc;
        }
    }

    /// <summary>
    /// Remembers directory measurements for a limited time. Walking a game folder is the most
    /// expensive part of a scan, and nothing about a folder usually changes between scans.
    /// In addition to a TTL, entries validate their stored LastWriteTimeUtc against disk so that
    /// untouched folders return instantly while modified ones are remeasured.
    /// </summary>
    public static class DirectorySizeCache
    {
        public static readonly TimeSpan TimeToLive = TimeSpan.FromHours(48);

        private static readonly object _lock = new object();
        private static Dictionary<string, DirectorySizeCacheEntry> _entries =
            new Dictionary<string, DirectorySizeCacheEntry>(StringComparer.OrdinalIgnoreCase);

        public static int Count
        {
            get
            {
                lock (_lock)
                {
                    return _entries.Count;
                }
            }
        }

        public static DirectorySizeProbeOutcome MeasureOrGet(string directoryPath, long thresholdBytes)
        {
            if (!string.IsNullOrWhiteSpace(directoryPath) && TryGet(directoryPath, thresholdBytes, out var cached))
            {
                return new DirectorySizeProbeOutcome(
                    cached.Result, cached.MeasuredBytes, cached.ReachedThreshold, cached.ThresholdBytes);
            }

            var outcome = DirectorySizeProbe.ProbeDetailed(directoryPath, thresholdBytes);
            if (outcome.Result != DirectorySizeProbeResult.Indeterminate)
            {
                Set(directoryPath, outcome);
            }

            return outcome;
        }

        public static bool TryGet(
            string directoryPath,
            long thresholdBytes,
            out DirectorySizeCacheEntry entry)
        {
            entry = null;
            var key = Normalize(directoryPath);
            if (key == null)
            {
                return false;
            }

            lock (_lock)
            {
                if (!_entries.TryGetValue(key, out var cached))
                {
                    return false;
                }

                if (DateTime.UtcNow - cached.TimestampUtc > TimeToLive)
                {
                    _entries.Remove(key);
                    return false;
                }

                if (cached.ThresholdBytes != thresholdBytes)
                {
                    return false;
                }

                // Verify if the folder has been modified on disk since the measurement
                try
                {
                    if (Directory.Exists(key))
                    {
                        var diskWriteTime = Directory.GetLastWriteTimeUtc(key);
                        if (diskWriteTime != cached.LastWriteTimeUtc)
                        {
                            _entries.Remove(key);
                            return false;
                        }
                    }
                }
                catch (Exception)
                {
                    _entries.Remove(key);
                    return false;
                }

                entry = cached;
                return true;
            }
        }

        public static void Set(string directoryPath, DirectorySizeProbeOutcome outcome)
        {
            var key = Normalize(directoryPath);
            if (key == null)
            {
                return;
            }

            DateTime lastWriteTime = DateTime.MinValue;
            try
            {
                if (Directory.Exists(key))
                {
                    lastWriteTime = Directory.GetLastWriteTimeUtc(key);
                }
            }
            catch (Exception)
            {
            }

            lock (_lock)
            {
                _entries[key] = new DirectorySizeCacheEntry(
                    outcome.Result,
                    outcome.MeasuredBytes,
                    outcome.ReachedThreshold,
                    outcome.ThresholdBytes,
                    DateTime.UtcNow,
                    lastWriteTime);
            }
        }

        public static void Invalidate(string changedPath)
        {
            var changed = Normalize(changedPath);
            if (changed == null)
            {
                return;
            }

            var separator = Path.DirectorySeparatorChar;
            var changedPrefix = changed + separator;

            lock (_lock)
            {
                var keysToRemove = new List<string>();
                foreach (var key in _entries.Keys)
                {
                    if (key.Equals(changed, StringComparison.OrdinalIgnoreCase) ||
                        key.StartsWith(changedPrefix, StringComparison.OrdinalIgnoreCase) ||
                        changedPrefix.StartsWith(key + separator, StringComparison.OrdinalIgnoreCase))
                    {
                        keysToRemove.Add(key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _entries.Remove(key);
                }
            }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _entries.Clear();
            }
        }

        public static void Save(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            try
            {
                string json;
                lock (_lock)
                {
                    json = Serialization.ToJson(_entries);
                }

                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(filePath, json);
            }
            catch (Exception)
            {
            }
        }

        public static void Load(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(filePath);
                var loaded = Serialization.FromJson<Dictionary<string, DirectorySizeCacheEntry>>(json);
                if (loaded != null)
                {
                    lock (_lock)
                    {
                        _entries = new Dictionary<string, DirectorySizeCacheEntry>(loaded, StringComparer.OrdinalIgnoreCase);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
