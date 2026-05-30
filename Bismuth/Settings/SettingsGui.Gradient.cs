using System.Collections.Generic;
using UnityEngine;

namespace Bismuth
{
    internal static partial class SettingsGui
    {
        private static void DrawGradientEditor(string key, ColorGradient gradient, ref bool changed)
        {
            if (gradient == null) return;

            if (!_stopExpanded.ContainsKey(key))
                _stopExpanded[key] = new List<bool>();
            var expanded = _stopExpanded[key];
            while (expanded.Count < gradient.Stops.Count) expanded.Add(false);
            while (expanded.Count > gradient.Stops.Count) expanded.RemoveAt(expanded.Count - 1);

            GUILayout.BeginHorizontal();
            GUILayout.Space(20f);
            GUILayout.BeginVertical();

            bool isSolid = GUILayout.Toggle(gradient.IsSolid, " Solid Color");
            if (isSolid != gradient.IsSolid) { gradient.IsSolid = isSolid; changed = true; }

            if (gradient.IsSolid)
            {
                if (gradient.Stops.Count == 0)
                {
                    gradient.Stops.Add(new ColorStop { Progress = 0f, R = 1f, G = 1f, B = 1f, A = 1f });
                    expanded.Add(false);
                    changed = true;
                }
                DrawColorControls(gradient.Stops[0], ref changed);
            }
            else
            {
                GUILayout.BeginHorizontal();
                bool hasPerfect = GUILayout.Toggle(gradient.HasPerfectColor, " Perfect Color (at 100%)", GUILayout.ExpandWidth(false));
                if (hasPerfect != gradient.HasPerfectColor) { gradient.HasPerfectColor = hasPerfect; changed = true; }
                if (gradient.HasPerfectColor)
                {
                    string perfKey = key + "|perf";
                    if (!_stopExpanded.ContainsKey(perfKey)) _stopExpanded[perfKey] = new List<bool> { false };
                    bool perfOpen = _stopExpanded[perfKey][0];
                    DrawSwatch(new Color(gradient.PR, gradient.PG, gradient.PB));
                    GUILayout.Space(4f);
                    if (GUILayout.Button((perfOpen ? "▼" : "►") + " Edit", GUILayout.ExpandWidth(false)))
                    {
                        perfOpen = !perfOpen;
                        _stopExpanded[perfKey][0] = perfOpen;
                    }
                }
                GUILayout.EndHorizontal();
                if (gradient.HasPerfectColor &&
                    _stopExpanded.TryGetValue(key + "|perf", out var perfList) && perfList[0])
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(20f);
                    GUILayout.BeginVertical();
                    DrawPerfectColorControls(gradient, ref changed);
                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                }

                GUILayout.Space(4f);

                int removeAt = -1;
                for (int i = 0; i < gradient.Stops.Count; i++)
                {
                    var stop = gradient.Stops[i];

                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button((expanded[i] ? "▼" : "►") + " Stop " + i, GUILayout.ExpandWidth(false)))
                        expanded[i] = !expanded[i];
                    DrawSwatch(new Color(stop.R, stop.G, stop.B));
                    GUILayout.EndHorizontal();

                    if (expanded[i])
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(20f);
                        GUILayout.BeginVertical();

                        GUILayout.Label("At: " + stop.Progress.ToString("F2"), _noWrapLabel);
                        float p = GUILayout.HorizontalSlider(stop.Progress, 0f, 1f, WMax(300));
                        if (p != stop.Progress) { stop.Progress = p; changed = true; }

                        DrawColorControls(stop, ref changed);

                        if (GUILayout.Button("Remove Stop", GUILayout.ExpandWidth(false)))
                            removeAt = i;

                        GUILayout.EndVertical();
                        GUILayout.EndHorizontal();
                    }
                }

                if (removeAt >= 0)
                {
                    gradient.Stops.RemoveAt(removeAt);
                    if (expanded.Count > removeAt) expanded.RemoveAt(removeAt);
                    changed = true;
                }

                if (GUILayout.Button("+ Add Stop", GUILayout.ExpandWidth(false)))
                {
                    float p = gradient.Stops.Count > 0 ? gradient.Stops[gradient.Stops.Count - 1].Progress : 1f;
                    gradient.Stops.Add(new ColorStop { Progress = p, R = 1f, G = 1f, B = 1f, A = 1f });
                    expanded.Add(true);
                    changed = true;
                }
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private static void DrawColorControls(ColorStop stop, ref bool changed)
        {
            GUILayout.BeginHorizontal();
            DrawSwatch(new Color(stop.R, stop.G, stop.B));
            GUILayout.Space(4f);
            GUILayout.Label("#", _noWrapLabel, GUILayout.ExpandWidth(false));
            string hex = ColorToHex(stop.R, stop.G, stop.B);
            if (DeferredText(hex, 65f, out string commitHex) &&
                TryParseHex(commitHex, out float hr, out float hg, out float hb))
            { stop.R = hr; stop.G = hg; stop.B = hb; changed = true; }
            GUILayout.EndHorizontal();

            GUILayout.Label("R: " + stop.R.ToString("F2"), _noWrapLabel);
            float r = GUILayout.HorizontalSlider(stop.R, 0f, 1f, WMax(300));
            if (r != stop.R) { stop.R = r; changed = true; }

            GUILayout.Label("G: " + stop.G.ToString("F2"), _noWrapLabel);
            float g = GUILayout.HorizontalSlider(stop.G, 0f, 1f, WMax(300));
            if (g != stop.G) { stop.G = g; changed = true; }

            GUILayout.Label("B: " + stop.B.ToString("F2"), _noWrapLabel);
            float b = GUILayout.HorizontalSlider(stop.B, 0f, 1f, WMax(300));
            if (b != stop.B) { stop.B = b; changed = true; }
        }

        private static void DrawPerfectColorControls(ColorGradient g, ref bool changed)
        {
            GUILayout.BeginHorizontal();
            DrawSwatch(new Color(g.PR, g.PG, g.PB));
            GUILayout.Space(4f);
            GUILayout.Label("#", _noWrapLabel, GUILayout.ExpandWidth(false));
            string hex = ColorToHex(g.PR, g.PG, g.PB);
            if (DeferredText(hex, 65f, out string commitHex) &&
                TryParseHex(commitHex, out float hr, out float hg, out float hb))
            { g.PR = hr; g.PG = hg; g.PB = hb; changed = true; }
            GUILayout.EndHorizontal();

            GUILayout.Label("R: " + g.PR.ToString("F2"), _noWrapLabel);
            float r = GUILayout.HorizontalSlider(g.PR, 0f, 1f, WMax(300));
            if (r != g.PR) { g.PR = r; changed = true; }

            GUILayout.Label("G: " + g.PG.ToString("F2"), _noWrapLabel);
            float gv = GUILayout.HorizontalSlider(g.PG, 0f, 1f, WMax(300));
            if (gv != g.PG) { g.PG = gv; changed = true; }

            GUILayout.Label("B: " + g.PB.ToString("F2"), _noWrapLabel);
            float b = GUILayout.HorizontalSlider(g.PB, 0f, 1f, WMax(300));
            if (b != g.PB) { g.PB = b; changed = true; }
        }
    }
}
