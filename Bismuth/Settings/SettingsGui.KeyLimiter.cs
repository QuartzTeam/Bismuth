using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bismuth
{
    internal static partial class SettingsGui
    {
        private static bool _klListening = false;

        private static void DrawKeyLimiterSection(Settings settings, ref bool changed)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button((_keyLimiterOpen ? "▼" : "►") + " Key Limiter", GUILayout.ExpandWidth(false)))
                _keyLimiterOpen = !_keyLimiterOpen;
            bool klEnabled = GUILayout.Toggle(settings.KeyLimiterEnabled, " Enabled");
            if (klEnabled != settings.KeyLimiterEnabled) { settings.KeyLimiterEnabled = klEnabled; changed = true; }
            GUILayout.EndHorizontal();

            if (!_keyLimiterOpen) return;

            Indent(() => GUILayout.Label(
                "Blocks keys not in the registered set. Esc always passes; P and Space pass when not in active play.",
                _noWrapLabel), 20f);
            GUILayout.Space(4f);

            GUILayout.BeginHorizontal();
            GUILayout.Space(20f);
            bool useKv = GUILayout.Toggle(settings.KeyLimiterUseKvKeys, " Use Key Viewer keys (active preset)");
            GUILayout.EndHorizontal();
            if (useKv != settings.KeyLimiterUseKvKeys) { settings.KeyLimiterUseKvKeys = useKv; changed = true; }

            if (settings.KeyLimiterUseKvKeys) return;

            // ── Custom allowed keys ──────────────────────────────────────────
            Indent(() => GUILayout.Label("Allowed keys:", _noWrapLabel), 20f);

            // Tokens are stored as a single space-separated string. Parse → grid → join back.
            var tokens = new List<string>();
            if (!string.IsNullOrWhiteSpace(settings.KeyLimiterCustomKeys))
                foreach (var t in settings.KeyLimiterCustomKeys.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
                    tokens.Add(t);

            bool tokensChanged = false;
            int removeAt = -1;

            GUILayout.BeginHorizontal();
            GUILayout.Space(20f);
            if (GUILayout.Button(_klListening ? "■ Stop" : "● Listen", GUILayout.ExpandWidth(false)))
                _klListening = !_klListening;
            for (int i = 0; i < tokens.Count; i++)
            {
                string label = KeyViewer.TryParseKey(tokens[i], out KeyCode kc)
                    ? PrettyKeyLabel(kc, tokens[i])
                    : tokens[i];
                if (GUILayout.Button("× " + label, GUILayout.ExpandWidth(false))) removeAt = i;
            }
            GUILayout.EndHorizontal();

            if (_klListening)
            {
                KeyCode pressed = ListenForKey();
                if (pressed != KeyCode.None && pressed != KeyCode.Escape)
                {
                    string tok = TokenFromKeyCode(pressed);
                    int existing = tokens.IndexOf(tok);
                    if (existing >= 0) tokens.RemoveAt(existing);
                    else tokens.Add(tok);
                    tokensChanged = true;
                }
            }

            if (removeAt >= 0) { tokens.RemoveAt(removeAt); tokensChanged = true; }

            if (tokensChanged)
            {
                settings.KeyLimiterCustomKeys = string.Join(" ", tokens.ToArray());
                changed = true;
            }
        }

        private static void DrawChatterBlockerSection(Settings settings, ref bool changed)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button((_chatterBlockerOpen ? "▼" : "►") + " Chatter Blocker", GUILayout.ExpandWidth(false)))
                _chatterBlockerOpen = !_chatterBlockerOpen;
            bool cbEnabled = GUILayout.Toggle(settings.ChatterBlockerEnabled, " Enabled");
            if (cbEnabled != settings.ChatterBlockerEnabled) { settings.ChatterBlockerEnabled = cbEnabled; changed = true; }
            GUILayout.EndHorizontal();

            if (!_chatterBlockerOpen) return;

            Indent(() => GUILayout.Label(
                "Drops a key press if it fires within the threshold of the same key's previous accepted press.",
                _noWrapLabel), 20f);
            GUILayout.Space(4f);

            int ms = settings.ChatterThresholdMs;
            SettingsInput.Slider("Threshold", ref ms, 1, 200, 20f, "F0", " ms");
            if (ms != settings.ChatterThresholdMs) { settings.ChatterThresholdMs = ms; changed = true; }
        }
    }
}
