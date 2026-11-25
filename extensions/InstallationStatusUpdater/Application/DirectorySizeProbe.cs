using PluginsCommon;
using System;
using System.IO;

namespace InstallationStatusUpdater.Application
{
    public enum DirectorySizeProbeResult
    {
        BelowThreshold = 0,
        AtOrAboveThreshold = 1,
        Indeterminate = 2
    }

    /// <summary>
    /// Verdict of a measurement together with how far the counter got before stopping.
    /// <see cref="ReachedThreshold"/> tells whether the count was abandoned early, which is what
    /// separates an exact size from one that only proves the folder is big enough, and
    /// <see cref="ThresholdBytes"/> records the limit the measurement was taken against.
    /// </summary>
    public struct DirectorySizeProbeOutcome
    {
        public DirectorySizeProbeResult Result { get; }
        public long MeasuredBytes { get; }
        public bool ReachedThreshold { get; }
        public long ThresholdBytes { get; }

        public DirectorySizeProbeOutcome(
            DirectorySizeProbeResult result,
            long measuredBytes,
            bool reachedThreshold,
            long thresholdBytes)
        {
            Result = result;
            MeasuredBytes = measuredBytes;
            ReachedThreshold = reachedThreshold;
            ThresholdBytes = thresholdBytes;
        }
    }

    /// <summary>
    /// Measures whether a directory holds at least a minimum number of bytes.
    /// Counting stops as soon as the threshold is reached, since only the comparison
    /// against the threshold is needed and not the exact size of the directory.
    /// Any failure to read the directory is reported as <see cref="DirectorySizeProbeResult.Indeterminate"/>
    /// so that callers can leave the current installation status untouched instead of
    /// incorrectly treating an unreadable directory as an uninstalled game.
    /// </summary>
    public static class DirectorySizeProbe
    {
        private const int MaxDirectoryDepth = 64;

        public static DirectorySizeProbeResult Probe(string directoryPath, long thresholdBytes)
        {
            return ProbeDetailed(directoryPath, thresholdBytes).Result;
        }

        public static DirectorySizeProbeOutcome ProbeDetailed(string directoryPath, long thresholdBytes)
        {
            if (directoryPath.IsNullOrEmpty())
            {
                return new DirectorySizeProbeOutcome(DirectorySizeProbeResult.Indeterminate, 0, false, thresholdBytes);
            }

            if (thresholdBytes <= 0)
            {
                return new DirectorySizeProbeOutcome(DirectorySizeProbeResult.AtOrAboveThreshold, 0, false, thresholdBytes);
            }

            try
            {
                var rootDirectory = new DirectoryInfo(FileSystem.FixPathLength(directoryPath));
                if (!rootDirectory.Exists)
                {
                    return new DirectorySizeProbeOutcome(DirectorySizeProbeResult.Indeterminate, 0, false, thresholdBytes);
                }

                long totalSize = 0;
                var skippedReparsePoints = false;
                var result = AccumulateDirectorySize(
                    rootDirectory, thresholdBytes, 0, ref totalSize, ref skippedReparsePoints);
                if (result == DirectorySizeProbeResult.AtOrAboveThreshold)
                {
                    return new DirectorySizeProbeOutcome(result, totalSize, true, thresholdBytes);
                }

                // Junctions and symlinks are not followed to avoid cycles, which means the
                // measured size can be lower than the real one. When that happens the result
                // cannot be trusted to mark a game as uninstalled.
                var finalResult = skippedReparsePoints
                    ? DirectorySizeProbeResult.Indeterminate
                    : DirectorySizeProbeResult.BelowThreshold;
                return new DirectorySizeProbeOutcome(finalResult, totalSize, false, thresholdBytes);
            }
            catch (Exception)
            {
                return new DirectorySizeProbeOutcome(DirectorySizeProbeResult.Indeterminate, 0, false, thresholdBytes);
            }
        }

        private static DirectorySizeProbeResult AccumulateDirectorySize(
            DirectoryInfo directory,
            long thresholdBytes,
            int depth,
            ref long totalSize,
            ref bool skippedReparsePoints)
        {
            if (depth > MaxDirectoryDepth)
            {
                throw new IOException($"Directory tree exceeds the maximum supported depth of {MaxDirectoryDepth}.");
            }

            foreach (var file in directory.GetFiles())
            {
                totalSize += file.Length;
                if (totalSize >= thresholdBytes)
                {
                    return DirectorySizeProbeResult.AtOrAboveThreshold;
                }
            }

            foreach (var subDirectory in directory.GetDirectories())
            {
                if ((subDirectory.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    skippedReparsePoints = true;
                    continue;
                }

                var subDirectoryResult = AccumulateDirectorySize(
                    subDirectory, thresholdBytes, depth + 1, ref totalSize, ref skippedReparsePoints);
                if (subDirectoryResult == DirectorySizeProbeResult.AtOrAboveThreshold)
                {
                    return subDirectoryResult;
                }
            }

            return DirectorySizeProbeResult.BelowThreshold;
        }
    }
}
