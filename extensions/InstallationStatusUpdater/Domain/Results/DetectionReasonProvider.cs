using Playnite.SDK;
using System.Collections.Generic;

namespace InstallationStatusUpdater.Domain.Results
{
    /// <summary>
    /// Maps a detection reason to its localized description so the user can tell why a game
    /// was marked as installed or uninstalled.
    /// </summary>
    public static class DetectionReasonProvider
    {
        private static readonly Dictionary<InstallationDetectionReason, string> _resourceKeys =
            new Dictionary<InstallationDetectionReason, string>
            {
                { InstallationDetectionReason.LaunchActionExists, "LOCInstallation_Status_Updater_DetectionReasonLaunchActionExists" },
                { InstallationDetectionReason.RomExists, "LOCInstallation_Status_Updater_DetectionReasonRomExists" },
                { InstallationDetectionReason.InstallationDirectoryExists, "LOCInstallation_Status_Updater_DetectionReasonInstallationDirectoryExists" },
                { InstallationDetectionReason.InstallationDirectoryMissing, "LOCInstallation_Status_Updater_DetectionReasonInstallationDirectoryMissing" },
                { InstallationDetectionReason.BelowMinimumInstallationSize, "LOCInstallation_Status_Updater_DetectionReasonBelowMinimumInstallationSize" },
                { InstallationDetectionReason.NoInstallationEvidence, "LOCInstallation_Status_Updater_DetectionReasonNoInstallationEvidence" }
            };

        public static string GetLocalizedReason(InstallationDetectionReason reason)
        {
            if (reason == InstallationDetectionReason.None ||
                !_resourceKeys.TryGetValue(reason, out var resourceKey))
            {
                return string.Empty;
            }

            return ResourceProvider.GetString(resourceKey);
        }
    }
}
