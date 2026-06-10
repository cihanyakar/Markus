# Markus

Avalonia 12 (NativeAOT) markdown editor for macOS/Windows/Linux. .NET 11 preview, C#.

## Build and test

- `dotnet build src/Markus -c Debug` - dev build; binary at `src/Markus/bin/Debug/net11.0/Markus`
- `dotnet test tests/Markus.Tests` - full suite; pure-logic tests only, no Avalonia headless infra
- Never pipe build output through `tail` alone. Check for `error`/`Hata` lines; a failed build leaves a stale dll that silently runs old code.

## Release process (do not break this)

- Releases are driven by release-please. Push conventional commits to main; release-please opens or updates a `chore: release main` PR. **Releasing = merging that PR.** Merging tags the version, and the tag's release event triggers `release-binaries.yml`.
- **Never create releases or tags by hand** (`gh release create` is forbidden for normal flow). Manual releases desync `.release-please-manifest.json` and leave the old release PR with `autorelease: pending`, which makes release-please abort on every push ("untagged, merged release PRs outstanding").
- If a manual release ever happens anyway, repair with two steps. Relabel the stale release PR from `autorelease: pending` to `autorelease: tagged`, then commit `.release-please-manifest.json` bumped to the manually published version.
- Versions v0.6.0 to v0.7.4 were manual; automation was repaired at v0.7.4.

## Git hooks (husky)

- commit-msg enforces Conventional Commits; subject after `type(scope): ` must be ≤72 chars
- pre-commit runs csharpier on staged files; pre-push runs Release build + full tests + csharpier-check-all

## Lint gotchas (warnings-as-errors in CI)

- StyleCop SA1204 requires static members before non-static; SA1636 treats a comment at the very top of a file as a copyright header, so never start a file with `// ...`
- global.json pins SDK preview.4 with `rollForward: latestFeature`, so CI may build with a newer preview SDK and flag diagnostics (e.g. IDE0005) that the local SDK does not. Fix with a scoped pragma, not by removing code the local SDK needs.

## Avalonia gotchas

- `EffectiveViewportChanged` does NOT fire when a control's own `IsVisible` flips, and on a container toggle the viewport rect is often unchanged so it does not fire either. For deferred-while-hidden work use `Views/VisibilityActivation.Subscribe` (watches `IsVisible` on the control and its ancestor chain). `Visual.IsEffectivelyVisibleChanged` would be ideal but is internal.
- View layout in MainWindow.axaml has one editor/preview copy per view mode; Source-only and Preview-only panes toggle their own `IsVisible`, split panes sit in Grids that toggle theirs.

## Manual app verification

- Run `src/Markus/bin/Debug/net11.0/Markus /path/to/test.md`; settings live at `~/Library/Application Support/Markus/settings.json` (`defaultViewMode`, `codeTheme` affect what you see at launch)
- View modes via View menu or Cmd+1..5 (Source, Preview, SplitV, SplitH, Detached); status bar bottom-right shows the active mode
- Screenshot reliably by window ID, not by region (other windows steal focus): find the ID with Quartz `CGWindowListCopyWindowInfo`, then `screencapture -x -l<id>`
