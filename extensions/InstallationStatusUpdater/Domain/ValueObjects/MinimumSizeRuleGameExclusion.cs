using System;

namespace InstallationStatusUpdater.Domain.ValueObjects
{
    /// <summary>
    /// A game that the minimum folder size rule must not touch, kept together with its name so the
    /// settings window can show what is being skipped even after the game itself is removed.
    /// </summary>
    public class MinimumSizeRuleGameExclusion
    {
        public Guid GameId { get; set; }
        public string GameName { get; set; } = string.Empty;

        public MinimumSizeRuleGameExclusion()
        {
        }

        public MinimumSizeRuleGameExclusion(Guid gameId, string gameName)
        {
            GameId = gameId;
            GameName = gameName ?? string.Empty;
        }
    }
}
