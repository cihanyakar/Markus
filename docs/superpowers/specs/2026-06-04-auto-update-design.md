# Auto-Update via GitHub Releases (Design)

- Date: 2026-06-04
- Status: Approved (design), pending implementation plan
- Repo: `cihanyakar/Markus`

## 1. Goal and chosen behavior

Markus should let users learn about and obtain newer versions from GitHub Releases without leaving the app. The chosen automation level is **notify plus one-click download**, not silent in-place replacement.

Decisions locked in during brainstorming:

- **Automation level.** Notify the user, then on one click download the correct artifact, verify it, and hand it to the OS to open. The final install step (drag to Applications, extract the zip) stays with the user.
- **Trigger.** Auto-check on launch (debounced to roughly once per day) plus a manual "Check for Updates..." menu item.
- **On click.** Download the matching artifact with the app's own `HttpClient`, verify the `.sha256` sidecar, then open the file via the OS (on macOS the dmg mounts and shows the "drag to Applications" window). Because the app downloads the file itself rather than a browser, macOS does not attach the `com.apple.quarantine` attribute, so Gatekeeper does not prompt.
- **Channel.** Selectable in Settings, default Stable. Stable ignores prereleases; Prerelease considers them too.

## 2. Distribution context (already in place)

- Versioning is driven by `release-please` plus MinVer. MinVer stamps `AssemblyInformationalVersion` from the most recent `v*` tag. Latest tag at design time is `v0.4.0`.
- On `release: published`, `.github/workflows/release-binaries.yml` builds four runtime identifiers and uploads each artifact plus a `.sha256` sidecar to the GitHub Release:
  - `win-x64` as `.zip`
  - `osx-x64` as `.dmg`
  - `osx-arm64` as `.dmg`
  - `linux-x64` as `.tar.gz`
- Artifact naming is `Markus-<tag>-<rid>.<ext>`, for example `Markus-v0.4.0-osx-arm64.dmg`. The RID string appears in the file name, which makes asset selection a simple name match.
- The distributed macOS build is NativeAOT and ad-hoc signed, not notarized. This matters two ways. First, JSON handling must be source-generated (no reflection-based serialization). Second, self-downloaded files avoid quarantine, so the chosen "open the file" flow does not trigger Gatekeeper.

## 3. Architecture and isolation

The only component that touches the network is hidden behind `IReleaseFeed`. Every decision is a pure function so it can be unit-tested without network or filesystem.

| Component | Responsibility | Test approach |
|---|---|---|
| `SemVer` | Parse and compare versions, including prerelease precedence. Build metadata after `+` is ignored. | Unit |
| `IReleaseFeed` then `GitHubReleaseFeed` | Call the GitHub releases API and map DTOs to `ReleaseInfo`. | Manual (network) |
| `ReleaseAssetSelector` | Map a RID to the correct asset using the `Markus-<tag>-<rid>.<ext>` naming. | Unit |
| `UpdateChecker` | Apply the channel filter, decide whether a release is newer, select the matching asset, return `UpdateCheckResult`. | Unit with a fake feed |
| `Sha256Verifier` | Parse the `.sha256` sidecar and compare hex digests. | Unit |
| `IUpdateDownloader` then `UpdateDownloader` | Download the artifact and its `.sha256`, verify. | Thin layer, manual |
| `IUpdateLauncher` then `UpdateLauncher` | Open the downloaded file via the OS, with a release-page fallback. | Manual |
| `IVersionProvider` then `AssemblyVersionProvider` | Read the running version from `AssemblyInformationalVersion`. | Unit with a fake |
| `UpdateViewModel` | Banner state and commands (Check, Download, Dismiss, Skip). | Unit with fake services |

### Proposed file layout

New files live under a dedicated `Services/Updates/` folder to keep the surface focused.

- `src/Markus/Models/SemVer.cs`
- `src/Markus/Models/ReleaseInfo.cs` (also `ReleaseAsset`, `UpdateChannel`, `UpdateCheckResult`)
- `src/Markus/Services/Updates/GitHubRelease.cs` (DTOs)
- `src/Markus/Services/Updates/GitHubReleaseJsonContext.cs` (source-generated)
- `src/Markus/Services/Updates/IReleaseFeed.cs`, `GitHubReleaseFeed.cs`
- `src/Markus/Services/Updates/UpdateChecker.cs`
- `src/Markus/Services/Updates/ReleaseAssetSelector.cs`
- `src/Markus/Services/Updates/Sha256Verifier.cs`
- `src/Markus/Services/Updates/IUpdateDownloader.cs`, `UpdateDownloader.cs`
- `src/Markus/Services/Updates/IUpdateLauncher.cs`, `UpdateLauncher.cs`
- `src/Markus/Services/Updates/IVersionProvider.cs`, `AssemblyVersionProvider.cs`
- `src/Markus/ViewModels/UpdateViewModel.cs`
- View changes in `MainWindow.axaml` (banner), Settings UI, and menu wiring in `AppCommands`.
- Tests under `tests/Markus.Tests/Models/` and `tests/Markus.Tests/Services/Updates/`.

