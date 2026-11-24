using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ExternalGameTracker.Models;
using ExternalGameTracker.Settings;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace ExternalGameTracker.Services
{
    public class SessionManager : IDisposable
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly IPlayniteAPI _playniteApi;
        private readonly Func<ExternalGameTrackerSettings> _settingsProvider;
        private readonly ConcurrentDictionary<Guid, ActiveSession> _activeSessions = new ConcurrentDictionary<Guid, ActiveSession>();

        public SessionManager(IPlayniteAPI playniteApi, Func<ExternalGameTrackerSettings> settingsProvider)
        {
            _playniteApi = playniteApi;
            _settingsProvider = settingsProvider;
        }

        public bool TryStartSession(Game game, int processId, string executablePath)
        {
            var settings = _settingsProvider();
            if (!settings.EnableTracking)
            {
                logger.Debug("[ExternalGameTracker] Rastreamento desabilitado nas configurações.");
                return false;
            }

            // Prevenção de concorrência: Se o Playnite já está marcando como rodando ou já há sessão ativa
            if (game.IsRunning || _activeSessions.ContainsKey(game.Id))
            {
                logger.Debug($"[ExternalGameTracker] Jogo '{game.Name}' já está marcado como em execução.");
                return false;
            }

            Process processHandle;
            try
            {
                processHandle = Process.GetProcessById(processId);
                if (processHandle.HasExited)
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Debug(ex, $"[ExternalGameTracker] Não foi possível obter o handle do processo PID {processId}");
                return false;
            }

            var session = new ActiveSession
            {
                GameId = game.Id,
                GameName = game.Name,
                ProcessId = processId,
                ExecutablePath = executablePath,
                StartTime = DateTime.UtcNow,
                ProcessHandle = processHandle
            };

            if (!_activeSessions.TryAdd(game.Id, session))
            {
                processHandle.Dispose();
                return false;
            }

            try
            {
                // Configura detecção passiva de término do processo
                processHandle.EnableRaisingEvents = true;
                processHandle.Exited += (s, e) => EndSession(game.Id);
            }
            catch (Exception ex)
            {
                logger.Debug(ex, $"[ExternalGameTracker] Falha ao assinar evento Exited para PID {processId}");
                _activeSessions.TryRemove(game.Id, out _);
                processHandle.Dispose();
                return false;
            }

            // Atualiza UI e banco na thread principal do Playnite
            _playniteApi.MainView.UIDispatcher?.Invoke(() =>
            {
                try
                {
                    game.IsRunning = true;
                    game.LastActivity = DateTime.Now;
                    _playniteApi.Database.Games.Update(game);

                    if (settings.ShowNotifications)
                    {
                        _playniteApi.Notifications.Add(new NotificationMessage(
                            $"ext_game_started_{game.Id}",
                            $"[Rastreamento Externo] Detectado: {game.Name} em execução.",
                            NotificationType.Info));
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"[ExternalGameTracker] Erro ao atualizar status de início de '{game.Name}' no Playnite.");
                }
            });

            logger.Info($"[ExternalGameTracker] >>> SESSÃO ATIVADA com sucesso para '{game.Name}' (PID: {processId}) <<<");
            return true;
        }

        public void EndSession(Guid gameId)
        {
            if (!_activeSessions.TryRemove(gameId, out var session))
            {
                return;
            }

            try
            {
                session.ProcessHandle?.Dispose();
            }
            catch
            {
                // Ignora erros ao descartar o handle
            }

            var duration = DateTime.UtcNow - session.StartTime;
            ulong elapsedSeconds = (ulong)duration.TotalSeconds;
            var settings = _settingsProvider();

            _playniteApi.MainView.UIDispatcher?.Invoke(() =>
            {
                try
                {
                    var game = _playniteApi.Database.Games.Get(gameId);
                    if (game != null)
                    {
                        game.IsRunning = false;

                        // Filtro anti-falso positivo (ignora sessões menores que o tempo configurado)
                        if (elapsedSeconds >= (ulong)settings.MinSessionSeconds)
                        {
                            game.Playtime += elapsedSeconds;
                            logger.Info($"[ExternalGameTracker] Sessão encerrada: '{game.Name}'. Tempo adicionado: {elapsedSeconds}s (Total: {game.Playtime}s)");
                        }
                        else
                        {
                            logger.Info($"[ExternalGameTracker] Sessão de '{game.Name}' descartada por ser menor que {settings.MinSessionSeconds}s ({elapsedSeconds}s).");
                        }

                        _playniteApi.Database.Games.Update(game);

                        if (settings.ShowNotifications && elapsedSeconds >= (ulong)settings.MinSessionSeconds)
                        {
                            var ts = TimeSpan.FromSeconds(elapsedSeconds);
                            string timeFormatted = ts.TotalHours >= 1 
                                ? $"{(int)ts.TotalHours}h {ts.Minutes}m" 
                                : $"{ts.Minutes}m {ts.Seconds}s";

                            _playniteApi.Notifications.Add(new NotificationMessage(
                                $"ext_game_stopped_{game.Id}",
                                $"[Rastreamento Externo] Sessão de {game.Name} encerrada ({timeFormatted}).",
                                NotificationType.Info));
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"[ExternalGameTracker] Erro ao encerrar sessão de jogo ({gameId}) no Playnite.");
                }
            });
        }

        public List<ActiveSession> GetActiveSessions()
        {
            return _activeSessions.Values.ToList();
        }

        public void StopAll()
        {
            foreach (var kvp in _activeSessions)
            {
                try
                {
                    kvp.Value.ProcessHandle?.Dispose();
                    var game = _playniteApi.Database.Games.Get(kvp.Key);
                    if (game != null && game.IsRunning)
                    {
                        var duration = DateTime.UtcNow - kvp.Value.StartTime;
                        ulong elapsedSeconds = (ulong)duration.TotalSeconds;
                        game.IsRunning = false;
                        if (elapsedSeconds >= (ulong)_settingsProvider().MinSessionSeconds)
                        {
                            game.Playtime += elapsedSeconds;
                        }
                        _playniteApi.Database.Games.Update(game);
                    }
                }
                catch
                {
                    // Limpeza preventiva
                }
            }
            _activeSessions.Clear();
        }

        public void Dispose()
        {
            StopAll();
        }
    }
}
