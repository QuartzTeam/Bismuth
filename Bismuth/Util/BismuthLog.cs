using System;
using System.IO;

namespace Bismuth
{
    internal static class BismuthLog
    {
        private static string _path;

        internal static void Init()
        {
            _path = Path.Combine(MainClass.ModPath, "BismuthLog.txt");
            try
            {
                File.WriteAllText(_path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Bismuth session start\n");
            }
            catch { _path = null; }
        }

        internal static void Log(string message)
        {
            if (_path == null) return;
            try { File.AppendAllText(_path, $"[{DateTime.Now:HH:mm:ss}] {message}\n"); }
            catch { }
        }
    }
}
