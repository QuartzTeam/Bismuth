using System;
using HarmonyLib;
using MonsterLove.StateMachine;
using UnityEngine;

namespace Bismuth
{
    internal static class Patches
    {
        // Reset display text when a level (re)starts.
        [HarmonyPatch(typeof(scrMarginTracker), "Reset")]
        private static class MistakesResetPatch
        {
            public static void Postfix() { BismuthLog.Log("[hook] scrMarginTracker.Reset"); Overlay.Instance?.OnAttempt(); }
        }

        // Multiple entry points because Start doesn't always fire and LoadCheckpointProgress
        // populates the tracker after Start would have run.
        [HarmonyPatch(typeof(scrController), "Start")]
        private static class ControllerStartPatch
        {
            public static void Postfix() { BismuthLog.Log("[hook] scrController.Start"); Overlay.Instance?.OnAttempt(); }
        }

        [HarmonyPatch(typeof(scrController), "Awake_Rewind")]
        private static class ControllerAwakeRewindPatch
        {
            public static void Postfix() { BismuthLog.Log("[hook] scrController.Awake_Rewind"); Overlay.Instance?.OnAttempt(); }
        }

        [HarmonyPatch(typeof(scrMistakesManager), "LoadCheckpointProgress")]
        private static class LoadCheckpointProgressPatch
        {
            public static void Postfix() { BismuthLog.Log("[hook] scrMistakesManager.LoadCheckpointProgress"); Overlay.Instance?.OnAttempt(); }
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
                }
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

            public static void Postfix()
            {
                RDC.auto = _prevAuto;
            }
        }

        // Move judgement text off-screen in hide-all mode; suppress Perfect when the setting is enabled.
        [HarmonyPatch(typeof(scrHitTextMesh), "Show")]
        private static class HitTextShowPatch
        {
            public static bool Prefix(scrHitTextMesh __instance, ref Vector3 position)
            {
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
