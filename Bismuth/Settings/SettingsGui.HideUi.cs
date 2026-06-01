using UnityEngine;

namespace Bismuth
{
    internal static partial class SettingsGui
    {
        private static void DrawHideUiSection(Settings settings, ref bool changed)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button((_hideUiOpen ? "▼" : "►") + " Hide UI", GUILayout.ExpandWidth(false)))
                _hideUiOpen = !_hideUiOpen;
            bool hideUiOn = GUILayout.Toggle(settings.HideUiEnabled, " Enabled");
            if (hideUiOn != settings.HideUiEnabled) { settings.HideUiEnabled = hideUiOn; changed = true; }
            GUILayout.EndHorizontal();

            if (_hideUiOpen)
                DrawHideUi(settings, ref changed);
        }

        private static void DrawHideUi(Settings settings, ref bool changed)
        {
            bool hideAll = GUILayout.Toggle(settings.HideAllUI, " Hide All");
            if (hideAll != settings.HideAllUI) { settings.HideAllUI = hideAll; changed = true; }

            if (settings.HideAllUI) return;

            bool hideAutoText = GUILayout.Toggle(settings.HideAutoplayText, " Hide Autoplay Text");
            if (hideAutoText != settings.HideAutoplayText) { settings.HideAutoplayText = hideAutoText; changed = true; }

            bool hideAutoIcon = GUILayout.Toggle(settings.HideAutoplayIcon, " Hide Autoplay Icon");
            if (hideAutoIcon != settings.HideAutoplayIcon) { settings.HideAutoplayIcon = hideAutoIcon; changed = true; }

            bool hideNoFail = GUILayout.Toggle(settings.HideNoFail, " Hide No-Fail");
            if (hideNoFail != settings.HideNoFail) { settings.HideNoFail = hideNoFail; changed = true; }

            bool hideDifficulty = GUILayout.Toggle(settings.HideDifficulty, " Hide Difficulty");
            if (hideDifficulty != settings.HideDifficulty) { settings.HideDifficulty = hideDifficulty; changed = true; }

            bool hideJudgements = GUILayout.Toggle(settings.HidePerfectJudgements, " Hide Perfect Judgements");
            if (hideJudgements != settings.HidePerfectJudgements) { settings.HidePerfectJudgements = hideJudgements; changed = true; }

            bool hideLevelName = GUILayout.Toggle(settings.HideLevelName, " Hide Song Title");
            if (hideLevelName != settings.HideLevelName) { settings.HideLevelName = hideLevelName; changed = true; }

            bool hideHitmeter = GUILayout.Toggle(settings.HideHitmeter, " Hide Hitmeter");
            if (hideHitmeter != settings.HideHitmeter) { settings.HideHitmeter = hideHitmeter; changed = true; }

            bool hideBetaBuild = GUILayout.Toggle(settings.HideBetaBuild, " Hide Alpha/Beta Build Text");
            if (hideBetaBuild != settings.HideBetaBuild) { settings.HideBetaBuild = hideBetaBuild; changed = true; }
        }
    }
}
