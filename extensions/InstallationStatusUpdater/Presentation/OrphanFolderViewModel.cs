using InstallationStatusUpdater.Domain.Results;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace InstallationStatusUpdater.Presentation
{
    public class OrphanFolderViewModel : ObservableObject
    {
        private bool _isSelected;

        public string FolderName { get; }
        public string FullPath { get; }
        public long SizeBytes { get; }
        public string SizeDisplay { get; }
        public DateTime LastModified { get; }
        public string LastModifiedDisplay => LastModified != DateTime.MinValue ? LastModified.ToString("g") : "—";
        public string ParentDetectionDir { get; }

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

        public RelayCommand OpenInExplorerCommand { get; }

        public OrphanFolderViewModel(OrphanFolderData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            FolderName = data.FolderName;
            FullPath = data.FullPath;
            SizeBytes = data.SizeBytes;
            SizeDisplay = SizeDisplayFormatter.Format(data.SizeBytes, false);
            LastModified = data.LastModified;
            ParentDetectionDir = data.ParentDetectionDir;

            OpenInExplorerCommand = new RelayCommand(OpenInExplorer);
        }

        public void OpenInExplorer()
        {
            if (Directory.Exists(FullPath))
            {
                try
                {
                    Process.Start("explorer.exe", FullPath);
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
