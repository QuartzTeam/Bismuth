using UnityEngine;

namespace Bismuth
{
    internal static partial class SettingsGui
    {
        private static void DrawOverlaySection(Settings settings, ref bool changed)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button((_overlayOpen ? "▼" : "►") + " Overlay", GUILayout.ExpandWidth(false)))
                _overlayOpen = !_overlayOpen;
            bool showOvr = GUILayout.Toggle(settings.ShowOverlay, " Enabled");
            if (showOvr != settings.ShowOverlay) { settings.ShowOverlay = showOvr; changed = true; }
            GUILayout.EndHorizontal();

            if (!_overlayOpen) return;

            GUILayout.BeginHorizontal();
            GUILayout.Space(20f);
            GUILayout.BeginVertical();

            DrawScale(settings, ref changed);
            GUILayout.Space(4f);

            DrawProgress(settings, ref changed);
            DrawAccuracy(settings, ref changed);
            DrawBpm(settings, ref changed);

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private static void DrawScale(Settings settings, ref bool changed)
        {
            if (SettingsInput.Slider("Scale", ref settings.Scale, 0.5f, 3.0f, 0f, "F2", "x"))
                changed = true;

            if (SettingsInput.Slider("Decimal places", ref settings.Precision, 0, 4))
                changed = true;
        }

        private static void DrawProgress(Settings settings, ref bool changed)
        {
            GUILayout.BeginHorizontal();
            bool v = GUILayout.Toggle(settings.ShowProgress, " Progress", W(140));
            if (v != settings.ShowProgress) { settings.ShowProgress = v; changed = true; }
            if (DrawPositionButtons(settings.ProgressPosition, out var pos)) { settings.ProgressPosition = pos; changed = true; }
            GUILayout.Space(8f);
            if (GUILayout.Button((_progressColorOpen ? "▼" : "►") + " Settings", GUILayout.ExpandWidth(false)))
                _progressColorOpen = !_progressColorOpen;
            GUILayout.EndHorizontal();

            if (_progressColorOpen)
                DrawGradientEditor("Progress", settings.ProgressGradient, ref changed);
        }

        private static void DrawAccuracy(Settings settings, ref bool changed)
        {
            GUILayout.BeginHorizontal();
            bool showAcc = GUILayout.Toggle(settings.ShowAcc, " Accuracy", W(140));
            if (showAcc != settings.ShowAcc) { settings.ShowAcc = showAcc; changed = true; }
            if (DrawPositionButtons(settings.AccPosition, out var accPos)) { settings.AccPosition = accPos; changed = true; }
            GUILayout.Space(8f);
            if (GUILayout.Button((_accColorOpen ? "▼" : "►") + " Settings", GUILayout.ExpandWidth(false)))
                _accColorOpen = !_accColorOpen;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            bool showXAcc = GUILayout.Toggle(settings.ShowXAcc, " X-Accuracy", W(140));
            if (showXAcc != settings.ShowXAcc) { settings.ShowXAcc = showXAcc; changed = true; }
            if (DrawPositionButtons(settings.XAccPosition, out var xaccPos)) { settings.XAccPosition = xaccPos; changed = true; }
            GUILayout.EndHorizontal();

            if (_accColorOpen)
                DrawGradientEditor("Accuracy", settings.AccGradient, ref changed);
        }

        private static void DrawAttempts(Settings settings, ref bool changed)
        {
            GUILayout.BeginHorizontal();
            bool v = GUILayout.Toggle(settings.ShowAttempts, " Attempts", W(140));
            if (v != settings.ShowAttempts) { settings.ShowAttempts = v; changed = true; }
            GUILayout.Space(8f);
            if (GUILayout.Button((_attemptsOpen ? "▼" : "►") + " Settings", GUILayout.ExpandWidth(false)))
                _attemptsOpen = !_attemptsOpen;
            GUILayout.EndHorizontal();

            if (!_attemptsOpen) return;

            SliderRow("X", out float ax, settings.AttemptsX, 0f, 1f, 40f, "F2");
            if (ax != settings.AttemptsX) { settings.AttemptsX = ax; changed = true; }
            SliderRow("Y", out float ay, settings.AttemptsY, 0f, 1f, 40f, "F2");
            if (ay != settings.AttemptsY) { settings.AttemptsY = ay; changed = true; }

            Indent(() =>
            {
                if (GUILayout.Button("Reset current level", GUILayout.ExpandWidth(false)))
                    Overlay.Instance?.ResetAttempts();
                GUILayout.Space(8f);
                if (GUILayout.Button("Reset all levels", GUILayout.ExpandWidth(false)))
                    AttemptsStore.ClearAll();
            }, 40f);
        }

