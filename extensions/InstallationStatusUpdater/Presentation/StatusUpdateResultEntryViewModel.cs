using InstallationStatusUpdater.Domain.Results;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;

namespace InstallationStatusUpdater.Presentation
{
    /// <summary>
    /// Presentation of a single changed game, adding the selection state needed by the results
    /// window on top of the detected data. The state is kept in sync with the DataGrid row
    /// selection through a two way binding, so a single click on a row selects it.
    /// </summary>
    public class StatusUpdateResultEntryViewModel : ObservableObject
    {
        private readonly Action _selectionChanged;
        private bool _isSelected;

        public Guid GameId { get; }
        public string Name { get; }
        public string Source { get; }
        public string SourceBadgeBackground { get; }
        public string SourceBadgeBorderBrush { get; }
        public string SourceBadgeForeground { get; }
        public IEnumerable<string> Platforms { get; }
        public string Reason { get; }

        /// <summary>
        /// Folder size measured by the size rule, or previously recorded by Playnite, or a dash when unknown.
        /// </summary>
        public string SizeDisplay { get; }
        public string SizeTooltip { get; }
        public bool IsPreviousSize { get; }
        public long? SizeBytes { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
                _selectionChanged?.Invoke();
            }
        }

        public StatusUpdateResultEntryViewModel(
            UpdatedEntryData entry,
            Action selectionChanged = null)
        {
            if (entry is null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            GameId = entry.Id;
            Name = entry.Name;
            Source = entry.Source;
            Platforms = entry.Platforms?.ToList() ?? Enumerable.Empty<string>();
            Reason = entry.Reason;

            var badgeStyle = SourceBadgeProvider.GetStyle(entry.Source);
            SourceBadgeBackground = badgeStyle.Background;
            SourceBadgeBorderBrush = badgeStyle.BorderBrush;
            SourceBadgeForeground = badgeStyle.Foreground;

            if (entry.MeasuredBytes != null)
            {
                SizeBytes = entry.MeasuredBytes.Value;
                SizeDisplay = SizeDisplayFormatter.Format(entry.MeasuredBytes, entry.SizeReachedThreshold);
                var tooltipStr = ResourceProvider.GetString("LOCInstallation_Status_Updater_SizeTooltipMeasured");
                SizeTooltip = !string.IsNullOrWhiteSpace(tooltipStr) ? tooltipStr : "Tamanho medido no disco";
                IsPreviousSize = false;
            }
            else if (entry.PreviousInstallSize != null && entry.PreviousInstallSize.Value > 0)
            {
                SizeBytes = (long)entry.PreviousInstallSize.Value;
                SizeDisplay = SizeDisplayFormatter.Format((long)entry.PreviousInstallSize.Value, false);
                var tooltipStr = ResourceProvider.GetString("LOCInstallation_Status_Updater_SizeTooltipPrevious");
                SizeTooltip = !string.IsNullOrWhiteSpace(tooltipStr) ? tooltipStr : "Tamanho registrado no Playnite antes da desinstalação";
                IsPreviousSize = true;
            }
            else
            {
                SizeBytes = null;
                SizeDisplay = "\u2014";
                var tooltipStr = ResourceProvider.GetString("LOCInstallation_Status_Updater_SizeTooltipUnknown");
                SizeTooltip = !string.IsNullOrWhiteSpace(tooltipStr) ? tooltipStr : "Tamanho não disponível";
                IsPreviousSize = false;
            }

            _selectionChanged = selectionChanged;
        }
    }
}
