using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Bismuth
{
    internal static class FontLoader
    {
        internal class FontEntry
        {
            public readonly string Name;
            public readonly Font Font;
            public FontEntry(string name, Font font) { Name = name; Font = font; }
        }

        private static string PlatformBundleSuffix()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsPlayer: return "-win";
                case RuntimePlatform.LinuxPlayer:   return "-linux";
                default:                            return "-mac";
            }
        }

        public static List<FontEntry> ScanFonts(string modPath)
        {
            var result = new List<FontEntry>();
            string fontsDir = Path.Combine(modPath, "Resources");
            if (!Directory.Exists(fontsDir)) return result;

            string suffix = PlatformBundleSuffix();

            foreach (string filePath in Directory.GetFiles(fontsDir))
            {
                string name = Path.GetFileName(filePath);
                if (Path.GetExtension(filePath).ToLowerInvariant() == ".meta") continue;
                // Skip bundles that belong to a different platform.
                foreach (string s in new[] { "-mac", "-win", "-linux" })
                    if (s != suffix && name.EndsWith(s, StringComparison.OrdinalIgnoreCase))
                        goto next;
                TryLoadBundle(filePath, result);
                next:;
            }

            return result;
        }

        private static void TryLoadBundle(string path, List<FontEntry> result)
        {
            AssetBundle bundle = null;
            try
            {
                bundle = AssetBundle.LoadFromFile(path);
                if (bundle == null) return;

                Font[] fonts = bundle.LoadAllAssets<Font>();
                if (fonts == null) return;

                foreach (Font font in fonts)
                {
                    if (font == null) continue;
                    MainClass.Logger.Log($"[Bismuth] Loaded font '{font.name}' from bundle");
                    result.Add(new FontEntry(font.name, font));
                }
            }
            catch (Exception e)
            {
                MainClass.Logger.Warning($"[Bismuth] Bundle '{Path.GetFileName(path)}': {e.Message}");
            }
            finally
            {
                // Unload bundle structure but keep assets alive in memory.
                bundle?.Unload(false);
            }
        }
    }
}
