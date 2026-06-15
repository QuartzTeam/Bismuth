using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Bismuth
{
    /* Moves and scales the game's own HUD elements (death %, congrats, difficulty
       pill, pause button, hit error meter, …) per saved overrides. Two mechanisms:

       1. scrUIController elements get a full-stretch wrapper RectTransform inserted
          between the element and its parent. The wrapper rect equals the parent
          rect, so the element's own anchors and anchoredPosition keep their exact
          meaning. Everything the game does to the element (difficulty show/minimize
          tweens, text rewrites) happens inside the wrapper and never fights our
          offset/scale. Scale pivots on the element's own center (computed per
          apply), not screen center, so growing an element doesn't slide it across
          the screen.

       2. The hit error meter already has the game's own layout pass
          (scrHitErrorMeter.UpdateLayout: anchors and pivot from pos, localScale
          from meterScale), called on scrController.Awake and from the settings
          menu. A Harmony postfix re-applies the absolute normalized position and
          scale multiplier on top, so persistence is free and no sweeps are needed.

       Scene loads spawn fresh game objects, so Reapply re-wraps on the same
       triggers GameFontApplier sweeps on: scene change, level start, state-change
       ticks. */
    internal static class GameUiLayout
    {
        private const string WrapPrefix = "BismuthGameUiWrap_";

        internal class TargetDef
        {
            public string Key;
            public string Label;
            public Func<RectTransform> Get;
        }

        /* txtLevelName is deliberately absent. Bismuth already owns its transform
           (LevelNameScale/LevelNameY via ApplyLevelNameTransform). */
        internal static readonly TargetDef[] Targets =
        {
            new TargetDef { Key = "percent",      Label = "Death %",        Get = () => Rect(Uic?.txtPercent) },
            new TargetDef { Key = "congrats",     Label = "Congrats",       Get = () => Rect(Uic?.txtCongrats) },
            new TargetDef { Key = "strictclear",  Label = "Strict Clear",   Get = () => Rect(Uic?.txtAllStrictClear) },
            new TargetDef { Key = "results",      Label = "Results",        Get = () => Rect(Uic?.txtResults) },
            new TargetDef { Key = "presstostart", Label = "Press To Start", Get = () => Rect(Uic?.txtPressToStart) },
            new TargetDef { Key = "countdown",    Label = "Countdown",      Get = () => Rect(Uic?.txtCountdown) },
            new TargetDef { Key = "difficulty",   Label = "Difficulty",     Get = () => Uic?.difficultyContainer },
            new TargetDef { Key = "modifiers",    Label = "Modifiers",      Get = () => Uic?.modifiersContainer },
            new TargetDef { Key = "pause",        Label = "Pause Button",   Get = () => Rect(Uic?.pauseButton) },
            new TargetDef { Key = "autoplay",     Label = "Autoplay Text",  Get = AutoplayText },
        };

        /* The autoplay label is a scrShowIfDebug component with no static accessor,
           so find it by type, throttled, and re-find when a scene swap kills it. */
        private static scrShowIfDebug _autoplay;
        private static int _autoplayNextSearch;

        private static RectTransform AutoplayText()
        {
            /* Several scrShowIfDebug instances carry a Text (autoplay label, debug
               label), so caching whichever came first could pin the handle to an
               inactive object while the real label is on screen. Prefer the active
               one, and re-search (throttled) while only an inactive one is cached. */
            bool cachedUsable = _autoplay != null && _autoplay.gameObject.activeInHierarchy;
            if (!cachedUsable && Time.frameCount >= _autoplayNextSearch)
            {
                _autoplayNextSearch = Time.frameCount + 60;
                try
                {
                    foreach (var c in UnityEngine.Object.FindObjectsByType<scrShowIfDebug>(
                                 FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (c.GetComponent<Text>() == null) continue;
                        if (c.gameObject.activeInHierarchy) { _autoplay = c; break; }
                        if (_autoplay == null) _autoplay = c;
                    }
                    BismuthLog.Debug("GameUiLayout: autoplay search → " + (_autoplay == null ? "none"
                        : _autoplay.name + (_autoplay.gameObject.activeInHierarchy ? " (active)" : " (inactive)")
                          + (_autoplay.transform is RectTransform ? "" : " (NO RectTransform)")));
                }
                catch (Exception e)
                {
                    BismuthLog.Debug("GameUiLayout: autoplay search failed: " + e.Message);
                }
            }
            return _autoplay != null ? _autoplay.transform as RectTransform : null;
        }

        /* Which HUD element (by target key) a text component belongs to, or null.
           Used by GameFontApplier's per-element weight overrides. Target rects are
           resolved once per frame, since a full sweep asks for every text. */
        private static int _ownerFrame = -1;
        private static readonly List<KeyValuePair<string, Transform>> _ownerRects =
            new List<KeyValuePair<string, Transform>>();

        internal static string OwnerKey(Component c)
        {
            if (c == null) return null;
            if (Time.frameCount != _ownerFrame)
            {
                _ownerFrame = Time.frameCount;
                _ownerRects.Clear();
                foreach (var t in Targets)
                {
                    RectTransform rt = null;
                    try { rt = t.Get?.Invoke(); } catch { }
                    if (rt != null) _ownerRects.Add(new KeyValuePair<string, Transform>(t.Key, rt));
                }
            }
            var tr = c.transform;
            foreach (var kv in _ownerRects)
                if (kv.Value != null && tr.IsChildOf(kv.Value)) return kv.Key;
            return null;
        }

        private static scrUIController Uic
        {
            get { try { return scrUIController.instance; } catch { return null; } }
        }

        private static RectTransform Rect(Component c) =>
            c != null ? c.transform as RectTransform : null;

        private static Settings S => MainClass.Settings;

        // Wrappers created this session, by key, unwrapped on RestoreAll
        private static readonly Dictionary<string, RectTransform> _wrappers =
            new Dictionary<string, RectTransform>();

        // ── Overrides storage ────────────────────────────────────────────────

        internal static GameUiOverride GetOverride(string key, bool create)
        {
            var s = S;
            if (s == null) return null;
            if (s.GameUiOverrides == null) s.GameUiOverrides = new List<GameUiOverride>();
            foreach (var o in s.GameUiOverrides)
                if (o != null && o.Key == key) return o;
            if (!create) return null;
            var n = new GameUiOverride { Key = key };
            s.GameUiOverrides.Add(n);
            return n;
        }

        internal static void RemoveOverride(string key)
        {
            var s = S;
            if (s?.GameUiOverrides == null) return;
            s.GameUiOverrides.RemoveAll(o => o == null || o.Key == key);
            Unwrap(key);
        }

        /* Right-click reset target: the Bismuth default when the key has one, else
           vanilla (override removed, wrapper unwound). */
        internal static void ResetToDefault(string key)
        {
            var d = Settings.DefaultGameUiOverride(key);
            if (d == null) { RemoveOverride(key); return; }
            var o = GetOverride(key, create: true);
            o.OffX = d.OffX;
            o.OffY = d.OffY;
            o.Scale = d.Scale;
            ApplyOne(key);
        }

        internal static void ResetAllToDefaults()
        {
            foreach (var t in Targets) ResetToDefault(t.Key);
            ResetMeterSettings();
        }

        internal static void ResetAllToGame()
        {
            foreach (var t in Targets) RemoveOverride(t.Key);
            ResetMeterSettings();
        }

        // Meter has no Bismuth default. Its default IS the game placement.
        private static void ResetMeterSettings()
        {
            var s = S;
            if (s != null)
            {
                s.GameErrorMeterOverride = false;
                s.GameErrorMeterX = 0.5f;
                s.GameErrorMeterY = 0.03f;
                s.GameErrorMeterScale = 1f;
            }
            RestoreErrorMeter();
        }

        internal static bool HasAnyOverride =>
            S != null && (S.GameErrorMeterOverride ||
                          (S.GameUiOverrides != null && S.GameUiOverrides.Count > 0));

        // ── Apply ────────────────────────────────────────────────────────────

        /* Delayed re-applies after controller state changes (death/results screens
           activate elements after the level-start pass). Ticked from Overlay.Update. */
        private static int _applyFrameA = -1;
        private static int _applyFrameB = -1;

        internal static void RequestApplySoon()
        {
            if (!HasAnyOverride) return;
            _applyFrameA = Time.frameCount + 2;
            _applyFrameB = Time.frameCount + 30;
        }

        internal static void Tick()
        {
            if (_applyFrameA > 0 && Time.frameCount >= _applyFrameA) { _applyFrameA = -1; Reapply(); }
            if (_applyFrameB > 0 && Time.frameCount >= _applyFrameB) { _applyFrameB = -1; Reapply(); }
        }

        internal static void Reapply()
        {
            if (S == null) return;
            PruneWrappers();
            foreach (var t in Targets)
            {
                var o = GetOverride(t.Key, create: false);
                if (o != null) ApplyOne(t, o);
            }
            var meter = CurrentMeter();
            if (meter != null) ApplyErrorMeter(meter);
        }

        // Applies one override live, also used by editor mid-drag
        internal static void ApplyOne(string key)
        {
            foreach (var t in Targets)
                if (t.Key == key)
                {
                    var o = GetOverride(key, create: false);
                    if (o != null) ApplyOne(t, o);
                    return;
                }
        }

        private static void ApplyOne(TargetDef t, GameUiOverride o)
        {
            var rt = t.Get?.Invoke();
            if (rt == null) return;
            var w = EnsureWrapper(t.Key, rt);
            if (w == null) return;

            /* Reset first to measure the element's untouched center, then pivot the
               wrapper there so scale grows the element in place. With wrapper rect ==
               parent rect, anchoredPosition equals the on-screen shift regardless of
               pivot. */
            w.localScale = Vector3.one;
            w.anchoredPosition = Vector2.zero;
            Vector2 c = (Vector2)w.InverseTransformPoint(WorldCenter(rt));
            var r = w.rect;
            if (r.width > 0.01f && r.height > 0.01f)
                w.pivot = new Vector2((c.x - r.xMin) / r.width, (c.y - r.yMin) / r.height);

            w.anchoredPosition = new Vector2(o.OffX, o.OffY);
            float sc = Mathf.Clamp(o.Scale, 0.1f, 5f);
            w.localScale = new Vector3(sc, sc, 1f);
        }

        private static Vector3 WorldCenter(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return (corners[0] + corners[2]) * 0.5f;
        }

        // ── Wrappers ─────────────────────────────────────────────────────────

        private static RectTransform EnsureWrapper(string key, RectTransform rt)
        {
            // Already wrapped (possibly by previous hot-reloaded assembly), reuse
            var parent = rt.parent as RectTransform;
            if (parent != null && parent.name == WrapPrefix + key)
            {
                _wrappers[key] = parent;
                return parent;
            }
            if (parent == null) return null;
            /* A layout group would treat the wrapper as its child and fight stretch
               anchors. None of the current targets sit in one, but don't silently
               corrupt layout if a game update changes that. */
            if (parent.GetComponent<LayoutGroup>() != null)
            {
                BismuthLog.Debug("GameUiLayout: '" + key + "' parent has a LayoutGroup, skipping");
                return null;
            }

            var go = new GameObject(WrapPrefix + key, typeof(RectTransform));
            var w = (RectTransform)go.transform;
            w.SetParent(parent, false);
            w.SetSiblingIndex(rt.GetSiblingIndex());
            w.anchorMin = Vector2.zero;
            w.anchorMax = Vector2.one;
            w.offsetMin = Vector2.zero;
            w.offsetMax = Vector2.zero;
            w.localScale = Vector3.one;
            /* Wrapper rect == parent rect, so element anchors/anchoredPosition mean
               exactly what they did before reparenting. */
            rt.SetParent(w, false);
            _wrappers[key] = w;
            return w;
        }

        private static void Unwrap(string key)
        {
            RectTransform w;
            if (!_wrappers.TryGetValue(key, out w)) w = null;
            _wrappers.Remove(key);
            if (w == null) return;
            var parent = w.parent;
            int idx = w.GetSiblingIndex();
            // Move children (normally exactly one) back out before destroying
            for (int i = w.childCount - 1; i >= 0; i--)
            {
                var child = w.GetChild(i);
                child.SetParent(parent, false);
                child.SetSiblingIndex(idx);
            }
            UnityEngine.Object.Destroy(w.gameObject);
        }

        private static void PruneWrappers()
        {
            List<string> dead = null;
            foreach (var kv in _wrappers)
                if (kv.Value == null) (dead = dead ?? new List<string>()).Add(kv.Key);
            if (dead != null)
                foreach (var k in dead) _wrappers.Remove(k);
        }

        /* Unwrap everything and drop the error meter override from the live meter.
           Called from StopMod so hot reloads leave no orphaned wrappers behind. */
        internal static void RestoreAll()
        {
            var keys = new List<string>(_wrappers.Keys);
            foreach (var k in keys) Unwrap(k);
            RestoreErrorMeter();
        }

        // ── Error meter ──────────────────────────────────────────────────────

        private struct MeterState
        {
            public Vector2 AnchorMin, AnchorMax, Pivot, AnchoredPos;
            public Vector3 Scale;
        }

        private static scrHitErrorMeter _meterSeen;
        private static MeterState _meterOrig;
        private static bool _meterOrigValid;

        internal static scrHitErrorMeter CurrentMeter()
        {
            try { return scrController.instance?.errorMeter; } catch { return null; }
        }

        /* Postfixed onto scrHitErrorMeter.UpdateLayout, and called directly for live
           editor updates. The game pass has just written its own anchors and scale.
           Capture them as the restore point, then override. */
        internal static void ApplyErrorMeter(scrHitErrorMeter meter)
        {
            var s = S;
            if (s == null || meter == null) return;
            var w = meter.wrapperRectTransform;
            if (w == null) return;

            if (!ReferenceEquals(meter, _meterSeen) || !_meterOrigValid)
            {
                _meterSeen = meter;
                _meterOrig = new MeterState
                {
                    AnchorMin = w.anchorMin, AnchorMax = w.anchorMax,
                    Pivot = w.pivot, AnchoredPos = w.anchoredPosition,
                    Scale = w.localScale,
                };
                _meterOrigValid = true;
            }

            if (!s.GameErrorMeterOverride) return;
            var p = new Vector2(Mathf.Clamp01(s.GameErrorMeterX), Mathf.Clamp01(s.GameErrorMeterY));
            w.anchorMin = p;
            w.anchorMax = p;
            w.pivot = p;
            /* The game's size switch positions the meter via a per-size pixel offset
               below the anchor. An absolute override replaces that entirely. */
            w.anchoredPosition = Vector2.zero;
            float sc = meter.meterScale * Mathf.Clamp(s.GameErrorMeterScale, 0.1f, 5f);
            w.localScale = new Vector3(sc, sc, 1f);
        }

        internal static void RestoreErrorMeter()
        {
            var meter = CurrentMeter();
            if (meter == null || !ReferenceEquals(meter, _meterSeen) || !_meterOrigValid) return;
            var w = meter.wrapperRectTransform;
            if (w == null) return;
            w.anchorMin = _meterOrig.AnchorMin;
            w.anchorMax = _meterOrig.AnchorMax;
            w.pivot = _meterOrig.Pivot;
            w.anchoredPosition = _meterOrig.AnchoredPos;
            w.localScale = _meterOrig.Scale;
        }
    }
}
