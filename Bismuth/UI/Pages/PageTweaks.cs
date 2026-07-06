using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bismuth.UI.Pages
{
    // "Tweaks" tab — small game-behaviour overrides.
    internal static class PageTweaks
    {
        public static void Build(PageStack stack)
        {
            var content = stack.Root;
            var s = UICore.Settings;
            var notify = UICore.OnSettingsChanged;

            UIBuilder.SectionHeaderWithHelp(content, "Autoplay",
                "Pauses/resumes autoplay while play-testing a level in the editor\n(the game " +
                "hardcodes Space). Turn it off entirely, or rebind:\nclick the button, then press a key.");

            UIBuilder.Collapsible(content, "Enable autoplay pause", s.AutoplayPauseEnabled,
                v => { s.AutoplayPauseEnabled = v; notify?.Invoke(); }, null);

            // A hidden per-frame key listener drives the rebind; it reads input directly
            // (exempt from the menu-open input block), so it works with the panel open.
            var listener = UIBuilder.Rect("AutoPauseKeyListener", content).AddComponent<KeyListener>();

            TextMeshProUGUI btnLabel = null; // set after the button is built; captured by the closures
            var btn = UIBuilder.Button(content, KeyLabel(s.AutoplayPauseKey), () =>
            {
                listener.Active = true;
                if (btnLabel != null) btnLabel.text = "Press a key…";
            });
            btnLabel = btn.GetComponentInChildren<TextMeshProUGUI>();

            listener.OnKey = kc =>
            {
                listener.Active = false;
                s.AutoplayPauseKey = kc;
                notify?.Invoke();
                if (btnLabel != null) btnLabel.text = KeyLabel(s.AutoplayPauseKey);
            };

            UIBuilder.Spacer(content);
            UIBuilder.SectionHeaderWithHelp(content, "Editor",
                "Shows the selected tile's angle at the top of the\nlevel editor (180° = straight).");
            UIBuilder.Collapsible(content, "Show selected tile angle", s.EditorTileAngle,
                v => { s.EditorTileAngle = v; notify?.Invoke(); }, null);

            UIBuilder.Spacer(content);
            UIBuilder.SectionHeader(content, "Custom levels");
            UIBuilder.Slider(content, "Preview volume %", s.ClsPreviewVolume * 100f, 0f, 100f,
                v =>
                {
                    s.ClsPreviewVolume = v / 100f;
                    Tweaks.ApplyClsPreviewVolume();
                    notify?.Invoke();
                }, "0", 1f);
        }

        private static string KeyLabel(KeyCode kc) =>
            "Pause key: " + KeyTokens.PrettyTokenLabel(KeyTokens.TokenFromKeyCode(kc));
    }
}
