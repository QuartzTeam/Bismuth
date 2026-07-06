# HarmonyX Patches

## HarmonyX patches (`Patches.cs`)

| Patch target | Timing | Action |
| ------------ | ------ | ------ |
| `scrMistakesManager.Reset` | Postfix | `OnAttempt()` |
| `scrMistakesManager.AddHit(HitMargin)` | Postfix | `UpdateDisplay(percentAcc, percentXAcc, hit)` |
| `scnGame.Play(seqID, isRestart)` | Postfix | `OnLevelStart(isRestart)` — custom levels; `isRestart` param distinguishes retry from first load |
| `scrPressToStart.ShowText` | Postfix | `OnLevelStart(false)` — official levels (always a fresh entry from this hook) |
| `scrController.StartLoadingScene` | Postfix | `OnLevelEnd()` |
| `scrUIController.WipeToBlack` | Postfix | `OnLevelEnd()` |
| `StateBehaviour.ChangeState(States.None)` | Postfix | `OnLevelEnd()`; every state change also requests delayed `GameFontApplier` sweeps + `GameUiLayout` re-applies (death/results text spawns late) |
| `scnEditor.ResetScene` | Postfix | `OnLevelEnd()` |
| `scnEditor.SwitchToEditMode` | Postfix | `ShowOrHideElements()` |
| `scrController.LevelNameTextRestore` | Postfix | `ApplyLevelNameTransform()` — re-applies our scale/offset after the game restores canonical position |
| `scrHitErrorMeter.UpdateLayout` | Postfix | `GameUiLayout.ApplyErrorMeter` — re-applies the meter position/scale override after the game's own layout pass |
| `scrShowIfDebug.Update` | Pre+Post | Temporarily sets `RDC.auto = false` (if `HideAutoplayText \|\| HideAllUI`) to suppress the autoplay text |
| `scrHitTextMesh.Show` | Prefix | Moves judgement popup off-screen (`HideAllUI`) or suppresses it (`HidePerfectJudgements`) |
| `scrHitText.Show(Vector3, float)` | Prefix | Moves legacy judgement text off-screen (`HideAllUI`) |
| `scrMissIndicator.Awake` | Postfix | Moves miss indicator off-screen (`HideAllUI`) |
| `scrPlanet.MoveToNextFloor` | Postfix | Hides error meter (`HideAllUI` or `HideHitmeter`) |
| `scrController.paused` (setter) | Postfix | Hides error meter (`HideAllUI` or `HideHitmeter`) |
| `OttoButtonController.Update` | Postfix | Hides Otto debug button (`HideAllUI`) |
| `RDInput.GetMain(ButtonState)` | Postfix | Key Limiter — clamps press count to allowed-key count when state=WentDown; zeroes it entirely while the Bismuth menu is open |
| `RDInput.WentDown(KeyCode)` / `RDInput.IsDown(KeyCode)` | Postfix | Menu input block — raw shortcut-key reads return false while the menu is open |
| `RDInput.GetState(InputAction, ButtonState)` | Postfix | Menu input block — Rewired action reads return false while the menu is open |
| `UnityEngine.Input.GetKeyDown(KeyCode)` | Postfix | Menu input block — direct polls (menu number-nav) return false while open, except KeyCode.B and `RawReadExempt` reads. Applied separately via `TryPatchRawInput` |
| `scrMistakesManager.AddHit(HitMargin)` | Prefix | Key Limiter — suppresses hit if no allowed key currently held, or unconditionally while the menu is open |
| `scnEditor.Update` | **Transpiler** | Tweaks tab — swaps the hardcoded `Ldc_I4 32` (KeyCode.Space) feeding the autoplay-pause `Input.GetKeyDown` for a `call Tweaks.AutoPauseKeyCode()`, so the pause key is rebindable/disableable. Fails safe: no pattern match → vanilla Space |
| `scnEditor.LateUpdate` | Postfix | `EditorLateUpdateShowHudPatch` — re-enables the HUD canvas while `GameUiEditor.IsActive` (the editor force-disables it outside play mode) |

### Optimizations (`Optimizations.cs`)

Independent file with Harmony patches gated on `Opt*` settings: `scrConductor.Update` (spectrum throttle), `TextureManager.LoadTexture` / `CustomTexture.GetTexture` / `CustomSprite.GetSprite` (non-readable / DXT), `scrPlanet.Update` / `scrFloor.Update` (physics non-alloc / DOTween fix).


