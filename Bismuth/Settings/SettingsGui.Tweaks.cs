using System;
using UnityEngine;

namespace Bismuth
{
    internal static partial class SettingsGui
    {
        private static void DrawTweaksSection(Settings settings, ref bool changed)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button((_tweaksOpen ? "▼" : "►") + " Tweaks", GUILayout.ExpandWidth(false)))
                _tweaksOpen = !_tweaksOpen;
            bool tweaksOn = GUILayout.Toggle(settings.TweaksEnabled, " Enabled");
            if (tweaksOn != settings.TweaksEnabled) { settings.TweaksEnabled = tweaksOn; changed = true; }
            GUILayout.EndHorizontal();

            if (_tweaksOpen)
                DrawTweaks(settings, ref changed);
        }

        private static void DrawMiscSection(Settings settings, ref bool changed)
        {
            if (GUILayout.Button((_miscOpen ? "▼" : "►") + " Misc", GUILayout.ExpandWidth(false)))
                _miscOpen = !_miscOpen;
            if (!_miscOpen) return;

            string savings;
            long bytes = MainClass.LastUnloadSavingsBytes;
            if (bytes < 0) savings = "----MB";
            else
            {
                float mb = bytes / (1024f * 1024f);
                savings = (mb >= 0f ? "+" : "") + mb.ToString("F2") + " MB";
            }
            Indent(() => GUILayout.Label("RAM savings (last scene load): " + savings, _noWrapLabel), 20f);

            GUILayout.Space(8f);
            Indent(() =>
            {
                if (GUILayout.Button((_optsOpen ? "▼" : "►") + " Optimizations", GUILayout.ExpandWidth(false)))
                    _optsOpen = !_optsOpen;
            }, 20f);
            if (!_optsOpen) return;

            bool spec = false;
            Indent(() =>
            {
                bool specThrottle = GUILayout.Toggle(settings.OptSpectrumThrottle, " Spectrum Throttle (every 2nd frame)");
                if (specThrottle != settings.OptSpectrumThrottle) { settings.OptSpectrumThrottle = specThrottle; spec = true; }
            }, 40f);
            if (spec) changed = true;
            Indent(() => GUILayout.Label("Halves AudioSource.GetSpectrumData FFT cost on levels that use audio visualization.", _noWrapLabel), 60f);

            GUILayout.Space(4f);

            bool texOptChanged = false;
            Indent(() =>
            {
                bool texOpt = GUILayout.Toggle(settings.OptTextureNonReadable, " Texture Non-Readable");
                if (texOpt != settings.OptTextureNonReadable) { settings.OptTextureNonReadable = texOpt; texOptChanged = true; }
            }, 40f);
            if (texOptChanged) changed = true;
            Indent(() => GUILayout.Label("Frees CPU-side pixel data after GPU upload. Halves RAM per custom level texture.", _noWrapLabel), 60f);

            bool dxtChanged = false;
            Indent(() =>
            {
                bool dxt = GUILayout.Toggle(settings.OptTextureDXT, " DXT Compression (lossy)");
                if (dxt != settings.OptTextureDXT) { settings.OptTextureDXT = dxt; dxtChanged = true; }
            }, 40f);
            if (dxtChanged) changed = true;
            Indent(() => GUILayout.Label("Compresses textures to DXT before upload. 4-6x VRAM savings, slight quality loss. Requires Non-Readable.", _noWrapLabel), 60f);

            GUILayout.Space(4f);

            bool physChanged = false;
            Indent(() =>
            {
                bool physOpt = GUILayout.Toggle(settings.OptPhysicsNonAlloc, " Physics NonAlloc");
                if (physOpt != settings.OptPhysicsNonAlloc) { settings.OptPhysicsNonAlloc = physOpt; physChanged = true; }
            }, 40f);
            if (physChanged) changed = true;
            Indent(() => GUILayout.Label("Eliminates per-frame Collider2D[] allocation from decoration hitbox checks.", _noWrapLabel), 60f);

            GUILayout.Space(4f);

            bool unloadChanged = false;
            Indent(() =>
            {
                bool unload = GUILayout.Toggle(settings.OptUnloadAssets, " Unload Assets on Scene Change");
                if (unload != settings.OptUnloadAssets) { settings.OptUnloadAssets = unload; unloadChanged = true; }
            }, 40f);
            if (unloadChanged) changed = true;
            Indent(() => GUILayout.Label("Forces GC and unloads unused textures/audio between levels to reclaim memory.", _noWrapLabel), 60f);

            GUILayout.Space(4f);

            bool dotweenChanged = false;
            Indent(() =>
            {
                bool dotween = GUILayout.Toggle(settings.OptVolumeTrackDOTween, " Volume Track DOTween Fix");
                if (dotween != settings.OptVolumeTrackDOTween) { settings.OptVolumeTrackDOTween = dotween; dotweenChanged = true; }
            }, 40f);
            if (dotweenChanged) changed = true;
            Indent(() => GUILayout.Label("Prevents abandoned DOTween sequences from being created every frame on Volume-type track colors.", _noWrapLabel), 60f);
        }

        private static void DrawTweaks(Settings settings, ref bool changed)
        {
            GUILayout.Label("Song Title", _noWrapLabel);

            SliderRow("Scale", out float lns, settings.LevelNameScale, 0.1f, 3f, 20f, "F2");
            if (lns != settings.LevelNameScale) { settings.LevelNameScale = lns; changed = true; }

            SliderRow("Y", out float lny, settings.LevelNameY, -500f, 500f, 20f, "F0");
            if (lny != settings.LevelNameY) { settings.LevelNameY = lny; changed = true; }
        }

        private static void DrawFont(Settings settings, ref bool changed, ref bool fontChanged)
        {
            if (fonts.Count == 0) return;

            string current = string.IsNullOrEmpty(settings.FontName) ? fonts[0].Name : settings.FontName;
            string arrow = fontDropdownOpen ? " ▲" : " ▼";

            GUILayout.BeginHorizontal();
            GUILayout.Label("Font:", _noWrapLabel, W(50));
            if (GUILayout.Button(current + arrow, W(220)))
                fontDropdownOpen = !fontDropdownOpen;
            GUILayout.EndHorizontal();

            if (!fontDropdownOpen) return;

            GUILayout.BeginHorizontal();
            GUILayout.Space(20f);
            GUILayout.BeginVertical();
            foreach (FontLoader.FontEntry entry in fonts)
            {
                bool selected = string.Equals(current, entry.Name, StringComparison.OrdinalIgnoreCase);
                string label = selected ? "● " + entry.Name : "○ " + entry.Name;
                if (GUILayout.Button(label, GUI.skin.label, GUILayout.ExpandWidth(false)))
                {
                    settings.FontName = entry.Name;
                    fontDropdownOpen = false;
                    changed = true;
                    fontChanged = true;
                }
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
    }
}
