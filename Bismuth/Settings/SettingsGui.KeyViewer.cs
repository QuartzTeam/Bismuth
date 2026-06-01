using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bismuth
{
    internal static partial class SettingsGui
    {
        private static void DrawKeyViewerSection(Settings settings, ref bool changed, Action onRebuild)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button((_keyViewerOpen ? "▼" : "►") + " Key Viewer", GUILayout.ExpandWidth(false)))
                _keyViewerOpen = !_keyViewerOpen;
            bool kvEnabled = GUILayout.Toggle(settings.ShowKeyViewer, " Enabled");
            if (kvEnabled != settings.ShowKeyViewer)
            {
                settings.ShowKeyViewer = kvEnabled;
                changed = true;
                onRebuild?.Invoke();
            }
            GUILayout.EndHorizontal();

            if (!_keyViewerOpen) return;

            GUILayout.Space(4f);

            DrawKvCategory(settings, isFoot: false, ref changed, onRebuild);
            GUILayout.Space(4f);
            DrawKvCategory(settings, isFoot: true, ref changed, onRebuild);
            GUILayout.Space(4f);
        }

        private static void DrawKvCategory(Settings settings, bool isFoot, ref bool changed, Action onRebuild)
        {
            string title = isFoot ? "Foot" : "Hand";
            var list = isFoot ? settings.KvFootPresets : settings.KvHandPresets;
            int active = isFoot ? settings.KvActiveFoot : settings.KvActiveHand;
            bool enabled = isFoot ? settings.ShowFootViewer : settings.ShowHandViewer;

            GUILayout.BeginHorizontal();
            GUILayout.Space(20f);
            GUILayout.Label(title + ":", _noWrapLabel, W(45));
            bool newEnabled = GUILayout.Toggle(enabled, " Enabled");
            if (newEnabled != enabled)
            {
                if (isFoot) settings.ShowFootViewer = newEnabled;
                else        settings.ShowHandViewer = newEnabled;
                changed = true;
                onRebuild?.Invoke();
            }
            GUILayout.EndHorizontal();

            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                int idx = i;
                GUILayout.BeginHorizontal();
                GUILayout.Space(40f);
                bool isActive = idx == active;
                string bullet = isActive ? "●" : "○";
                if (GUILayout.Button(bullet + " " + list[idx].Name, GUILayout.ExpandWidth(false)))
                {
                    if (isFoot) settings.KvActiveFoot = idx;
                    else        settings.KvActiveHand = idx;
                    changed = true;
                    onRebuild?.Invoke();
                }
                GUILayout.Space(8f);
                if (GUILayout.Button("Edit", GUILayout.ExpandWidth(false)))
                {
                    _editingPreset = idx;
                    _editingIsFoot = isFoot;
                }
                GUILayout.Space(4f);
                GUI.enabled = list.Count > 1;
                if (GUILayout.Button("Delete", GUILayout.ExpandWidth(false)))
                {
                    list.RemoveAt(idx);
                    int newActive = active >= list.Count ? list.Count - 1 : active;
                    if (isFoot) settings.KvActiveFoot = newActive;
                    else        settings.KvActiveHand = newActive;
                    changed = true;
                    onRebuild?.Invoke();
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                    return;
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Space(40f);
            if (GUILayout.Button("+ Add " + title + " Preset", GUILayout.ExpandWidth(false)))
            {
                var np = new KeyViewerPreset { Name = title + " " + (list.Count + 1) };
                np.EnsureDefaults();
                list.Add(np);
                changed = true;
            }
            GUILayout.EndHorizontal();
        }

        // ── Preset edit view ──────────────────────────────────────────────────

        private static void DrawKvPreset(KeyViewerPreset preset, ref bool changed,
            Action onRebuild, Action onReset)
        {
            // Name
            GUILayout.BeginHorizontal();
            GUILayout.Space(20f);
            GUILayout.Label("Name:", _noWrapLabel, GUILayout.ExpandWidth(false));
            string newName = GUILayout.TextField(preset.Name ?? "", WMax(200));
            GUILayout.EndHorizontal();
            if (newName != preset.Name) { preset.Name = newName; changed = true; }

            GUILayout.Space(4f);

            Indent(() =>
            {
                if (GUILayout.Button("Reset Counters", GUILayout.ExpandWidth(false)))
                    onReset?.Invoke();
            });

            GUILayout.Space(4f);

            // ── Main Settings ────────────────────────────────────────────────
            Indent(() =>
            {
                if (GUILayout.Button((_kvMainOpen ? "▼" : "►") + " Main Settings", GUILayout.ExpandWidth(false)))
                    _kvMainOpen = !_kvMainOpen;
            });

            if (_kvMainOpen)
            {
                SliderRow("Key Width", out float kvKW, preset.KeyWidth, 24f, 200f, 40f, "F0", "px");
                float newKW = Mathf.Round(kvKW);
                if (newKW != preset.KeyWidth) { preset.KeyWidth = newKW; changed = true; _needsKvRebuild = true; }

                SliderRow("Gap", out float kvGap, preset.Gap, 0f, 20f, 40f, "F1", "px");
                float newGap = Mathf.Round(kvGap * 2f) * 0.5f;
                if (newGap != preset.Gap) { preset.Gap = newGap; changed = true; _needsKvRebuild = true; }

                SliderRow("X", out float kvX, preset.X, 0f, 1f, 40f, "F2");
                if (kvX != preset.X) { preset.X = kvX; changed = true; }

                SliderRow("Y", out float kvY, preset.Y, 0f, 1f, 40f, "F2");
                if (kvY != preset.Y) { preset.Y = kvY; changed = true; }

                SliderRow("Scale", out float kvScale, preset.Scale, 0.25f, 3f, 40f, "F2", "x");
                if (kvScale != preset.Scale) { preset.Scale = kvScale; changed = true; }

                GUILayout.BeginHorizontal();
                GUILayout.Space(40f);
                bool persist = GUILayout.Toggle(preset.PersistCounts, " Persist Counts Across Sessions",
                    GUILayout.ExpandWidth(false));
                GUILayout.EndHorizontal();
                if (persist != preset.PersistCounts) { preset.PersistCounts = persist; changed = true; }
            }

            GUILayout.Space(4f);

            DrawKvRows(preset, ref changed);

            GUILayout.Space(4f);

            DrawKvRain(preset, ref changed);

            GUILayout.Space(4f);

            // Ghost Keys — hand presets only. Foot doesn't use them.
            if (!_editingIsFoot)
            {
                DrawKvGhost(preset, ref changed);
                GUILayout.Space(4f);
            }

            Indent(() =>
            {
                if (GUILayout.Button((_kvBgOpen ? "▼" : "►") + " Background", GUILayout.ExpandWidth(false)))
                    _kvBgOpen = !_kvBgOpen;
            });
            if (_kvBgOpen)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(20f);
                GUILayout.BeginVertical();
                DrawKvColorEditor("Released Color", preset.BgIdle, ref _kvBgIdleOpen, ref changed);
                GUILayout.Space(4f);
                DrawKvColorEditor("Pressed Color",  preset.BgHeld, ref _kvBgHeldOpen, ref changed);
                GUILayout.Space(4f);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4f);
            Indent(() =>
            {
                if (GUILayout.Button((_kvBorderOpen ? "▼" : "►") + " Border", GUILayout.ExpandWidth(false)))
                    _kvBorderOpen = !_kvBorderOpen;
            });
            if (_kvBorderOpen)
            {
                SliderRow("Radius", out float kvRadius, preset.Radius, 0f, 64f, 40f, "F0", "px");
                int newRadius = Mathf.RoundToInt(kvRadius);
                if (newRadius != preset.Radius) { preset.Radius = newRadius; changed = true; _needsKvRebuild = true; }

                SliderRow("Width", out float kvBw, preset.BorderWidth, 0f, 16f, 40f, "F1", "px");
                float newBw = Mathf.Round(kvBw * 2f) * 0.5f;
                if (newBw != preset.BorderWidth) { preset.BorderWidth = newBw; changed = true; _needsKvRebuild = true; }

                GUILayout.BeginHorizontal();
                GUILayout.Space(20f);
                GUILayout.BeginVertical();
                DrawKvColorEditor("Released Color", preset.BorderIdle, ref _kvBorderIdleOpen, ref changed);
                GUILayout.Space(4f);
                DrawKvColorEditor("Pressed Color",  preset.BorderHeld, ref _kvBorderHeldOpen, ref changed);
                GUILayout.Space(4f);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4f);
            Indent(() =>
            {
                if (GUILayout.Button((_kvLabelOpen ? "▼" : "►") + " Label Text", GUILayout.ExpandWidth(false)))
                    _kvLabelOpen = !_kvLabelOpen;
            });
            if (_kvLabelOpen)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(20f);
                GUILayout.BeginVertical();
                bool showLabel = GUILayout.Toggle(preset.ShowLabel, " Visible");
                if (showLabel != preset.ShowLabel) { preset.ShowLabel = showLabel; changed = true; _needsKvRebuild = true; }
                DrawKvColorEditor("Released Color", preset.TxtIdle, ref _kvTxtIdleOpen, ref changed);
                GUILayout.Space(4f);
                DrawKvColorEditor("Pressed Color",  preset.TxtHeld, ref _kvTxtHeldOpen, ref changed);
                SliderRow("Font Size", out float labelSz, preset.LabelSize, 6f, 32f, 20f, "F0");
                int newLabelSz = Mathf.RoundToInt(labelSz);
                if (newLabelSz != preset.LabelSize) { preset.LabelSize = newLabelSz; changed = true; }
                GUILayout.Space(4f);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4f);
            Indent(() =>
            {
                if (GUILayout.Button((_kvCountOpen ? "▼" : "►") + " Count Text", GUILayout.ExpandWidth(false)))
                    _kvCountOpen = !_kvCountOpen;
            });
            if (_kvCountOpen)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(20f);
                GUILayout.BeginVertical();
                bool showCount = GUILayout.Toggle(preset.ShowCount, " Visible");
                if (showCount != preset.ShowCount) { preset.ShowCount = showCount; changed = true; _needsKvRebuild = true; }
                DrawKvColorEditor("Released Color", preset.CountIdle, ref _kvCountIdleOpen, ref changed);
                GUILayout.Space(4f);
                DrawKvColorEditor("Pressed Color",  preset.CountHeld, ref _kvCountHeldOpen, ref changed);
                SliderRow("Font Size", out float countSz, preset.CountSize, 6f, 32f, 20f, "F0");
                int newCountSz = Mathf.RoundToInt(countSz);
                if (newCountSz != preset.CountSize) { preset.CountSize = newCountSz; changed = true; }
                GUILayout.Space(4f);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(20f);
        }

        private static void DrawKvRows(KeyViewerPreset preset, ref bool changed)
        {
            Indent(() =>
            {
                if (GUILayout.Button((_kvRowsOpen ? "▼" : "►") + " Edit Rows", GUILayout.ExpandWidth(false)))
                    _kvRowsOpen = !_kvRowsOpen;
            });

            if (!_kvRowsOpen) return;

            Indent(() => GUILayout.Label(
                "Click a key to edit its label / width / position. Use the + button to add a key by pressing it.",
                _noWrapLabel), 40f);

            var rows = preset.Rows;
            if (rows == null) { rows = new List<KeyViewerRow>(); preset.Rows = rows; }

            while (_kvRowRainOpen.Count < rows.Count) _kvRowRainOpen.Add(false);
            while (_kvRowOpen.Count     < rows.Count) _kvRowOpen.Add(true);

            int removeRow = -1;
            for (int ri = 0; ri < rows.Count; ri++)
            {
                int idx = ri;
                var row = rows[idx];
                row.EnsureDefaults();

                GUILayout.BeginHorizontal();
                GUILayout.Space(40f);
                if (GUILayout.Button((_kvRowOpen[idx] ? "▼" : "►") + " Row " + (idx + 1), GUILayout.ExpandWidth(false)))
                    _kvRowOpen[idx] = !_kvRowOpen[idx];
                GUILayout.EndHorizontal();

                if (!_kvRowOpen[idx]) { GUILayout.Space(4f); continue; }

                DrawCellGrid(row, idx, preset, ref changed);

                SliderRow("Height", out float rowH, row.Height, 24f, 128f, 60f, "F0", "px");
                float newRowH = Mathf.Round(rowH);
                if (newRowH != row.Height) { row.Height = newRowH; changed = true; _needsKvRebuild = true; }

                GUILayout.BeginHorizontal();
                GUILayout.Space(60f);
                bool rowShowRain = GUILayout.Toggle(rows[idx].ShowRain, " Show Rain", GUILayout.ExpandWidth(false));
                if (rowShowRain != rows[idx].ShowRain) { rows[idx].ShowRain = rowShowRain; changed = true; _needsKvRebuild = true; }
                GUILayout.EndHorizontal();

                if (rows[idx].ShowRain)
                {
                GUILayout.BeginHorizontal();
                GUILayout.Space(60f);
                if (rows[idx].RainColor == null)
                {
                    GUILayout.Label("Rain: default", _noWrapLabel, GUILayout.ExpandWidth(false));
                    if (GUILayout.Button("Custom", GUILayout.ExpandWidth(false)))
                    {
                        var bh = preset.BgHeld;
                        rows[idx].RainColor = new KvColor { R = bh.R, G = bh.G, B = bh.B, A = bh.A };
                        _kvRowRainOpen[idx] = true;
                        changed = true;
                        _needsKvRebuild = true;
                    }
                }
                else
                {
                    if (GUILayout.Button((_kvRowRainOpen[idx] ? "▼" : "►") + " Rain:", GUILayout.ExpandWidth(false)))
                        _kvRowRainOpen[idx] = !_kvRowRainOpen[idx];
                    DrawSwatch(rows[idx].RainColor.ToColor());
                    if (GUILayout.Button("Reset", GUILayout.ExpandWidth(false)))
                    { rows[idx].RainColor = null; _kvRowRainOpen[idx] = false; changed = true; _needsKvRebuild = true; }
                }
                GUILayout.Space(10f);
                GUILayout.EndHorizontal();

                if (rows[idx].RainColor != null && _kvRowRainOpen[idx])
                {
                    var rc = rows[idx].RainColor;
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(80f);
                    GUILayout.BeginVertical();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("#", _noWrapLabel, GUILayout.ExpandWidth(false));
                    string rcHex = ColorToHex(rc.R, rc.G, rc.B);
                    if (DeferredText(rcHex, 65f, out string rcCommit) &&
                        TryParseHex(rcCommit, out float rh, out float rg, out float rb2))
                    { rc.R = rh; rc.G = rg; rc.B = rb2; changed = true; }
                    GUILayout.EndHorizontal();

                    GUILayout.Label("R: " + rc.R.ToString("F2"), _noWrapLabel);
                    float rr = GUILayout.HorizontalSlider(rc.R, 0f, 1f, WMax(300));
                    if (rr != rc.R) { rc.R = rr; changed = true; }
                    GUILayout.Label("G: " + rc.G.ToString("F2"), _noWrapLabel);
                    float rgg = GUILayout.HorizontalSlider(rc.G, 0f, 1f, WMax(300));
                    if (rgg != rc.G) { rc.G = rgg; changed = true; }
                    GUILayout.Label("B: " + rc.B.ToString("F2"), _noWrapLabel);
                    float rbb = GUILayout.HorizontalSlider(rc.B, 0f, 1f, WMax(300));
                    if (rbb != rc.B) { rc.B = rbb; changed = true; }
                    GUILayout.Label("A: " + rc.A.ToString("F2"), _noWrapLabel);
                    float raa = GUILayout.HorizontalSlider(rc.A, 0f, 1f, WMax(300));
                    if (raa != rc.A) { rc.A = raa; changed = true; }

                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                }
                }

                GUILayout.Space(6f);
                GUILayout.BeginHorizontal();
                GUILayout.Space(40f);
                if (GUILayout.Button("× Delete Row", GUILayout.ExpandWidth(false))) removeRow = idx;
                GUILayout.EndHorizontal();
                GUILayout.Space(20f);
            }

            if (removeRow >= 0)
            {
                rows.RemoveAt(removeRow);
                if (_kvRowRainOpen.Count > removeRow) _kvRowRainOpen.RemoveAt(removeRow);
                if (_kvRowOpen.Count     > removeRow) _kvRowOpen.RemoveAt(removeRow);
                changed = true;
                _needsKvRebuild = true;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Space(40f);
            if (GUILayout.Button("+ Add Row", GUILayout.ExpandWidth(false)))
            { rows.Add(new KeyViewerRow()); changed = true; _needsKvRebuild = true; }
            GUILayout.EndHorizontal();
        }

        // Row's button grid + listen + per-cell edit panel.
        private static void DrawCellGrid(KeyViewerRow row, int rowIdx, KeyViewerPreset preset, ref bool changed)
        {
            // Row of cell buttons
            GUILayout.BeginHorizontal();
            GUILayout.Space(60f);

            int removeCell = -1;
            int moveLeft = -1;
            int moveRight = -1;

            // Start/Stop listen button (before the cell row)
            bool listening = _kvListenRow == rowIdx;
            if (GUILayout.Button(listening ? "■ Stop" : "● Listen", GUILayout.ExpandWidth(false)))
            {
                _kvListenRow = listening ? -1 : rowIdx;
                _kvExpandedRow = -1; _kvExpandedCell = -1;
            }
            if (listening && GUILayout.Button("+ KPS", GUILayout.ExpandWidth(false)))
            { row.Cells.Add(new KeyViewerCell { Token = "KPS" }); changed = true; _needsKvRebuild = true; }
            if (listening && GUILayout.Button("+ Total", GUILayout.ExpandWidth(false)))
            { row.Cells.Add(new KeyViewerCell { Token = "Total" }); changed = true; _needsKvRebuild = true; }

            for (int c = 0; c < row.Cells.Count; c++)
            {
                var cell = row.Cells[c];
                string label = cell.Label;
                if (string.IsNullOrEmpty(label))
                {
                    if (cell.Token == "KPS" || cell.Token == "Total") label = cell.Token;
                    else label = KeyViewer.TryParseKey(cell.Token, out UnityEngine.KeyCode kc)
                        ? PrettyKeyLabel(kc, cell.Token)
                        : cell.Token;
                }
                bool selected = _kvExpandedRow == rowIdx && _kvExpandedCell == c;
                if (GUILayout.Button((selected ? "▼ " : "") + label, GUILayout.ExpandWidth(false)))
                {
                    if (selected) { _kvExpandedRow = -1; _kvExpandedCell = -1; }
                    else { _kvExpandedRow = rowIdx; _kvExpandedCell = c; }
                }
            }
            GUILayout.EndHorizontal();

            // Listen for the next keydown if this row is listening. Stays listening so
            // multiple keys can be added/removed in one session. Pressing an already-registered
            // key removes it (toggle).
            if (listening)
            {
                KeyCode pressed = ListenForKey();
                if (pressed != KeyCode.None && pressed != KeyCode.Escape)
                {
                    string tok = TokenFromKeyCode(pressed);
                    int existing = row.Cells.FindIndex(x => x.Token == tok);
                    if (existing >= 0) row.Cells.RemoveAt(existing);
                    else row.Cells.Add(new KeyViewerCell { Token = tok });
                    changed = true;
                    _needsKvRebuild = true;
                }
            }

            // Inline edit panel for the selected cell
            if (_kvExpandedRow == rowIdx && _kvExpandedCell >= 0 && _kvExpandedCell < row.Cells.Count)
            {
                int ci = _kvExpandedCell;
                var cell = row.Cells[ci];

                GUILayout.BeginHorizontal();
                GUILayout.Space(80f);
                GUILayout.Label("Token: " + cell.Token, _noWrapLabel, GUILayout.ExpandWidth(false));
                GUILayout.Space(8f);
                if (GUILayout.Button("◀", GUILayout.ExpandWidth(false))) moveLeft = ci;
                if (GUILayout.Button("▶", GUILayout.ExpandWidth(false))) moveRight = ci;
                GUILayout.Space(8f);
                if (GUILayout.Button("× Delete", GUILayout.ExpandWidth(false))) removeCell = ci;
                GUILayout.EndHorizontal();

                // Change Key — rebind this cell to a different key. Hidden for stat cells (KPS/Total).
                if (cell.Token != "KPS" && cell.Token != "Total")
                {
                    bool cellListening = _kvCellListenRow == rowIdx && _kvCellListenCell == ci;
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(80f);
                    if (GUILayout.Button(cellListening ? "■ Cancel" : "Change Key", GUILayout.ExpandWidth(false)))
                    {
                        if (cellListening) { _kvCellListenRow = -1; _kvCellListenCell = -1; }
                        else { _kvCellListenRow = rowIdx; _kvCellListenCell = ci; _kvListenRow = -1; }
                    }
                    GUILayout.EndHorizontal();
                    if (cellListening)
                    {
                        Indent(() => GUILayout.Label("Listening…", _noWrapLabel), 80f);
                        KeyCode pressed = ListenForKey();
                        if (pressed != KeyCode.None && pressed != KeyCode.Escape)
                        {
                            // Swap the token first so TransferKeyCount's "is oldKey still in use"
                            // scan sees the new binding; otherwise it spots this very cell still
                            // holding the old token and leaves the old count in _counts, which
                            // then double-counts on the next rebind back.
                            bool hadOld = KeyViewer.TryParseKey(cell.Token, out var oldKey);
                            cell.Token = TokenFromKeyCode(pressed);
                            if (hadOld && KeyViewer.Instance != null)
                                KeyViewer.Instance.TransferKeyCount(preset, oldKey, pressed);
                            _kvCellListenRow = -1; _kvCellListenCell = -1;
                            changed = true;
                            _needsKvRebuild = true;
                        }
                    }
                }

                GUILayout.BeginHorizontal();
                GUILayout.Space(80f);
                GUILayout.Label("Label override:", _noWrapLabel, GUILayout.ExpandWidth(false));
                string labelInput = cell.Label ?? "";
                string newLabel = GUILayout.TextField(labelInput, WMax(160));
                if (newLabel != labelInput) { cell.Label = string.IsNullOrEmpty(newLabel) ? null : newLabel; changed = true; _needsKvRebuild = true; }
                GUILayout.EndHorizontal();

                // Width slider — adjusts this cell AND its symmetric mirror in the row.
                SliderRow("Width", out float w, cell.WidthMul, 0.25f, 4f, 80f, "F2", "x");
                if (Mathf.Abs(w - cell.WidthMul) > 1e-4f)
                {
                    cell.WidthMul = w;
                    int mirror = row.Cells.Count - 1 - ci;
                    if (mirror != ci && mirror >= 0 && mirror < row.Cells.Count)
                        row.Cells[mirror].WidthMul = w;
                    changed = true;
                    _needsKvRebuild = true;
                }
            }

            if (removeCell >= 0)
            {
                row.Cells.RemoveAt(removeCell);
                if (_kvExpandedRow == rowIdx)
                {
                    if (_kvExpandedCell >= row.Cells.Count) _kvExpandedCell = row.Cells.Count - 1;
                    if (_kvExpandedCell < 0) { _kvExpandedRow = -1; _kvExpandedCell = -1; }
                }
                changed = true;
                _needsKvRebuild = true;
            }
            if (moveLeft > 0)
            {
                var tmp = row.Cells[moveLeft]; row.Cells[moveLeft] = row.Cells[moveLeft - 1]; row.Cells[moveLeft - 1] = tmp;
                _kvExpandedCell = moveLeft - 1;
                changed = true;
                _needsKvRebuild = true;
            }
            if (moveRight >= 0 && moveRight < row.Cells.Count - 1)
            {
                var tmp = row.Cells[moveRight]; row.Cells[moveRight] = row.Cells[moveRight + 1]; row.Cells[moveRight + 1] = tmp;
                _kvExpandedCell = moveRight + 1;
                changed = true;
                _needsKvRebuild = true;
            }
        }

        // Returns the KeyCode of the first key pressed this frame while a listen widget is active.
        // Combines Event.KeyDown (which fires for most keys) with an explicit Input.GetKeyDown
        // poll for the modifier keys, since on macOS modifier-only presses don't surface as
        // IMGUI key events. Returns Escape for cancel, or KeyCode.None when nothing was pressed.
        private static KeyCode ListenForKey()
        {
            if (Event.current != null && Event.current.type == EventType.KeyDown &&
                Event.current.keyCode != KeyCode.None)
            {
                var kc = Event.current.keyCode;
                Event.current.Use();
                return kc;
            }
            KeyCode[] modifiers = {
                KeyCode.LeftShift,   KeyCode.RightShift,
                KeyCode.LeftControl, KeyCode.RightControl,
                KeyCode.LeftAlt,     KeyCode.RightAlt,
                KeyCode.LeftCommand, KeyCode.RightCommand,
                KeyCode.CapsLock,
            };
            foreach (var k in modifiers)
                if (Input.GetKeyDown(k)) return k;
            return KeyCode.None;
        }

        private static string PrettyKeyLabel(KeyCode kc, string fallbackToken)
        {
            switch (kc)
            {
                case KeyCode.LeftShift:    return "LShift";
                case KeyCode.RightShift:   return "RShift";
                case KeyCode.LeftControl:  return "LCtrl";
                case KeyCode.RightControl: return "RCtrl";
                case KeyCode.LeftAlt:      return "LAlt";
                case KeyCode.RightAlt:     return "RAlt";
                case KeyCode.CapsLock:     return "Caps";
                case KeyCode.Return:       return "Enter";
                case KeyCode.Backspace:    return "Backspace";
                case KeyCode.Escape:       return "Esc";
                case KeyCode.UpArrow:      return "↑";
                case KeyCode.DownArrow:    return "↓";
                case KeyCode.LeftArrow:    return "←";
                case KeyCode.RightArrow:   return "→";
                default:                   return fallbackToken;
            }
        }

        // Map a KeyCode captured from a live keypress back into the Token string that
        // KeyViewer.TryParseKey expects (mirrors the friendly-name table in KeyViewer.Keys.cs).
        private static string TokenFromKeyCode(KeyCode kc)
        {
            switch (kc)
            {
                case KeyCode.Tab:          return "Tab";
                case KeyCode.CapsLock:     return "Caps";
                case KeyCode.Space:        return "Space";
                case KeyCode.Return:       return "Enter";
                case KeyCode.Backspace:    return "Backspace";
                case KeyCode.Escape:       return "Escape";
                case KeyCode.LeftShift:    return "LShift";
                case KeyCode.RightShift:   return "RShift";
                case KeyCode.LeftControl:  return "LCtrl";
                case KeyCode.RightControl: return "RCtrl";
                case KeyCode.LeftAlt:      return "LAlt";
                case KeyCode.RightAlt:     return "RAlt";
                case KeyCode.LeftCommand:  return "LCmd";
                case KeyCode.RightCommand: return "RCmd";
                case KeyCode.UpArrow:      return "Up";
                case KeyCode.DownArrow:    return "Down";
                case KeyCode.LeftArrow:    return "Left";
                case KeyCode.RightArrow:   return "Right";
                case KeyCode.LeftBracket:  return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Backslash:    return "\\";
                case KeyCode.Semicolon:    return ";";
                case KeyCode.Quote:        return "'";
                case KeyCode.Comma:        return ",";
                case KeyCode.Period:       return ".";
                case KeyCode.Slash:        return "/";
                case KeyCode.BackQuote:    return "`";
                case KeyCode.Minus:        return "-";
                case KeyCode.Equals:       return "=";
                default:
                    if (kc >= KeyCode.A && kc <= KeyCode.Z) return ((char)('A' + (kc - KeyCode.A))).ToString();
                    if (kc >= KeyCode.Alpha0 && kc <= KeyCode.Alpha9) return ((char)('0' + (kc - KeyCode.Alpha0))).ToString();
                    return kc.ToString();
            }
        }

        private static void DrawKvRain(KeyViewerPreset preset, ref bool changed)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(20f);
            if (GUILayout.Button((_kvKeyRainOpen ? "▼" : "►") + " Key Rain", GUILayout.ExpandWidth(false)))
                _kvKeyRainOpen = !_kvKeyRainOpen;
            GUILayout.EndHorizontal();

            if (!_kvKeyRainOpen) return;

            SliderRow("Track Length", out float rainTrack, preset.RainTrackLength, 50f, 1200f, 40f, "F0", "px");
            float newTrack = Mathf.Round(rainTrack);
            if (newTrack != preset.RainTrackLength)
            {
                preset.RainTrackLength = newTrack;
                if (preset.RainDistance > newTrack) preset.RainDistance = newTrack;
                changed = true;
            }

            float fadeMax = Mathf.Max(0f, preset.RainTrackLength);
            SliderRow("Fade Start", out float rainDist, preset.RainDistance, 0f, fadeMax, 40f, "F0", "px");
            float newDist = Mathf.Round(rainDist);
            if (newDist != preset.RainDistance) { preset.RainDistance = newDist; changed = true; }

            SliderRow("Rain Speed", out float rainSpd, preset.RainSpeed, 50f, 1200f, 40f, "F0", "px/s");
            float newSpd = Mathf.Round(rainSpd);
            if (newSpd != preset.RainSpeed) { preset.RainSpeed = newSpd; changed = true; }

            SliderRow("Rain Width Step", out float rainStep, preset.RainWidthStep, 0f, 64f, 40f, "F0", "px/row");
            float newStep = Mathf.Round(rainStep);
            if (newStep != preset.RainWidthStep) { preset.RainWidthStep = newStep; changed = true; }

            SliderRow("Shadow Size", out float rainSh, preset.RainShadowSize, 0f, 64f, 40f, "F0", "px");
            float newSh = Mathf.Round(rainSh);
            if (newSh != preset.RainShadowSize) { preset.RainShadowSize = newSh; changed = true; }

            if (preset.RainShadowSize > 0f && preset.RainShadowColor != null)
            {
                var sc = preset.RainShadowColor;
                GUILayout.BeginHorizontal();
                GUILayout.Space(40f);
                GUILayout.Label("Shadow Color:", _noWrapLabel, GUILayout.ExpandWidth(false));
                DrawSwatch(sc.ToColor());
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(60f);
                GUILayout.Label("#", _noWrapLabel, GUILayout.ExpandWidth(false));
                string scHex = ColorToHex(sc.R, sc.G, sc.B);
                if (DeferredText(scHex, 90f, out string scCommit) &&
                    TryParseHex(scCommit, out float shR, out float shG, out float shB))
                { sc.R = shR; sc.G = shG; sc.B = shB; changed = true; }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(60f);
                GUILayout.Label("A: " + sc.A.ToString("F2"), _noWrapLabel);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Space(60f);
                float scA = GUILayout.HorizontalSlider(sc.A, 0f, 1f, WMax(300));
                if (scA != sc.A) { sc.A = scA; changed = true; }
                GUILayout.EndHorizontal();
            }
        }

        // Number of non-stat cells in the top row — defines the ghost-key slot count.
        private static int TopRowKeySlots(KeyViewerPreset preset)
        {
            if (preset?.Rows == null || preset.Rows.Count == 0) return 0;
            var row = preset.Rows[0];
            if (row == null) return 0;
            row.EnsureDefaults();
            if (row.Cells == null) return 0;
            int n = 0;
            foreach (var c in row.Cells)
                if (c.Token != "KPS" && c.Token != "Total") n++;
            return n;
        }

        private static void DrawKvGhost(KeyViewerPreset preset, ref bool changed)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(20f);
            if (GUILayout.Button((_kvGhostOpen ? "▼" : "►") + " Ghost Keys", GUILayout.ExpandWidth(false)))
                _kvGhostOpen = !_kvGhostOpen;
            bool enabled = GUILayout.Toggle(preset.GhostKeysEnabled, " Enabled");
            GUILayout.EndHorizontal();
            if (enabled != preset.GhostKeysEnabled)
            {
                preset.GhostKeysEnabled = enabled;
                changed = true;
                _needsKvRebuild = true;
            }

            if (!_kvGhostOpen) return;

            Indent(() => GUILayout.Label(
                "Ghost keys spawn rain at the matching top-row position but don't count as input.",
                _noWrapLabel), 20f);
            GUILayout.Space(4f);

            if (!preset.GhostKeysEnabled) return;

            int slots = TopRowKeySlots(preset);
            if (slots == 0)
            {
                Indent(() => GUILayout.Label("(top row has no key cells)", _noWrapLabel), 20f);
                return;
            }

            // Sync GhostKeys length to slot count. Pad with "None"; trim extras.
            if (preset.GhostKeys == null) preset.GhostKeys = new List<string>();
            bool listChanged = false;
            while (preset.GhostKeys.Count < slots) { preset.GhostKeys.Add("None"); listChanged = true; }
            while (preset.GhostKeys.Count > slots) { preset.GhostKeys.RemoveAt(preset.GhostKeys.Count - 1); listChanged = true; }

            GUILayout.BeginHorizontal();
            GUILayout.Space(40f);
            for (int i = 0; i < slots; i++)
            {
                string tok = preset.GhostKeys[i] ?? "None";
                bool listening = _kvGhostListenIdx == i;
                string label = listening
                    ? "Press a key…"
                    : (tok == "None" || string.IsNullOrEmpty(tok)
                        ? "None"
                        : (KeyViewer.TryParseKey(tok, out KeyCode kc) ? PrettyKeyLabel(kc, tok) : tok));
                if (GUILayout.Button(label, GUILayout.ExpandWidth(false)))
                {
                    if (listening) _kvGhostListenIdx = -1;
                    else if (tok != "None" && !string.IsNullOrEmpty(tok))
                    {
                        preset.GhostKeys[i] = "None";
                        listChanged = true;
                    }
                    else _kvGhostListenIdx = i;
                }
            }
            GUILayout.EndHorizontal();

            if (_kvGhostListenIdx >= 0 && _kvGhostListenIdx < preset.GhostKeys.Count)
            {
                KeyCode pressed = ListenForKey();
                if (pressed != KeyCode.None && pressed != KeyCode.Escape)
                {
                    preset.GhostKeys[_kvGhostListenIdx] = TokenFromKeyCode(pressed);
                    _kvGhostListenIdx = -1;
                    listChanged = true;
                }
            }

            if (listChanged) { changed = true; _needsKvRebuild = true; }

            GUILayout.Space(4f);

            // Ghost Rain Color editor — None / Custom toggle like the per-row editor.
            GUILayout.BeginHorizontal();
            GUILayout.Space(40f);
            if (preset.GhostRainColor == null)
            {
                GUILayout.Label("Ghost Rain: default ", _noWrapLabel, GUILayout.ExpandWidth(false));
                DrawSwatch(new UnityEngine.Color(1f, 0.9f, 0f, 1f));
                if (GUILayout.Button("Custom", GUILayout.ExpandWidth(false)))
                {
                    preset.GhostRainColor = new KvColor { R = 1f, G = 0.9f, B = 0f, A = 1f };
                    _kvGhostRainOpen = true;
                    changed = true;
                    _needsKvRebuild = true;
                }
            }
            else
            {
                if (GUILayout.Button((_kvGhostRainOpen ? "▼" : "►") + " Ghost Rain:", GUILayout.ExpandWidth(false)))
                    _kvGhostRainOpen = !_kvGhostRainOpen;
                DrawSwatch(preset.GhostRainColor.ToColor());
                if (GUILayout.Button("Reset", GUILayout.ExpandWidth(false)))
                { preset.GhostRainColor = null; _kvGhostRainOpen = false; changed = true; _needsKvRebuild = true; }
            }
            GUILayout.EndHorizontal();

            if (preset.GhostRainColor != null && _kvGhostRainOpen)
            {
                var gc = preset.GhostRainColor;
                GUILayout.BeginHorizontal();
                GUILayout.Space(80f);
                GUILayout.BeginVertical();
                GUILayout.Label("R: " + gc.R.ToString("F2"), _noWrapLabel);
                float gr = GUILayout.HorizontalSlider(gc.R, 0f, 1f, WMax(300));
                if (gr != gc.R) { gc.R = gr; changed = true; }
                GUILayout.Label("G: " + gc.G.ToString("F2"), _noWrapLabel);
                float gg = GUILayout.HorizontalSlider(gc.G, 0f, 1f, WMax(300));
                if (gg != gc.G) { gc.G = gg; changed = true; }
                GUILayout.Label("B: " + gc.B.ToString("F2"), _noWrapLabel);
                float gb = GUILayout.HorizontalSlider(gc.B, 0f, 1f, WMax(300));
                if (gb != gc.B) { gc.B = gb; changed = true; }
                GUILayout.Label("A: " + gc.A.ToString("F2"), _noWrapLabel);
                float ga = GUILayout.HorizontalSlider(gc.A, 0f, 1f, WMax(300));
                if (ga != gc.A) { gc.A = ga; changed = true; }
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
        }

        private static void DrawKvColorEditor(string label, KvColor color, ref bool open, ref bool changed)
        {
            if (color == null) return;

            GUILayout.BeginHorizontal();
            GUILayout.Space(20f);
            if (GUILayout.Button((open ? "▼" : "►") + " " + label, GUILayout.ExpandWidth(false)))
                open = !open;
            DrawSwatch(color.ToColor());
            GUILayout.EndHorizontal();

            if (!open) return;

            GUILayout.BeginHorizontal();
            GUILayout.Space(40f);
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label("#", _noWrapLabel, GUILayout.ExpandWidth(false));
            string hex = ColorToHex(color.R, color.G, color.B);
            if (DeferredText(hex, 65f, out string commitHex) &&
                TryParseHex(commitHex, out float hr, out float hg, out float hb))
            { color.R = hr; color.G = hg; color.B = hb; changed = true; }
            GUILayout.EndHorizontal();

            GUILayout.Label("R: " + color.R.ToString("F2"), _noWrapLabel);
            float r = GUILayout.HorizontalSlider(color.R, 0f, 1f, WMax(300));
            if (r != color.R) { color.R = r; changed = true; }

            GUILayout.Label("G: " + color.G.ToString("F2"), _noWrapLabel);
            float g = GUILayout.HorizontalSlider(color.G, 0f, 1f, WMax(300));
            if (g != color.G) { color.G = g; changed = true; }

            GUILayout.Label("B: " + color.B.ToString("F2"), _noWrapLabel);
            float b = GUILayout.HorizontalSlider(color.B, 0f, 1f, WMax(300));
            if (b != color.B) { color.B = b; changed = true; }

            GUILayout.Label("A: " + color.A.ToString("F2"), _noWrapLabel);
            float a = GUILayout.HorizontalSlider(color.A, 0f, 1f, WMax(300));
            if (a != color.A) { color.A = a; changed = true; }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
    }
}
