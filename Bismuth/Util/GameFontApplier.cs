using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bismuth
{
    /* Repaints the game's own text with the overlay font. A generalized version of
       the song-title swap. Covers all three text systems the game mixes: legacy
       uGUI Text, TextMeshPro, and 3D TextMesh (judgement popups). Opt-in via
       Settings.GameTextUseOverlayFont. Sweeps on scene change and level start, plus
       a per-spawn hook for pooled judgement popups. Originals are cached so toggling
       off restores live objects. Scene loads reset naturally, since fresh objects
       come from prefabs with the original fonts.

       Fonts fill the em square differently. Pretendard renders visually larger than
       the game fonts at the same fontSize, so every swap also scales the text size
       by the line-height/em ratio of the original vs ours (fallback 0.85). */
    internal static class GameFontApplier
    {
        private const float DefaultScale = 0.85f;
        /* World-space TextMesh (judgement popups, results screens) reads visually
           bigger than canvas text at the same metric ratio, so shrink it further. */
        private const float MeshExtraScale = 0.8f;

        private static Font _font;
        private static TMP_FontAsset _tmpFont;
        /* Family bold for title text (scrHUDText.isTitle: world number/name on level
           select, etc). Falls back to the regular weight when absent. */
        private static Font _boldFont;
        private static TMP_FontAsset _boldTmpFont;

        private struct TextState
        {
            public Font Font; public int Size; public float LineSpacing; public FontStyle Style;
            public bool BestFit; public int BestFitMax;
        }
        private struct TmpState { public TMP_FontAsset Font; public float Size; public float LineSpacing; public FontStyles Style; public bool AutoSize; public float SizeMin, SizeMax; }
        private struct MeshState { public Font Font; public Material Mat; public int Size; public float CharSize; public float LineSpacing; public FontStyle Style; }

        /* Original (pre-rewrite) text for guest-track credit labels whose size is
           driven through a <size=…> rich-text tag. Kept so Restore can put the
           string back. */
        private static readonly Dictionary<Text, string> _guestCreditOrigText = new Dictionary<Text, string>();
        private static readonly System.Text.RegularExpressions.Regex SizeTagRe =
            new System.Text.RegularExpressions.Regex(@"</?size(=[^>]*)?>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        private static readonly Dictionary<Text, TextState> _origText = new Dictionary<Text, TextState>();
        private static readonly Dictionary<TMP_Text, TmpState> _origTmp = new Dictionary<TMP_Text, TmpState>();
        private static readonly Dictionary<TextMesh, MeshState> _origMesh = new Dictionary<TextMesh, MeshState>();

        private static bool Enabled =>
            MainClass.Settings != null && MainClass.Settings.GameTextUseOverlayFont;

        /* Hand-tuned bases for Pretendard over the game fonts (June 2026). Metric
           normalization alone leaves text ~1.4× too large and leading ~1.5× too
           tight. Sliders apply ON TOP of these, centered at 1.0. */
        private const float BaseGameTextScale = 0.6f;
        private const float BaseGameLineSpacing = 1.5f;
        private const float BaseStatsScale = 0.8f;

        // User-tunable multiplier on top of metric normalization (Game UI tab)
        private static float UserScale =>
            (MainClass.Settings != null ? Mathf.Clamp(MainClass.Settings.GameTextScale, 0.4f, 1.5f) : 1f)
            * BaseGameTextScale;

        /* Line-advance multiplier (Game UI tab). Pretendard glyphs fill far more of
           the em box than the game fonts, so swapped multi-line text reads cramped
           at a metrically "equal" size. Widen the leading to compensate. */
        private static float UserLineSpacing =>
            (MainClass.Settings != null ? Mathf.Clamp(MainClass.Settings.GameTextLineSpacing, 0.8f, 2f) : 1f)
            * BaseGameLineSpacing;

        // Separate multiplier for level-select per-level stats panels
        private static float UserStatsScale =>
            (MainClass.Settings != null ? Mathf.Clamp(MainClass.Settings.GameStatsScale, 0.4f, 1.5f) : 1f)
            * BaseStatsScale;

        // Per-level stats (attempts, max x-acc, …) sit under "StatsText Container"
        private static bool IsStatsText(Component c)
        {
            var p = c.transform;
            for (int i = 0; i < 5 && p != null; i++, p = p.parent)
                if (p.name.Contains("StatsText")) return true;
            return false;
        }

        /* Stats size applied to CONTAINER localScale, not font size: these labels
           best-fit their rects, so font-size changes only register once they
           undercut the fitted size (the slider felt stepped/dead). Originals are
           cached per container for restore, and it stays idempotent (always
           orig × multiplier). */
        private struct XformState { public Vector3 Scale; public Vector3 Pos; }
        private static readonly Dictionary<Transform, XformState> _statsOrigScale =
            new Dictionary<Transform, XformState>();

        private static void ApplyStatsScale(Component c)
        {
            Transform container = null;
            var p = c.transform;
            for (int i = 0; i < 5 && p != null; i++, p = p.parent)
                if (p.name.Contains("StatsText")) container = p; // topmost match wins
            ScaleTransform(container, UserStatsScale);
        }

        private static void ScaleTransform(Transform tr, float m, bool keepCenter = true)
        {
            if (tr == null) return;
            XformState st;
            if (!_statsOrigScale.TryGetValue(tr, out st))
            {
                st = new XformState { Scale = tr.localScale, Pos = tr.localPosition };
                _statsOrigScale[tr] = st;
            }
            var ns = new Vector3(st.Scale.x * m, st.Scale.y * m, st.Scale.z);
            tr.localScale = ns;
            /* Scaling happens about the pivot, which sits off the visual center on
               stats containers (the block drifted downward as it shrank). Shift
               localPosition so the rect center stays put. rect is in pivot-relative
               local units, unaffected by localScale. keepCenter=false scales about
               the pivot instead: right-anchored labels (Continue/LastLevel) must keep
               the pivot edge flush to the margin, and center-preserving shoved them
               off it. */
            var rt = tr as RectTransform;
            if (keepCenter && rt != null)
            {
                Vector2 c = rt.rect.center;
                tr.localPosition = st.Pos + new Vector3(
                    c.x * (st.Scale.x - ns.x),
                    c.y * (st.Scale.y - ns.y), 0f);
            }
            else tr.localPosition = st.Pos;
        }

        // Called from MainClass.ApplySelectedFont whenever overlay font resolves
        internal static void SetFonts(Font font, TMP_FontAsset tmpFont, Font boldFont, TMP_FontAsset boldTmpFont)
        {
            _font = font;
            _tmpFont = tmpFont;
            _boldFont = boldFont != null ? boldFont : font;
            _boldTmpFont = boldTmpFont != null ? boldTmpFont : tmpFont;
            _lastSweepFrame = -1; // font identity changed, never dedupe this sweep
            Reapply();
            RequestFullSweepSoon(); // catch Start()-time localized-font re-stamps
        }

        /* Game titles (world number/name on level select…) are visually bold in the
           stock fonts, so match them with the family's heaviest weight.
           scrHUDText.isTitle alone misses some. The strongest signal is the ORIGINAL
           font being a bold variant (the game ships dedicated bold assets for
           titles). */
        private static bool IsTitle(Component c)
        {
            try
            {
                var hud = c.GetComponent<scrHUDText>();
                if (hud != null && hud.isTitle) return true;
                /* The credits block ("by 7th Beat Games") is display text, title
                   weight regardless of which scene rule catches it. */
                return c.GetComponentInParent<scrCreditsText>() != null;
            }
            catch { return false; }
        }

        private static bool NameLooksBold(string fontName)
        {
            if (string.IsNullOrEmpty(fontName)) return false;
            var n = fontName.ToLowerInvariant();
            return n.Contains("bold") || n.Contains("black") || n.Contains("heavy") ||
                   n.EndsWith("-bd") || n.Contains("_bd") || n.Contains(" bd");
        }

        private static int _sweepBoldCount;

        /* On the level-select / title screen? World content (portal floors,
           world-name text, keycaps, credits) is parented under DontDestroyOnLoad, so
           a component's OWN scene is not "scnLevelSelect". Gate on the ACTIVE scene
           instead, which makes them title-styled only while the menu is showing (they
           persist into gameplay scenes too). */
        private static bool InLevelSelect()
        {
            try { return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "scnLevelSelect"; }
            catch { return false; }
        }

        /* The level select / title screen is set entirely in bold-looking display
           text in vanilla, but none of the per-component signals (scrHUDText.isTitle,
           fontStyle, original font name) reliably fire there. So while the menu is
           showing, this verdict is AUTHORITATIVE (the caller ignores
           NameLooksBold/style, which mis-bold single-letter keycaps). Bold everything
           except: the news sign and tip cycler (body copy), stats panels, and single
           glyphs (keycaps). The credits block is handled as a title via IsTitle. */
        private static bool LevelSelectBold(Component c, string content)
        {
            try
            {
                if (c.GetComponentInParent<NewsSign>() != null) return false;
                /* The press-to-start hint cluster ("Hit Space" group) holds cycling
                   tips (number-key navigation, completion taglines) that stay body
                   copy. NOTE: portal labels and the "by 7th Beat Games" subtitle are
                   ALSO scrTextChanger components but sit outside this group, so they
                   must NOT be excluded by component type, only by this cluster. */
                if (InHintCluster(c)) return false;
                if (content == null || content.Trim().Length <= 1) return false;
                return !IsStatsText(c);
            }
            catch { return false; }
        }

        private static bool InHintCluster(Component c)
        {
            var p = c.transform;
            for (int i = 0; i < 6 && p != null; i++, p = p.parent)
                if (p.name == "Hit Space") return true;
            return false;
        }

        private static bool InCls()
        {
            try { return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "scnCLS"; }
            catch { return false; }
        }

        /* Custom-level-select chrome to bold. Unlike the title screen, DON'T bold the
           whole scene (it's full of body copy: level descriptions, help text). Only
           the screen title, portal/world-name labels, and the "불러오는 중…" loading
           text. */
        private static bool ClsBold(Component c)
        {
            // Selected level title in detail view
            try { var cls = scnCLS.instance; if (cls != null && ReferenceEquals(c, cls.portalName)) return true; }
            catch { }
            /* Difficulty name ("엄격"/"느슨"…) shown when opening a level from CLS. Its
               sibling description (txtDescription) stays body copy. */
            if (c.gameObject.name == "txtValue" && HasAncestor(c, "Difficulty Container")) return true;
            for (var p = c.transform; p != null; p = p.parent)
            {
                var n = p.name;
                if (n == "WorldNameCanvas" || n == "title" || n == "Loading") return true;
            }
            return false;
        }

        // Transform downscale + line spacing for CLS portal labels
        private const float ClsLabelScale = 0.8f;
        private const float ClsLabelLineSpacing = 1.1f;

        private static bool HasAncestor(Component c, string name)
        {
            for (var p = c.transform; p != null; p = p.parent)
                if (p.name == name) return true;
            return false;
        }

        /* Single bold decision for all three text systems. Authoritative per scene:
           the title screen bolds nearly everything (LevelSelectBold), CLS bolds only
           its chrome (ClsBold), and elsewhere falls back to the per-component
           heuristic. */
        private static bool ShouldBold(Component c, string text, bool styleBold, string origFontName)
        {
            if (ForceRegular(c)) return false;
            /* The settings submenu (child of PauseMenu) keeps its designed weight
               EVERYWHERE. Checked first so no scene rule (e.g. LevelSelectBold's
               whole-scene bold when settings is opened from the main menu) overrides
               it. */
            if (!HasAncestor(c, "SettingsMenu"))
            {
                if (IsTitle(c)) return true;
                /* Pause menu: bold all of its (non-settings) text. includeInactive
                   because the menu may be inactive during a full sweep. */
                try { if (c.GetComponentInParent<PauseMenu>(true) != null) return true; }
                catch { }
                if (InLevelSelect()) return LevelSelectBold(c, text);
                if (InCls()) return ClsBold(c);
            }
            return styleBold || NameLooksBold(origFontName);
        }

        /* Per-element weight table (Game UI tab, Element weights): weight name to the
           font entry of the game-font family. Resolved in MainClass.ApplySelectedFont. */
        private static Dictionary<string, FontLoader.FontEntry> _elementWeights;

        internal static void SetElementWeights(Dictionary<string, FontLoader.FontEntry> table)
            => _elementWeights = table;

        // Explicit weight chosen for HUD element this component belongs to, or null
        private static FontLoader.FontEntry ElementWeightEntry(Component c)
        {
            var s = MainClass.Settings;
            if (s == null || s.GameUiTextWeights == null || s.GameUiTextWeights.Count == 0) return null;
            if (_elementWeights == null || _elementWeights.Count == 0) return null;
            string key = GameUiLayout.OwnerKey(c);
            /* Judgement popups (Perfect/EPerfect…) are pooled world-space TextMeshes
               under scrHitTextMesh, not GameUiLayout targets, so they get a synthetic
               key. The parent lookup is gated to the TextMesh path to keep full sweeps
               cheap. The popups are world-space TMP (TMPro.TextMeshPro, a TMP_Text),
               not legacy TextMesh. includeInactive is REQUIRED: popups are pooled and
               inactive both during full sweeps and at the Show-prefix moment (the game
               activates them after), and the no-arg GetComponentInParent skips
               inactive GameObjects, so the weight would silently never resolve. */
            if (key == null && IsJudgement(c))
                key = "judgement";
            if (key == null) return null;
            string w = s.GameUiWeightFor(key);
            if (string.IsNullOrEmpty(w)) return null;
            FontLoader.FontEntry e;
            return _elementWeights.TryGetValue(w, out e) ? e : null;
        }

        /* Hit-judgement popups (pooled scrHitTextMesh TMP). includeInactive because
           pooled popups are inactive at sweep / Show-prefix time. */
        private static bool IsJudgement(Component c)
        {
            try { return c is TMP_Text && c.GetComponentInParent<scrHitTextMesh>(true) != null; }
            catch { return false; }
        }

        // Dedicated size multiplier for judgement popups (Game UI tab)
        private static float JudgementScale =>
            MainClass.Settings != null ? Mathf.Clamp(MainClass.Settings.GameJudgementScale, 0.3f, 4f) : 1f;

        /* "Is this a font Bismuth assigned?" Needed to recognize the game re-stamping
           a (localized) font onto an already-swapped component. Covers regular, bold,
           and every per-element weight font. */
        private static bool IsOurLegacyFont(Font f)
        {
            if (f == null) return false;
            if (f == _font || f == _boldFont) return true;
            if (_elementWeights != null)
                foreach (var kv in _elementWeights)
                    if (kv.Value != null && kv.Value.Font == f) return true;
            return false;
        }

        private static bool IsOurTmpFont(TMP_FontAsset f)
        {
            if (f == null) return false;
            if (f == _tmpFont || f == _boldTmpFont) return true;
            if (_elementWeights != null)
                foreach (var kv in _elementWeights)
                    if (kv.Value != null && kv.Value.TmpFont == f) return true;
            return false;
        }

        /* Keycap letters (scrLetterPress) sit on small key sprites everywhere:
           world-select shortcut keys AND in-level multi-key indicators, and the
           family's Black weight overwhelms the sprite. Overrides every bold signal. */
        private static bool ForceRegular(Component c)
        {
            try
            {
                if (c.GetComponentInParent<scrLetterPress>() != null) return true;
                /* Speed-trial best-multiplier badge ("1.5배"): a small accent label
                   that shouldn't take title weight. */
                if (c.GetComponentInParent<scrBestMultiplierText>() != null) return true;
                return false;
            }
            catch { return false; }
        }

        /* Called on scene change / level start / toggle flip. Full sweeps are
           frame-deduped, since scene change and level start can land on the same
           frame and the FindObjectsByType scan is the expensive part. */
        private static int _lastSweepFrame = -1;

        internal static void Reapply()
        {
            if (Enabled)
            {
                if (Time.frameCount == _lastSweepFrame) return;
                _lastSweepFrame = Time.frameCount;
                Apply();
            }
            else Restore();
        }

        /* Scoped sweep: only the game HUD canvas. Everything that (re)spawns or gets
           its font re-stamped mid-scene (death %, results, congrats, rewind
           re-localization) sits under scrUIController.canvas, and a full
           FindObjectsByType scene scan here was a visible hitch at start/death/retry
           on large custom maps. Full sweeps stay reserved for scene loads
           (custom-level text decorations sit outside the canvas). */
        internal static void ReapplyHud()
        {
            if (!Enabled || _font == null) return;
            try
            {
                var uic = scrUIController.instance;
                if (uic != null && uic.canvas != null) ApplyTo(uic.canvas.gameObject);
            }
            catch { }
        }

        /* Death/results text spawns on controller state changes, after the level-start
           sweep. The state-change patch requests delayed sweeps, and Overlay.Update
           ticks them (one shortly after for instant texts, one later for animated
           screens). */
        private static int _sweepFrameA = -1;
        private static int _sweepFrameB = -1;

        internal static void RequestSweepSoon()
        {
            if (!Enabled) return;
            _sweepFrameA = Time.frameCount + 2;
            _sweepFrameB = Time.frameCount + 30;
        }

        /* Scene-entry texts get localized fonts assigned in their object Start(), one
           frame AFTER sceneLoaded, so an immediate sweep runs too early and gets
           stomped (cold launch showed the vanilla title screen until the toggle was
           cycled). Delayed FULL sweeps after scene entry / font resolution catch the
           re-stamp. Mid-scene state-change ticks stay HUD-scoped for perf. */
        private static int _fullSweepFrameA = -1;
        private static int _fullSweepFrameB = -1;

        internal static void RequestFullSweepSoon()
        {
            if (!Enabled) return;
            _fullSweepFrameA = Time.frameCount + 2;
            _fullSweepFrameB = Time.frameCount + 30;
        }

        internal static void Tick()
        {
            /* State-change sweeps are HUD-scoped in gameplay: the texts they exist to
               catch spawn under the HUD canvas, and full sweeps caused death-screen
               lag. Level select is different: world text activates late on approach
               (portal labels, world names), sits outside any canvas, and the scene is
               small, so sweep it fully. */
            if (_sweepFrameA > 0 && Time.frameCount >= _sweepFrameA) { _sweepFrameA = -1; StateSweep(); }
            if (_sweepFrameB > 0 && Time.frameCount >= _sweepFrameB) { _sweepFrameB = -1; StateSweep(); }
            if (_fullSweepFrameA > 0 && Time.frameCount >= _fullSweepFrameA) { _fullSweepFrameA = -1; Reapply(); }
            if (_fullSweepFrameB > 0 && Time.frameCount >= _fullSweepFrameB) { _fullSweepFrameB = -1; Reapply(); }
            /* Size-multiplier changes need a full restore+apply (Apply skips text
               already on the Bismuth font). Debounce so slider drags don't sweep the
               scene every tick. */
            if (_resizeFrame > 0 && Time.frameCount >= _resizeFrame)
            {
                _resizeFrame = -1;
                if (Enabled) { Restore(); Apply(); _lastSweepFrame = Time.frameCount; }
            }
        }

        private static void StateSweep()
        {
            bool levelSelect = false;
            try
            {
                levelSelect = UnityEngine.SceneManagement.SceneManager
                    .GetActiveScene().name == "scnLevelSelect";
            }
            catch { }
            if (levelSelect) Reapply();
            else ReapplyHud();
        }

        private static int _resizeFrame = -1;

        // Called when a size slider moves. Coalesces into one re-sweep shortly after.
        internal static void RequestResize()
        {
            if (!Enabled) return;
            _resizeFrame = Time.frameCount + 15;
        }

        private static int _lastBoldLogged = -1;

        // ── Sweep diagnostics (opt-in) ───────────────────────────────────────
        /* Flip DiagEnabled to true to dump, once per sweep, the bold/font decision for
           every text whose content matches DiagFilter. Invaluable for tracing which
           component a stray label belongs to and why it did/didn't bold (it pinned
           level-select portal labels and "by 7th Beat Games" to their scrTextChanger
           components, June 2026). Off by default, no log spam. */
        internal static bool DiagEnabled = false;
        // Substrings to match. null/empty matches every non-empty text under DiagMaxLen.
        internal static string[] DiagFilter = null;
        private static int _diagBudget;

        // Restrict dump to one scene (active scene name) when set. null = any.
        internal static string DiagScene = null;
        internal static int DiagMaxLen = 40;

        private static bool DiagMatch(Component c, string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length >= DiagMaxLen) return false;
            // Skip Bismuth's own panel UI (DontDestroyOnLoad noise)
            var root = c.transform.root;
            if (root != null && root.name.StartsWith("Bismuth")) return false;
            if (DiagScene != null)
            {
                try { if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != DiagScene) return false; }
                catch { }
            }
            if (DiagFilter == null || DiagFilter.Length == 0) return true;
            foreach (var f in DiagFilter)
                if (!string.IsNullOrEmpty(f) && text.Contains(f)) return true;
            return false;
        }

        private static float DiagLineSpacing(Component c)
        {
            if (c is Text t) return t.lineSpacing;
            if (c is TMP_Text m) return m.lineSpacing;
            return 0f;
        }

        // Post-apply dump for one component. font/style are the applied result.
        private static void Diag(Component c, string text, string type, string font, object style)
        {
            if (!DiagEnabled || _diagBudget <= 0 || !DiagMatch(c, text)) return;
            _diagBudget--;
            string ns = "-", tc = "-", desktop = "-";
            try { var n = c.GetComponentInParent<NewsSign>(); if (n != null) ns = n.name; } catch { }
            try
            {
                var x = c.GetComponentInParent<scrTextChanger>();
                if (x != null)
                {
                    tc = x.name;
                    var dt = typeof(scrTextChanger).GetField("desktopText");
                    if (dt != null) desktop = (dt.GetValue(x) as string) ?? "null";
                }
            }
            catch { }
            string extra = "";
            try
            {
                var rt = c.transform as RectTransform;
                if (rt != null) extra = " pos=" + rt.anchoredPosition + " size=" + rt.rect.size + " scale=" + rt.localScale.x;
                if (c is Text tt) extra += " hOver=" + tt.horizontalOverflow + " vOver=" + tt.verticalOverflow + " fs=" + tt.fontSize + " bestFit=" + tt.resizeTextForBestFit + " raw=[" + tt.text.Trim() + "]";
                if (c is TMP_Text mm) extra += " over=" + mm.overflowMode + " wrap=" + mm.enableWordWrapping + " fs=" + mm.fontSize + " autoSize=" + mm.enableAutoSizing;
            }
            catch { }
            BismuthLog.Debug("GameFontDiag '" + text.Trim() + "' " + type + " path=" + DiagPath(c.transform) +
                " textChanger=" + tc + " title=" + IsTitle(c) + " lsBold=" + LevelSelectBold(c, text) +
                " lineSpacing=" + DiagLineSpacing(c) + extra + " -> font=" + font + " style=" + style);
        }

        private static string DiagPath(Transform t)
        {
            var sb = new System.Text.StringBuilder();
            for (var p = t; p != null; p = p.parent)
            {
                if (sb.Length > 0) sb.Insert(0, '/');
                sb.Insert(0, p.name);
            }
            return sb.ToString();
        }

        private static void Apply()
        {
            if (_font == null) return;
            Prune();
            _sweepBoldCount = 0;
            if (DiagEnabled) _diagBudget = 16; // per-sweep cap so it can't flood log
            foreach (var t in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                ApplyText(t);
                if (DiagEnabled && t != null) Diag(t, t.text, "Text", t.font != null ? t.font.name : "null", t.fontStyle);
            }
            foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                ApplyTmp(t);
                if (DiagEnabled && t != null) Diag(t, t.text, t.GetType().Name, t.font != null ? t.font.name : "null", t.fontStyle);
            }
            foreach (var t in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                ApplyTextMesh(t);
                if (DiagEnabled && t != null) Diag(t, t.text, "TextMesh", t.font != null ? t.font.name : "null", t.fontStyle);
            }
            if (_sweepBoldCount != _lastBoldLogged)
            {
                _lastBoldLogged = _sweepBoldCount;
                BismuthLog.Debug("GameFont: sweep bold-swapped " + _sweepBoldCount +
                                 " texts (bold font: " + (_boldFont != null ? _boldFont.name : "none") + ")");
            }
        }

        /* Re-apply the Bismuth font right after the game stamps a localized one
           (RDString.SetLocalizedFont, patched). Fixes language-selector previews
           reverting to each language's own preview font over the swap. Note: a name
           in a script Pretendard doesn't cover (e.g. Thai) falls back to tofu, which
           is acceptable per the "keep our font" request. The skip-guard makes these
           idempotent. */
        internal static void OnLocalizedFontSet(Text t)
        {
            if (Enabled && _font != null) ApplyText(t);
        }

        internal static void OnLocalizedFontSet(TMP_Text t)
        {
            if (Enabled && _tmpFont != null) ApplyTmp(t);
        }

        internal static void OnLocalizedFontSet(TextMesh t)
        {
            if (Enabled && _font != null) ApplyTextMesh(t);
        }

        // Per-spawn hook for pooled/instantiated objects (judgement popups)
        internal static void ApplyTo(GameObject go)
        {
            if (!Enabled || _font == null || go == null) return;
            if (DiagEnabled) _diagBudget = 16; // HUD sweeps get their own budget (filter is specific)
            foreach (var t in go.GetComponentsInChildren<Text>(true))
            {
                ApplyText(t);
                if (DiagEnabled && t != null) Diag(t, t.text, "Text", t.font != null ? t.font.name : "null", t.fontStyle);
            }
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
            {
                ApplyTmp(t);
                if (DiagEnabled && t != null) Diag(t, t.text, t.GetType().Name, t.font != null ? t.font.name : "null", t.fontStyle);
            }
            foreach (var t in go.GetComponentsInChildren<TextMesh>(true)) ApplyTextMesh(t);
        }

        /* Level editor form panels are dense, hand-fitted UI. User size/leading tweaks
           meant for gameplay/menu text wreck them (and some editor labels auto-fit, so
           a global shrink lands unevenly). Editor-scene canvas text keeps metric
           normalization only: visually vanilla, just in the Bismuth font. */
        private static bool IsEditorUi(Component c)
        {
            try { return c.gameObject.scene.name == "scnEditor"; }
            catch { return false; }
        }

        /* World XI/MN guest-track credit decorations
           (BG/XtraInfo/Info../GuestTrackWorld..) are hand-placed prefab text. Role
           labels carry an embedded <size=N> rich-text tag that overrides fontSize, and
           name lines get their fontSize re-stamped every frame by the localized-font
           refresh. Both defeat normal fontSize scaling, so Pretendard (which renders
           taller than the game font at the same point size) overruns the
           fixed-position layout and crowds the name into the label (World XI packs them
           tighter than other worlds, so only it visibly breaks). Fix by driving size
           through a rich-text <size=…> tag instead, which beats both the tag override
           and the fontSize re-stamp. See ApplyGuestCreditSize. */
        private static bool InGuestTrackCredit(Component c)
        {
            for (var p = c.transform; p != null; p = p.parent)
                if (p.name.StartsWith("GuestTrack")) return true;
            return false;
        }

        private static bool Skip(Component c)
        {
            /* Bismuth's own canvases manage their fonts themselves, and txtLevelName
               has its dedicated swap/restore in ApplyLevelNameTransform. Check BOTH
               owner references: scrController.instance can be unset during the
               level-select scene sweep, which let txtLevelName slip through and get
               full-size swapped and scene-bolded ("8-X Jungle City" rendered huge). */
            var root = c.transform.root;
            if (root != null && root.name.StartsWith("Bismuth")) return true;
            try { if (ReferenceEquals(c, scrController.instance?.txtLevelName)) return true; }
            catch { }
            try { if (ReferenceEquals(c, scrUIController.instance?.txtLevelName)) return true; }
            catch { }
            return false;
        }

        /* Visual-size normalization: the ratio of (line height / em size) between the
           original font and the Bismuth font. > 1 means the original is airier, so
           swapped text must shrink. Clamped, since metric outliers shouldn't halve a
           label. */
        private static float LegacyScale(Font orig)
        {
            try
            {
                if (orig != null && orig.fontSize > 0 && orig.lineHeight > 0 &&
                    _font.fontSize > 0 && _font.lineHeight > 0)
                {
                    float o = (float)orig.lineHeight / orig.fontSize;
                    float u = (float)_font.lineHeight / _font.fontSize;
                    if (o > 0f && u > 0f) return Mathf.Clamp(o / u, 0.6f, 1.1f);
                }
            }
            catch { }
            return DefaultScale;
        }

        private static float TmpScale(TMP_FontAsset orig)
        {
            try
            {
                if (orig != null && _tmpFont != null)
                {
                    float o = orig.faceInfo.lineHeight / orig.faceInfo.pointSize;
                    float u = _tmpFont.faceInfo.lineHeight / _tmpFont.faceInfo.pointSize;
                    if (o > 0f && u > 0f) return Mathf.Clamp(o / u, 0.6f, 1.1f);
                }
            }
            catch { }
            return DefaultScale;
        }

        /* IMPORTANT: all three Apply* methods derive sizes from the CACHED ORIGINAL
           state, never the component's current values. The game re-assigns its own
           (localized) fonts to surviving components on rewind, which defeats the
           "font == ours" skip, and recomputing from current values then compounds the
           scale once per attempt (text shrank/grew every death until this fix). */

        private static void ApplyText(Text t)
        {
            if (t == null || Skip(t)) return;
            var elem = ElementWeightEntry(t);
            TextState st;
            if (!_origText.TryGetValue(t, out st))
            {
                st = new TextState
                {
                    Font = t.font, Size = t.fontSize, LineSpacing = t.lineSpacing, Style = t.fontStyle,
                    BestFit = t.resizeTextForBestFit, BestFitMax = t.resizeTextMaxSize,
                };
                _origText[t] = st;
            }
            else if (!IsOurLegacyFont(t.font))
            {
                /* The game re-stamped a font since caching (Start()-time localization
                   runs AFTER the scene-entry sweep). Adopt it as the restore target,
                   or toggling off restores a pre-localization font with no Korean
                   glyphs (title-screen tip/credits rendered as tofu). */
                st.Font = t.font;
                _origText[t] = st;
            }
            /* Bold for legacy Text is faux via fontStyle (the font stays _font), so the
               skip check below must compare BOTH font and style, or boldness can never
               flip once a component is on the Bismuth font (keycaps stayed bold, names
               regular). */
            bool italic = st.Style == FontStyle.Italic || st.Style == FontStyle.BoldAndItalic;
            bool bold = ShouldBold(t, t.text,
                st.Style == FontStyle.Bold || st.Style == FontStyle.BoldAndItalic,
                st.Font != null ? st.Font.name : null);
            Font desiredFont = elem != null && elem.Font != null ? elem.Font : _font;
            FontStyle desiredStyle = elem != null && elem.Font != null
                ? (italic ? FontStyle.Italic : FontStyle.Normal)
                : (bold ? (italic ? FontStyle.BoldAndItalic : FontStyle.Bold) : st.Style);
            /* Guest-track credit labels ignore fontSize (embedded <size> tag /
               per-frame re-stamp), so drive their size via a rich-text tag, and do it
               BEFORE the font/style skip below so it keeps re-applying once already on
               the Bismuth font. */
            if (InGuestTrackCredit(t)) ApplyGuestCreditSize(t, st);
            if (t.font == desiredFont && t.fontStyle == desiredStyle) return;
            if (bold) _sweepBoldCount++;
            bool editorUi = IsEditorUi(t);
            float scale = LegacyScale(st.Font) * (editorUi ? 1f : UserScale);
            /* CLS portal/world-name labels ("라이브러리", two-line "추천\n클래식") render
               oversized once bolded in Pretendard. These are scrTextChanger components
               that re-stamp fontSize every frame, so a fontSize change gets reverted.
               Shrink the TRANSFORM instead (idempotent, not re-stamped), like the stats
               panels and the Continue sublabel. */
            if (InCls() && HasAncestor(t, "WorldNameCanvas"))
                ScaleTransform(t.transform, ClsLabelScale, keepCenter: false);
            if (IsStatsText(t)) ApplyStatsScale(t);
            /* Continue tile level-name sublabel ("8-X Jungle City",
               Canvas World/Continue/LastLevel) renders oversized in Pretendard and
               ignores font-size writes (fit-container text, same as stats panels), so
               shrink its transform instead. */
            if (t.name == "LastLevel" && t.transform.parent != null && t.transform.parent.name == "Continue")
                ScaleTransform(t.transform, 0.6f, keepCenter: false);
            /* Faux bold via fontStyle, not the bundled Black Font asset: the legacy
               Black asset silently renders as regular (dynamic-font name fallback),
               whereas engine-synthesized bold is reliable for any dynamic font. */
            t.font = desiredFont;
            t.fontStyle = desiredStyle;
            t.fontSize = Mathf.Max(1, Mathf.RoundToInt(st.Size * scale));
            /* Auto-fit text ignores fontSize and renders at up to resizeTextMaxSize, so
               scale the cap too or size tweaks (UserScale, LastLevel) do nothing. */
            if (st.BestFit)
                t.resizeTextMaxSize = Mathf.Max(1, Mathf.RoundToInt(st.BestFitMax * scale));
            t.lineSpacing = st.LineSpacing * (editorUi ? 1f : UserLineSpacing);
            /* CLS portal labels ship with a very tight lineSpacing (0.6) for their
               big-over-small two-line format ("추천\n클래식"), so give them a fixed,
               readable gap instead. */
            if (InCls() && HasAncestor(t, "WorldNameCanvas")) t.lineSpacing = ClsLabelLineSpacing;
        }

        // Extra shrink for split-layout guest-track credits, on top of font swap
        private const float GuestCreditSizeScale = 0.7f;

        /* Drive a guest-track credit label's rendered size through a <size=N>
           rich-text tag. fontSize writes don't stick here: the role labels embed their
           own <size> tag and the name lines get fontSize re-stamped each frame, but the
           rich-text tag overrides both. Only the split-layout worlds crowd, where the
           title and name are separate single-line objects and shrinking the title opens
           the gap to the fixed-position name. The combined worlds hold title and name
           in one multiline <size> block that scales uniformly and never crowds, so a
           newline means skip. Strips any existing tag first, so it stays idempotent. */
        private static void ApplyGuestCreditSize(Text t, TextState st)
        {
            if (t.text.IndexOf('\n') >= 0) return;
            string orig;
            if (!_guestCreditOrigText.TryGetValue(t, out orig))
                _guestCreditOrigText[t] = t.text;
            int target = Mathf.Max(1, Mathf.RoundToInt(st.Size * GuestCreditSizeScale));
            string bare = SizeTagRe.Replace(t.text, "");
            string wrapped = "<size=" + target + ">" + bare + "</size>";
            if (t.text != wrapped)
            {
                t.supportRichText = true;
                t.text = wrapped;
            }
        }

        private static void ApplyTmp(TMP_Text t)
        {
            if (t == null || _tmpFont == null || Skip(t)) return;
            var elem = ElementWeightEntry(t);
            TmpState st;
            if (!_origTmp.TryGetValue(t, out st))
            {
                st = new TmpState
                {
                    Font = t.font, Size = t.fontSize, LineSpacing = t.lineSpacing, Style = t.fontStyle,
                    AutoSize = t.enableAutoSizing, SizeMin = t.fontSizeMin, SizeMax = t.fontSizeMax,
                };
                _origTmp[t] = st;
            }
            else if (!IsOurTmpFont(t.font))
            {
                st.Font = t.font; // re-stamped since cached, see ApplyText
                _origTmp[t] = st;
            }
            bool bold = ShouldBold(t, t.text, (st.Style & FontStyles.Bold) != 0,
                st.Font != null ? st.Font.name : null);
            bool explicitWeight = elem != null && elem.TmpFont != null;
            /* TMP bold = a different font asset, so the skip check on font alone is
               mostly right, but also compare style (the faux-Bold flag) so a flip
               re-applies. */
            var target = explicitWeight ? elem.TmpFont : (bold && _boldTmpFont != null ? _boldTmpFont : _tmpFont);
            FontStyles desiredStyle = bold || explicitWeight ? (st.Style & ~FontStyles.Bold) : st.Style;
            if (t.font == target && t.fontStyle == desiredStyle) return;
            if (bold) _sweepBoldCount++;
            bool editorUi = IsEditorUi(t);
            float scale = TmpScale(st.Font) * (editorUi ? 1f : UserScale);
            /* Judgement popups take their own size multiplier (independent of the global
               game-text scale) so they can be enlarged without touching anything else. */
            if (IsJudgement(t)) scale *= JudgementScale;
            if (IsStatsText(t)) ApplyStatsScale(t);
            /* The original asset becomes a fallback of the Bismuth asset, so glyphs
               Pretendard lacks (kana, symbols) keep rendering instead of turning into
               boxes. */
            if (st.Font != null)
            {
                var fb = target.fallbackFontAssetTable;
                if (fb == null) target.fallbackFontAssetTable = fb = new List<TMP_FontAsset>();
                if (!fb.Contains(st.Font)) fb.Add(st.Font);
            }
            t.font = target;
            /* The real Black asset replaces the faux-bold style. Leaving the Bold flag
               set would stack engine-simulated bold on top and smudge the glyphs. */
            t.fontStyle = desiredStyle;
            /* Auto-sizing TMP (level descriptions) IGNORES fontSize and fits text
               between fontSizeMin/Max, so short text balloons to Max. Scale the BOUNDS
               instead, the TMP analog of legacy best-fit resizeTextMaxSize. */
            if (st.AutoSize)
            {
                t.enableAutoSizing = true;
                t.fontSizeMin = st.SizeMin * scale;
                t.fontSizeMax = st.SizeMax * scale;
            }
            else
                t.fontSize = st.Size * scale;
            /* TMP lineSpacing is additive, in font units where ~100 = one em. Convert
               the multiplier into the extra advance it implies for the Bismuth font. */
            float emLine = 100f;
            try
            {
                if (_tmpFont.faceInfo.pointSize > 0)
                    emLine = _tmpFont.faceInfo.lineHeight / _tmpFont.faceInfo.pointSize * 100f;
            }
            catch { }
            t.lineSpacing = editorUi ? st.LineSpacing : st.LineSpacing + (UserLineSpacing - 1f) * emLine;
        }

        private static void ApplyTextMesh(TextMesh t)
        {
            if (t == null || Skip(t)) return;
            var elem = ElementWeightEntry(t);
            var renderer = t.GetComponent<MeshRenderer>();
            if (renderer == null) return;
            MeshState st;
            if (!_origMesh.TryGetValue(t, out st))
            {
                st = new MeshState
                {
                    Font = t.font, Mat = renderer.sharedMaterial,
                    Size = t.fontSize, CharSize = t.characterSize, LineSpacing = t.lineSpacing,
                    Style = t.fontStyle,
                };
                _origMesh[t] = st;
            }
            else if (!IsOurLegacyFont(t.font))
            {
                st.Font = t.font; // re-stamped since cached, see ApplyText
                st.Mat = renderer.sharedMaterial;
                _origMesh[t] = st;
            }
            // Like legacy Text: bold is faux via fontStyle, so compare font AND style
            bool meshItalic = st.Style == FontStyle.Italic || st.Style == FontStyle.BoldAndItalic;
            bool bold = ShouldBold(t, t.text,
                st.Style == FontStyle.Bold || st.Style == FontStyle.BoldAndItalic,
                st.Font != null ? st.Font.name : null);
            Font desiredFont = elem != null && elem.Font != null ? elem.Font : _font;
            FontStyle desiredStyle = elem != null && elem.Font != null
                ? (meshItalic ? FontStyle.Italic : FontStyle.Normal)
                : (bold ? (meshItalic ? FontStyle.BoldAndItalic : FontStyle.Bold) : st.Style);
            if (t.font == desiredFont && t.fontStyle == desiredStyle) return;
            if (bold) _sweepBoldCount++;
            float scale = Mathf.Clamp(LegacyScale(st.Font) * MeshExtraScale, 0.45f, 1.1f) * UserScale;
            if (IsStatsText(t)) ApplyStatsScale(t);
            /* Legacy 3D TextMesh renders through the font's own material. fontSize 0
               means "font import size", so scale characterSize instead in that case. */
            t.font = desiredFont;
            renderer.sharedMaterial = desiredFont.material;
            t.fontStyle = desiredStyle;
            if (st.Size > 0)
                t.fontSize = Mathf.Max(1, Mathf.RoundToInt(st.Size * scale));
            else
                t.characterSize = st.CharSize * scale;
            t.lineSpacing = st.LineSpacing * UserLineSpacing; // multiplier semantics
        }

        /* StopMod must restore. After a hot reload the caches are gone and the new
           assembly's fresh Font instances make swapped text look unswapped, so it
           would re-cache the scaled state as "original" and compound across deploys. */
        internal static void RestoreAll() => Restore();

        private static void Restore()
        {
            foreach (var kv in _origText)
                if (kv.Key != null)
                {
                    kv.Key.font = kv.Value.Font;
                    kv.Key.fontSize = kv.Value.Size;
                    kv.Key.lineSpacing = kv.Value.LineSpacing;
                    kv.Key.fontStyle = kv.Value.Style;
                    if (kv.Value.BestFit) kv.Key.resizeTextMaxSize = kv.Value.BestFitMax;
                }
            foreach (var kv in _guestCreditOrigText)
                if (kv.Key != null) kv.Key.text = kv.Value; // undo <size=…> rewrite
            foreach (var kv in _origTmp)
                if (kv.Key != null)
                {
                    kv.Key.font = kv.Value.Font;
                    kv.Key.fontSize = kv.Value.Size;
                    kv.Key.lineSpacing = kv.Value.LineSpacing;
                    kv.Key.fontStyle = kv.Value.Style;
                    if (kv.Value.AutoSize)
                    {
                        kv.Key.fontSizeMin = kv.Value.SizeMin;
                        kv.Key.fontSizeMax = kv.Value.SizeMax;
                    }
                }
            foreach (var kv in _origMesh)
                if (kv.Key != null)
                {
                    kv.Key.font = kv.Value.Font;
                    kv.Key.fontSize = kv.Value.Size;
                    kv.Key.characterSize = kv.Value.CharSize;
                    kv.Key.lineSpacing = kv.Value.LineSpacing;
                    kv.Key.fontStyle = kv.Value.Style;
                    var r = kv.Key.GetComponent<MeshRenderer>();
                    if (r != null) r.sharedMaterial = kv.Value.Mat;
                }
            foreach (var kv in _statsOrigScale)
                if (kv.Key != null)
                {
                    kv.Key.localScale = kv.Value.Scale;
                    kv.Key.localPosition = kv.Value.Pos;
                }
            _origText.Clear();
            _guestCreditOrigText.Clear();
            _origTmp.Clear();
            _origMesh.Clear();
            _statsOrigScale.Clear();
        }

        // Drop entries whose components died with their scene
        private static void Prune()
        {
            PruneDict(_origText);
            PruneDict(_guestCreditOrigText);
            PruneDict(_origTmp);
            PruneDict(_origMesh);
            PruneDict(_statsOrigScale);
        }

        private static void PruneDict<TKey, TVal>(Dictionary<TKey, TVal> dict) where TKey : Object
        {
            List<TKey> dead = null;
            foreach (var k in dict.Keys)
                if (k == null) (dead = dead ?? new List<TKey>()).Add(k);
            if (dead != null)
                foreach (var k in dead) dict.Remove(k);
        }
    }
}