        private static void DrawBpm(Settings settings, ref bool changed)
        {
            GUILayout.BeginHorizontal();
            bool showBpm = GUILayout.Toggle(settings.ShowBpm, " BPM", W(140));
            if (showBpm != settings.ShowBpm) { settings.ShowBpm = showBpm; changed = true; }
            if (DrawPositionButtons(settings.BpmPosition, out var bpmPos)) { settings.BpmPosition = bpmPos; changed = true; }
            GUILayout.Space(8f);
            if (GUILayout.Button((_bpmColorOpen ? "▼" : "►") + " Settings", GUILayout.ExpandWidth(false)))
                _bpmColorOpen = !_bpmColorOpen;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            bool showTileBpm = GUILayout.Toggle(settings.ShowTileBpm, " Tile BPM", W(140));
            if (showTileBpm != settings.ShowTileBpm) { settings.ShowTileBpm = showTileBpm; changed = true; }
            if (DrawPositionButtons(settings.TileBpmPosition, out var tileBpmPos)) { settings.TileBpmPosition = tileBpmPos; changed = true; }
            GUILayout.EndHorizontal();

            if (_bpmColorOpen)
                DrawGradientEditor("BPM", settings.BpmGradient, ref changed);

            GUILayout.Space(4f);

            DrawAttempts(settings, ref changed);

            GUILayout.Space(4f);

            GUILayout.BeginHorizontal();
            bool showTimingScale = GUILayout.Toggle(settings.ShowTimingScale, " Timing Scale", W(140));
            if (showTimingScale != settings.ShowTimingScale) { settings.ShowTimingScale = showTimingScale; changed = true; }
            GUILayout.Space(8f);
            if (GUILayout.Button((_timingScaleOpen ? "▼" : "►") + " Settings", GUILayout.ExpandWidth(false)))
                _timingScaleOpen = !_timingScaleOpen;
            GUILayout.EndHorizontal();

            if (_timingScaleOpen)
            {
                SliderRow("Y Offset", out float tsY, settings.TimingScaleY, -300f, 300f, 40f, "F0", "px");
                if (tsY != settings.TimingScaleY) { settings.TimingScaleY = tsY; changed = true; }

                SliderRow("Size", out float tsSize, settings.TimingScaleSize, 0.25f, 2.0f, 40f, "F2", "x");
                if (tsSize != settings.TimingScaleSize) { settings.TimingScaleSize = tsSize; changed = true; }
            }

            GUILayout.BeginHorizontal();
            bool showJudgements = GUILayout.Toggle(settings.ShowJudgements, " Judgements", W(140));
            if (showJudgements != settings.ShowJudgements) { settings.ShowJudgements = showJudgements; changed = true; }
            GUILayout.Space(8f);
            if (GUILayout.Button((_judgementsOpen ? "▼" : "►") + " Settings", GUILayout.ExpandWidth(false)))
                _judgementsOpen = !_judgementsOpen;
            GUILayout.EndHorizontal();

            if (_judgementsOpen)
            {
                SliderRow("Y Offset", out float jsY, settings.JudgementsY, 0f, 400f, 40f, "F0", "px");
                if (jsY != settings.JudgementsY) { settings.JudgementsY = jsY; changed = true; }

                SliderRow("Size", out float jsSize, settings.JudgementsSize, 0.25f, 2.0f, 40f, "F2", "x");
                if (jsSize != settings.JudgementsSize) { settings.JudgementsSize = jsSize; changed = true; }
            }

            GUILayout.BeginHorizontal();
            bool showFps = GUILayout.Toggle(settings.ShowFps, " FPS", W(140));
            if (showFps != settings.ShowFps) { settings.ShowFps = showFps; changed = true; }
            GUILayout.EndHorizontal();
        }
    }
}
