using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;

namespace InstallationStatusUpdater.Application
{
    public enum DiskKind
    {
        Unknown = 0,
        Mechanical = 1,
        SolidState = 2
    }

    /// <summary>
    /// A mounted drive together with how the disk behind it was classified.
    /// </summary>
    public class DetectedDisk
    {
        public string Root { get; }
        public DiskKind Kind { get; }

        public string KindLabel => ResourceProvider.GetString(
            Kind == DiskKind.SolidState
                ? "LOCInstallation_Status_Updater_DiskKindSolidStateLabel"
                : Kind == DiskKind.Mechanical
                    ? "LOCInstallation_Status_Updater_DiskKindMechanicalLabel"
                    : "LOCInstallation_Status_Updater_DiskKindUnknownLabel");

        public DetectedDisk(string root, DiskKind kind)
        {
            Root = root;
            Kind = kind;
        }
    }

    /// <summary>
    /// Bounds how many directory trees are walked at the same time on each physical disk.
    /// A single global limit is wrong when games span solid state drives and mechanical ones:
    /// the concurrency that suits a solid state drive makes a mechanical drive thrash, and the
    /// limit that suits the mechanical drive leaves the solid state drive idle.
    /// Only the classification of each drive is remembered, since asking the system for it is
    /// expensive; the numbers themselves are read from the settings on every use so that a
    /// change takes effect without restarting Playnite.
    /// </summary>
    public class DiskConcurrencyLimiter
    {
        // A limit of zero would wait forever and a huge one would only spawn threads that queue
        // up, so the configured values are kept inside a range that always makes progress.
        private const int MinLimit = 1;
        private const int MaxLimit = 64;

        private readonly Func<DiskKind, int> _limitResolver;
        private readonly object _lock = new object();
        private Dictionary<string, DiskKind> _kindsByRoot;
        private Dictionary<string, SemaphoreHolder> _holdersByRoot;

        public DiskConcurrencyLimiter(Func<DiskKind, int> limitResolver)
        {
            _limitResolver = limitResolver ?? throw new ArgumentNullException(nameof(limitResolver));
        }

        /// <summary>
        /// Waits for a slot on the disk holding the given path and releases it when disposed.
        /// </summary>
        public IDisposable Acquire(string path)
        {
            var root = GetRoot(path);
            var holder = GetOrCreateHolder(root, GetLimitForRoot(root));
            holder.Semaphore.Wait();
            return new Releaser(holder.Semaphore);
        }

        /// <summary>
        /// Highest number of concurrent walks allowed on the disk holding the given path.
        /// </summary>
        public int GetLimit(string path)
        {
            return GetLimitForRoot(GetRoot(path));
        }

        /// <summary>
        /// Every mounted drive with the type it was classified as, so the user can check whether
        /// the classification matches what the disk really is.
        /// </summary>
        public IReadOnlyList<DetectedDisk> GetDetectedDisks()
        {
            lock (_lock)
            {
                EnsureClassification();
                return _kindsByRoot
                    .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new DetectedDisk(x.Key, x.Value))
                    .ToList();
            }
        }

        /// <summary>
        /// Forgets how each drive was classified so that the next use asks the system again.
        /// </summary>
        public void ResetClassification()
        {
            lock (_lock)
            {
                _kindsByRoot = null;
                _holdersByRoot = null;
            }
        }

        private int GetLimitForRoot(string root)
        {
            DiskKind kind;
            lock (_lock)
            {
                EnsureClassification();
                _kindsByRoot.TryGetValue(root, out kind);
            }

            var configured = Sanitize(_limitResolver(kind));
            return configured;
        }

