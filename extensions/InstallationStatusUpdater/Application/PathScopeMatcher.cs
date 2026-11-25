using System;
using System.Collections.Generic;

namespace InstallationStatusUpdater.Application
{
    /// <summary>
    /// Decides whether a path belongs to a set of watched paths so a status scan can be limited
    /// to the games affected by a file system change.
    /// </summary>
    public static class PathScopeMatcher
    {
        public static bool IsSameOrChildPath(string path, string parentPath)
        {
            if (path is null || parentPath is null || parentPath.Length == 0)
            {
                return false;
            }

            if (path.Equals(parentPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!path.StartsWith(parentPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Guards against a sibling sharing the same prefix, such as "C:\Games\Foo2" when
            // compared against "C:\Games\Foo".
            var nextChar = path[parentPath.Length];
            return nextChar == '\\' || nextChar == '/';
        }

        public static bool IsUnderAnyScope(string path, ICollection<string> scopePaths)
        {
            if (path is null || scopePaths is null || scopePaths.Count == 0)
            {
                return false;
            }

            foreach (var scopePath in scopePaths)
            {
                // A change can happen inside the game folder or in a folder containing it, such
                // as a directory being renamed in the middle of a library folder.
                if (IsSameOrChildPath(path, scopePath) || IsSameOrChildPath(scopePath, path))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
