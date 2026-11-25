using InstallationStatusUpdater.Domain.Results;
using PluginsCommon;
using System;

namespace InstallationStatusUpdater.Application
{
    /// <summary>
    /// Overrides an "Installed" detection with "Uninstalled" when the game installation
    /// folder exists but holds less than the configured minimum size, which happens with
    /// folders left behind by uninstalls that only removed the game files.
    /// The directory passed in is expected to be the confirmed game root folder, not a
    /// binary subfolder, since binary subfolders alone are usually far smaller than the
    /// configured minimum size.
    /// </summary>
    public static class MinimumInstallationSizeRule
    {
        public static InstallationDetectionResult Apply(
            InstallationDetectionResult detectedStatus,
            string directoryToMeasure,
            long thresholdBytes)
        {
            if (detectedStatus != InstallationDetectionResult.Installed ||
                thresholdBytes <= 0 ||
                directoryToMeasure.IsNullOrEmpty())
            {
                return detectedStatus;
            }

            if (!FileSystem.DirectoryExists(directoryToMeasure))
            {
                return detectedStatus;
            }

            return ApplyProbe(detectedStatus, DirectorySizeProbe.Probe(directoryToMeasure, thresholdBytes));
        }

        public static InstallationDetectionResult ApplyProbe(
            InstallationDetectionResult detectedStatus,
            DirectorySizeProbeResult probeResult)
        {
            if (detectedStatus == InstallationDetectionResult.Installed &&
                probeResult == DirectorySizeProbeResult.BelowThreshold)
            {
                return InstallationDetectionResult.Uninstalled;
            }

            return detectedStatus;
        }
    }
}
