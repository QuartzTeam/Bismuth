using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bismuth.UI.Pages
{
    internal static class PageMisc
    {
        // Updated from MainClass.OnSceneUnloaded so the readout stays live while the
        // panel is open (IMGUI re-read it every draw; uGUI text is built once).
        private static TextMeshProUGUI _savingsText;

        public static void Build(PageStack stack)
        {
            var content = stack.Root;
            var s = UICore.Settings;
            var notify = UICore.OnSettingsChanged;

            UIBuilder.SectionHeader(content, "Misc");

            var savingsRow = UIBuilder.Row(content);
            _savingsText = UIBuilder.Label(savingsRow.transform, SavingsLabel(), (int)UIBuilder.LabelFontSize, TextAnchor.MiddleLeft, Theme.TextMuted);
            _savingsText.rectTransform.offsetMin = new Vector2(8f, 0f);

            UIBuilder.Button(content, "View log", LogViewer.Show);

            UIBuilder.Spacer(content);
            BuildProfiles(content);

            // Debug sits under Profiles — its dumps/sweep traces write to the log above.
            BuildDebug(content, s, notify);

            UIBuilder.Spacer(content);
            BuildOptimizations(stack, content, s, notify);
        }

        // ── Profiles ───────────────────────────────────────────────────────
        // Full-settings snapshots; the .xml files in the Profiles folder ARE the
        // import/export format. Loading copies into the live Settings then rides the
        // force-reload path (deferred, so the panel isn't torn down inside its own
        // button handler).
        private static void BuildProfiles(Transform content)
        {
            UIBuilder.SectionHeaderWithHelp(content, "Profiles",
                "A profile snapshots ALL Bismuth settings.\n" +
                "Load applies it and rebuilds the panel.\n" +
                "Profiles are .xml files in the Profiles folder —\n" +
                "share them, or drop one in and Rescan to import.");

            var listHost = UIBuilder.VGroup(content, "ProfileList");
            string pendingName = "";

            System.Action rebuildList = null;
            rebuildList = () =>
            {
                for (int i = listHost.transform.childCount - 1; i >= 0; i--)
                {
                    var c = listHost.transform.GetChild(i);
                    c.SetParent(null);
                    UnityEngine.Object.Destroy(c.gameObject);
                }
                foreach (var name in Profiles.BuiltIn)
                    BuildProfileRow(listHost.transform, name, builtIn: true, rebuildList);
                foreach (var name in Profiles.ListSaved())
                    BuildProfileRow(listHost.transform, name, builtIn: false, rebuildList);
            };
            rebuildList();

            UIBuilder.TextInput(content, "New profile name", "", v => pendingName = v);
            UIBuilder.Button(content, "Save current settings as profile", () =>
            {
                if (Profiles.SaveCurrent(pendingName, out string err))
                    rebuildList();
                else
                    BismuthLog.Log("Profiles: " + err);
            });
            UIBuilder.Button(content, "Rescan profiles folder", rebuildList);
            UIBuilder.Button(content, "Open profiles folder", () => OsShell.OpenFolder(Profiles.ProfilesDir()));
        }

        private static void BuildProfileRow(Transform parent, string name, bool builtIn, System.Action rebuildList)
        {
            var row = UIBuilder.Row(parent);
            var bg = UIBuilder.SolidImage(row, Theme.RowBg);
            bg.raycastTarget = true;

            var label = UIBuilder.Label(row.transform, builtIn ? name + "  (built-in)" : name,
                (int)UIBuilder.LabelFontSize, TextAnchor.MiddleLeft, Theme.Text);
            label.rectTransform.offsetMin = new Vector2(8f, 0);
            label.rectTransform.offsetMax = new Vector2(-140f, 0);

            MiniButton(row.transform, "Load", 56f, builtIn ? -8f : -44f, () =>
            {
                if (Profiles.Load(name, out string err))
                    MainClass.RequestForceReload();
                else
                    BismuthLog.Log("Profiles: " + err);
            });
            if (!builtIn)
                MiniButton(row.transform, "×", 28f, -8f, () =>
                {
                    if (Profiles.Delete(name, out string err)) rebuildList();
                    else BismuthLog.Log("Profiles: " + err);
                });
        }

        private static void MiniButton(Transform parent, string label, float width, float anchoredX, System.Action onClick)
        {
            var btn = UIBuilder.Rect(label, parent);
            var rect = (RectTransform)btn.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(anchoredX, 0);
            rect.sizeDelta = new Vector2(width, 22f);
            var bg = btn.AddComponent<RoundedRectGraphic>();
            bg.Radius = 3f;
            bg.AAFringe = 0.5f;
            bg.color = Theme.ButtonBg;
            bg.raycastTarget = true;
            var t = UIBuilder.Label(btn.transform, label, (int)UIBuilder.LabelFontSize - 1, TextAnchor.MiddleCenter, Theme.Text);
            ClickHandler.Attach(btn, () => onClick());
        }

        // Optimizations drill into a subpage; the NavRow's ring is the master switch
        // (Settings.OptimizationsEnabled gates every flag below it).
        private static void BuildOptimizations(PageStack stack, Transform content, Settings s, System.Action notify)
        {
            UIBuilder.NavRow(content, "Optimizations", s.OptimizationsEnabled,
                v => { s.OptimizationsEnabled = v; notify?.Invoke(); },
                () => stack.Push("Optimizations", body =>
                {
                    UIBuilder.Collapsible(body, "Spectrum Throttle (every 2nd frame)", s.OptSpectrumThrottle,
                        v => { s.OptSpectrumThrottle = v; notify?.Invoke(); }, null);
                    Desc(body, "Halves AudioSource.GetSpectrumData FFT cost on levels that use audio visualization.");

                    UIBuilder.Collapsible(body, "Texture Non-Readable", s.OptTextureNonReadable,
                        v => { s.OptTextureNonReadable = v; notify?.Invoke(); }, null);
                    Desc(body, "Frees CPU-side pixel data after GPU upload. Halves RAM per custom level texture.");

                    UIBuilder.Collapsible(body, "DXT Compression (lossy)", s.OptTextureDXT,
                        v => { s.OptTextureDXT = v; notify?.Invoke(); }, null);
                    Desc(body, "Compresses textures to DXT before upload. 4-6x VRAM savings, slight quality loss. Requires Non-Readable.");

                    UIBuilder.Collapsible(body, "Physics NonAlloc", s.OptPhysicsNonAlloc,
                        v => { s.OptPhysicsNonAlloc = v; notify?.Invoke(); }, null);
                    Desc(body, "Eliminates per-frame Collider2D[] allocation from decoration hitbox checks.");

                    UIBuilder.Collapsible(body, "Unload Assets on Scene Change", s.OptUnloadAssets,
                        v => { s.OptUnloadAssets = v; notify?.Invoke(); }, null);
                    Desc(body, "Forces GC and unloads unused textures/audio between levels to reclaim memory.");

                    UIBuilder.Collapsible(body, "Volume Track DOTween Fix", s.OptVolumeTrackDOTween,
                        v => { s.OptVolumeTrackDOTween = v; notify?.Invoke(); }, null);
                    Desc(body, "Prevents abandoned DOTween sequences from being created every frame on Volume-type track colors.");
                }),
                "spectrum throttle, texture non-readable, dxt compression, physics nonalloc, unload assets, dotween, volume track, performance, ram");
        }

        // Developer tools, revealed by the Debug mode toggle. Polls live game objects /
        // assets and dumps their references to the log (Misc → View log) — see GameProbe.
        private static void BuildDebug(Transform content, Settings s, System.Action notify)
        {
            UIBuilder.Spacer(content);
            UIBuilder.SectionHeader(content, "Debug");

            GameObject tools = null;
            UIBuilder.Toggle(content, "Debug mode", s.DebugMode, v =>
            {
                s.DebugMode = v;
                if (tools != null) tools.SetActive(v);
                notify?.Invoke();
            });

            tools = UIBuilder.Rect("DebugTools", content);
            var vlg = tools.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 2f;
            var t = tools.transform;

            // Re-scan fonts (pick up newly dropped Fonts/ files), rebuild the panel, and
            // reapply everything — a soft reload without the UMM Ctrl+F10.
            UIBuilder.Button(t, "Force reload", MainClass.RequestForceReload);

            UIBuilder.TextInput(t, "Filter", GameProbe.Filter, v =>
            {
                GameProbe.Filter = v ?? "";
                if (GameFontApplier.DiagEnabled)
                    GameFontApplier.DiagFilter = string.IsNullOrEmpty(GameProbe.Filter) ? null : new[] { GameProbe.Filter };
            });
            UIBuilder.Button(t, "Dump texts", GameProbe.DumpTexts);
            UIBuilder.Button(t, "Dump images", GameProbe.DumpImages);
            UIBuilder.Button(t, "Dump assets (sprites/textures)", GameProbe.DumpAssets);

            string compType = "";
            UIBuilder.TextInput(t, "Component type", compType, v => compType = v);
            UIBuilder.Button(t, "Dump components", () => GameProbe.DumpComponents(compType));

            UIBuilder.Toggle(t, "Trace font sweep", GameFontApplier.DiagEnabled, v =>
            {
                GameFontApplier.DiagEnabled = v;
                GameFontApplier.DiagFilter = string.IsNullOrEmpty(GameProbe.Filter) ? null : new[] { GameProbe.Filter };
            });

            tools.SetActive(s.DebugMode);
        }

        public static void RefreshSavings()
        {
            if (_savingsText != null) _savingsText.text = SavingsLabel();
        }

        private static string SavingsLabel()
        {
            string savings;
            long bytes = MainClass.LastUnloadSavingsBytes;
            if (bytes < 0) savings = "----MB";
            else
            {
                float mb = bytes / (1024f * 1024f);
                savings = (mb >= 0f ? "+" : "") + mb.ToString("F2") + " MB";
            }
            return "RAM savings (last scene load): " + savings;
        }

        // Wrapping muted caption under a toggle. The page VLG controls child rects, so the
        // indent comes from a padded wrapper group rather than offsetMin; Wrap + the inner
        // group's childControlHeight lets the Text's preferred height drive the row height.
        private static void Desc(Transform parent, string text)
        {
            var wrap = UIBuilder.Rect("Desc", parent);
            var vlg = wrap.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(10, 4, 0, 6);

            var t = UIBuilder.Label(wrap.transform, text, (int)UIBuilder.LabelFontSize - 2, TextAnchor.UpperLeft, Theme.TextMuted);
            t.textWrappingMode = TextWrappingModes.Normal;
        }
    }
}
