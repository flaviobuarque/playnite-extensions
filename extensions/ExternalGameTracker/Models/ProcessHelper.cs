using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ExternalGameTracker.Models
{
    public static class ProcessHelper
    {
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        /// <summary>
        /// Obtém o caminho completo do executável de um processo pelo PID.
        /// Funciona mesmo em processos protegidos por anti-cheat (como COD/Ricochet ou EAC),
        /// pois utiliza PROCESS_QUERY_LIMITED_INFORMATION sem exigir elevação de Administrador.
        /// Inclui retry leve caso o executável ainda esteja carregando no kernel.
        /// </summary>
        public static string GetProcessExecutablePath(int processId, int maxRetries = 4, int retryDelayMs = 75)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
                if (hProcess != IntPtr.Zero)
                {
                    try
                    {
                        var buffer = new StringBuilder(1024);
                        int size = buffer.Capacity;
                        if (QueryFullProcessImageName(hProcess, 0, buffer, ref size) && size > 0)
                        {
                            return buffer.ToString();
                        }
                    }
                    catch
                    {
                        // Continua para o próximo retry
                    }
                    finally
                    {
                        CloseHandle(hProcess);
                    }
                }

                if (i < maxRetries - 1)
                {
                    Thread.Sleep(retryDelayMs);
                }
            }

            return null;
        }
    }
}
