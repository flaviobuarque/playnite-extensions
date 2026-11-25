using PluginsCommon;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace InstallationStatusUpdater.Application
{
    /// <summary>
    /// Resolves the root directory of a game from its installation directory.
    /// Installation directories often point to binary subfolders such as "bin" or
    /// "Binaries\Win32" instead of the actual game folder, so measuring them alone
    /// drastically underreports the real installation size.
    /// A directory is only returned when it can be confirmed as the game root, either
    /// because its name matches the game name or because it was reached by climbing
    /// known binary subfolder names. Otherwise an empty string is returned so that
    /// callers leave the current installation status untouched.
    /// </summary>
    public static class GameFolderRootResolver
    {
        private const int MaxLevelsToClimb = 5;
        private const int MinimumNameMatchLength = 4;

        private static readonly HashSet<string> _binarySubfolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bin",
            "binary",
            "binaries",
            "win32",
            "win64",
            "x86",
            "x64",
            "app",
            "data",
            "game",
            "engine",
            "system",
            "release",
            "debug",
            "executables"
        };

        public static string ResolveRootDirectory(string gameName, string installDirectory)
        {
            if (installDirectory.IsNullOrEmpty())
            {
                return string.Empty;
            }

            if (!FileSystem.DirectoryExists(installDirectory))
            {
                return string.Empty;
            }

            var normalizedGameName = NormalizeName(gameName);
            if (normalizedGameName.IsNullOrEmpty())
            {
                return string.Empty;
            }

            var currentDirectory = new DirectoryInfo(installDirectory);
            var confirmedByBinaryClimb = false;

            for (var level = 0; level < MaxLevelsToClimb; level++)
            {
                if (currentDirectory == null)
                {
                    break;
                }

                if (NameMatchesGame(currentDirectory.Name, normalizedGameName))
                {
                    return currentDirectory.FullName;
                }

                var parentDirectory = currentDirectory.Parent;
                if (parentDirectory == null)
                {
                    break;
                }

                if (IsBinarySubfolderName(currentDirectory.Name))
                {
                    confirmedByBinaryClimb = true;
                    currentDirectory = parentDirectory;
                    continue;
                }

                if (NameMatchesGame(parentDirectory.Name, normalizedGameName))
                {
                    return parentDirectory.FullName;
                }

                break;
            }

            // The game may have been renamed on the library, in which case its folder name no
            // longer matches. Folders reached by climbing known binary subfolders are still
            // trusted, since those always sit inside the game installation.
            if (confirmedByBinaryClimb && currentDirectory != null)
            {
                return currentDirectory.FullName;
            }

            return string.Empty;
        }

        private static bool IsBinarySubfolderName(string directoryName)
        {
            return _binarySubfolderNames.Contains(directoryName.Trim());
        }

        private static bool NameMatchesGame(string directoryName, string normalizedGameName)
        {
            var normalizedDirectoryName = NormalizeName(directoryName);
            if (normalizedDirectoryName.IsNullOrEmpty())
            {
                return false;
            }

            if (normalizedDirectoryName == normalizedGameName)
            {
                return true;
            }

            // Partial matches allow for differences such as "The Witcher 3" against
            // "The Witcher 3 Wild Hunt", but short folder names are excluded to avoid
            // trivial matches against substrings of the game name.
            if (normalizedDirectoryName.Length >= MinimumNameMatchLength &&
                normalizedGameName.Contains(normalizedDirectoryName))
            {
                return true;
            }

            if (normalizedGameName.Length >= MinimumNameMatchLength &&
                normalizedDirectoryName.Contains(normalizedGameName))
            {
                return true;
            }

            return false;
        }

        private static string NormalizeName(string name)
        {
            if (name.IsNullOrEmpty())
            {
                return string.Empty;
            }

            var decomposedName = name.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposedName.Length);
            var pendingSpace = false;

            foreach (var character in decomposedName)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(character))
                {
                    if (pendingSpace && builder.Length > 0)
                    {
                        builder.Append(' ');
                    }

                    pendingSpace = false;
                    builder.Append(char.ToLowerInvariant(character));
                }
                else
                {
                    pendingSpace = true;
                }
            }

            return builder.ToString();
        }
    }
}
