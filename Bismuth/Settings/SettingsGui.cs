using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bismuth
{
    // SettingsGui is split into partial files by section:
    //   SettingsGui.cs              (this) — entry point + shared state fields
    //   SettingsGui.Overlay.cs      — overlay stat rows (Scale / Progress / Acc / BPM / Attempts / Timing / Judgements)
    //   SettingsGui.Combo.cs        — combo display
    //   SettingsGui.KeyViewer.cs    — key viewer category, preset edit page, KV color editor
    //   SettingsGui.KeyLimiter.cs   — key limiter
    //   SettingsGui.HideUi.cs       — hide UI toggles
    //   SettingsGui.Tweaks.cs       — tweaks + optimizations + font + misc
    //   SettingsGui.Gradient.cs     — gradient + color-stop editors
    //   SettingsGui.Helpers.cs      — Indent / W / WMax / DrawSwatch / hex / position buttons / SliderRow shim
    internal static partial class SettingsGui
    {
        private static List<FontLoader.FontEntry> fonts = new List<FontLoader.FontEntry>();
        private static bool fontDropdownOpen = false;

        // Section open flags
        private static bool _overlayOpen = false;
        private static bool _hideUiOpen = false;
        private static bool _tweaksOpen = false;
        private static bool _miscOpen = false;
        private static bool _optsOpen = false;
        private static bool _timingScaleOpen = false;
        private static bool _judgementsOpen = false;
        private static bool _comboDisplayOpen = false;
        private static bool _progressColorOpen = false;
        private static bool _accColorOpen = false;
        private static bool _bpmColorOpen = false;
        private static bool _attemptsOpen = false;
        private static bool _comboColorOpen = false;
        private static bool _comboLabelOpen = false;
        private static bool _comboCountOpen = false;
        private static bool _comboAnimationsOpen = false;
        private static bool _comboLabelShadowOpen = false;
        private static bool _comboLabelShadowColorOpen = false;
        private static bool _comboCountShadowOpen = false;
        private static bool _comboCountShadowColorOpen = false;
        private static bool _keyViewerOpen   = false;
        private static bool _keyLimiterOpen  = false;
        private static bool _chatterBlockerOpen = false;
        private static int  _editingPreset   = -1;
        private static bool _editingIsFoot   = false;
        private static bool _kvBgIdleOpen = false;
        private static bool _kvBgHeldOpen = false;
        private static bool _kvBorderOpen      = false;
        private static bool _kvBorderIdleOpen  = false;
        private static bool _kvBorderHeldOpen  = false;
        private static bool _kvTxtIdleOpen = false;
        private static bool _kvTxtHeldOpen   = false;
        private static bool _kvCountIdleOpen = false;
        private static bool _kvCountHeldOpen = false;
        private static bool _kvLabelOpen     = false;
        private static bool _kvCountOpen     = false;
        private static bool _kvBgOpen        = false;
        private static bool _kvRowsOpen      = false;
        private static bool _kvKeyRainOpen   = false;
        private static bool _kvGhostOpen     = false;
        private static bool _kvGhostRainOpen = false;
        private static int  _kvGhostListenIdx = -1;
        private static bool _kvMainOpen      = true;
        private static bool _kvFootOpen      = false;
        private static readonly List<bool> _kvRowRainOpen = new List<bool>();
        private static readonly List<bool> _kvRowOpen     = new List<bool>();

        // Per-section transient state
        private static readonly Dictionary<string, List<bool>> _stopExpanded = new Dictionary<string, List<bool>>();
        private static GUIStyle _swatchStyle;
        private static GUIStyle _noWrapLabel;
        private static float _uiScale = 1f;
        private static int _lastEditingPreset = -2;
        private static bool _lastEditingIsFoot = false;
        // Set anywhere a structural change to the active preset happens; the Draw() loop
        // flushes it once at the end of the preset-edit page by invoking the rebuild callback.
        private static bool _needsKvRebuild = false;

        // Cell grid editor state (one row at a time can be listening / one cell expanded)
        private static int _kvListenRow = -1;       // row index currently waiting for a keypress; -1 = none
        private static int _kvExpandedRow = -1;     // row whose cell is in inline-edit mode; -1 = none
        private static int _kvExpandedCell = -1;
        private static int _kvCellListenRow = -1;   // (row, cell) currently listening to rebind via Change Key
        private static int _kvCellListenCell = -1;

        internal static void SetFonts(List<FontLoader.FontEntry> available)
        {
            fonts = available;
        }

        internal static void Draw(Settings settings, Action onChanged, Action onFontChanged,
            Action onKeyViewerRebuild = null, Action onKeyViewerReset = null)
        {
            bool changed = false;
            bool fontChanged = false;
            _uiScale = GUI.matrix.m00;
            if (_noWrapLabel == null)
                _noWrapLabel = new GUIStyle(GUI.skin.label) { wordWrap = false };

            if (_lastEditingPreset != _editingPreset || _lastEditingIsFoot != _editingIsFoot)
            {
                SettingsInput.ResetState();
                _lastEditingPreset = _editingPreset;
                _lastEditingIsFoot = _editingIsFoot;
            }
            SettingsInput.BeginFrame(_uiScale, _noWrapLabel);

            // ── Preset edit view (full-screen replacement) ────────────────────
            var editingList = _editingIsFoot ? settings.KvFootPresets : settings.KvHandPresets;
            if (_editingPreset >= 0 && editingList != null &&
                _editingPreset < editingList.Count)
            {
                var preset = editingList[_editingPreset];
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("< Return to Menu", GUILayout.ExpandWidth(false)))
                    _editingPreset = -1;
                GUILayout.Space(8f);
                GUILayout.Label("Editing: " + (_editingIsFoot ? "Foot / " : "Hand / ") + preset.Name, _noWrapLabel);
                GUILayout.EndHorizontal();
                GUILayout.Space(8f);
                DrawKvPreset(preset, ref changed, onKeyViewerRebuild, onKeyViewerReset);
                if (changed) onChanged?.Invoke();
                if (_needsKvRebuild)
                {
                    onKeyViewerRebuild?.Invoke();
                    _needsKvRebuild = false;
                }
                return;
            }

            DrawOverlaySection(settings, ref changed);
            GUILayout.Space(4f);

            DrawComboSection(settings, ref changed);
            GUILayout.Space(4f);

            DrawHideUiSection(settings, ref changed);
            GUILayout.Space(4f);

            DrawTweaksSection(settings, ref changed);
            GUILayout.Space(4f);

            DrawKeyLimiterSection(settings, ref changed);
            GUILayout.Space(4f);

            DrawChatterBlockerSection(settings, ref changed);
            GUILayout.Space(4f);

            DrawKeyViewerSection(settings, ref changed, onKeyViewerRebuild);
            GUILayout.Space(4f);

            DrawMiscSection(settings, ref changed);

            DrawFont(settings, ref changed, ref fontChanged);
            GUILayout.Space(4f);

            if (fontChanged) onFontChanged?.Invoke();
            if (changed || fontChanged) onChanged?.Invoke();
        }
    }
}
