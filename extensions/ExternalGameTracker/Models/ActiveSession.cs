using System;
using System.Diagnostics;

namespace ExternalGameTracker.Models
{
    public class ActiveSession
    {
        public Guid GameId { get; set; }
        public string GameName { get; set; }
        public int ProcessId { get; set; }
        public string ExecutablePath { get; set; }
        public DateTime StartTime { get; set; }
        public Process ProcessHandle { get; set; }
    }
}
