using System;
using System.Diagnostics;
using System.IO;

namespace WuwaModModifier.Common
{
    /// <summary>
    /// DI-friendly implementation of <see cref="ILogService"/>.
    /// Replaces the legacy static <see cref="LogManager"/>.
    /// </summary>
    public class LogService : ILogService
    {
        private static readonly object LockObj = new object();

        public void Log(string message)
        {
            WriteLog("Log", message, postfix: null);
        }

        public void Info(string message)
        {
            WriteLog("MessageLog", message, postfix: null);
        }

        public void Error(string message, Exception ex)
        {
            WriteLog("ErrorLog", $"{message} => {ex}", postfix: null);
        }

        private static void WriteLog(string folder, string content, string? postfix)
        {
            lock (LockObj)
            {
                try
                {
                    var fileDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'), folder);
                    if (!Directory.Exists(fileDir))
                    {
                        Directory.CreateDirectory(fileDir);
                    }

                    var fileName = postfix == null
                        ? $"{DateTime.Now:yyyy-MM-dd}.log"
                        : $"{DateTime.Now:yyyy-MM-dd}_{postfix}.log";
                    var filePath = Path.Combine(fileDir, fileName);

                    using var sw = new StreamWriter(filePath, append: true);
                    sw.AutoFlush = true;
                    sw.WriteLine($"[{DateTime.Now:HH:mm:ss fff}]{content}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"LogService failed: {ex.Message}");
                }
            }
        }
    }
}
