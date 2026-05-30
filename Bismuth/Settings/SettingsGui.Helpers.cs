using System;
using UnityEngine;

namespace Bismuth
{
    internal static partial class SettingsGui
    {
        private static GUILayoutOption W(float px) => GUILayout.Width(Mathf.RoundToInt(px * _uiScale));
        private static GUILayoutOption WMax(float px) => GUILayout.MaxWidth(Mathf.RoundToInt(px * _uiScale));

        private static void Indent(Action content, float space = 20f)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(space);
            content();
            GUILayout.EndHorizontal();
        }

        // Thin shims so existing call sites keep compiling — both delegate to SettingsInput.
        private static void SliderRow(string label, out float result, float value, float min, float max,
            float indent = 20f, string fmt = "F2", string suffix = "")
        {
            float v = value;
            SettingsInput.Slider(label, ref v, min, max, indent, fmt, suffix);
            result = v;
        }

        private static bool DeferredText(string canonical, float width, out string committed)
            => SettingsInput.DeferredText(canonical, width, out committed);

        private static bool DrawPositionButtons(OverlayPosition current, out OverlayPosition result)
        {
            result = current;
            bool changed = false;
            GUI.enabled = current != OverlayPosition.Left;
            if (GUILayout.Button("L", W(45))) { result = OverlayPosition.Left; changed = true; }
            GUI.enabled = current != OverlayPosition.Right;
            if (GUILayout.Button("R", W(45))) { result = OverlayPosition.Right; changed = true; }
            GUI.enabled = true;
            return changed;
        }

        private static void DrawSwatch(Color color)
        {
            if (_swatchStyle == null)
            {
                _swatchStyle = new GUIStyle();
                _swatchStyle.normal.background = Texture2D.whiteTexture;
            }
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUILayout.Label("", _swatchStyle, W(30), GUILayout.Height(Mathf.RoundToInt(18 * _uiScale)));
            GUI.backgroundColor = prev;
        }

        private static string ColorToHex(float r, float g, float b)
        {
            int ri = Mathf.Clamp(Mathf.RoundToInt(r * 255f), 0, 255);
            int gi = Mathf.Clamp(Mathf.RoundToInt(g * 255f), 0, 255);
            int bi = Mathf.Clamp(Mathf.RoundToInt(b * 255f), 0, 255);
            return ri.ToString("X2") + gi.ToString("X2") + bi.ToString("X2");
        }

        private static bool TryParseHex(string hex, out float r, out float g, out float b)
        {
            r = g = b = 0f;
            if (hex == null) return false;
            hex = hex.TrimStart('#');
            if (hex.Length != 6) return false;
            try
            {
                r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
                g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
                b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
                return true;
            }
            catch { return false; }
        }
    }
}
