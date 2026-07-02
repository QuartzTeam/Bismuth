using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bismuth.UI
{
    // In-game viewer for BismuthLog.txt on its own canvas above the settings panel.
    // Opened from the Misc page. Shows the log tail, scrolled to the newest lines.
    internal static class LogViewer
    {
        private static GameObject _canvasGo;
        private static TextMeshProUGUI _text;
        private static ScrollRect _scroll;
        private static string[] _lines = System.Array.Empty<string>();

        public static void Show()
        {
            if (_canvasGo != null) { Refresh(); return; }

            _canvasGo = new GameObject("BismuthLogViewer");
            Object.DontDestroyOnLoad(_canvasGo);
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32500;
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _canvasGo.AddComponent<GraphicRaycaster>();

            var panel = UIBuilder.Rect("Panel", _canvasGo.transform);
            var pr = (RectTransform)panel.transform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(1000f, 640f);
            var bg = panel.AddComponent<Image>();
            bg.sprite = Theme.White;
            bg.color = new Color(0.08f, 0.08f, 0.10f, 0.98f);
            bg.raycastTarget = true;

            // Transparent strip behind the title. Drag target for moving the window.
            var dragGo = UIBuilder.Rect("DragBar", panel.transform);
            var dr = (RectTransform)dragGo.transform;
            dr.anchorMin = new Vector2(0f, 1f);
            dr.anchorMax = new Vector2(1f, 1f);
            dr.pivot = new Vector2(0.5f, 1f);
            dr.sizeDelta = new Vector2(0f, 44f);
            var dragImg = dragGo.AddComponent<Image>();
            dragImg.sprite = Theme.White;
            dragImg.color = new Color(0f, 0f, 0f, 0f);
            dragImg.raycastTarget = true;
            dragGo.AddComponent<DragHandle>();

            UpdatePopup.MakeText(panel.transform, "Title", "Bismuth Log",
                17, FontStyle.Bold, Theme.Text, -8f, 30f);

            // Scroll area: viewport (masked) filling the panel between title and buttons.
            var viewportGo = UIBuilder.Rect("Viewport", panel.transform);
            var vr = (RectTransform)viewportGo.transform;
            vr.anchorMin = new Vector2(0f, 0f);
            vr.anchorMax = new Vector2(1f, 1f);
            vr.offsetMin = new Vector2(12f, 62f);
            vr.offsetMax = new Vector2(-12f, -44f);
            var vpImg = viewportGo.AddComponent<Image>();
            vpImg.sprite = Theme.White;
            vpImg.color = new Color(0f, 0f, 0f, 0.35f);
            vpImg.raycastTarget = true; // scroll wheel needs a raycast target
            viewportGo.AddComponent<RectMask2D>();

            var contentGo = UIBuilder.Rect("Content", viewportGo.transform);
            var cr = (RectTransform)contentGo.transform;
            cr.anchorMin = new Vector2(0f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(0.5f, 1f);
            cr.offsetMin = new Vector2(8f, 0f);
            cr.offsetMax = new Vector2(-8f, 0f);

            _text = UIBuilder.Tmp(contentGo, "", 13, TextAnchor.UpperLeft, Theme.Text, wrap: true);
            _text.richText = true;          // line-number <link> + <noparse> content
            _text.raycastTarget = true;     // receive clicks for copy-on-line-number
            var csf = contentGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var copier = contentGo.AddComponent<LogLineCopier>();
            copier.Text = _text;
            copier.LineForLink = i => (i >= 0 && i < _lines.Length) ? _lines[i] : null;

            _scroll = panel.AddComponent<ScrollRect>();
            _scroll.viewport = vr;
            _scroll.content = cr;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 30f;

            UpdatePopup.MakeButton(panel.transform, "Refresh", -210f, 110f, 14f, Refresh);
            UpdatePopup.MakeButton(panel.transform, "Clear", -95f, 100f, 14f,
                () => { BismuthLog.Clear(); Refresh(); });
            UpdatePopup.MakeButton(panel.transform, "Open in File Manager", 65f, 200f, 14f,
                () => OsShell.OpenFolder(MainClass.ModPath));
            UpdatePopup.MakeButton(panel.transform, "Close", 220f, 90f, 14f, Close);

            // Resize handles go last so they sit above everything in sibling order
            // (same ordering requirement as the settings panel).
            ResizeHandle.AttachAll(pr);

            Refresh();
        }

        public static void Refresh()
        {
            if (_text == null) return;
            string raw = BismuthLog.ReadTail();
            // [dbg] lines show only in debug mode (Misc → Debug mode).
            bool dbg = MainClass.Settings != null && MainClass.Settings.DebugMode;
            var lines = new System.Collections.Generic.List<string>();
            foreach (var line in raw.Split('\n'))
                if (dbg || !line.Contains("[dbg]")) lines.Add(line);
            // Drop the trailing empty line from the final newline.
            if (lines.Count > 0 && lines[lines.Count - 1].Length == 0) lines.RemoveAt(lines.Count - 1);
            _lines = lines.ToArray();

            // Each line: a clickable <link> line number (copies the line) + the raw text in
            // <noparse> so the log's own <color>/<size> tags show literally instead of rendering.
            var sb = new System.Text.StringBuilder(raw.Length + _lines.Length * 28);
            for (int i = 0; i < _lines.Length; i++)
                sb.Append("<link=\"").Append(i).Append("\"><color=#6E7681>")
                  .Append((i + 1).ToString().PadLeft(4)).Append("</color></link>  <noparse>")
                  .Append(_lines[i]).Append("</noparse>\n");
            _text.text = sb.ToString();
            Canvas.ForceUpdateCanvases();
            if (_scroll != null) _scroll.verticalNormalizedPosition = 0f; // newest at bottom
        }

        public static void Close()
        {
            if (_canvasGo != null) Object.Destroy(_canvasGo);
            _canvasGo = null;
            _text = null;
            _scroll = null;
        }
    }

    // Click a line-number <link> in the log text → copy that line to the system clipboard.
    internal class LogLineCopier : MonoBehaviour, IPointerClickHandler
    {
        internal TMP_Text Text;
        internal System.Func<int, string> LineForLink;

        public void OnPointerClick(PointerEventData e)
        {
            if (Text == null || LineForLink == null) return;
            int li = TMP_TextUtilities.FindIntersectingLink(Text, e.position, e.pressEventCamera);
            if (li < 0) return;
            if (int.TryParse(Text.textInfo.linkInfo[li].GetLinkID(), out int idx))
            {
                string line = LineForLink(idx);
                if (line != null) GUIUtility.systemCopyBuffer = line;
            }
        }
    }
}
