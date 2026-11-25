using System;

namespace InstallationStatusUpdater.Presentation
{
    public struct SourceBadgeStyle
    {
        public string Background { get; }
        public string BorderBrush { get; }
        public string Foreground { get; }

        public SourceBadgeStyle(string background, string borderBrush, string foreground)
        {
            Background = background;
            BorderBrush = borderBrush;
            Foreground = foreground;
        }
    }

    public static class SourceBadgeProvider
    {
        public static SourceBadgeStyle GetStyle(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return new SourceBadgeStyle("#262015", "#B45309", "#F59E0B");
            }

            var s = source.Trim();

            if (s.IndexOf("steam", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new SourceBadgeStyle("#101D2C", "#2A475E", "#66C0F4");
            }

            if (s.IndexOf("epic", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new SourceBadgeStyle("#222226", "#52525B", "#F4F4F5");
            }

            if (s.IndexOf("gog", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new SourceBadgeStyle("#261633", "#7C3AED", "#C084FC");
            }

            if (s.IndexOf("xbox", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("microsoft", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new SourceBadgeStyle("#112716", "#16A34A", "#4ADE80");
            }

            if (s.IndexOf("ubisoft", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("uplay", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new SourceBadgeStyle("#102038", "#2563EB", "#60A5FA");
            }

            if (s.IndexOf("ea", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("origin", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new SourceBadgeStyle("#2E1814", "#EA580C", "#FB923C");
            }

            if (s.IndexOf("battle.net", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("blizzard", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new SourceBadgeStyle("#0F2438", "#0284C7", "#38BDF8");
            }

            if (s.IndexOf("amazon", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new SourceBadgeStyle("#2A2312", "#D97706", "#FBBF24");
            }

            if (s.IndexOf("riot", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new SourceBadgeStyle("#2D1216", "#DC2626", "#F87171");
            }

            if (s.IndexOf("itch", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new SourceBadgeStyle("#2E1418", "#FA5C5C", "#FFA6A6");
            }

            if (s.IndexOf("manual", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("playnite", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("local", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new SourceBadgeStyle("#262015", "#B45309", "#F59E0B");
            }

            return new SourceBadgeStyle("#1E2228", "#475569", "#94A3B8");
        }
    }
}
