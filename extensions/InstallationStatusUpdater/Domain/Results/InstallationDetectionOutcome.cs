namespace InstallationStatusUpdater.Domain.Results
{
    /// <summary>
    /// Result of the detection performed for a single game, including the reason behind the verdict
    /// and, when the minimum folder size rule measured it, how big the game folder turned out to be.
    /// </summary>
    public class InstallationDetectionOutcome
    {
        public InstallationDetectionResult Result { get; }
        public InstallationDetectionReason Reason { get; }

        /// <summary>
        /// Bytes counted in the game folder, or null when no folder was measured for this game.
        /// </summary>
        public long? MeasuredBytes { get; }

        /// <summary>
        /// True when counting stopped at the size limit instead of reaching the end of the folder,
        /// which makes <see cref="MeasuredBytes"/> a lower bound rather than an exact size.
        /// </summary>
        public bool SizeReachedThreshold { get; }

        public InstallationDetectionOutcome(
            InstallationDetectionResult result,
            InstallationDetectionReason reason,
            long? measuredBytes = null,
            bool sizeReachedThreshold = false)
        {
            Result = result;
            Reason = reason;
            MeasuredBytes = measuredBytes;
            SizeReachedThreshold = sizeReachedThreshold;
        }

        public static InstallationDetectionOutcome Installed(InstallationDetectionReason reason)
        {
            return new InstallationDetectionOutcome(InstallationDetectionResult.Installed, reason);
        }

        public static InstallationDetectionOutcome Uninstalled(InstallationDetectionReason reason)
        {
            return new InstallationDetectionOutcome(InstallationDetectionResult.Uninstalled, reason);
        }

        public static InstallationDetectionOutcome Skipped()
        {
            return new InstallationDetectionOutcome(InstallationDetectionResult.Skipped, InstallationDetectionReason.None);
        }

        /// <summary>
        /// Returns a copy of this outcome with a different verdict, keeping the given reason.
        /// </summary>
        public InstallationDetectionOutcome WithResult(
            InstallationDetectionResult result,
            InstallationDetectionReason reason)
        {
            return new InstallationDetectionOutcome(result, reason, MeasuredBytes, SizeReachedThreshold);
        }

        /// <summary>
        /// Returns a copy of this outcome carrying the measured folder size.
        /// </summary>
        public InstallationDetectionOutcome WithMeasuredSize(long? measuredBytes, bool sizeReachedThreshold)
        {
            return new InstallationDetectionOutcome(
                Result, Reason, measuredBytes, sizeReachedThreshold);
        }
    }
}
