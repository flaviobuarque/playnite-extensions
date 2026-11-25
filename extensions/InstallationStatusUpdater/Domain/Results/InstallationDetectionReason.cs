namespace InstallationStatusUpdater.Domain.Results
{
    /// <summary>
    /// Why a given installation status was detected for a game. Used to explain status changes
    /// to the user instead of only reporting the resulting status.
    /// </summary>
    public enum InstallationDetectionReason
    {
        /// <summary>
        /// No reason was recorded because the game status did not change.
        /// </summary>
        None = 0,

        /// <summary>
        /// A launch action points to an existing file.
        /// </summary>
        LaunchActionExists = 1,

        /// <summary>
        /// A rom file exists.
        /// </summary>
        RomExists = 2,

        /// <summary>
        /// The installation directory exists.
        /// </summary>
        InstallationDirectoryExists = 3,

        /// <summary>
        /// The installation directory does not exist.
        /// </summary>
        InstallationDirectoryMissing = 4,

        /// <summary>
        /// The installation directory is smaller than the minimum size configured for the game platform.
        /// </summary>
        BelowMinimumInstallationSize = 5,

        /// <summary>
        /// The game has no launch action, no rom and no installation directory to check.
        /// </summary>
        NoInstallationEvidence = 6
    }
}
