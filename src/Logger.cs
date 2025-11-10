using System;
using System.IO;

namespace ProgramaOTLauncher
{
    public static class Logger
    {
        private static readonly object _gate = new object();
        private static readonly string _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "launcher.log");

        public static void Info(string message) => Write("INFO", message);
        public static void Warn(string message) => Write("WARN", message);
        public static void Error(string message, Exception ex = null) => Write("ERROR", ex == null ? message : message + " | " + ex.ToString());

        private static void Write(string level, string message)
        {
            try
            {
                var dir = Path.GetDirectoryName(_logPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level} {message}";
                lock (_gate)
                {
                    File.AppendAllText(_logPath, line + Environment.NewLine);
                }
            }
            catch
            {
                // Evita exceções de logging quebrando o fluxo principal
            }
        }
    }
}

