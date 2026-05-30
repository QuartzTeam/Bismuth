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

            bool textChanged = false;
            Indent(() =>
            {
                GUILayout.Label("Label Text:", _noWrapLabel, GUILayout.ExpandWidth(false));
                string newText = GUILayout.TextField(settings.ComboDisplayText, WMax(300));
                if (newText != settings.ComboDisplayText) { settings.ComboDisplayText = newText; textChanged = true; }
            });
            if (textChanged) changed = true;

            SliderRow("Label Size", out float cdLabelSize, settings.ComboLabelSize, 0.25f, 3.0f, 20f, "F2", "x");
            if (cdLabelSize != settings.ComboLabelSize) { settings.ComboLabelSize = cdLabelSize; changed = true; }

            SliderRow("Label Y Offset", out float cdLabelY, settings.ComboLabelY, -300f, 300f, 20f, "F0", "px");
            if (cdLabelY != settings.ComboLabelY) { settings.ComboLabelY = cdLabelY; changed = true; }

            GUILayout.Space(8f);

            SliderRow("Label Pulse Offset Y", out float cdPulseOff, settings.ComboPulseOffsetY, -40f, 40f, 20f, "F0", "px");
            if (cdPulseOff != settings.ComboPulseOffsetY) { settings.ComboPulseOffsetY = cdPulseOff; changed = true; }

            SliderRow("Count Pulse Scale", out float cdPulseScale, settings.ComboPulseScale, 0f, 1f, 20f, "F2", "x");
            if (cdPulseScale != settings.ComboPulseScale) { settings.ComboPulseScale = cdPulseScale; changed = true; }

            SliderRow("Pulse Duration", out float cdPulseDur, settings.ComboPulseDuration, 0f, 0.5f, 20f, "F2", "s");
            if (cdPulseDur != settings.ComboPulseDuration) { settings.ComboPulseDuration = cdPulseDur; changed = true; }

            SliderRow("Shadow Size", out float cdShadow, settings.ComboShadowSize, 0f, 10f, 20f, "F1", "px");
            if (cdShadow != settings.ComboShadowSize) { settings.ComboShadowSize = cdShadow; changed = true; }

            GUILayout.Space(8f);

            Indent(() =>
            {
                if (GUILayout.Button((_comboColorOpen ? "▼" : "►") + " Color", GUILayout.ExpandWidth(false)))
                    _comboColorOpen = !_comboColorOpen;
            });

            if (_comboColorOpen)
            {
                SliderRow("Max Combo", out float cgMax, settings.ComboGradientMax, 10f, 5000f, 20f, "F0");
                if (cgMax != settings.ComboGradientMax) { settings.ComboGradientMax = Mathf.Round(cgMax); changed = true; }
                DrawGradientEditor("Combo", settings.ComboGradient, ref changed);
            }
        }
    }
}
