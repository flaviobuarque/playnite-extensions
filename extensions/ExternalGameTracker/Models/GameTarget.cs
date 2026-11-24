using System;
using Playnite.SDK.Models;

namespace ExternalGameTracker.Models
{
    public class GameTarget
    {
        public Game Game { get; set; }
        public string ExpectedExecutablePath { get; set; }
        public string InstallDirectory { get; set; }
    }
}
