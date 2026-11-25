using InstallationStatusUpdater.Domain.Results;
using InstallationStatusUpdater.Domain.ValueObjects;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace InstallationStatusUpdater.Application
{
    public static class OrphanFoldersScanner
    {
        public static List<OrphanFolderData> Scan(
            IPlayniteAPI playniteApi,
            IEnumerable<WatchedDirectory> detectionDirectories,
            Action<string> reportProgress = null,
            CancellationToken cancelToken = default)
        {
            var results = new List<OrphanFolderData>();
            if (playniteApi?.Database?.Games == null || detectionDirectories == null)
            {
                return results;
            }

            reportProgress?.Invoke(ResourceProvider.GetString("LOCInstallation_Status_Updater_OrphanScannerBuildingIndex") ?? "Indexando jogos da biblioteca...");

            // Collect all known paths from the Playnite database
            var knownPaths = new List<string>();
            foreach (var game in playniteApi.Database.Games)
            {
                if (cancelToken.IsCancellationRequested)
                {
                    return results;
                }

                if (!string.IsNullOrWhiteSpace(game.InstallDirectory))
                {
                    var norm = NormalizePath(game.InstallDirectory);
                    if (norm != null)
                    {
                        knownPaths.Add(norm);
                    }
                }

                if (game.GameActions != null)
                {
                    foreach (var action in game.GameActions)
                    {
                        if (!string.IsNullOrWhiteSpace(action.Path))
                        {
                            var norm = NormalizePath(action.Path);
                            if (norm != null)
                            {
                                knownPaths.Add(norm);
                            }
                        }
                        if (!string.IsNullOrWhiteSpace(action.WorkingDir))
                        {
                            var norm = NormalizePath(action.WorkingDir);
                            if (norm != null)
                            {
                                knownPaths.Add(norm);
                            }
                        }
                    }
                }

                if (game.Roms != null)
                {
                    foreach (var rom in game.Roms)
                    {
                        if (!string.IsNullOrWhiteSpace(rom.Path))
                        {
                            var norm = NormalizePath(rom.Path);
                            if (norm != null)
                            {
                                knownPaths.Add(norm);
                            }
                        }
                    }
                }
            }

            var enabledDirs = detectionDirectories.Where(d => d.Enabled && Directory.Exists(d.DirectoryPath)).ToList();

            foreach (var watchedDir in enabledDirs)
            {
                if (cancelToken.IsCancellationRequested)
                {
                    break;
                }

                string[] subDirs;
                try
                {
                    subDirs = Directory.GetDirectories(watchedDir.DirectoryPath);
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (var subDir in subDirs)
                {
                    if (cancelToken.IsCancellationRequested)
                    {
                        break;
                    }

                    var normalizedSubDir = NormalizePath(subDir);
                    if (normalizedSubDir == null)
                    {
                        continue;
                    }

                    var folderName = Path.GetFileName(subDir);
                    reportProgress?.Invoke(string.Format(
                        ResourceProvider.GetString("LOCInstallation_Status_Updater_OrphanScannerCheckingFolder") ?? "Verificando: {0}",
                        folderName));

                    var subDirPrefix = normalizedSubDir + Path.DirectorySeparatorChar;

                    // A folder is known if any known path is equal to it, inside it, or contains it
                    var isKnown = knownPaths.Any(kp =>
                        kp.Equals(normalizedSubDir, StringComparison.OrdinalIgnoreCase) ||
                        kp.StartsWith(subDirPrefix, StringComparison.OrdinalIgnoreCase) ||
                        normalizedSubDir.StartsWith(kp + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

                    if (!isKnown)
                    {
                        long sizeBytes = 0;
                        DateTime lastModified = DateTime.MinValue;

                        try
                        {
                            lastModified = Directory.GetLastWriteTime(subDir);
                            sizeBytes = CalculateFolderSize(subDir, cancelToken);
                        }
                        catch (Exception)
                        {
                        }

                        results.Add(new OrphanFolderData(
                            folderName,
                            subDir,
                            sizeBytes,
                            lastModified,
                            watchedDir.DirectoryPath));
                    }
                }
            }

            return results.OrderByDescending(x => x.SizeBytes).ToList();
        }

        private static long CalculateFolderSize(string folderPath, CancellationToken cancelToken)
        {
            long size = 0;
            var stack = new Stack<string>();
            stack.Push(folderPath);

            while (stack.Count > 0)
            {
                if (cancelToken.IsCancellationRequested)
                {
                    break;
                }

                var current = stack.Pop();
                try
                {
                    var dirInfo = new DirectoryInfo(current);
                    foreach (var file in dirInfo.EnumerateFiles())
                    {
                        if (cancelToken.IsCancellationRequested)
                        {
                            break;
                        }
                        size += file.Length;
                    }

                    foreach (var sub in dirInfo.EnumerateDirectories())
                    {
                        stack.Push(sub.FullName);
                    }
                }
                catch (Exception)
                {
                }
            }

            return size;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
