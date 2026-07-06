# Logging

## Log locations

Bismuth can run under two loader stacks. The log paths differ — check the right one for the current install.

### Native UMM (UnityModManager directly)

| Log | Path |
| --- | ---- |
| UMM log (`modEntry.Logger.Log`) | `…/ADanceOfFireAndIce.app/Contents/Resources/Data/Managed/UnityModManager/Log.txt` |
| Unity player log (`Debug.Log`) | `~/Library/Logs/7th Beat Games/A Dance of Fire and Ice/Player.log` |
| Bismuth log (`BismuthLog.Log`) | `<ModPath>/BismuthLog.txt` → `…/Mods/Bismuth/BismuthLog.txt` |

Full UMM path: `/Users/preluminance/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice/ADanceOfFireAndIce.app/Contents/Resources/Data/Managed/UnityModManager/Log.txt`

### MelonLoader + UMMCompat plugin

UMMCompat loads UMM-style mods from `UMMMods/` (not `Mods/`), and `modEntry.Path` resolves there — so `BismuthLog.txt` follows the dll into `UMMMods/Bismuth/`.

| Log | Path |
| --- | ---- |
| MelonLoader log | `…/A Dance of Fire and Ice/MelonLoader/Logs/<YY-M-D_H-M-S>.log` (one file per launch; latest = most recently modified) |
| UMMCompat / per-mod console output | Mixed into the MelonLoader log, prefixed `[UMMCompat]` / `[<ModName>]` |
| Unity player log (`Debug.Log`) | `~/Library/Logs/7th Beat Games/A Dance of Fire and Ice/Player.log` |
| Bismuth log (`BismuthLog.Log`) | `<ModPath>/BismuthLog.txt` → `…/UMMMods/Bismuth/BismuthLog.txt` |

Full MelonLoader log dir: `/Users/preluminance/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice/MelonLoader/Logs/`  
Full UMMMods Bismuth log: `/Users/preluminance/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice/UMMMods/Bismuth/BismuthLog.txt`

`deploy.sh` writes to `Mods/Bismuth/`, but UMMCompat appears to sweep mods into `UMMMods/` on startup — so the live dll, settings, and log all end up there. If `Mods/Bismuth/BismuthLog.txt` is stale or missing, check `UMMMods/Bismuth/BismuthLog.txt` instead.

### Log lifecycle

`BismuthLog.txt` is cleared and re-created each session (on `StartMod`). Call `BismuthLog.Log("message")` from anywhere — swallows IO errors silently. If `StartMod` never runs (mod disabled in the manager UI, or load failure), the log keeps its previous-session contents and won't reflect the current launch — so a stale-looking log usually means "mod didn't start," not "logger broke."

## BismuthLog (`BismuthLog.cs`)

Static session logger writing to `<ModPath>/BismuthLog.txt`.

Lives in the mod folder (`MainClass.ModPath`), so all Bismuth-owned persistent data sits in one directory. On macOS that is typically `…/Steam/steamapps/common/A Dance of Fire and Ice/Mods/Bismuth/BismuthLog.txt`.

| Method | Purpose |
| ------ | ------- |
| `Init()` | Called by `MainClass.StartMod` — clears the file and writes a timestamped session header |
| `Log(message)` | Appends `[HH:mm:ss] message\n`; no-op if `Init` failed; swallows IO exceptions. Plain file IO — safe from background threads |
| `Debug(message)` | `Log` with a `"[dbg] "` prefix, for high-frequency diagnostics (hook traces, per-attempt dumps). The in-game viewer hides `[dbg]` lines unless its Debug toggle is on |
| `ReadTail(maxChars)` | Tail of the current log for the in-game viewer, capped ~12k chars (uGUI Text 65k-vertex limit) |

Use `BismuthLog.Log(...)` for any Bismuth-specific diagnostic output, `Debug(...)` for anything per-hook/per-attempt. The UMM log (`modEntry.Logger`) is still used by `FontLoader` for font load results.

### LogViewer (`UI/LogViewer.cs`)

Misc → "View log" opens a standalone window (own canvas) showing `ReadTail()`, auto-scrolled to the newest lines. Buttons: Refresh, **Clear** (`BismuthLog.Clear`), Open in File Manager (`OsShell.OpenFolder(ModPath)`), Close. Each line is **numbered** — the number is a clickable TMP `<link>` (`LogLineCopier` → `FindIntersectingLink` → copies that raw line to the clipboard); the line's own `<color>`/`<size>` tags are wrapped in `<noparse>` so they show literally. `[dbg]` lines are hidden unless Debug mode is on.


