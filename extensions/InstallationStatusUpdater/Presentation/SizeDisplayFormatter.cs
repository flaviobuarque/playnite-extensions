using Playnite.SDK;
using System;

namespace InstallationStatusUpdater.Presentation
{
    /// <summary>
    /// Turns a measured folder size into the text shown by the results window.
    /// A size that only reached the configured limit is printed as a lower bound, since the
    /// counting stopped there on purpose and the real folder may hold much more.
    /// </summary>
    public static class SizeDisplayFormatter
    {
        private const long Kilobyte = 1024L;
        private const long Megabyte = Kilobyte * 1024L;
        private const long Gigabyte = Megabyte * 1024L;

        public static string Format(long? measuredBytes, bool reachedThreshold)
        {
            if (measuredBytes == null)
            {
                return "\u2014";
            }

            return string.Format(
                ResourceProvider.GetString(
                    reachedThreshold
                        ? "LOCInstallation_Status_Updater_SizeAtLeastFormat"
                        : "LOCInstallation_Status_Updater_SizeExactFormat"),
                FormatValue(measuredBytes.Value));
        }

        private static string FormatValue(long bytes)
        {
            if (bytes >= Gigabyte)
            {
                return WithUnit(bytes, Gigabyte, "LOCInstallation_Status_Updater_SizeUnitGB");
            }

            if (bytes >= Megabyte)
            {
                return WithUnit(bytes, Megabyte, "LOCInstallation_Status_Updater_SizeUnitMB");
            }

            if (bytes >= Kilobyte)
            {
                return WithUnit(bytes, Kilobyte, "LOCInstallation_Status_Updater_SizeUnitKB");
            }

            return bytes + " " + ResourceProvider.GetString("LOCInstallation_Status_Updater_SizeUnitB");
        }

        private static string WithUnit(long bytes, long divisor, string unitKey)
        {
            // The current culture keeps the decimal separator the way the rest of Playnite shows it.
            return (bytes / (double)divisor).ToString("0.#") + " " + ResourceProvider.GetString(unitKey);
        }
    }
}
