using System;
using System.IO;
using System.Text;

namespace McProtocolClientLib.Diagnostics
{
    /// <summary>
    /// Thread-safe text file logger — ghi log trạng thái kết nối và lỗi PLC.
    /// </summary>
    /// <remarks>
    /// File: McProtocolScada.Core/Diagnostics/PlcLogger.cs
    /// Created: 2026-06-15
    /// Log file: [AppBase]/logs/plc_YYYYMMDD.log
    /// </remarks>
    public static class PlcLogger
    {
        private static readonly object _fileLock = new object();
        private static string? _logDirectory;

        /// <summary>
        /// Thư mục chứa log. Mặc định: [AppBase]/logs/
        /// AppBase = thư mục chứa .exe khi chạy WinForms (Debug/Release output).
        /// </summary>
        public static string LogDirectory
        {
            get => _logDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            set => _logDirectory = value;
        }

        /// <summary>Bật/tắt log (mặc định bật).</summary>
        public static bool Enabled { get; set; } = true;

        public static void Info(string message) => Write("INFO ", message);
        public static void Warn(string message) => Write("WARN ", message);
        public static void Error(string message) => Write("ERROR", message);

        private static void Write(string level, string message)
        {
            if (!Enabled) return;
            try
            {
                string dir = LogDirectory;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string path = Path.Combine(dir, $"plc_{DateTime.Now:yyyyMMdd}.log");
                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";

                lock (_fileLock)
                {
                    File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // Không throw từ logger — mất log không crash app
            }
        }
    }
}
