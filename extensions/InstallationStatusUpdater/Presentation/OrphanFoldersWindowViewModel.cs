using InstallationStatusUpdater.Domain.Results;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace InstallationStatusUpdater.Presentation
{
    public class OrphanFoldersWindowViewModel : ObservableObject
    {
        public ObservableCollection<OrphanFolderViewModel> Folders { get; }

        public int TotalFolders => Folders.Count;
        public long TotalBytes => Folders.Sum(x => x.SizeBytes);
        public string TotalWastedSpaceFormatted => SizeDisplayFormatter.Format(TotalBytes, false);

        public event EventHandler CloseRequested;

        public RelayCommand OpenSelectedCommand { get; }
        public RelayCommand CopyPathsCommand { get; }
        public RelayCommand CloseCommand { get; }

        public OrphanFoldersWindowViewModel(IEnumerable<OrphanFolderData> orphanFolders)
        {
            Folders = new ObservableCollection<OrphanFolderViewModel>(
                orphanFolders.Select(x => new OrphanFolderViewModel(x)));

            OpenSelectedCommand = new RelayCommand(OpenSelected, () => Folders.Any(x => x.IsSelected));
            CopyPathsCommand = new RelayCommand(CopyPaths, () => Folders.Any());
            CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
        }

        private void OpenSelected()
        {
            var selected = Folders.FirstOrDefault(x => x.IsSelected);
            selected?.OpenInExplorer();
        }

        private void CopyPaths()
        {
            var selectedOrAll = Folders.Where(x => x.IsSelected).ToList();
            if (selectedOrAll.Count == 0)
            {
                selectedOrAll = Folders.ToList();
            }

            var text = string.Join(Environment.NewLine, selectedOrAll.Select(x => x.FullPath));
            try
            {
                Clipboard.SetText(text);
            }
            catch (Exception)
            {
            }
        }
    }
}
