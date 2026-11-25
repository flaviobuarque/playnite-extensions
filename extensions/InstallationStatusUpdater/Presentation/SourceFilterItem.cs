using Playnite.SDK;
using System.Collections.Generic;

namespace InstallationStatusUpdater.Presentation
{
    public class SourceFilterItem : ObservableObject
    {
        private bool _isSelected;

        public string SourceName { get; }
        public string DisplayName { get; }
        public int Count { get; set; }
        public string Background { get; }
        public string BorderBrush { get; }
        public string Foreground { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public SourceFilterItem(string sourceName, string displayName, int count, string bg, string border, string fg)
        {
            SourceName = sourceName;
            DisplayName = displayName;
            Count = count;
            Background = bg;
            BorderBrush = border;
            Foreground = fg;
        }
    }
}
