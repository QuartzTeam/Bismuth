using UnityEngine;

namespace Bismuth
{
    internal static partial class SettingsGui
    {
        private static void DrawComboSection(Settings settings, ref bool changed)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button((_comboDisplayOpen ? "▼" : "►") + " Combo Display", GUILayout.ExpandWidth(false)))
                _comboDisplayOpen = !_comboDisplayOpen;
            bool showCd = GUILayout.Toggle(settings.ShowComboDisplay, " Enabled");
            if (showCd != settings.ShowComboDisplay) { settings.ShowComboDisplay = showCd; changed = true; }
            GUILayout.EndHorizontal();

            if (!_comboDisplayOpen) return;

            bool changedLocal = changed;
            Indent(() =>
            {
                bool countAuto = GUILayout.Toggle(settings.ComboCountAuto, " Count Auto Tiles");
                if (countAuto != settings.ComboCountAuto) { settings.ComboCountAuto = countAuto; changedLocal = true; }
            });
            changed = changedLocal;

            SliderRow("Y Offset", out float cdY, settings.ComboDisplayY, -400f, 400f, 20f, "F0", "px");
            if (cdY != settings.ComboDisplayY) { settings.ComboDisplayY = cdY; changed = true; }

            SliderRow("Size", out float cdSize, settings.ComboDisplaySize, 0.25f, 3.0f, 20f, "F2", "x");
            if (cdSize != settings.ComboDisplaySize) { settings.ComboDisplaySize = cdSize; changed = true; }

            GUILayout.Space(8f);

            Indent(() =>
            {
                if (GUILayout.Button((_comboLabelOpen ? "▼" : "►") + " Label", GUILayout.ExpandWidth(false)))
                    _comboLabelOpen = !_comboLabelOpen;
            });
            if (_comboLabelOpen)
            {
                bool textChanged = false;
                Indent(() =>
                {
                    GUILayout.Label("Text:", _noWrapLabel, GUILayout.ExpandWidth(false));
                    string newText = GUILayout.TextField(settings.ComboDisplayText, WMax(300));
                    if (newText != settings.ComboDisplayText) { settings.ComboDisplayText = newText; textChanged = true; }
                }, 40f);
                if (textChanged) changed = true;

                SliderRow("Size", out float cdLabelSize, settings.ComboLabelSize, 0.25f, 3.0f, 40f, "F2", "x");
                if (cdLabelSize != settings.ComboLabelSize) { settings.ComboLabelSize = cdLabelSize; changed = true; }

                SliderRow("Y Offset", out float cdLabelY, settings.ComboLabelY, -300f, 300f, 40f, "F0", "px");
                if (cdLabelY != settings.ComboLabelY) { settings.ComboLabelY = cdLabelY; changed = true; }

                Indent(() =>
                {
                    if (GUILayout.Button((_comboLabelShadowOpen ? "▼" : "►") + " Shadow", GUILayout.ExpandWidth(false)))
                        _comboLabelShadowOpen = !_comboLabelShadowOpen;
                }, 40f);
                if (_comboLabelShadowOpen)
                {
                    SliderRow("X", out float lShX, settings.ComboLabelShadowOffsetX, -20f, 20f, 60f, "F1", "px");
                    if (lShX != settings.ComboLabelShadowOffsetX) { settings.ComboLabelShadowOffsetX = lShX; changed = true; }

                    SliderRow("Y", out float lShY, settings.ComboLabelShadowOffsetY, -20f, 20f, 60f, "F1", "px");
                    if (lShY != settings.ComboLabelShadowOffsetY) { settings.ComboLabelShadowOffsetY = lShY; changed = true; }

                    bool lShColorCh = false;
                    Indent(() => DrawKvColorEditor("Color", settings.ComboLabelShadowColor, ref _comboLabelShadowColorOpen, ref lShColorCh), 40f);
                    if (lShColorCh) changed = true;
                }
            }

            Indent(() =>
            {
                if (GUILayout.Button((_comboCountOpen ? "▼" : "►") + " Count", GUILayout.ExpandWidth(false)))
                    _comboCountOpen = !_comboCountOpen;
            });
            if (_comboCountOpen)
            {
                SliderRow("Size", out float cdCountSize, settings.ComboCountSize, 0.25f, 3.0f, 40f, "F2", "x");
                if (cdCountSize != settings.ComboCountSize) { settings.ComboCountSize = cdCountSize; changed = true; }

                Indent(() =>
                {
                    if (GUILayout.Button((_comboCountShadowOpen ? "▼" : "►") + " Shadow", GUILayout.ExpandWidth(false)))
                        _comboCountShadowOpen = !_comboCountShadowOpen;
                }, 40f);
                if (_comboCountShadowOpen)
                {
                    SliderRow("X", out float cShX, settings.ComboShadowOffsetX, -20f, 20f, 60f, "F1", "px");
                    if (cShX != settings.ComboShadowOffsetX) { settings.ComboShadowOffsetX = cShX; changed = true; }

                    SliderRow("Y", out float cShY, settings.ComboShadowOffsetY, -20f, 20f, 60f, "F1", "px");
                    if (cShY != settings.ComboShadowOffsetY) { settings.ComboShadowOffsetY = cShY; changed = true; }

                    bool cShColorCh = false;
                    Indent(() => DrawKvColorEditor("Color", settings.ComboShadowColor, ref _comboCountShadowColorOpen, ref cShColorCh), 40f);
                    if (cShColorCh) changed = true;
                }
            }

            Indent(() =>
            {
                if (GUILayout.Button((_comboAnimationsOpen ? "▼" : "►") + " Animations", GUILayout.ExpandWidth(false)))
                    _comboAnimationsOpen = !_comboAnimationsOpen;
            });
            if (_comboAnimationsOpen)
            {
                SliderRow("Pulse Duration", out float cdPulseDur, settings.ComboPulseDuration, 0f, 0.5f, 40f, "F2", "s");
                if (cdPulseDur != settings.ComboPulseDuration) { settings.ComboPulseDuration = cdPulseDur; changed = true; }

                SliderRow("Label Pulse Offset Y", out float cdPulseOff, settings.ComboPulseOffsetY, -40f, 40f, 40f, "F0", "px");
                if (cdPulseOff != settings.ComboPulseOffsetY) { settings.ComboPulseOffsetY = cdPulseOff; changed = true; }

                SliderRow("Count Pulse Scale", out float cdPulseScale, settings.ComboPulseScale, 0f, 1f, 40f, "F2", "x");
                if (cdPulseScale != settings.ComboPulseScale) { settings.ComboPulseScale = cdPulseScale; changed = true; }
            }

            GUILayout.Space(8f);

            Indent(() =>
            {
                if (GUILayout.Button((_comboColorOpen ? "▼" : "►") + " Color", GUILayout.ExpandWidth(false)))
                    _comboColorOpen = !_comboColorOpen;
            });

            if (_comboColorOpen)
            {
                SliderRow("Max Combo", out float cgMax, settings.ComboGradientMax, 10f, 5000f, 40f, "F0");
                if (cgMax != settings.ComboGradientMax) { settings.ComboGradientMax = Mathf.Round(cgMax); changed = true; }
                DrawGradientEditor("Combo", settings.ComboGradient, ref changed);
            }
        }
    }
}
