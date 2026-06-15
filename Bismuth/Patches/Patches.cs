using System;
using HarmonyLib;
using MonsterLove.StateMachine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bismuth
{
    internal static class Patches
    {
        // Reset display text when a level (re)starts.
        [HarmonyPatch(typeof(scrMarginTracker), "Reset")]
        private static class MistakesResetPatch
        {
            public static void Postfix() { BismuthLog.Debug("[hook] scrMarginTracker.Reset"); Overlay.Instance?.OnAttempt(); }
        }

        // Multiple entry points because Start doesn't always fire and LoadCheckpointProgress
        // populates the tracker after Start would have run.
        [HarmonyPatch(typeof(scrController), "Start")]
        private static class ControllerStartPatch
        {
            public static void Postfix() { BismuthLog.Debug("[hook] scrController.Start"); Overlay.Instance?.OnAttempt(); }
        }

        [HarmonyPatch(typeof(scrController), "Awake_Rewind")]
        private static class ControllerAwakeRewindPatch
        {
            public static void Postfix() { BismuthLog.Debug("[hook] scrController.Awake_Rewind"); Overlay.Instance?.OnAttempt(); }
        }

        [HarmonyPatch(typeof(scrMistakesManager), "LoadCheckpointProgress")]
        private static class LoadCheckpointProgressPatch
        {
            public static void Postfix() { BismuthLog.Debug("[hook] scrMistakesManager.LoadCheckpointProgress"); Overlay.Instance?.OnAttempt(); }
        }

        // Update display after every hit.
        [HarmonyPatch(typeof(scrMarginTracker), "AddHit", new[] { typeof(HitMargin) })]
        private static class AddHitPatch
        {
            public static void Postfix(scrMarginTracker __instance, HitMargin hit)
            {
                Overlay.Instance?.UpdateDisplay(__instance.percentAcc, __instance.percentXAcc, hit);
            }
        }

        // Custom level starts playing. isRestart=true on in-game retry, false on first load.
        [HarmonyPatch(typeof(scnGame), "Play")]
        private static class ScnGamePlayPatch
        {
            public static void Postfix(bool isRestart)
            {
                Overlay.Instance?.OnLevelStart(isRestart);
            }
        }

        // Official level "press any key" prompt appears (always a fresh entry from this hook).
        [HarmonyPatch(typeof(scrPressToStart), "ShowText")]
        private static class PressToStartShowTextPatch
        {
            public static void Postfix()
            {
                Overlay.Instance?.OnLevelStart(false);
            }
        }

        // Scene is being replaced — hide until the next level starts.
        [HarmonyPatch(typeof(scrController), "StartLoadingScene")]
        private static class StartLoadingScenePatch
        {
            public static void Postfix()
            {
                Overlay.Instance?.OnLevelEnd();
            }
        }

        // Screen wipes to black (win screen exit, scene transitions).
        [HarmonyPatch(typeof(scrUIController), "WipeToBlack")]
        private static class WipeToBlackPatch
        {
            public static void Postfix()
            {
                Overlay.Instance?.OnLevelEnd();
            }
        }

        // State machine transitions to None (ESC / direct menu exit).
        [HarmonyPatch(typeof(StateBehaviour), "ChangeState", new[] { typeof(Enum) })]
        private static class StateChangePatch
        {
            public static void Postfix(Enum newState)
            {
                try
                {
                    if ((States)newState == States.None)
                        Overlay.Instance?.OnLevelEnd();
                    // Fail/Won spawn fresh text (death "% completed", results screens)
                    // that the level-start sweep can't see.
                    GameFontApplier.RequestSweepSoon();
                    GameUiLayout.RequestApplySoon();
                }
                catch { }
            }
        }

        // The title-screen news sign fills its TMP text only when the async fetch of
        // adofai-news.json lands, after the scene-change sweep. Repaint on arrival.
        [HarmonyPatch(typeof(NewsSign), "ShowNews")]
        private static class NewsSignShowPatch
        {
            public static void Postfix(NewsSign __instance)
            {
                try { GameFontApplier.ApplyTo(__instance.gameObject); }
                catch { }
            }
        }

        // Custom-level select updates the portal info (name/artist/description) when you
        // navigate between levels. The game re-stamps the default font as it does, and
        // CLS navigation fires no sweep, so only the level selected at the last sweep
        // kept the overlay font. Re-sweep after each DisplayLevel.
        [HarmonyPatch(typeof(scnCLS), "DisplayLevel")]
        private static class CLSDisplayLevelPatch
        {
            public static void Postfix()
            {
                try { GameFontApplier.RequestFullSweepSoon(); }
                catch { }
            }
        }

        // The game stamps the per-language localized font via RDString.SetLocalizedFont,
        // notably the language-selector previews, which set each language's OWN font over
        // our swap (the name reverted while cycling languages). Re-apply ours right after.
        [HarmonyPatch(typeof(RDString), "SetLocalizedFont", new[] { typeof(Text) })]
        private static class LocalizedFontTextPatch
        {
            public static void Postfix(Text text)
            {
                try { GameFontApplier.OnLocalizedFontSet(text); } catch { }
            }
        }

        [HarmonyPatch(typeof(RDString), "SetLocalizedFont", new[] { typeof(TMP_Text) })]
        private static class LocalizedFontTmpPatch
        {
            public static void Postfix(TMP_Text text)
            {
                try { GameFontApplier.OnLocalizedFontSet(text); } catch { }
            }
        }

        [HarmonyPatch(typeof(RDString), "SetLocalizedFont", new[] { typeof(TextMesh) })]
        private static class LocalizedFontMeshPatch
        {
            public static void Postfix(TextMesh text)
            {
                try { GameFontApplier.OnLocalizedFontSet(text); } catch { }
            }
        }

        // The pause/settings menu is shown over gameplay with no scene change, so the
        // font sweep never ran for it (it only picked up the font after some later sweep,
        // e.g. entering the CLS). Re-sweep when it opens. Covers both the pause menu and
        // the settings submenu (Show handles submenu switches, ShowSettingsMenu covers
        // opening settings directly).
        [HarmonyPatch(typeof(PauseMenu), "Show")]
        private static class PauseMenuShowPatch
        {
            public static void Postfix(PauseMenu __instance)
            {
                try { GameFontApplier.ApplyTo(__instance.gameObject); GameFontApplier.RequestFullSweepSoon(); }
                catch { }
            }
        }

        [HarmonyPatch(typeof(PauseMenu), "ShowSettingsMenu")]
        private static class PauseMenuSettingsPatch
        {
            public static void Postfix(PauseMenu __instance)
            {
                try { GameFontApplier.ApplyTo(__instance.gameObject); GameFontApplier.RequestFullSweepSoon(); }
                catch { }
            }
        }

        // The game's own layout pass for the hit error meter (anchors from `pos`,
        // localScale from `meterScale`). Runs on scrController.Awake and when the
        // user changes the meter size/shape in the game's settings. Re-apply the
        // Bismuth override on top each time.
        [HarmonyPatch(typeof(scrHitErrorMeter), "UpdateLayout")]
        private static class ErrorMeterLayoutPatch
        {
            public static void Postfix(scrHitErrorMeter __instance)
            {
                try { GameUiLayout.ApplyErrorMeter(__instance); }
                catch { }
            }
        }

        // Re-apply level name transform after the game restores it to its default position.
        [HarmonyPatch(typeof(scrController), "LevelNameTextRestore")]
        private static class LevelNameTextRestorePatch
        {
            public static void Postfix()
            {
                Overlay.Instance?.ApplyLevelNameTransform();
            }
        }

        // Editor stopped playback — reset scene fires when the user presses stop.
        [HarmonyPatch(typeof(scnEditor), "ResetScene")]
        private static class EditorResetScenePatch
        {
            public static void Postfix()
            {
                Overlay.Instance?.OnLevelEnd();
            }
        }

        // Switching back to edit mode — re-apply editor element visibility.
        [HarmonyPatch(typeof(scnEditor), "SwitchToEditMode")]
        private static class SwitchToEditModePatch
        {
            public static void Postfix()
            {
                Overlay.Instance?.ShowOrHideElements();
            }
        }

        // Hide all scrShowIfDebug elements (autoplay text + rabbit icon) by temporarily
        // setting RDC.auto = false so the component hides its own content, then restoring it.
        // While the game-UI editor is open the opposite applies: Update force-disables the
        // private Text component every frame when autoplay is off (Behaviour.enabled, which
        // GameUiEditor's force-show doesn't cover), so re-enable it with the real label.
        [HarmonyPatch(typeof(scrShowIfDebug), "Update")]
        private static class ShowIfDebugUpdatePatch
        {
            private static bool _prevAuto;

            public static void Prefix()
            {
                _prevAuto = RDC.auto;
                if (RDC.auto && (MainClass.Settings.ActiveHideAutoplayText || MainClass.Settings.ActiveHideAllUI)
                    && Overlay.Instance != null && Overlay.Instance.InLevel)
                    RDC.auto = false;
            }

            public static void Postfix(scrShowIfDebug __instance)
            {
                RDC.auto = _prevAuto;
                if (!Bismuth.UI.GameUiEditor.IsActive) return;
                var txt = __instance.GetComponent<UnityEngine.UI.Text>();
                if (txt == null) return;
                txt.enabled = true;
                if (string.IsNullOrEmpty(txt.text))
                    txt.text = RDString.Get("status.autoplay");
            }
        }

        // Move judgement text off-screen in hide-all mode. Suppress Perfect when the setting is enabled.
        [HarmonyPatch(typeof(scrHitTextMesh), "Show")]
        private static class HitTextShowPatch
        {
            public static bool Prefix(scrHitTextMesh __instance, ref Vector3 position)
            {
                // Pooled popups dodge the scene-change sweep, so repaint on spawn.
                GameFontApplier.ApplyTo(__instance.gameObject);
                if (MainClass.Settings.ActiveHideAllUI)
                {
                    position = new Vector3(100000f, 100000f, 100000f);
                    return true;
                }
                if (MainClass.Settings.ActiveHidePerfectJudgements && __instance.hitMargin == HitMargin.Perfect)
                    return false;
                return true;
            }
        }

        // Move the miss indicator off-screen on spawn in hide-all mode.
        [HarmonyPatch(typeof(scrMissIndicator), "Awake")]
        private static class MissIndicatorAwakePatch
        {
            public static void Postfix(scrMissIndicator __instance)
            {
                if (MainClass.Settings.ActiveHideAllUI)
                    __instance.transform.position = new Vector3(100000f, 100000f, 100000f);
            }
        }

        // Hide the hit error meter whenever a new floor is entered or the game pauses.
        private static void HideErrorMeter()
        {
            var controller = scrController.instance;
            if (controller == null) return;
            if (!MainClass.Settings.ActiveHideAllUI && !MainClass.Settings.ActiveHideHitmeter) return;
            var errorMeter = controller.errorMeter;
            if (errorMeter != null && controller.gameworld && errorMeter.gameObject.activeSelf)
                errorMeter.gameObject.SetActive(false);
        }

        [HarmonyPatch(typeof(scrPlanet), "MoveToNextFloor")]
        private static class MoveToNextFloorHideErrorMeterPatch
        {
            public static void Postfix() => HideErrorMeter();
        }

        [HarmonyPatch(typeof(scrController), "paused", MethodType.Setter)]
        private static class PausedSetterHideErrorMeterPatch
        {
            public static void Postfix() => HideErrorMeter();
        }

        // The level editor force-disables the game's HUD canvas every frame outside
        // play mode (LateUpdate: uiController.canvas.enabled = playMode), so the
        // game-UI editor showed handles over nothing there. Re-enable it after the
        // game's write while an edit session is open. scnEditor takes back over
        // on the first frame after Close.
        [HarmonyPatch(typeof(scnEditor), "LateUpdate")]
        private static class EditorLateUpdateShowHudPatch
        {
            public static void Postfix()
            {
                if (!Bismuth.UI.GameUiEditor.IsActive) return;
                var uic = scrUIController.instance;
                if (uic != null && uic.canvas != null && !uic.canvas.enabled)
                    uic.canvas.enabled = true;
            }
        }

        // Hide the Otto debug button in hide-all mode.
        [HarmonyPatch(typeof(OttoButtonController), "Update")]
        private static class OttoButtonUpdatePatch
        {
            public static void Postfix(OttoButtonController __instance)
            {
                if (MainClass.Settings.ActiveHideAllUI && Overlay.Instance != null && Overlay.Instance.InLevel
                    && __instance.button != null)
                    __instance.button.gameObject.SetActive(false);
            }
        }

    }
}