        private SemaphoreHolder GetOrCreateHolder(string root, int limit)
        {
            lock (_lock)
            {
                EnsureClassification();
                if (_holdersByRoot.TryGetValue(root, out var existing) && existing.Limit == limit)
                {
                    return existing;
                }

                // Walks already holding a slot on the previous semaphore keep it until they finish,
                // so a limit lowered during a running scan is only fully felt by the next one.
                var holder = new SemaphoreHolder(limit);
                _holdersByRoot[root] = holder;
                return holder;
            }
        }

        private static int Sanitize(int value)
        {
            if (value < MinLimit)
            {
                return MinLimit;
            }

            return value > MaxLimit ? MaxLimit : value;
        }

        private void EnsureClassification()
        {
            if (_kindsByRoot != null)
            {
                return;
            }

            _kindsByRoot = new Dictionary<string, DiskKind>(StringComparer.OrdinalIgnoreCase);
            _holdersByRoot = new Dictionary<string, SemaphoreHolder>(StringComparer.OrdinalIgnoreCase);

            var solidStateRoots = ReadSolidStateDriveRoots();
            foreach (var root in EnumerateMountedRoots())
            {
                // Without a reliable answer the middle ground is safer than guessing high or low,
                // since either mistake costs either time or responsiveness.
                var kind = solidStateRoots.Contains(root) ? DiskKind.SolidState : DiskKind.Unknown;
                _kindsByRoot[root] = kind;
            }
        }

        /// <summary>
        /// Collects the drive letters whose underlying disk is solid state. WMI is the only
        /// supported way to ask, and it can fail on locked down systems, so any failure leaves
        /// the drives unknown instead of breaking the scan.
        /// </summary>
        private static HashSet<string> ReadSolidStateDriveRoots()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var solidStateDisks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var disks = new ManagementObjectSearcher(
                    @"root\Microsoft\Windows\Storage",
                    "SELECT DeviceId, MediaType FROM MSFT_PhysicalDisk"))
                {
                    foreach (var disk in disks.Get())
                    {
                        using (disk)
                        {
                            // 3 is a hard disk, anything else that is not unspecified is solid state.
                            var mediaType = Convert.ToInt32(disk["MediaType"]);
                            if (mediaType != 3)
                            {
                                solidStateDisks.Add(Convert.ToString(disk["DeviceId"]));
                            }
                        }
                    }
                }

                using (var partitions = new ManagementObjectSearcher(
                    @"root\Microsoft\Windows\Storage",
                    "SELECT DiskNumber, DriveLetter FROM MSFT_Partition"))
                {
                    foreach (var partition in partitions.Get())
                    {
                        using (partition)
                        {
                            var driveLetter = Convert.ToString(partition["DriveLetter"]);
                            if (string.IsNullOrWhiteSpace(driveLetter) ||
                                !solidStateDisks.Contains(Convert.ToString(partition["DiskNumber"])))
                            {
                                continue;
                            }

                            result.Add(driveLetter.TrimEnd('\\') + ":\\");
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Every drive is reported as unknown below, which still keeps the scan bounded.
            }

            return result;
        }

        private static IEnumerable<string> EnumerateMountedRoots()
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady)
                {
                    continue;
                }

                yield return drive.RootDirectory.FullName;
            }
        }

        private static string GetRoot(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    var root = Path.GetPathRoot(Path.GetFullPath(path));
                    if (!string.IsNullOrWhiteSpace(root))
                    {
                        return root;
                    }
                }
            }
            catch (Exception)
            {
                // Falls through to the default below.
            }

            return Path.GetPathRoot(Environment.CurrentDirectory);
        }

        private sealed class SemaphoreHolder
        {
            public int Limit { get; }
            public SemaphoreSlim Semaphore { get; }

            public SemaphoreHolder(int limit)
            {
                Limit = limit;
                Semaphore = new SemaphoreSlim(limit, limit);
            }
        }

        private sealed class Releaser : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;

            public Releaser(SemaphoreSlim semaphore)
            {
                _semaphore = semaphore;
            }

            public void Dispose()
            {
                _semaphore.Release();
            }
        }
    }
}
