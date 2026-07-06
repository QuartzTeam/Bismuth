# Self-Updater

## Self-updater (`Util/UpdateChecker.cs` + `UI/UpdatePopup.cs`)

UMMCompat doesn't run UMM's own updater, so the mod updates itself. `UpdateChecker.Begin(modEntry)` (from `TryEagerInit`) fetches `Repository.json` (the URL comes from Info.json's `Repository` field, kept current by `release.sh`). A newer `Releases[0].Version` shows `UpdatePopup` with three buttons: **Update now (requires restart)**, **Manual update** (opens the releases page derived from `DownloadUrl`), and **Later**.

"Update now" downloads the release zip and extracts the `Bismuth/` payload over every existing install dir, running copy last, dll last within each (UMM watches it for hot reload). Persistent data survives by construction: the zip carries only the payload and nothing is deleted, and `MainClass.PersistNow()` flushes settings/counts first. On success the primary button becomes **Close**.

**Dual-install handling.** Native UMM loads from `<game>/Mods/Bismuth`, MelonLoader+UMMCompat from `<game>/UMMMods/Bismuth`. When both exist (and `Settings.IgnoreDuplicateInstall` is unset), `DuplicateInstallPopup` asks which loader is in use and offers to delete the unused copy. Deleting the _running_ copy first flushes and carries `Settings.xml`/`keycounts.txt`/`BismuthAttempts.txt` to the kept dir. The version check runs after the prompt resolves.

### MelonLoader/UMMCompat landmines (all hit while building this)

1. **`modEntry.Info.Repository` is not populated** from Info.json — the checker has a hardcoded fallback URL.
2. **UnityWebRequest coroutines silently never resume** — no timeout, no error, no completion. Networking runs on the ThreadPool, results drained by `Update()` under a lock.
3. **Child processes inherit `DYLD_INSERT_LIBRARIES`** (MelonLoader's arm64 bootstrap dylib), which dyld refuses to load into arm64e system binaries — curl dies with exit 134. `CurlFetch` strips `DYLD_*`/`LD_PRELOAD` from the child env. Applies to ANY `Process.Start` from the mod.
4. **`JsonUtility.FromJson` silently returned nothing** for Repository.json — the two fields are parsed with regexes instead.

Transport order: curl subprocess (macOS / Win10+ / Linux) → .NET `WebClient` fallback (TLS 1.2 forced via `ServicePointManager`). Every stage logs to BismuthLog (`Update check: …`, `Update worker: …`).

### Popup canvases: sortingOrder is signed 16-bit

`Canvas.sortingOrder` wraps beyond 32767 (35000 became −30536 and rendered _under_ the settings panel). Layering: LocationEditor 31000 < settings panel 32000 < LogViewer 32500 < UpdatePopup 32600 < DuplicateInstallPopup 32601. Never exceed 32767.

