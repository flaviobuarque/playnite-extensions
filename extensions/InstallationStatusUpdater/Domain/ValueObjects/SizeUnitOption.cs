namespace InstallationStatusUpdater.Domain.ValueObjects
{
    /// <summary>
    /// Display entry for the size unit selector. Units are not localized since KB, MB and GB
    /// are the same in every language.
    /// </summary>
    public class SizeUnitOption
    {
        public MinimumFolderSizeUnit Unit { get; set; }
        public string DisplayName { get; set; }
    }
}
