using System;
using System.Collections.Generic;

namespace InstallationStatusUpdater.Domain.ValueObjects
{
    public enum MinimumFolderSizeUnit
    {
        Kilobytes = 0,
        Megabytes = 1,
        Gigabytes = 2
    }

    public class PlatformMinimumSizeRule
    {
        private static readonly Dictionary<MinimumFolderSizeUnit, long> _bytesPerUnit =
            new Dictionary<MinimumFolderSizeUnit, long>
            {
                { MinimumFolderSizeUnit.Kilobytes, 1024L },
                { MinimumFolderSizeUnit.Megabytes, 1024L * 1024L },
                { MinimumFolderSizeUnit.Gigabytes, 1024L * 1024L * 1024L }
            };

        public bool Enabled { get; set; } = true;
        public Guid PlatformId { get; set; } = Guid.Empty;
        public long SizeValue { get; set; } = 1;
        public MinimumFolderSizeUnit Unit { get; set; } = MinimumFolderSizeUnit.Megabytes;

        public long GetThresholdBytes()
        {
            if (SizeValue <= 0)
            {
                return 0;
            }

            if (!_bytesPerUnit.TryGetValue(Unit, out var bytesPerUnit))
            {
                return 0;
            }

            if (SizeValue > long.MaxValue / bytesPerUnit)
            {
                return long.MaxValue;
            }

            return SizeValue * bytesPerUnit;
        }
    }
}
