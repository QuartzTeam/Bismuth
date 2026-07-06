using TMPro;
using UnityEngine;

namespace Bismuth
{
    // Support hooks for the "Tweaks" tab.
    internal static class Tweaks
    {
        /* KeyCode (as int) the editor's autoplay-pause check should read. A transpiler in
           Patches.cs swaps the hardcoded KeyCode.Space (32) in scnEditor.Update for a call
           to this, so the pause key is rebindable. Falls back to Space so behaviour is
           unchanged when settings aren't loaded yet. */
        internal static int AutoPauseKeyCode()
        {
            try
            {
                var s = MainClass.Settings;
                if (s == null) return (int)UnityEngine.KeyCode.Space;
                // Disabled → return None, which Input.GetKeyDown never reports, so the pause
                // never fires (checked live each frame, so the toggle takes effect at once).
                if (!s.AutoplayPauseEnabled) return (int)UnityEngine.KeyCode.None;
                return (int)s.AutoplayPauseKey;
            }
            catch { return (int)UnityEngine.KeyCode.Space; }
        }

        // Live-apply for the CLS preview-volume slider: the fade postfix only rescales
        // future volume writes, so retune any preview that's already playing steady.
        internal static void ApplyClsPreviewVolume()
        {
            try
            {
                var s = MainClass.Settings;
                if (s == null) return;
                var p = Object.FindAnyObjectByType<PreviewSongPlayer>();
                if (p != null && p.playing && p.audioSource != null)
                    p.audioSource.volume = Mathf.Clamp01(s.ClsPreviewVolume);
            }
            catch { }
        }

        // ── Editor: selected tile angle readout ────────────────────────────
        // Small own-canvas text near the top of the editor showing the angle of the last
        // selected tile (angleLength → degrees; 180° = straight). Ticked from
        // Overlay.Update like the other per-frame helpers; hidden outside the editor,
        // during play-testing, or with nothing selected.

        private static GameObject _angleGo;
        private static TextMeshProUGUI _angleText;

        internal static void TickTileAngle()
        {
            var s = MainClass.Settings;
            scnEditor ed = null;
            bool want = false;
            try
            {
                if (s != null && s.EditorTileAngle)
                {
                    ed = scnEditor.instance;
                    want = ed != null && !ed.playMode
                        && ed.selectedFloors != null && ed.selectedFloors.Count > 0;
                }
            }
            catch { want = false; }

            if (!want)
            {
                if (_angleGo != null && _angleGo.activeSelf) _angleGo.SetActive(false);
                return;
            }
            if (_angleGo == null) BuildAngleDisplay();
            if (!_angleGo.activeSelf) _angleGo.SetActive(true);

            var fl = ed.selectedFloors[ed.selectedFloors.Count - 1];
            if (fl == null) return;
            float deg = (float)(fl.angleLength * Mathf.Rad2Deg);
            string txt = ed.selectedFloors.Count > 1
                ? $"Angle: {deg:0.##}°  ({ed.selectedFloors.Count} tiles)"
                : $"Angle: {deg:0.##}°";
            if (_angleText != null && _angleText.text != txt) _angleText.text = txt;
        }

        private static void BuildAngleDisplay()
        {
            _angleGo = new GameObject("BismuthTileAngle", typeof(RectTransform));
            Object.DontDestroyOnLoad(_angleGo);
            var canvas = _angleGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            var scaler = _angleGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(_angleGo.transform, false);
            var rect = (RectTransform)txtGo.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.93f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(700f, 40f);
            _angleText = txtGo.AddComponent<TextMeshProUGUI>();
            _angleText.font = UI.Theme.TmpFont;
            _angleText.fontSize = 26;
            _angleText.color = Color.white;
            _angleText.alignment = TextAlignmentOptions.Center;
            _angleText.textWrappingMode = TextWrappingModes.NoWrap;
            _angleText.overflowMode = TextOverflowModes.Overflow;
            _angleText.raycastTarget = false;
            var sh = txtGo.AddComponent<TmpShadow>();
            sh.OffsetPx = new Vector2(2f, -2f);
            sh.Apply();
        }

        internal static void DisposeTileAngle()
        {
            if (_angleGo != null) Object.Destroy(_angleGo);
            _angleGo = null;
            _angleText = null;
        }
    }
}
