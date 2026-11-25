using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstallationStatusUpdater.Domain.Results
{
    public class StatusUpdateResults
    {
        private readonly List<UpdatedEntryData> _installed = new List<UpdatedEntryData>();
        private readonly List<UpdatedEntryData> _uninstalled = new List<UpdatedEntryData>();

        public IReadOnlyList<UpdatedEntryData> Installed => _installed;
        public IReadOnlyList<UpdatedEntryData> Uninstalled => _uninstalled;

        public int Count => _installed.Count + _uninstalled.Count;

        public bool HasChanges => Count > 0;

        /// <summary>
        /// Returns the installation status the game had before this scan changed it.
        /// </summary>
        public bool TryGetPreviousIsInstalled(Guid gameId, out bool previousIsInstalled)
        {
            // Games set as installed were not installed before and games set as uninstalled were.
            if (_installed.Any(x => x.Id == gameId))
            {
                previousIsInstalled = false;
                return true;
            }

            if (_uninstalled.Any(x => x.Id == gameId))
            {
                previousIsInstalled = true;
                return true;
            }

            previousIsInstalled = false;
            return false;
        }

        /// <summary>
        /// Drops a game from the results, used when its status change has been reverted.
        /// </summary>
        public bool Remove(Guid gameId)
        {
            var installedIndex = _installed.FindIndex(x => x.Id == gameId);
            if (installedIndex >= 0)
            {
                _installed.RemoveAt(installedIndex);
                return true;
            }

            var uninstalledIndex = _uninstalled.FindIndex(x => x.Id == gameId);
            if (uninstalledIndex >= 0)
            {
                _uninstalled.RemoveAt(uninstalledIndex);
                return true;
            }

            return false;
        }

        internal void AddInstalled(
            Game game,
            InstallationDetectionReason reason,
            long? measuredBytes = null,
            bool sizeReachedThreshold = false)
        {
            _installed.Add(new UpdatedEntryData(game, reason, measuredBytes, sizeReachedThreshold));
        }

        internal void AddUninstalled(
            Game game,
            InstallationDetectionReason reason,
            long? measuredBytes = null,
            bool sizeReachedThreshold = false)
        {
            _uninstalled.Add(new UpdatedEntryData(game, reason, measuredBytes, sizeReachedThreshold));
        }
    }
}