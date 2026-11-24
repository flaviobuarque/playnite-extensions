using System;
using System.Management;
using Playnite.SDK;

namespace ExternalGameTracker.Services
{
    public class ProcessWatcher : IDisposable
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private ManagementEventWatcher _watcher;
        private bool _isDisposed;

        public event Action<string, int> ProcessStarted;

        public void Start()
        {
            if (_watcher != null) return;

            try
            {
                // ATENÇÃO: Win32_ProcessStartTrace requer obrigatoriamente SELECT *
                // Usar SELECT de colunas específicas causa 'Consulta não analisável' no WMI.
                var query = new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace");
                _watcher = new ManagementEventWatcher(query);
                _watcher.EventArrived += Watcher_EventArrived;
                _watcher.Start();
                logger.Info("[ExternalGameTracker] ProcessWatcher (Win32_ProcessStartTrace) iniciado com sucesso.");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "[ExternalGameTracker] FALHA CRÍTICA ao iniciar ProcessWatcher.");
            }
        }

        public void Stop()
        {
            if (_watcher != null)
            {
                try
                {
                    _watcher.Stop();
                    _watcher.EventArrived -= Watcher_EventArrived;
                    _watcher.Dispose();
                    logger.Info("[ExternalGameTracker] ProcessWatcher parado.");
                }
                catch (Exception ex)
                {
                    logger.Debug(ex, "[ExternalGameTracker] Exceção ao parar ProcessWatcher.");
                }
                finally
                {
                    _watcher = null;
                }
            }
        }

        private void Watcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            try
            {
                var newEvent = e.NewEvent;
                if (newEvent == null) return;

                string processName = newEvent["ProcessName"]?.ToString();
                object pidObj = newEvent["ProcessID"];
                if (string.IsNullOrEmpty(processName) || pidObj == null) return;

                int processId = Convert.ToInt32(pidObj);
                ProcessStarted?.Invoke(processName, processId);
            }
            catch (Exception ex)
            {
                logger.Debug(ex, "[ExternalGameTracker] Falha ao processar evento de criação de processo.");
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                Stop();
                _isDisposed = true;
            }
        }
    }
}
