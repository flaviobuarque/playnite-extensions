using System;
using System.Collections.Generic;

namespace ExternalGameTracker.Settings
{
    public class ExternalGameTrackerSettings
    {
        public bool EnableTracking { get; set; } = true;
        public int MinSessionSeconds { get; set; } = 15;
        public bool ScanSubfolders { get; set; } = true;
        public bool ShowNotifications { get; set; } = true;
        public string Blacklist { get; set; } = "crash,report,installer,unitycrashhandler,easyanticheat,battlenet,epicwebhelper,dxsetup,vcredist,bootstrapper";
        public List<Guid> IgnoredGameIds { get; set; } = new List<Guid>();

        public IEnumerable<string> GetBlacklistKeywords()
        {
            if (string.IsNullOrWhiteSpace(Blacklist)) yield break;
            var parts = Blacklist.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    yield return trimmed;
            }
        }
    }
}
