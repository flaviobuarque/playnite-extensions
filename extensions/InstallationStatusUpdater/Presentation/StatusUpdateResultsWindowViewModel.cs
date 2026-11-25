using InstallationStatusUpdater.Application;
using InstallationStatusUpdater.Domain.Results;
using Playnite.SDK;
using PlayniteUtilitiesCommon;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace InstallationStatusUpdater.Presentation
{
    public class StatusUpdateResultsWindowViewModel : ObservableObject
    {
        private readonly Action<ICollection<Guid>> _confirmAction;
        private readonly Action<ICollection<Guid>> _excludeAction;
        private readonly Action _discardAction;
        private readonly IPlayniteAPI _playniteApi;
        private readonly PathsResolver _pathsResolver;

        public ObservableCollection<StatusUpdateResultEntryViewModel> GamesSetAsInstalled { get; }
        public ObservableCollection<StatusUpdateResultEntryViewModel> GamesSetAsUninstalled { get; }
        public ObservableCollection<SourceFilterItem> SourceFilters { get; } = new ObservableCollection<SourceFilterItem>();

        public int TotalGamesSetAsInstalled => GamesSetAsInstalled.Count;
        public int TotalGamesSetAsUninstalled => GamesSetAsUninstalled.Count;

        public long TotalInstalledBytes => GamesSetAsInstalled.Where(x => x.SizeBytes.HasValue).Sum(x => x.SizeBytes.Value);
        public long TotalUninstalledBytes => GamesSetAsUninstalled.Where(x => x.SizeBytes.HasValue).Sum(x => x.SizeBytes.Value);

        public bool HasInstalledSizeDelta => TotalInstalledBytes > 0;
        public bool HasUninstalledSizeDelta => TotalUninstalledBytes > 0;

        public string InstalledSizeDeltaFormatted => HasInstalledSizeDelta ? $"(+{SizeDisplayFormatter.Format(TotalInstalledBytes, false)})" : string.Empty;
        public string UninstalledSizeDeltaFormatted => HasUninstalledSizeDelta ? $"(-{SizeDisplayFormatter.Format(TotalUninstalledBytes, false)})" : string.Empty;

        public string InstalledTabHeader => $"{ResourceProvider.GetString("LOCInstallation_Status_Updater_StatusUpdateResultsWindowSetInstalledLabel")} ({TotalGamesSetAsInstalled})";
        public string UninstalledTabHeader => $"{ResourceProvider.GetString("LOCInstallation_Status_Updater_StatusUpdateResultsWindowSetUninstalledLabel")} ({TotalGamesSetAsUninstalled})";

        public Visibility InstalledTabVisibility => TotalGamesSetAsInstalled > 0 ? Visibility.Visible : Visibility.Collapsed;
        public Visibility UninstalledTabVisibility => TotalGamesSetAsUninstalled > 0 ? Visibility.Visible : Visibility.Collapsed;

        public double InstalledCardOpacity => TotalGamesSetAsInstalled > 0 ? 1.0 : 0.6;
        public double UninstalledCardOpacity => TotalGamesSetAsUninstalled > 0 ? 1.0 : 0.6;

        private bool _isInstalledTabSelected;
        public bool IsInstalledTabSelected
        {
            get => _isInstalledTabSelected;
            set
            {
                if (_isInstalledTabSelected != value)
                {
                    _isInstalledTabSelected = value;
                    OnPropertyChanged();
                    RefreshSourceFilters();
                }
            }
        }

        private bool _isUninstalledTabSelected;
        public bool IsUninstalledTabSelected
        {
            get => _isUninstalledTabSelected;
            set
            {
                if (_isUninstalledTabSelected != value)
                {
                    _isUninstalledTabSelected = value;
                    OnPropertyChanged();
                    RefreshSourceFilters();
                }
            }
        }

        public bool RequiresConfirmation { get; }

        public string InstalledHeaderLabel { get; }
        public string UninstalledHeaderLabel { get; }
        public string SearchLabel { get; }
        public string ExcludeFromSizeRuleButtonLabel { get; }
        public string ExcludeFromSizeRuleToolTip { get; }

        public Visibility ApplyButtonsVisibility { get; }
        public Visibility RevertButtonsVisibility { get; }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value ?? string.Empty;
                    OnPropertyChanged();
                    ApplySearchFilter();
                }
            }
        }

        public string SelectedSourceFilter { get; private set; }

        public string SelectAllCommandLabel => AreAllEntriesSelected
            ? ResourceProvider.GetString("LOCInstallation_Status_Updater_StatusUpdateResultsWindowClearSelectionButtonLabel")
            : ResourceProvider.GetString("LOCInstallation_Status_Updater_StatusUpdateResultsWindowSelectAllButtonLabel");

        private bool AreAllEntriesSelected =>
            GetVisibleEntries().Any() && GetVisibleEntries().All(entry => entry.IsSelected);

        public event EventHandler CloseRequested;

        public RelayCommand SelectAllCommand { get; }
        public RelayCommand ApplySelectedCommand { get; }
        public RelayCommand ApplyCommand { get; }
        public RelayCommand DiscardCommand { get; }
        public RelayCommand RevertSelectedCommand { get; }
        public RelayCommand RevertAllCommand { get; }
        public RelayCommand ExcludeFromSizeRuleCommand { get; }
        public RelayCommand<SourceFilterItem> SelectSourceFilterCommand { get; }

        public RelayCommand<StatusUpdateResultEntryViewModel> OpenFolderCommand { get; }
        public RelayCommand<StatusUpdateResultEntryViewModel> ExcludeSingleFromSizeRuleCommand { get; }
        public RelayCommand<StatusUpdateResultEntryViewModel> AddIgnoreCommand { get; }

        public StatusUpdateResultsWindowViewModel(
            StatusUpdateResults updateResults,
            bool requiresConfirmation,
            Action<ICollection<Guid>> confirmAction = null,
            Action<ICollection<Guid>> excludeAction = null,
            Action discardAction = null,
            IPlayniteAPI playniteApi = null,
            PathsResolver pathsResolver = null)
        {
            if (updateResults is null)
            {
                throw new ArgumentNullException(nameof(updateResults));
            }

            _confirmAction = confirmAction;
            _excludeAction = excludeAction;
            _discardAction = discardAction;
            _playniteApi = playniteApi;
            _pathsResolver = pathsResolver;
            RequiresConfirmation = requiresConfirmation;

            InstalledHeaderLabel = ResourceProvider.GetString(requiresConfirmation
                ? "LOCInstallation_Status_Updater_StatusUpdateResultsWindowGamesToSetAsInstalledLabel"
                : "LOCInstallation_Status_Updater_StatusUpdateResultsWindowSetGamesInstalledLabel");
            UninstalledHeaderLabel = ResourceProvider.GetString(requiresConfirmation
                ? "LOCInstallation_Status_Updater_StatusUpdateResultsWindowGamesToSetAsUninstalledLabel"
                : "LOCInstallation_Status_Updater_StatusUpdateResultsWindowSetGamesUninstalledLabel");
            SearchLabel = ResourceProvider.GetString(
                "LOCInstallation_Status_Updater_StatusUpdateResultsWindowSearchLabel");
            ExcludeFromSizeRuleButtonLabel = ResourceProvider.GetString(
                "LOCInstallation_Status_Updater_StatusUpdateResultsWindowExcludeFromSizeRuleLabel");
            ExcludeFromSizeRuleToolTip = ResourceProvider.GetString(
                "LOCInstallation_Status_Updater_StatusUpdateResultsWindowExcludeFromSizeRuleToolTip");
            ApplyButtonsVisibility = requiresConfirmation ? Visibility.Visible : Visibility.Collapsed;
            RevertButtonsVisibility = requiresConfirmation ? Visibility.Collapsed : Visibility.Visible;

            GamesSetAsInstalled = new ObservableCollection<StatusUpdateResultEntryViewModel>(
                updateResults.Installed
                    .OrderBy(x => x.DetectionReason)
                    .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Select(CreateEntryViewModel));

            GamesSetAsUninstalled = new ObservableCollection<StatusUpdateResultEntryViewModel>(
                updateResults.Uninstalled
                    .OrderBy(x => x.DetectionReason)
                    .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Select(CreateEntryViewModel));

            if (TotalGamesSetAsInstalled > 0)
            {
                _isInstalledTabSelected = true;
                _isUninstalledTabSelected = false;
            }
            else
            {
                _isInstalledTabSelected = false;
                _isUninstalledTabSelected = true;
            }

            Func<bool> hasVisibleEntries = () => GetVisibleEntries().Any();
            Func<bool> hasSelectedEntries = () =>
                GamesSetAsInstalled.Any(x => x.IsSelected) || GamesSetAsUninstalled.Any(x => x.IsSelected);

            SelectAllCommand = new RelayCommand(ToggleSelectAll, hasVisibleEntries);
            ApplySelectedCommand = new RelayCommand(() => Confirm(GetSelectedGameIds()), hasSelectedEntries);
            ApplyCommand = new RelayCommand(() => Confirm(GetVisibleGameIds()), hasVisibleEntries);
            DiscardCommand = new RelayCommand(() =>
            {
                _discardAction?.Invoke();
                CloseRequested?.Invoke(this, EventArgs.Empty);
            });
            RevertSelectedCommand = new RelayCommand(() => Confirm(GetSelectedGameIds()), hasSelectedEntries);
            RevertAllCommand = new RelayCommand(() => Confirm(GetVisibleGameIds()), hasVisibleEntries);
            ExcludeFromSizeRuleCommand = new RelayCommand(
                ExcludeFromSizeRule,
                () => hasSelectedEntries() && _excludeAction != null);

            SelectSourceFilterCommand = new RelayCommand<SourceFilterItem>(SelectSourceFilter);
            OpenFolderCommand = new RelayCommand<StatusUpdateResultEntryViewModel>(OpenFolder);
            ExcludeSingleFromSizeRuleCommand = new RelayCommand<StatusUpdateResultEntryViewModel>(ExcludeSingle);
            AddIgnoreCommand = new RelayCommand<StatusUpdateResultEntryViewModel>(AddIgnore);

            RefreshSourceFilters();
        }

        private StatusUpdateResultEntryViewModel CreateEntryViewModel(UpdatedEntryData entry)
        {
            return new StatusUpdateResultEntryViewModel(entry, CommandManager.InvalidateRequerySuggested);
        }

        public void RefreshSourceFilters()
        {
            SourceFilters.Clear();
            var activeList = IsInstalledTabSelected ? GamesSetAsInstalled : GamesSetAsUninstalled;
            if (activeList.Count == 0)
            {
                return;
            }

            var allLabel = ResourceProvider.GetString("LOCInstallation_Status_Updater_FilterAllSources");
            if (string.IsNullOrWhiteSpace(allLabel))
            {
                allLabel = "Todas";
            }

            var allItem = new SourceFilterItem(null, $"{allLabel} ({activeList.Count})", activeList.Count, "#1E2228", "#475569", "#F4F4F5")
            {
                IsSelected = string.IsNullOrEmpty(SelectedSourceFilter)
            };
            SourceFilters.Add(allItem);

            var groups = activeList.GroupBy(x => x.Source ?? "Manual")
                                   .OrderByDescending(g => g.Count())
                                   .ThenBy(g => g.Key);

            foreach (var g in groups)
            {
                var style = SourceBadgeProvider.GetStyle(g.Key);
                var item = new SourceFilterItem(g.Key, $"{g.Key} ({g.Count()})", g.Count(), style.Background, style.BorderBrush, style.Foreground)
                {
                    IsSelected = string.Equals(SelectedSourceFilter, g.Key, StringComparison.OrdinalIgnoreCase)
                };
                SourceFilters.Add(item);
            }
        }

        private void SelectSourceFilter(SourceFilterItem item)
        {
            if (item == null || item.SourceName == null)
            {
                SelectedSourceFilter = null;
            }
            else if (string.Equals(SelectedSourceFilter, item.SourceName, StringComparison.OrdinalIgnoreCase))
            {
                SelectedSourceFilter = null;
            }
            else
            {
                SelectedSourceFilter = item.SourceName;
            }

            foreach (var f in SourceFilters)
            {
                f.IsSelected = (string.IsNullOrEmpty(SelectedSourceFilter) && f.SourceName == null) ||
                               string.Equals(SelectedSourceFilter, f.SourceName, StringComparison.OrdinalIgnoreCase);
            }

            ApplySearchFilter();
        }

        private void OpenFolder(StatusUpdateResultEntryViewModel entry)
        {
            if (entry == null || _playniteApi == null)
            {
                return;
            }

            var game = _playniteApi.Database.Games.Get(entry.GameId);
            if (game == null)
            {
                return;
            }

            var path = _pathsResolver?.GetInstallDirForDetection(game);
            if (string.IsNullOrEmpty(path))
            {
                path = game.InstallDirectory;
            }

            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                try
                {
                    Process.Start("explorer.exe", path);
                }
                catch (Exception)
                {
                }
            }
            else
            {
                var msg = ResourceProvider.GetString("LOCInstallation_Status_Updater_FolderNotFoundMessage");
                if (string.IsNullOrWhiteSpace(msg))
                {
                    msg = "A pasta deste jogo não existe ou não pôde ser encontrada.";
                }
                _playniteApi.Dialogs.ShowMessage(msg, "Installation Status Updater");
            }
        }

        private void ExcludeSingle(StatusUpdateResultEntryViewModel entry)
        {
            if (entry == null || _excludeAction == null)
            {
                return;
            }

            _excludeAction(new[] { entry.GameId });
            var msg = ResourceProvider.GetString("LOCInstallation_Status_Updater_GameExcludedSuccessMessage");
            if (string.IsNullOrWhiteSpace(msg))
            {
                msg = "Jogo excluído da regra de tamanho mínimo para as próximas verificações.";
            }
            _playniteApi?.Dialogs?.ShowMessage(msg, "Installation Status Updater");
        }

        private void AddIgnore(StatusUpdateResultEntryViewModel entry)
        {
            if (entry == null || _playniteApi == null)
            {
                return;
            }

            var game = _playniteApi.Database.Games.Get(entry.GameId);
            if (game == null)
            {
                return;
            }

            PlayniteUtilities.AddFeatureToGames(_playniteApi, new[] { game }, "[Status Updater] Ignore");
            _playniteApi.Dialogs.ShowMessage(
                string.Format(ResourceProvider.GetString("LOCInstallation_Status_Updater_StatusUpdaterAddIgnoreFeatureMessage"), 1),
                "Installation Status Updater");
        }

        private void ApplySearchFilter()
        {
            ApplyFilterTo(GamesSetAsInstalled);
            ApplyFilterTo(GamesSetAsUninstalled);
        }

        private void ApplyFilterTo(ObservableCollection<StatusUpdateResultEntryViewModel> entries)
        {
            var view = CollectionViewSource.GetDefaultView(entries);
            if (view is null)
            {
                return;
            }

            view.Filter = MatchesFilter;
        }

        private bool MatchesFilter(object candidate)
        {
            if (!(candidate is StatusUpdateResultEntryViewModel entry))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(SelectedSourceFilter) &&
                !string.Equals(entry.Source, SelectedSourceFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var searchText = _searchText.Trim();
            if (searchText.Length == 0)
            {
                return true;
            }

            return Contains(entry.Name, searchText) ||
                Contains(entry.Source, searchText) ||
                Contains(entry.Reason, searchText) ||
                entry.Platforms.Any(platform => Contains(platform, searchText));
        }

        private static bool Contains(string value, string searchText)
        {
            return value != null &&
                value.IndexOf(searchText, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private IEnumerable<StatusUpdateResultEntryViewModel> GetVisibleEntries()
        {
            var activeEntries = IsInstalledTabSelected ? GamesSetAsInstalled : GamesSetAsUninstalled;
            var view = CollectionViewSource.GetDefaultView(activeEntries);
            if (view is null)
            {
                return Enumerable.Empty<StatusUpdateResultEntryViewModel>();
            }

            return view.Cast<StatusUpdateResultEntryViewModel>();
        }

        private ICollection<Guid> GetSelectedGameIds()
        {
            return GamesSetAsInstalled
                .Concat(GamesSetAsUninstalled)
                .Where(entry => entry.IsSelected)
                .Select(entry => entry.GameId)
                .ToList();
        }

        private ICollection<Guid> GetVisibleGameIds()
        {
            return GetVisibleEntries()
                .Select(entry => entry.GameId)
                .ToList();
        }

        private void Confirm(ICollection<Guid> gameIds)
        {
            if (!gameIds.Any())
            {
                return;
            }

            _confirmAction?.Invoke(gameIds);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ExcludeFromSizeRule()
        {
            var selectedIds = GetSelectedGameIds();
            if (!selectedIds.Any())
            {
                return;
            }

            _excludeAction?.Invoke(selectedIds);
            RemoveEntries(selectedIds);
            NotifyCountsChanged();
        }

        private void ToggleSelectAll()
        {
            var selectAll = !AreAllEntriesSelected;
            foreach (var entry in GetVisibleEntries())
            {
                entry.IsSelected = selectAll;
            }

            NotifyCountsChanged();
        }

        private void RemoveEntries(ICollection<Guid> gameIds)
        {
            var ids = new HashSet<Guid>(gameIds);
            RemoveWhere(GamesSetAsInstalled, ids);
            RemoveWhere(GamesSetAsUninstalled, ids);
        }

        private static void RemoveWhere(
            ObservableCollection<StatusUpdateResultEntryViewModel> entries,
            ICollection<Guid> gameIds)
        {
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                if (gameIds.Contains(entries[i].GameId))
                {
                    entries.RemoveAt(i);
                }
            }
        }

        private void NotifyCountsChanged()
        {
            OnPropertyChanged(nameof(TotalGamesSetAsInstalled));
            OnPropertyChanged(nameof(TotalGamesSetAsUninstalled));
            OnPropertyChanged(nameof(TotalInstalledBytes));
            OnPropertyChanged(nameof(TotalUninstalledBytes));
            OnPropertyChanged(nameof(InstalledSizeDeltaFormatted));
            OnPropertyChanged(nameof(UninstalledSizeDeltaFormatted));
            OnPropertyChanged(nameof(HasInstalledSizeDelta));
            OnPropertyChanged(nameof(HasUninstalledSizeDelta));
            OnPropertyChanged(nameof(InstalledTabHeader));
            OnPropertyChanged(nameof(UninstalledTabHeader));
            OnPropertyChanged(nameof(InstalledTabVisibility));
            OnPropertyChanged(nameof(UninstalledTabVisibility));
            OnPropertyChanged(nameof(InstalledCardOpacity));
            OnPropertyChanged(nameof(UninstalledCardOpacity));
            OnPropertyChanged(nameof(SelectAllCommandLabel));

            if (TotalGamesSetAsInstalled == 0 && TotalGamesSetAsUninstalled > 0 && !IsUninstalledTabSelected)
            {
                IsInstalledTabSelected = false;
                IsUninstalledTabSelected = true;
            }
            else if (TotalGamesSetAsUninstalled == 0 && TotalGamesSetAsInstalled > 0 && !IsInstalledTabSelected)
            {
                IsInstalledTabSelected = true;
                IsUninstalledTabSelected = false;
            }

            RefreshSourceFilters();
        }
    }
}
