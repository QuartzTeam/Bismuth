using UnityEngine;
using UnityEngine.UI;

namespace Bismuth
{
    public partial class Overlay
    {
        public void ApplySettings(Settings settings)
        {
            PlaceRows(settings);
            ShowOrHideElements();

            bool ovr = settings.ShowOverlay;
            if (progressRow != null)     progressRow.SetActive(ovr && settings.ShowProgress);
            if (attemptsRow != null)     attemptsRow.SetActive(ovr && settings.ShowAttempts);
            if (accRow != null)          accRow.SetActive(ovr && settings.ShowAcc);
            if (xaccRow != null)         xaccRow.SetActive(ovr && settings.ShowXAcc);
            if (bpmRow != null)          bpmRow.SetActive(ovr && settings.ShowBpm);
            if (tileBpmRow != null)      tileBpmRow.SetActive(ovr && settings.ShowTileBpm);
            if (timingScaleRow != null)  timingScaleRow.SetActive(ovr && settings.ShowTimingScale);
            if (judgementsRow != null)   judgementsRow.SetActive(ovr && settings.ShowJudgements);
            if (comboDisplayContainer != null)
            {
                comboDisplayContainer.gameObject.SetActive(settings.ShowComboDisplay);
                comboDisplayContainer.anchoredPosition = new Vector2(0f, settings.ComboDisplayY);
                comboDisplayContainer.localScale = Vector3.one * settings.ComboDisplaySize;
                if (_comboLabelWrapper != null)
                    _comboLabelWrapper.anchoredPosition = new Vector2(0f, settings.ComboLabelY);
            }
            if (comboDisplayLabel != null)
            {
                comboDisplayLabel.text = settings.ComboDisplayText;
                comboDisplayLabel.fontSize = Mathf.RoundToInt(ComboLabelBaseFontSize * settings.ComboLabelSize);
            }
            float sh = settings.ComboShadowSize;
            if (_comboValueShadow != null) _comboValueShadow.effectDistance = new Vector2(sh, -sh);

            if (timingScaleContainer != null)
            {
                timingScaleContainer.anchoredPosition = new Vector2(0f, settings.TimingScaleY);
                timingScaleContainer.localScale = Vector3.one * settings.TimingScaleSize;
            }
            if (judgementsContainer != null)
            {
                judgementsContainer.anchoredPosition = new Vector2(0f, settings.JudgementsY);
                judgementsContainer.localScale = Vector3.one * settings.JudgementsSize;
            }

            if (attemptsContainer != null)
            {
                var anchor = new Vector2(settings.AttemptsX, settings.AttemptsY);
                attemptsContainer.anchorMin = anchor;
                attemptsContainer.anchorMax = anchor;
            }

            if (fpsContainer != null) fpsContainer.SetActive(settings.ShowFps);

            float scale = settings.Scale;
            if (leftContainer != null)  leftContainer.localScale  = Vector3.one * scale;
            if (rightContainer != null) rightContainer.localScale = Vector3.one * scale;
        }

        private void PlaceRows(Settings settings)
        {
            if (progressRow != null)  progressRow.transform.SetParent(null, false);
            if (accRow != null)       accRow.transform.SetParent(null, false);
            if (xaccRow != null)      xaccRow.transform.SetParent(null, false);
            if (bpmRow != null)       bpmRow.transform.SetParent(null, false);
            if (tileBpmRow != null)   tileBpmRow.transform.SetParent(null, false);

            Attach(progressRow,  settings.ProgressPosition);
            Attach(accRow,       settings.AccPosition);
            Attach(xaccRow,      settings.XAccPosition);
            Attach(bpmRow,       settings.BpmPosition);
            Attach(tileBpmRow,   settings.TileBpmPosition);
        }

        private void Attach(GameObject row, OverlayPosition pos)
        {
            if (row == null) return;
            row.transform.SetParent(pos == OverlayPosition.Right ? rightContainer : leftContainer, false);
        }

