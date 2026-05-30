using System.Collections.Generic;
using UnityEngine;

namespace Bismuth
{
    // Shared input widgets for the settings GUI:
    //   - deferred-commit text fields (parse / clamp on Enter or focus loss)
    //   - sliders with editable value field and per-row undo button
    //
    // All state is keyed by a per-Draw counter; call BeginFrame() at the top of
    // each Draw() and ResetState() when navigating between sections so stale
    // baselines / edit buffers don't apply to a different control.
    internal static class SettingsInput
    {
        private static int _counter;
        private static readonly Dictionary<string, string> _buffers   = new Dictionary<string, string>();
        private static readonly Dictionary<string, float>  _baselines = new Dictionary<string, float>();

        private static GUIStyle _noWrapLabel;
        private static float _uiScale = 1f;

        private static GUILayoutOption W(float px)    => GUILayout.Width(Mathf.RoundToInt(px * _uiScale));
        private static GUILayoutOption WMax(float px) => GUILayout.MaxWidth(Mathf.RoundToInt(px * _uiScale));

        public static void BeginFrame(float uiScale, GUIStyle noWrapLabel)
        {
            _counter = 0;
            _uiScale = uiScale;
            _noWrapLabel = noWrapLabel;
        }

        // Wipe edit buffers + undo baselines. Call when the user navigates
        // (e.g., enters/exits the preset edit view) so counter→control mapping
        // changes don't carry stale state.
        public static void ResetState()
        {
            _buffers.Clear();
            _baselines.Clear();
        }

        // Slider with editable value field above and an undo button to the right.
        // Returns true if `value` was modified.
        public static bool Slider(string label, ref float value, float min, float max,
            float indent = 20f, string fmt = "F2", string suffix = "", float step = 0f)
        {
            string ctrl = "ctrl" + (_counter++);
            bool isFocused = GUI.GetNameOfFocusedControl() == ctrl;
            bool hasBuffer = _buffers.TryGetValue(ctrl, out string pending);

            // Establish baseline on first observation.
            if (!_baselines.ContainsKey(ctrl)) _baselines[ctrl] = value;
            float baseline = _baselines[ctrl];

            float current = value;
            bool changed = false;

            // Commit text buffer on focus loss.
            if (hasBuffer && !isFocused)
            {
                float committed = ParseFloat(pending, value, min, max, suffix);
                if (step > 0f) committed = Mathf.Round(committed / step) * step;
                if (committed != value) { value = committed; current = committed; changed = true; }
                _buffers.Remove(ctrl);
                hasBuffer = false;
                pending = null;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Space(indent);
            GUILayout.Label(label + ":", _noWrapLabel, GUILayout.ExpandWidth(false));

            string display = hasBuffer ? pending : current.ToString(fmt) + suffix;
            GUI.SetNextControlName(ctrl);
            string newText = GUILayout.TextField(display, W(80));
            if (newText != display) _buffers[ctrl] = newText;

            if (isFocused && Event.current != null && Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            {
                float committed = ParseFloat(newText, value, min, max, suffix);
                if (step > 0f) committed = Mathf.Round(committed / step) * step;
                if (committed != value) { value = committed; current = committed; changed = true; }
                _buffers.Remove(ctrl);
                GUI.FocusControl("");
                Event.current.Use();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(indent);
            float sliderResult = GUILayout.HorizontalSlider(current, min, max, WMax(300));
            if (sliderResult != current)
            {
                if (step > 0f) sliderResult = Mathf.Round(sliderResult / step) * step;
                if (sliderResult != value) { value = sliderResult; changed = true; }
            }

            // Undo button — shown only when the current value differs from the baseline.
            if (Mathf.Abs(value - baseline) > 1e-6f)
            {
                if (GUILayout.Button("↶", GUILayout.ExpandWidth(false)))
                {
                    value = baseline;
                    _buffers.Remove(ctrl);
                    GUI.FocusControl("");
                    changed = true;
                }
            }
            GUILayout.EndHorizontal();

            return changed;
        }

        // Integer slider — convenience wrapper.
        public static bool Slider(string label, ref int value, int min, int max,
            float indent = 20f, string fmt = "F0", string suffix = "")
        {
            float f = value;
            bool c = Slider(label, ref f, min, max, indent, fmt, suffix, 1f);
            if (c) value = Mathf.RoundToInt(f);
            return c;
        }

        // Plain text field with deferred commit. Returns true (with `committed` set)
        // when the user finishes editing (focus lost or Enter pressed). While editing,
        // the buffer is shown and `committed` is null.
        public static bool DeferredText(string canonical, float width, out string committed)
        {
            string ctrl = "ctrl" + (_counter++);
            bool isFocused = GUI.GetNameOfFocusedControl() == ctrl;
            bool hasBuffer = _buffers.TryGetValue(ctrl, out string pending);

            committed = null;
            if (hasBuffer && !isFocused)
            {
                committed = pending;
                _buffers.Remove(ctrl);
                return true;
            }

            string display = hasBuffer ? pending : canonical;
            GUI.SetNextControlName(ctrl);
            string newText = GUILayout.TextField(display, W(width));
            if (newText != display) _buffers[ctrl] = newText;

            if (isFocused && Event.current != null && Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            {
                committed = newText;
                _buffers.Remove(ctrl);
                GUI.FocusControl("");
                Event.current.Use();
                return true;
            }
            return false;
        }

        private static float ParseFloat(string text, float fallback, float min, float max, string suffix)
        {
            if (text == null) return fallback;
            string num = text.Trim();
            if (!string.IsNullOrEmpty(suffix) && num.EndsWith(suffix))
                num = num.Substring(0, num.Length - suffix.Length).Trim();
            if (float.TryParse(num, out float p))
                return Mathf.Clamp(p, min, max);
            return fallback;
        }
    }
}
