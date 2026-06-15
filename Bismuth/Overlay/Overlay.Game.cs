using System;
using TMPro;
using UnityEngine;

namespace Bismuth
{
    public partial class Overlay
    {
        public void OnAttempt()
        {
            bool atCheckpoint = false;
            try { atCheckpoint = GCS.checkpointNum > 0; } catch { }

            // Diagnostic for open official-level judgement-saving investigation
            try
            {
                var trk = scrMistakesManager.marginTrackers;
                var t0 = (trk != null && trk.Length > 0) ? trk[0] : null;
                int hmCount = t0?.hitMargins?.Count ?? -1;
                int last = t0?.lastHitMarginsSize ?? -1;
                int perfectCount = (t0?.hitMarginsCount != null && t0.hitMarginsCount.Length > 3) ? t0.hitMarginsCount[3] : -1;
                BismuthLog.Debug($"OnAttempt: checkpointNum={GCS.checkpointNum} atCp={atCheckpoint} hm.Count={hmCount} lastHmSize={last} hmc[Perfect]={perfectCount} chkUsed={scrController.checkpointsUsed}");
            }
            catch (Exception ex) { BismuthLog.Debug("OnAttempt diag failed: " + ex.Message); }

            if (atCheckpoint)
            {
                SyncFromTracker();
            }
            else
            {
                /* Game only zeroes checkpointsUsed when Start runs with checkpointNum == 0.
                   Without this, a stuck checkpointNum keeps the 0.9875^used XAcc penalty
                   across fresh attempts */
                try { scrController.checkpointsUsed = 0; } catch { }
                ShowEmpty();
            }
        }

        private void SyncFromTracker()
        {
            // Invalidate display cache so per-frame Update re-renders fresh values
            _lastProgressT = -1f;
            _lastBpm = -1f;
            _lastTileBpmVal = -1f;
            _lastTimingScale = -1f;
            _lastComboDisplay = -1;

            var trackers = scrMistakesManager.marginTrackers;
            var tracker = (trackers != null && trackers.Length > 0) ? trackers[0] : null;
            if (tracker == null) return;

            /* Walk saved prefix (hitMargins[0..lastHitMarginsSize]), not live hitMarginsCount:
               scnGame.Play skips RevertToLastCheckpoint outside editor, so on revive tracker
               still contains every pre-death hit */
            for (int i = 0; i < _judgementCounts.Length; i++) _judgementCounts[i] = 0;

            var hm = tracker.hitMargins;
            int depth = tracker.lastHitMarginsSize;
            int cap = hm != null ? System.Math.Min(depth, hm.Count) : 0;
            for (int i = 0; i < cap; i++)
            {
                int mi = (int)hm[i];
                if (mi >= 0 && mi < _judgementCounts.Length) _judgementCounts[mi]++;
            }

            // Rebuild combo by walking saved prefix backwards until streak breaks
            _combo = 0;
            if (hm != null)
            {
                var s = MainClass.Settings;
                for (int i = cap - 1; i >= 0; i--)
                {
                    var m = hm[i];
                    if (m == HitMargin.Perfect) { _combo++; continue; }
                    if (m == HitMargin.Auto)
                    {
                        if (s != null && s.ComboCountAuto) { _combo++; continue; }
                        // ComboCountAuto=false: auto neither breaks nor extends streak
                        continue;
                    }
                    break;
                }
            }

            // Suppress saved-checkpoint accuracy on attempt start. The next AddHit repaints it.
            var dim = new Color(0.7f, 0.7f, 0.7f);
            if (accValue != null)  { accValue.text  = "--.--%"; accValue.color  = dim; }
            if (xaccValue != null) { xaccValue.text = "--.--%"; xaccValue.color = dim; }

            RefreshDisplay(includeAccuracy: false);
        }

        public void OnLevelStart(bool isRestart)
        {
            string key = GetLevelKey();
            /* Full attempts only count starts from 0% (no checkpoint loaded). Regular
               attempts count every attempt, including checkpoint restarts */
            bool atCp = false;
            try { atCp = GCS.checkpointNum > 0; } catch { }

            if (isRestart && !RDC.auto)
            {
                // scnGame.Play(isRestart=true): in-game retry
                if (_currentLevelKey != null)
                {
                    _attempts++;
                    AttemptsStore.Set(_currentLevelKey, _attempts);
                    if (!atCp)
                    {
                        _fullAttempts++;
                        AttemptsStore.SetFull(_currentLevelKey, _fullAttempts);
                    }
                }
            }
            else if (!inLevel && !RDC.auto)
            {
                // Coming from outside (exit+re-enter or first play)
                if (key != null && key == _currentLevelKey)
                {
                    _attempts++;
                    AttemptsStore.Set(_currentLevelKey, _attempts);
                    if (!atCp)
                    {
                        _fullAttempts++;
                        AttemptsStore.SetFull(_currentLevelKey, _fullAttempts);
                    }
                }
                else
                {
                    _currentLevelKey = key;
                    _attempts = AttemptsStore.Get(_currentLevelKey);
                    _fullAttempts = AttemptsStore.GetFull(_currentLevelKey);
                }
            }
            _currentLevelKey = key ?? _currentLevelKey;
            inLevel = true;
            // Reuse OnAttempt so checkpoint sync from Awake_Rewind isn't wiped here
            OnAttempt();
            if (attemptsValue != null) attemptsValue.text = _attempts.ToString();
            if (attemptsFullValue != null) attemptsFullValue.text = _fullAttempts.ToString();
            ShowOrHideElements();
            /* Levels spawn HUD texts after scene loads, so catch them here too. Retries
               re-stamp only HUD fonts (rewind re-localization), so the full scene scan
               (a per-attempt hitch on large maps) is first-entry only. */
            if (isRestart) GameFontApplier.ReapplyHud();
            else GameFontApplier.Reapply();
            GameUiLayout.Reapply();
        }