        public void ShowOrHideElements()
        {
            ApplyLevelNameTransform();
            var settings = MainClass.Settings;
            bool hideAll        = settings.HideAllUI;
            bool hideAutoIcon   = hideAll || settings.HideAutoplayIcon;
            bool hideNoFail     = hideAll || settings.HideNoFail;
            bool hideDifficulty = hideAll || settings.HideDifficulty;

            // RDC.noHud dereferences RDC.data; both can NRE during startup.
            try { if (RDConstants.data == null) return; }
            catch { return; }
            RDC.noHud = hideAll;

            var ctrl = scrController.instance;
            if (ctrl?.errorMeter != null && ctrl.gameworld && scnEditor.instance == null)
                ctrl.errorMeter.gameObject.SetActive(!hideAll && !settings.HideHitmeter);

            var editor = scnEditor.instance;
            if (editor != null)
            {
                if (editor.buttonNoFail != null)
                    editor.buttonNoFail.gameObject.SetActive(!hideNoFail);
                if (editor.editorDifficultySelector != null)
                    editor.editorDifficultySelector.gameObject.SetActive(!hideDifficulty);
                if (editor.autoImage != null)
                    editor.autoImage.enabled = !hideAutoIcon;
                if (editor.buttonAuto != null)
                    editor.buttonAuto.enabled = !hideAutoIcon;
            }

            var uic = scrUIController.instance;
            if (uic != null)
            {
                if (uic.noFailImage != null)      uic.noFailImage.enabled = !hideNoFail;
                if (uic.difficultyImage != null)   uic.difficultyImage.enabled = !hideDifficulty;
                // The gameplay HUD's difficulty widget leaks into the editor when a level is
                // loaded via the custom-levels-menu editor button; force-hide there.
                bool inEditor = scnEditor.instance != null;
                if (uic.difficultyContainer != null)
                {
                    bool show = !hideDifficulty && !inEditor && uic.difficultyUIMode != DifficultyUIMode.DontShow;
                    if (uic.difficultyContainer.gameObject.activeSelf != show)
                        uic.difficultyContainer.gameObject.SetActive(show);
                }
                if (uic.difficultyFadeContainer != null)
                {
                    bool show = !hideDifficulty && !inEditor && uic.difficultyUIMode != DifficultyUIMode.DontShow;
                    if (uic.difficultyFadeContainer.gameObject.activeSelf != show)
                        uic.difficultyFadeContainer.gameObject.SetActive(show);
                }

                if (hideDifficulty)
                {
                    var indicators = Object.FindObjectsByType<DifficultyIndicator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (var ind in indicators)
                        if (ind != null && ind.gameObject.activeSelf)
                            ind.gameObject.SetActive(false);
                }

                if (hideAll || settings.HideBetaBuild)
                {
                    var betas = Object.FindObjectsByType<scrEnableIfBeta>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (var b in betas)
                        if (b != null && b.gameObject.activeSelf)
                            b.gameObject.SetActive(false);
                }
                if (uic.txtLevelName != null)
                    uic.txtLevelName.gameObject.SetActive(!hideAll && !settings.HideLevelName);
            }
        }

        public void ApplyLevelNameTransform()
        {
            var ctrl = scrController.instance;
            if (ctrl?.txtLevelName == null) return;
            var settings = MainClass.Settings;
            var rt = ctrl.txtLevelName.rectTransform;
            if (!_levelNameOrigPos.HasValue)
                _levelNameOrigPos = rt.anchoredPosition;
            rt.localScale = Vector3.one * settings.LevelNameScale;
            rt.anchoredPosition = _levelNameOrigPos.Value + new Vector2(0f, settings.LevelNameY);
            ctrl.txtLevelName.gameObject.SetActive(!settings.HideAllUI && !settings.HideLevelName);
        }
    }
}
