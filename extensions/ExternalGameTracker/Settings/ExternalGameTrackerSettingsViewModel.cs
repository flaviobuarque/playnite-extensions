using System;
using System.Collections.Generic;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace ExternalGameTracker.Settings
{
    public class ExternalGameTrackerSettingsViewModel : ObservableObject, ISettings
    {
        private readonly ExternalGameTrackerPlugin _plugin;
        private ExternalGameTrackerSettings _editingClone;

        private ExternalGameTrackerSettings _settings;
        public ExternalGameTrackerSettings Settings
        {
            get => _settings;
            set => SetValue(ref _settings, value);
        }

        public ExternalGameTrackerSettingsViewModel(ExternalGameTrackerPlugin plugin)
        {
            _plugin = plugin;
            var savedSettings = plugin.LoadPluginSettings<ExternalGameTrackerSettings>();
            Settings = savedSettings ?? new ExternalGameTrackerSettings();
        }

        public void BeginEdit()
        {
            _editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            Settings = _editingClone;
        }

        public void EndEdit()
        {
            _plugin.SavePluginSettings(Settings);
            _plugin.ReindexLibrary();
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            if (Settings.MinSessionSeconds < 0)
            {
                errors.Add("O tempo mínimo de sessão não pode ser negativo.");
            }
            return errors.Count == 0;
        }
    }
}
