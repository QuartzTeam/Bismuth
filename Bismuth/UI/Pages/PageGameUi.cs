using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Bismuth.UI.Pages
{
    // Game-text repaint settings (GameFontApplier). Its own font family/weight,
    // decoupled from the overlay font, plus title weight and size tuning.
    internal static class PageGameUi
    {
        public static void Build(RectTransform content)
        {
            var s = UICore.Settings;

            // Game-HUD on-screen layout editor (moved from the old Locations tab).
            UIBuilder.SectionHeader(content, "Layout");
            UIBuilder.Description(content,
                "Move and scale the game's own HUD: death percentage, congrats / strict " +
                "clear and results text, press-to-start, countdown, autoplay text, " +
                "difficulty pill, modifier icons, pause button and the hit error meter. " +
                "Drag to move, drag a corner grip or scroll to scale, right-click a " +
                "handle to reset that element to the Bismuth default. Hidden elements " +
                "are shown with sample content while editing.");
            UIBuilder.Button(content, "Edit game UI on screen", GameUiEditor.Open);
            UIBuilder.DangerButton(content, "Reset layout to Bismuth defaults", () =>
            {
                GameUiLayout.ResetAllToDefaults();
                UICore.OnSettingsChanged?.Invoke();
            });
            UIBuilder.DangerButton(content, "Reset layout to game defaults", () =>
            {
                GameUiLayout.ResetAllToGame();
                UICore.OnSettingsChanged?.Invoke();
            });

            UIBuilder.Spacer(content);
            UIBuilder.SectionHeader(content, "Game text");

            // One font selector instead of a toggle. "Game default" = the game's
            // own fonts (swap off, extra options below hidden), any family = swap on.
            GameObject optionsHost = null;
            PageUI.BuildFontSelector(content, "Game font", UICore.AvailableFonts, s.GameFontName,
                entry =>
                {
                    s.GameFontName = entry.Name;
                    s.GameTextUseOverlayFont = true;
                    MainClass.ApplySelectedFont();
                    UICore.OnSettingsChanged?.Invoke();
                    RebuildTitleWeightRow();
                    RebuildElementWeightRows();
                    if (optionsHost != null) optionsHost.SetActive(true);
                },
                defaultOption: "Game default",
                defaultSelected: !s.GameTextUseOverlayFont,
                onDefault: () =>
                {
                    s.GameTextUseOverlayFont = false;
                    GameFontApplier.Reapply(); // restores originals
                    UICore.OnSettingsChanged?.Invoke();
                    if (optionsHost != null) optionsHost.SetActive(false);
                });

            optionsHost = UIBuilder.Rect("GameTextOptions", content);
            var ovlg = optionsHost.AddComponent<VerticalLayoutGroup>();
            ovlg.childControlWidth = true;
            ovlg.childControlHeight = true;
            ovlg.childForceExpandWidth = true;
            ovlg.childForceExpandHeight = false;
            ovlg.spacing = 2f;
            var body = optionsHost.transform;

            _titleWeightHost = UIBuilder.Rect("TitleWeightHost", body);
            var vlg = _titleWeightHost.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            RebuildTitleWeightRow();

            // Extra shrink/grow on top of the automatic metric normalization.
            // World-space menu text especially reads larger than the originals.
            UIBuilder.Slider(body, "Game text size", s.GameTextScale, 0.4f, 1.5f, v =>
            {
                s.GameTextScale = v;
                GameFontApplier.RequestResize();
            });
            UIBuilder.Slider(body, "Line spacing", s.GameTextLineSpacing, 0.8f, 2f, v =>
            {
                s.GameTextLineSpacing = v;
                GameFontApplier.RequestResize();
            });
            // Level-select per-level stats panels (attempts, max x-acc, …).
            UIBuilder.Slider(body, "Level stats size", s.GameStatsScale, 0.4f, 1.5f, v =>
            {
                s.GameStatsScale = v;
                GameFontApplier.RequestResize();
            });
            // Hit-judgement popups (Perfect/완벽 …) only. Independent of the global size.
            UIBuilder.Slider(body, "Judgement size", s.GameJudgementScale, 0.5f, 3f, v =>
            {
                s.GameJudgementScale = v;
                GameFontApplier.RequestResize();
            });

            _elementWeightsHost = UIBuilder.Rect("ElementWeightsHost", body);
            var evlg = _elementWeightsHost.AddComponent<VerticalLayoutGroup>();
            evlg.childControlWidth = true;
            evlg.childControlHeight = true;
            evlg.childForceExpandWidth = true;
            evlg.childForceExpandHeight = false;
            evlg.spacing = 2f;
            RebuildElementWeightRows();

            optionsHost.SetActive(s.GameTextUseOverlayFont);
        }

        // Per-HUD-element weight overrides ("Auto" = the bold/regular heuristic).
        // Only the text-bearing GameUiLayout targets get a row, and only when the
        // game-font family has more than one weight. Rebuilt on family change.
        private static GameObject _elementWeightsHost;

        private static readonly (string Key, string Label)[] WeightTargets =
        {
            ("percent",      "Death %"),
            ("congrats",     "Congrats"),
            ("strictclear",  "Strict Clear"),
            ("results",      "Results"),
            ("presstostart", "Press To Start"),
            ("countdown",    "Countdown"),
            ("autoplay",     "Autoplay Text"),
            // Synthetic key: pooled scrHitTextMesh popups, mapped in GameFontApplier.
            ("judgement",    "Judgements"),
            // Consumed by MainClass.ApplySelectedFont (Bismuth manages txtLevelName),
            // not GameFontApplier's element-weight path. Defaults to the title weight.
            ("levelname",    "Level Name"),
        };

        private static void RebuildElementWeightRows()
        {
            if (_elementWeightsHost == null) return;
            for (int i = _elementWeightsHost.transform.childCount - 1; i >= 0; i--)
            {
                var c = _elementWeightsHost.transform.GetChild(i);
                c.SetParent(null);
                UnityEngine.Object.Destroy(c.gameObject);
            }

            var s = UICore.Settings;
            var weights = GameFamilyWeights(s);
            if (weights.Count <= 1) return;

            UIBuilder.SectionHeader(_elementWeightsHost.transform, "Element weights");
            foreach (var (key, label) in WeightTargets)
            {
                string k = key;
                var options = new List<string> { "Auto" };
                options.AddRange(weights);
                string current = s.GameUiWeightFor(k);
                int idx = 0;
                for (int i = 0; i < weights.Count; i++)
                    if (string.Equals(weights[i], current, StringComparison.OrdinalIgnoreCase))
                    { idx = i + 1; break; }
                UIBuilder.Dropdown(_elementWeightsHost.transform, label, options, idx, i =>
                {
                    s.SetGameUiWeight(k, i == 0 ? "" : weights[i - 1]);
                    MainClass.ApplySelectedFont();
                });
            }
        }

        // Weights available in the game-font family, canonically sorted.
        private static List<string> GameFamilyWeights(Settings s)
        {
            var result = new List<string>();
            var fonts = UICore.AvailableFonts;
            if (fonts == null || fonts.Count == 0) return result;
            FontLoader.SplitWeight(
                string.IsNullOrEmpty(s.GameFontName) ? fonts[0].Name : s.GameFontName,
                out string family, out _);
            foreach (var e in fonts)
            {
                FontLoader.SplitWeight(e.Name, out string fam, out string w);
                if (fam == family && !string.IsNullOrEmpty(w) && !result.Contains(w))
                    result.Add(w);
            }
            result.Sort((a, b) => FontLoader.WeightRank(a).CompareTo(FontLoader.WeightRank(b)));
            return result;
        }

        // Weight used for title/bold game text. Options: the game-font family's
        // weights plus the "Heaviest" sentinel. Rebuilt when the family changes.
        private static GameObject _titleWeightHost;

        private static void RebuildTitleWeightRow()
        {
            if (_titleWeightHost == null) return;
            for (int i = _titleWeightHost.transform.childCount - 1; i >= 0; i--)
            {
                var c = _titleWeightHost.transform.GetChild(i);
                c.SetParent(null);
                UnityEngine.Object.Destroy(c.gameObject);
            }

            var s = UICore.Settings;
            var fonts = UICore.AvailableFonts;
            if (fonts == null || fonts.Count == 0) return;

            FontLoader.SplitWeight(
                string.IsNullOrEmpty(s.GameFontName) ? fonts[0].Name : s.GameFontName,
                out string family, out _);

            var weights = new List<string> { FontLoader.WeightHeaviest };
            foreach (var e in fonts)
            {
                FontLoader.SplitWeight(e.Name, out string fam, out string w);
                if (fam == family && !string.IsNullOrEmpty(w) && !weights.Contains(w))
                    weights.Add(w);
            }
            weights.Sort((a, b) =>
            {
                // Heaviest pinned first, then canonical light→heavy order.
                if (a == FontLoader.WeightHeaviest) return -1;
                if (b == FontLoader.WeightHeaviest) return 1;
                return FontLoader.WeightRank(a).CompareTo(FontLoader.WeightRank(b));
            });

            int idx = 0;
            for (int i = 0; i < weights.Count; i++)
                if (string.Equals(weights[i], s.GameTextTitleWeight, StringComparison.OrdinalIgnoreCase))
                    idx = i;

            UIBuilder.Dropdown(_titleWeightHost.transform, "Title weight", weights, idx, i =>
            {
                s.GameTextTitleWeight = weights[i];
                MainClass.ApplySelectedFont();
            });
        }
    }
}
