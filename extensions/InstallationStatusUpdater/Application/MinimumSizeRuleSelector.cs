using InstallationStatusUpdater.Domain.ValueObjects;
using System;
using System.Collections.Generic;

namespace InstallationStatusUpdater.Application
{
    /// <summary>
    /// Picks the minimum installation size rule that applies to a game.
    /// Rules are evaluated in list order and the first enabled rule configured for one of the
    /// game platforms wins, so the order of the list defines the priority.
    /// Returns null when no rule matches, which means the folder size must not be checked.
    /// </summary>
    public static class MinimumSizeRuleSelector
    {
        public static long? SelectThresholdBytes(
            IEnumerable<PlatformMinimumSizeRule> rules,
            ICollection<Guid> gamePlatformIds)
        {
            if (rules == null || gamePlatformIds == null || gamePlatformIds.Count == 0)
            {
                return null;
            }

            foreach (var rule in rules)
            {
                if (rule == null ||
                    !rule.Enabled ||
                    rule.PlatformId == Guid.Empty ||
                    rule.SizeValue <= 0)
                {
                    continue;
                }

                if (gamePlatformIds.Contains(rule.PlatformId))
                {
                    return rule.GetThresholdBytes();
                }
            }

            return null;
        }
    }
}
