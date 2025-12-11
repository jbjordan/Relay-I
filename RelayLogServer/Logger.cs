using System;
using System.IO;

namespace Server
{
    /// <summary>
    /// Simple thread-safe file logger for the RelayServer.
    /// </summary>
    internal static class Logger
    {
        private static readonly object _sync = new();
        private static readonly string _logFilePath = Path.Combine(AppContext.BaseDirectory, "relayserver.log");

        public static void Log(string level, string message, Exception? ex = null, bool isError = false)
        {
            if (message.Length < 20)
            {
                return; // ignore very short messages
            }
            var line = $"{DateTime.UtcNow:o} [{level}] {message}" + (ex != null ? $" | {ex}" : string.Empty);
            try
            {
                lock (_sync)
                {
                    File.AppendAllText(_logFilePath, line + Environment.NewLine);
                }
            }
            catch
            {
                // swallow file I/O errors
            }
            if (isError)
                Console.Error.WriteLine(line);
            else
                Console.WriteLine(line);
        }
    }
}
