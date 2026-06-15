using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

namespace Bismuth
{
    internal static class FontLoader
    {
        internal class FontEntry
        {
            public readonly string Name;
            public readonly Font Font;
            // Same family Bold weight, wired by LinkFamilies after scan
            internal FontEntry BoldSibling;
            private TMP_FontAsset _tmp;

            public FontEntry(string name, Font font) { Name = name; Font = font; }

            /* Created on first use: dynamic SDF atlas, with family real Bold in weight
               table so <b>/FontStyles.Bold doesn't fall back to synthetic bold */
            public TMP_FontAsset TmpFont
            {
                get
                {
                    if (_tmp == null && Font != null)
                    {
                        _tmp = TMP_FontAsset.CreateFontAsset(Font);
                        if (_tmp != null)
                        {
                            _tmp.name = Name + " (TMP)";
                            if (BoldSibling != null && BoldSibling != this)
                                _tmp.fontWeightTable[7].regularTypeface = BoldSibling.TmpFont;
                        }
                    }
                    return _tmp;
                }
            }

            internal void DestroyTmp()
            {
                if (_tmp == null) return;
                var atlases = _tmp.atlasTextures;
                if (atlases != null)
                    foreach (var tex in atlases)
                        if (tex != null) UnityEngine.Object.Destroy(tex);
                if (_tmp.material != null) UnityEngine.Object.Destroy(_tmp.material);
                UnityEngine.Object.Destroy(_tmp);
                _tmp = null;
            }
        }

        /* Canonical ordering for weight cycle. Names not in this list sort last, in
           scan order */
        internal static readonly string[] WeightOrder =
        {
            "Thin", "ExtraLight", "UltraLight", "Light", "Regular", "Medium",
            "SemiBold", "DemiBold", "Bold", "ExtraBold", "UltraBold", "Heavy", "Black",
        };

        /* "Pretendard SemiBold" / "Pretendard-SemiBold" maps to ("Pretendard",
           "SemiBold"). A name whose last token isn't a known weight is a single-weight
           family, shown under its full name. */
        internal static void SplitWeight(string name, out string family, out string weight)
        {
            family = name;
            weight = "Regular";
            if (string.IsNullOrEmpty(name)) return;
            int sp = name.LastIndexOfAny(new[] { ' ', '-' });
            if (sp <= 0) return;
            string last = name.Substring(sp + 1);
            foreach (var w in WeightOrder)
            {
                if (string.Equals(last, w, StringComparison.OrdinalIgnoreCase))
                {
                    family = name.Substring(0, sp);
                    weight = w;
                    return;
                }
            }
        }

        internal static int WeightRank(string weight)
        {
            for (int i = 0; i < WeightOrder.Length; i++)
                if (string.Equals(WeightOrder[i], weight, StringComparison.OrdinalIgnoreCase)) return i;
            return WeightOrder.Length;
        }

        /* Weight-override sentinel: resolves to family heaviest weight at apply time, so
           it tracks family switches instead of pinning specific name */
        internal const string WeightHeaviest = "Heaviest";

        /* Saved settings may spell font with spaces ("Maplestory Bold") while bundle
           asset uses hyphens ("Maplestory-Bold"), match ignoring both */
        private static string NormalizeName(string s) =>
            s == null ? "" : s.Replace(" ", "").Replace("-", "").ToLowerInvariant();

        internal static FontEntry Find(IList<FontEntry> fonts, string name)
        {
            if (fonts == null || string.IsNullOrEmpty(name)) return null;
            string norm = NormalizeName(name);
            foreach (var e in fonts)
                if (NormalizeName(e.Name) == norm) return e;
            return null;
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
                // Skip bundles belonging to different platform
                foreach (string s in new[] { "-mac", "-win", "-linux" })
                    if (s != suffix && name.EndsWith(s, StringComparison.OrdinalIgnoreCase))
                        goto next;
                TryLoadBundle(filePath, result);
                next:;
            }

            LinkFamilies(result);
            return result;
        }

        private static void LinkFamilies(List<FontEntry> entries)
        {
            var bolds = new Dictionary<string, FontEntry>();
            foreach (var e in entries)
            {
                SplitWeight(e.Name, out string fam, out string w);
                if (string.Equals(w, "Bold", StringComparison.OrdinalIgnoreCase)) bolds[fam] = e;
            }
            foreach (var e in entries)
            {
                SplitWeight(e.Name, out string fam, out _);
                if (bolds.TryGetValue(fam, out var bold)) e.BoldSibling = bold;
            }
        }

        internal static void DestroyTmpAssets(List<FontEntry> entries)
        {
            if (entries == null) return;
            foreach (var e in entries) e.DestroyTmp();
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
                // Unload bundle structure but keep assets alive in memory
                bundle?.Unload(false);
            }
        }
    }
}
