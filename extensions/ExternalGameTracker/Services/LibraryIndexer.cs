using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExternalGameTracker.Models;
using ExternalGameTracker.Settings;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace ExternalGameTracker.Services
{
    public class LibraryIndexer
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly IPlayniteAPI _playniteApi;
        private readonly Func<ExternalGameTrackerSettings> _settingsProvider;
        private Dictionary<string, List<GameTarget>> _exeLookup = new Dictionary<string, List<GameTarget>>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new object();

        public int IndexedGamesCount { get; private set; }
        public int IndexedExecutablesCount { get; private set; }

        public LibraryIndexer(IPlayniteAPI playniteApi, Func<ExternalGameTrackerSettings> settingsProvider)
        {
            _playniteApi = playniteApi;
            _settingsProvider = settingsProvider;
        }

        public void Reindex()
        {
            var settings = _settingsProvider();
            var blacklist = settings.GetBlacklistKeywords().ToList();
            var ignoredGameIds = new HashSet<Guid>(settings.IgnoredGameIds ?? new List<Guid>());
            var newLookup = new Dictionary<string, List<GameTarget>>(StringComparer.OrdinalIgnoreCase);

            int gamesCount = 0;
            int exesCount = 0;

            foreach (var game in _playniteApi.Database.Games)
            {
                if (game == null || ignoredGameIds.Contains(game.Id))
                {
                    continue;
                }

                bool gameMapped = false;
                string installDir = game.InstallDirectory?.Trim('\"', ' ');

                // 1. Mapeamento a partir da Pasta de Instalação (Steam, Battle.net, Epic, etc.)
                if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
                {
                    try
                    {
                        var exeFiles = new List<string>();

                        // Varre raiz da pasta de instalação
                        exeFiles.AddRange(Directory.GetFiles(installDir, "*.exe", SearchOption.TopDirectoryOnly));

                        // Se configurado, varre subpastas (para COD _retail_, UE Binaries\Win64, etc.)
                        if (settings.ScanSubfolders)
                        {
                            var subDirs = Directory.GetDirectories(installDir, "*", SearchOption.TopDirectoryOnly);
                            foreach (var subDir in subDirs)
                            {
                                try
                                {
                                    exeFiles.AddRange(Directory.GetFiles(subDir, "*.exe", SearchOption.TopDirectoryOnly));

                                    // Para subpastas aninhadas como Binaries\Win64 ou bin\x64
                                    string dirName = Path.GetFileName(subDir);
                                    if (dirName.Equals("Binaries", StringComparison.OrdinalIgnoreCase) ||
                                        dirName.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                                        dirName.Equals("game", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var nested = Directory.GetDirectories(subDir, "*", SearchOption.TopDirectoryOnly);
                                        foreach (var n in nested)
                                        {
                                            exeFiles.AddRange(Directory.GetFiles(n, "*.exe", SearchOption.TopDirectoryOnly));
                                        }
                                    }
                                }
                                catch
                                {
                                    // Ignora subpastas sem permissão
                                }
                            }
                        }

                        foreach (var exePath in exeFiles.Distinct(StringComparer.OrdinalIgnoreCase))
                        {
                            string exeName = Path.GetFileName(exePath);
                            if (IsBlacklisted(exeName, blacklist))
                            {
                                continue;
                            }

                            AddTarget(newLookup, exeName, new GameTarget
                            {
                                Game = game,
                                ExpectedExecutablePath = exePath,
                                InstallDirectory = installDir
                            });
                            gameMapped = true;
                            exesCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Debug(ex, $"[ExternalGameTracker] Falha ao varrer pasta do jogo: {game.Name} ({installDir})");
                    }
                }

                // 2. Mapeamento a partir das Ações de Jogo manuais do Playnite
                if (game.GameActions != null)
                {
                    foreach (var action in game.GameActions)
                    {
                        if (string.IsNullOrEmpty(action.Path)) continue;

                        string resolvedPath = action.Path.Trim('\"', ' ');
                        if (!Path.IsPathRooted(resolvedPath) && !string.IsNullOrEmpty(installDir))
                        {
                            resolvedPath = Path.Combine(installDir, resolvedPath);
                        }

                        if (resolvedPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            string exeName = Path.GetFileName(resolvedPath);
                            if (IsBlacklisted(exeName, blacklist)) continue;

                            AddTarget(newLookup, exeName, new GameTarget
                            {
                                Game = game,
                                ExpectedExecutablePath = resolvedPath,
                                InstallDirectory = installDir
                            });
                            gameMapped = true;
                            exesCount++;
                        }
                    }
                }

                if (gameMapped)
                {
                    gamesCount++;
                }
            }

            lock (_lock)
            {
                _exeLookup = newLookup;
                IndexedGamesCount = gamesCount;
                IndexedExecutablesCount = exesCount;
            }

            logger.Info($"[ExternalGameTracker] Indexação concluída: {gamesCount} jogos mapeados com {exesCount} executáveis únicos no cache.");
        }

        public List<GameTarget> FindCandidates(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return null;

            lock (_lock)
            {
                if (_exeLookup.TryGetValue(processName, out var targets))
                {
                    return targets.ToList();
                }
            }

            return null;
        }

        public List<string> GetMappedExecutables(Game game)
        {
            var list = new List<string>();
            if (game == null) return list;

            lock (_lock)
            {
                foreach (var kvp in _exeLookup)
                {
                    foreach (var target in kvp.Value)
                    {
                        if (target.Game.Id == game.Id)
                        {
                            list.Add($"{kvp.Key} -> {target.ExpectedExecutablePath}");
                        }
                    }
                }
            }

            return list.Distinct().ToList();
        }

        private static bool IsBlacklisted(string exeName, List<string> blacklist)
        {
            foreach (var kw in blacklist)
            {
                if (exeName.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static void AddTarget(Dictionary<string, List<GameTarget>> lookup, string exeName, GameTarget target)
        {
            if (!lookup.TryGetValue(exeName, out var list))
            {
                list = new List<GameTarget>();
                lookup[exeName] = list;
            }

            if (!list.Any(t => t.Game.Id == target.Game.Id && string.Equals(t.ExpectedExecutablePath, target.ExpectedExecutablePath, StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(target);
            }
        }
    }
}
