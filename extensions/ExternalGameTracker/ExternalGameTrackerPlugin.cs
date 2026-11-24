using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using ExternalGameTracker.Models;
using ExternalGameTracker.Services;
using ExternalGameTracker.Settings;
using ExternalGameTracker.Views;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace ExternalGameTracker
{
    public class ExternalGameTrackerPlugin : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly ExternalGameTrackerSettingsViewModel settingsViewModel;
        private readonly LibraryIndexer indexer;
        private readonly SessionManager sessionManager;
        private readonly ProcessWatcher watcher;

        public override Guid Id { get; } = Guid.Parse("6f982b13-054d-44a6-b51c-81b4d0eb38c9");

        public ExternalGameTrackerPlugin(IPlayniteAPI api) : base(api)
        {
            settingsViewModel = new ExternalGameTrackerSettingsViewModel(this);
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };

            indexer = new LibraryIndexer(api, () => settingsViewModel.Settings);
            sessionManager = new SessionManager(api, () => settingsViewModel.Settings);
            watcher = new ProcessWatcher();
            watcher.ProcessStarted += OnProcessStarted;
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settingsViewModel;
        }

        public override UserControl GetSettingsView(bool firstRunView)
        {
            return new ExternalGameTrackerSettingsView();
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            try
            {
                logger.Info("[ExternalGameTracker] Inicializando indexador de biblioteca e watcher de processos...");
                indexer.Reindex();
                watcher.Start();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "[ExternalGameTracker] Erro ao inicializar plugin.");
            }
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            try
            {
                logger.Info("[ExternalGameTracker] Encerrando watcher e liberando sessões ativas...");
                watcher.Stop();
                sessionManager.StopAll();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "[ExternalGameTracker] Erro ao encerrar plugin.");
            }
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            try
            {
                logger.Info("[ExternalGameTracker] Biblioteca atualizada. Reindexando executáveis...");
                indexer.Reindex();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "[ExternalGameTracker] Erro ao reindexar biblioteca após atualização.");
            }
        }

        public void ReindexLibrary()
        {
            indexer.Reindex();
        }

        private void OnProcessStarted(string processName, int processId)
        {
            if (!settingsViewModel.Settings.EnableTracking)
            {
                return;
            }

            // Descarte O(1): se o processo não for um executável cadastrado em nenhum jogo
            var candidates = indexer.FindCandidates(processName);
            if (candidates == null || candidates.Count == 0)
            {
                return;
            }

            try
            {
                logger.Info($"[ExternalGameTracker] Candidato detectado: '{processName}' (PID: {processId}). Obtendo caminho completo...");

                // Obtém caminho completo de forma compatível com Anti-Cheat (Ricochet/COD, EAC, BattlEye)
                string fullPath = ProcessHelper.GetProcessExecutablePath(processId);
                if (string.IsNullOrEmpty(fullPath))
                {
                    logger.Debug($"[ExternalGameTracker] Não foi possível resolver caminho para PID {processId} ({processName}).");
                    return;
                }

                logger.Info($"[ExternalGameTracker] Caminho resolvido: '{fullPath}' para processo '{processName}' (PID: {processId})");

                foreach (var target in candidates)
                {
                    bool isMatch = false;

                    // 1. Validação de caminho idêntico
                    if (!string.IsNullOrEmpty(target.ExpectedExecutablePath) &&
                        string.Equals(fullPath, target.ExpectedExecutablePath, StringComparison.OrdinalIgnoreCase))
                    {
                        isMatch = true;
                    }
                    // 2. Validação por pasta de instalação (jogos da Steam / Battle.net)
                    else if (!string.IsNullOrEmpty(target.InstallDirectory) &&
                             fullPath.StartsWith(target.InstallDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        isMatch = true;
                    }

                    if (isMatch)
                    {
                        logger.Info($"[ExternalGameTracker] Match confirmado com o jogo: '{target.Game.Name}'. Iniciando sessão...");
                        if (sessionManager.TryStartSession(target.Game, processId, fullPath))
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"[ExternalGameTracker] Falha ao inspecionar processo {processName} ({processId})");
            }
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem
            {
                MenuSection = "@External Game Tracker",
                Description = "Reescanear executáveis da biblioteca agora",
                Action = a =>
                {
                    indexer.Reindex();
                    PlayniteApi.Dialogs.ShowMessage(
                        $"A biblioteca foi reindexada com sucesso!\n\n• Jogos mapeados: {indexer.IndexedGamesCount}\n• Executáveis monitorados no cache: {indexer.IndexedExecutablesCount}",
                        "External Game Tracker");
                }
            };

            yield return new MainMenuItem
            {
                MenuSection = "@External Game Tracker",
                Description = "Ver processos de jogos ativos no momento",
                Action = a =>
                {
                    var active = sessionManager.GetActiveSessions();
                    if (active.Count == 0)
                    {
                        PlayniteApi.Dialogs.ShowMessage("Nenhum jogo monitorado externamente está em execução no momento.", "External Game Tracker");
                    }
                    else
                    {
                        var lines = active.Select(s =>
                        {
                            var elapsed = DateTime.UtcNow - s.StartTime;
                            string elapsedStr = $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";
                            return $"• {s.GameName} (PID: {s.ProcessId})\n  Tempo decorrido: {elapsedStr}\n  Caminho: {s.ExecutablePath}";
                        });

                        PlayniteApi.Dialogs.ShowMessage(string.Join("\n\n", lines), "Jogos Ativos no Momento");
                    }
                }
            };
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            var selectedGame = args.Games?.FirstOrDefault();
            if (selectedGame == null) yield break;

            bool isIgnored = settingsViewModel.Settings.IgnoredGameIds != null &&
                             settingsViewModel.Settings.IgnoredGameIds.Contains(selectedGame.Id);

            yield return new GameMenuItem
            {
                MenuSection = "External Game Tracker",
                Description = isIgnored ? "Habilitar rastreamento para este jogo" : "Ignorar rastreamento para este jogo",
                Action = a =>
                {
                    if (settingsViewModel.Settings.IgnoredGameIds == null)
                    {
                        settingsViewModel.Settings.IgnoredGameIds = new List<Guid>();
                    }

                    if (isIgnored)
                    {
                        settingsViewModel.Settings.IgnoredGameIds.Remove(selectedGame.Id);
                        PlayniteApi.Dialogs.ShowMessage($"O jogo '{selectedGame.Name}' voltará a ser rastreado.", "External Game Tracker");
                    }
                    else
                    {
                        settingsViewModel.Settings.IgnoredGameIds.Add(selectedGame.Id);
                        PlayniteApi.Dialogs.ShowMessage($"O jogo '{selectedGame.Name}' agora será ignorado pelo rastreador.", "External Game Tracker");
                    }

                    SavePluginSettings(settingsViewModel.Settings);
                    indexer.Reindex();
                }
            };

            yield return new GameMenuItem
            {
                MenuSection = "External Game Tracker",
                Description = "Ver executáveis mapeados deste jogo (Diagnóstico)",
                Action = a =>
                {
                    var mapped = indexer.GetMappedExecutables(selectedGame);
                    if (mapped.Count == 0)
                    {
                        PlayniteApi.Dialogs.ShowMessage(
                            $"Nenhum executável foi mapeado para '{selectedGame.Name}'.\n\nPasta cadastrada: '{selectedGame.InstallDirectory}'\n\nVerifique se o jogo está instalado ou se a pasta possui arquivos .exe.",
                            "Diagnóstico de Executáveis");
                    }
                    else
                    {
                        PlayniteApi.Dialogs.ShowMessage(
                            $"Executáveis vinculados a '{selectedGame.Name}':\n\n" + string.Join("\n", mapped),
                            "Diagnóstico de Executáveis");
                    }
                }
            };
        }
    }
}
