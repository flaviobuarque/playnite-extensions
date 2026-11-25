using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstallationStatusUpdater.Domain.Results
{
    public class UpdatedEntryData
    {
        private static readonly Dictionary<Guid, string> _knownPluginSources = new Dictionary<Guid, string>
        {
            { Guid.Parse("cb91aec9-6843-4172-ac2b-ba7b92704b63"), "Steam" },
            { Guid.Parse("00000002-dbd1-46c6-b5d0-b1cb559c10a0"), "Epic Games" },
            { Guid.Parse("aebe8550-42ef-42ae-9461-2eab8163f5ce"), "GOG" },
            { Guid.Parse("e3c26a3d-d695-4cb7-ac76-292110c4704c"), "Battle.net" },
            { Guid.Parse("85dd7072-a23f-4549-915d-4384da2e4eec"), "EA App" },
            { Guid.Parse("c2f038e5-8b92-4877-9163-0d6378533b6d"), "Ubisoft Connect" },
            { Guid.Parse("7e4fbb5e-2304-4c27-9347-78e30f168086"), "Xbox" },
            { Guid.Parse("402da361-9fa6-4fa0-83dc-34bb31ae01b2"), "Amazon Games" },
            { Guid.Parse("00000001-ebb0-4636-b605-e20406d4277b"), "itch.io" },
            { Guid.Parse("91d13c6f-63d3-42ed-a100-6f811a8387ea"), "Riot Games" }
        };

        public string Name { get; }
        public Guid Id { get; }
        public string Source { get; }
        public Guid PluginId { get; }
        public IEnumerable<string> Platforms { get; }
        public InstallationDetectionReason DetectionReason { get; }
        public string Reason { get; }

        /// <summary>
        /// Folder size measured by the minimum size rule, or null when the rule did not measure it.
        /// </summary>
        public long? MeasuredBytes { get; }

        /// <summary>
        /// True when the measurement stopped at the size limit, making <see cref="MeasuredBytes"/>
        /// a lower bound instead of an exact size.
        /// </summary>
        public bool SizeReachedThreshold { get; }

        /// <summary>
        /// Size previously recorded in Playnite's database before uninstallation, or null if unset.
        /// </summary>
        public ulong? PreviousInstallSize { get; }

        public UpdatedEntryData(Game game, InstallationDetectionReason reason)
            : this(game, reason, null, false)
        {
        }

        public UpdatedEntryData(
            Game game,
            InstallationDetectionReason reason,
            long? measuredBytes,
            bool sizeReachedThreshold)
        {
            Name = game.Name ?? string.Empty;
            Source = ResolveSource(game);
            PluginId = game.PluginId;
            Platforms = game.Platforms?.Select(x => x.Name).ToList() ?? Enumerable.Empty<string>();
            Id = game.Id;
            DetectionReason = reason;
            Reason = DetectionReasonProvider.GetLocalizedReason(reason);
            MeasuredBytes = measuredBytes;
            SizeReachedThreshold = sizeReachedThreshold;
            PreviousInstallSize = game.InstallSize > 0 ? (ulong?)game.InstallSize : null;
        }

        private static string ResolveSource(Game game)
        {
            if (game.Source != null && !string.IsNullOrWhiteSpace(game.Source.Name))
            {
                return game.Source.Name.Trim();
            }

            if (game.PluginId != Guid.Empty && _knownPluginSources.TryGetValue(game.PluginId, out var pluginSource))
            {
                return pluginSource;
            }

            var manualLabel = ResourceProvider.GetString("LOCInstallation_Status_Updater_SourceManual");
            return !string.IsNullOrWhiteSpace(manualLabel) ? manualLabel : "Manual";
        }
    }
}