## 4. Data flow

### Launch

1. If the process is a `--spawned` child, do nothing. This reuses the existing spawn-marker concept and avoids duplicate checks across windows.
2. If `CheckForUpdatesOnLaunch` is off, do nothing.
3. If `LastUpdateCheckUtc` is newer than the debounce window (roughly 20 hours), do nothing.
4. Otherwise run `UpdateChecker.CheckAsync` as fire-and-forget. On a positive result whose version is not equal to `SkippedVersion`, show the banner. Update `LastUpdateCheckUtc` regardless. Swallow errors silently and log them.

### Manual check

Menu item runs `CheckForUpdatesCommand`, which ignores the debounce and always reports a result. The three outcomes are an update banner, an "up to date" confirmation, or a friendly error.

### Download

`DownloadUpdateCommand` downloads the artifact and its `.sha256` into `Application Support/Markus/updates/`, verifies the digest, then calls `IUpdateLauncher` to open the file. On macOS this mounts the dmg and shows the drag-to-Applications view. No quarantine is attached because the app downloaded the file itself.

## 5. Version comparison and asset selection

- The running version comes from MinVer's `AssemblyInformationalVersion`, for example `0.4.0` or `0.4.1-alpha.0.5+sha`. Anything after `+` is dropped before parsing.
- GitHub DTOs (`tag_name`, `name`, `body`, `prerelease`, `draft`, `assets[].browser_download_url`, `assets[].name`, `assets[].size`) are parsed with a source-generated `GitHubReleaseJsonContext` using snake_case naming. This keeps NativeAOT working and adds no NuGet dependency.
- The GitHub API requires a `User-Agent` header. The feed sends `User-Agent: Markus/<version>`. An unauthenticated request is used (60 requests per hour is plenty for this flow).
- The current RID is determined at runtime via `RuntimeInformation`. `ReleaseAssetSelector` matches the asset whose name contains the RID. When no asset matches, the flow falls back to opening the release HTML page in the browser.

## 6. Settings additions

`AppSettings` gains the following, mirrored in `Clone()`. The existing JSON context already serializes string enums.

- `bool CheckForUpdatesOnLaunch = true`
- `UpdateChannel UpdateChannel = UpdateChannel.Stable`
- `DateTimeOffset? LastUpdateCheckUtc`
- `string? SkippedVersion`

## 7. UI

- A dismissible banner at the top of `MainWindow` when an update is available. It shows the new version, with actions Download, Release notes, Skip this version, and Dismiss.
- A "Check for Updates..." menu item wired through `AppCommands`, matching the existing About and Preferences pattern.
- A new "Updates" Settings category with a "Check for updates on launch" toggle and a channel dropdown (Stable, Prerelease).
- The About window shows the current version and a "Check for Updates" button.

## 8. Error handling and edge cases

- Offline, DNS failure, or timeout. Silent on launch, friendly message on manual check.
- GitHub rate limit (HTTP 403). Treated as "could not check," no repeated attempts within the debounce window.
- No matching asset for the current RID. Fall back to opening the release HTML page.
- SHA256 mismatch. Abort, delete the partial download, show an error, and offer to open the release page.
- Draft releases are always ignored. Prereleases are considered only when the channel is Prerelease.
- Untagged dev builds. Skip the auto-check so local development is not nagged. The manual check still works.
- Multiple windows. Each window is its own process, so the launch check is gated to non-spawned processes and the shared `LastUpdateCheckUtc` in settings prevents repeated network calls during rapid spawns.

## 9. Test strategy (TDD)

Unit tests (xUnit v3 plus Shouldly, no network or real filesystem):

- `SemVer` parsing and comparison, including prerelease precedence and ignored build metadata.
- `ReleaseAssetSelector` for each RID and the no-match case.
- `UpdateChecker.CheckAsync` with a fake `IReleaseFeed`: newer, older, equal, channel filtering (prerelease excluded on Stable), skipped version, and the no-asset fallback.
- `Sha256Verifier` for the `<hash>  <name>` sidecar format and case-insensitive hex comparison.
- `ShouldAutoCheck(lastCheckUtc, now, enabled, isSpawned)` as a pure debounce decision.
- JSON round-trip for the new settings fields, including defaults.

Manual and integration checks (documented, not automated in this environment):

- A real GitHub fetch against the public repo.
- A real download plus verification.
- macOS dmg mount and the drag-to-Applications view.
- Banner and Settings UI behavior.

## 10. Out of scope (YAGNI)

- Silent in-place replacement and a relaunch trampoline.
- Delta or binary patching.
- A periodic background timer while the app is open.
- Any code-signing or notarization change.
- Automatic install. The final drag or extract stays with the user.
- Multi-repo or enterprise configuration.
