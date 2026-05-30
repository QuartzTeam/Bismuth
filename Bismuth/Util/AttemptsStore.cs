using System.Collections.Generic;
using System.IO;

namespace Bismuth
{
    internal static class AttemptsStore
    {
        private static Dictionary<string, int> _data;
        private static string FilePath => Path.Combine(MainClass.ModPath, "BismuthAttempts.txt");

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            _data = new Dictionary<string, int>();
            string path = FilePath;
            if (!File.Exists(path)) return;
            foreach (string line in File.ReadAllLines(path))
            {
                int sep = line.IndexOf('=');
                if (sep < 0) continue;
                string key = line.Substring(0, sep);
                if (int.TryParse(line.Substring(sep + 1), out int val))
                    _data[key] = val;
            }
        }

        public static int Get(string key)
        {
            if (key == null) return 0;
            EnsureLoaded();
            return _data.TryGetValue(key, out int v) ? v : 0;
        }

        public static void Set(string key, int value)
        {
            if (key == null) return;
            EnsureLoaded();
            _data[key] = value;
            Save();
        }

        public static void ClearAll()
        {
            EnsureLoaded();
            _data.Clear();
            Save();
        }

        private static void Save()
        {
            var lines = new List<string>();
            foreach (var kv in _data)
                lines.Add(kv.Key + "=" + kv.Value);
            File.WriteAllLines(FilePath, lines.ToArray());
        }
    }
}