        public void OnLevelEnd()
        {
            inLevel = false;
        }

        public void ShowEmpty()
        {
            _lastProgressT = -1f;
            _lastBpm = -1f;
            _lastTileBpmVal = -1f;
            _lastTimingScale = -1f;
            _lastComboDisplay = -1;

            var dim = new Color(0.7f, 0.7f, 0.7f);
            if (progressValue != null)  { progressValue.text  = "--.--%"; progressValue.color  = dim; }
            if (attemptsValue != null)  { attemptsValue.color = Color.white; }
            if (attemptsFullValue != null) { attemptsFullValue.color = Color.white; }
            _combo = 0;
            if (comboDisplayValue != null)  { comboDisplayValue.text = "0"; }
            if (comboDisplayLabel != null)  { comboDisplayLabel.color = Color.white; }
            if (accValue != null)      { accValue.text      = "--.--%"; accValue.color      = dim; }
            if (xaccValue != null)     { xaccValue.text     = "--.--%"; xaccValue.color     = dim; }
            if (bpmValue != null)      { bpmValue.text      = "---";    bpmValue.color      = dim; }
            if (tileBpmValue != null)      { tileBpmValue.text      = "---";    tileBpmValue.color      = dim; }
            if (timingScaleValue != null)  { timingScaleValue.text  = "---%";   timingScaleValue.color  = dim; }
            for (int i = 0; i < _judgementCounts.Length; i++) _judgementCounts[i] = 0;
            if (judgementTexts != null)
                for (int i = 0; i < judgementTexts.Length; i++)
                    if (judgementTexts[i] != null) { judgementTexts[i].text = "0"; judgementTexts[i].color = MarginColor(DisplayedMargins[i]); }
        }

        public void ResetAttempts()
        {
            _attempts = 0;
            _fullAttempts = 0;
            AttemptsStore.Set(_currentLevelKey, 0);
            AttemptsStore.SetFull(_currentLevelKey, 0);
            if (attemptsValue != null) attemptsValue.text = "0";
            if (attemptsFullValue != null) attemptsFullValue.text = "0";
        }

        /* labelFont/valueFont override stat rows label/value texts, and
           comboLabelFont/comboValueFont the combo display (null = font); judgements and FPS
           always use base font */
        public void SetFont(TMP_FontAsset font, TMP_FontAsset labelFont = null, TMP_FontAsset valueFont = null,
            TMP_FontAsset comboLabelFont = null, TMP_FontAsset comboValueFont = null)
        {
            if (font == null) return;
            var lf = labelFont ?? font;
            var vf = valueFont ?? font;
            if (progressLabel != null)  progressLabel.font  = lf;
            if (progressValue != null)  progressValue.font  = vf;
            if (attemptsLabel != null)  attemptsLabel.font  = lf;
            if (attemptsValue != null)  attemptsValue.font  = vf;
            if (attemptsFullLabel != null) attemptsFullLabel.font = lf;
            if (attemptsFullValue != null) attemptsFullValue.font = vf;
            if (accLabel != null)       accLabel.font       = lf;
            if (accValue != null)       accValue.font       = vf;
            if (xaccLabel != null)      xaccLabel.font      = lf;
            if (xaccValue != null)      xaccValue.font      = vf;
            if (bpmLabel != null)       bpmLabel.font       = lf;
            if (bpmValue != null)       bpmValue.font       = vf;
            if (tileBpmLabel != null)       tileBpmLabel.font       = lf;
            if (tileBpmValue != null)       tileBpmValue.font       = vf;
            if (timingScaleLabel != null)    timingScaleLabel.font    = lf;
            if (timingScaleValue != null)    timingScaleValue.font    = vf;
            if (comboDisplayLabel != null)   comboDisplayLabel.font   = comboLabelFont ?? font;
            if (comboDisplayValue != null)   comboDisplayValue.font   = comboValueFont ?? font;
            if (fpsText != null)             fpsText.font             = font;
            if (judgementTexts != null)
                foreach (var t in judgementTexts)
                    if (t != null) t.font = font;
            /* Changing the font asset swaps the material, dropping the underlay setup,
               so re-apply every shadow against the new per-text material instance. */
            foreach (var sh in GetComponentsInChildren<TmpShadow>(true))
                sh.Apply();
        }
    }
}
