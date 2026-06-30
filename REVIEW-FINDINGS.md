# Markus — Review Findings Log

Living backlog from recurring whole-app reviews. Each pass takes a different
angle, fixes the obvious low-risk items inline (see git log on `fix/ux-pass`),
and records the larger or riskier findings here for later.

## Angles covered so far

- Behavioral UX dead-ends and data-loss paths (fixed in batch).
- OS-capability + best-practice + code-robustness sweep (partly fixed).
- Markdown authoring ergonomics.
- Performance & responsiveness.
- Settings validation & resilience.
- macOS quit-crash diagnosis + guard hardening.
- Error handling & user-facing feedback (this pass).

---

## Open findings (not yet fixed)

### P0 — macOS NativeAOT shutdown crash on quit (needs device testing)

A crash on quit was observed (SIGABRT) with this stack: `_handleAEQuit` ->
`-[NSApplication terminate:]` -> `exit` -> `__cxa_finalize_ranges` ->
`ComPtr<IAvnDispatcher>::~ComPtr()` -> managed VSD resolve -> CLR FailFast ->
abort. This is exactly the class of crash the existing guard
(`MacosAppleEventHandler.InstallTerminationGuard`, commit 6c80904) targets:
the AvaloniaNative dispatcher's C++ static destructor runs during atexit and
calls into an already-torn-down CLR. The guard overrides the app delegate's
`applicationWillTerminate:` to call `_exit(0)` (skipping atexit), and it is
still wired and correct in code, but it did not intercept this instance.

Contributing factors in the captured report: a very long uptime and memory
pressure ("VM Fault hit memory shortage"). Likely root cause of the miss: the
guard adds `applicationWillTerminate:` to the delegate class at startup, but
AppKit caches a delegate's `respondsToSelector:` for notification methods when
the delegate is set, so a method added afterward may not receive the
`NSApplicationWillTerminateNotification`.

APPLIED (needs verification): the guard now also registers the delegate as an
explicit `NSNotificationCenter` observer for
`NSApplicationWillTerminateNotification` with a `markusWillTerminate:` selector
that `_exit`s. An explicit `addObserver:selector:name:object:` is not subject to
the delegate-method `respondsToSelector:` caching, so the `_exit` should fire
reliably on quit; the existing `applicationWillTerminate:` override stays as a
fallback. Startup was smoke-tested (no regression), but the Cmd+Q /
AppleEvent-quit path cannot be reproduced headlessly (osascript cannot target
the non-bundle dev binary), so this needs interactive Cmd+Q verification on the
device. If a crash still occurs after this, the next levers are reverting the
`ShutdownCts.Cancel()`-on-`ShutdownRequested` removal as a precaution, or
`_exit`-ing from `applicationShouldTerminate:` after Avalonia's cleanup.

Context: this crash is on quit, after the app has saved and persisted the
session, so it does not lose data, and it is not a regression from the UX
branch (the crashed binary predates the macOS-integration commit and the guard
itself was untouched).

### P1 — Windows / Linux are barely usable

- **All shortcuts use `Cmd`/Meta**, which is the Win/Super key on Windows and
  Linux, so the entire app shortcut layer is dead there (and collides with OS
  reservations like Win+L/Win+R/Win+S). Fix: a platform "primary modifier"
  (`OperatingSystem.IsMacOS() ? Meta : Control`) feeding the default catalog
  gestures, the NativeMenu `Gesture="Cmd+…"` display, and the palette
  accelerator column. `src/Markus/Services/ShortcutActions.cs`. Effort M.
- **No menu bar on Windows/Linux**: `NativeMenu` only exports to the macOS
  system bar; there is no `<NativeMenuBar/>` / `<Menu>` in-window, so File/Edit/
  View, About, and Check-for-Updates are unreachable off macOS. Add a
  `NativeMenuBar` shown when `!OperatingSystem.IsMacOS()`. Effort M.
- **Shortcut hint glyphs hardcoded as `⌘`** in toolbar tooltips and the palette
  (`MainWindow.axaml`, `MainWindow.axaml.cs`). Render from the resolved gesture
  per platform. Effort S (ties to the modifier fix).

### P2 — Core editor features a markdown app is expected to have

- **No Export to HTML/PDF and no Print.** The whole point is rendering markdown,
  but there is no way to get the rendering out. Markdig is already in the
  pipeline for HTML. Effort L (feature).
- **No autosave / crash recovery for scratch buffers.** A purely in-memory
  scratch buffer is lost on crash/SIGKILL/power loss; only a graceful quit is
  guarded. Periodically snapshot dirty buffers under the settings dir and offer
  restore on next launch. Effort M-L.
- **No live zoom (Cmd +/−/0).** Editor font size is only a Settings slider; add
  zoom shortcuts that nudge `EditorFontSize` (and preview `FontSize`) with a
  reset. Plumbing already exists. Effort S-M.
- **Window size/position/maximized not persisted**; every launch reopens at the
  default geometry. Add fields to `AppSettings`, save in `PersistSession`, apply
  in `OnWindowOpened`. Effort M.
- **Last view mode not remembered** across launches when session restore is on
  (only `DefaultViewMode` is applied). Effort S.

### P3 — Markdown authoring ergonomics (this pass)

- DONE: **Insert Link (Cmd+K)** wraps the selection in `[sel]()` / inserts
  `[]()`, caret in the URL. (fixed this pass)
- **Generate Table of Contents** from the outline the app already builds
  (`OutlineBuilder`). Effort M.
- **Paste-as-markdown** / smart paste: paste is plain text; pasting a URL onto a
  selection could auto-link it; pasting rich text could convert to markdown.
  Effort M.
- **List/checkbox toggle** and **heading-level** shortcuts (e.g. Cmd+Shift+7/8
  for ordered/unordered list, Cmd+1..6 conflict with view modes so pick others;
  toggle `- [ ]` / `- [x]`). Smart-list continuation already exists. Effort S-M.
- **Block helpers**: wrap selection in a fenced code block / blockquote. Effort S.

### Performance & responsiveness (this pass)

- DONE: **Mermaid SVG is now cached by source.** Each debounced preview rebuild
  re-attached a fresh `MermaidControl`, which re-spawned the `mmdr` subprocess
  for unchanged diagrams; now identical diagrams return a cached SVG.
  (fixed this pass)
- Overall the perf-sensitive paths are already solid: the preview render is
  debounced (120 ms) + force-timed with generation counters, streamed into a
  pending tree, and serialized; the outline rebuild is debounced (200 ms) on the
  thread pool. No changes needed there.
- WATCH: `WordCount = CountWords(value)` runs synchronously on the UI thread on
  every keystroke (`MainWindowViewModel.OnSourceTextChanged`), an O(n) scan of
  the whole buffer. Fine for normal docs; for a multi-MB file it adds per-key
  latency. Ties to the large-file guard (P4). Debouncing it would lag the live
  count, so leave until the large-file work.
- Math (CSharpMath) rendering was not audited for caching this pass; if a doc
  has many identical inline formulas, check whether they re-render on each
  preview rebuild and apply the same source-keyed cache if so.

### Settings validation & resilience (this pass)

- DONE: **Numeric settings are clamped on load** (`AppSettings.Normalize`, called
  from `SettingsService.TryLoad`). A corrupted or hand-edited settings.json with
  FontSize 0, TabWidth 0, or a negative/NaN MermaidScale no longer produces
  invisible text, a zero tab, or a broken layout. (fixed this pass)
- String settings are already safe: `MarkdownThemes.Resolve` falls back to
  GitHubDark for an unknown theme key (no change needed).
- WATCH: **enum settings are not validated.** A corrupted JSON with an
  out-of-range int for `DefaultViewMode` / `UpdateChannel` / `OutlinePlacement`
  / `RendererKind` deserializes into an undefined enum value; the consuming
  switches mostly fall through to a default, but a defensive `Enum.IsDefined`
  check in `Normalize()` (reset to the field default when invalid) would harden
  this. Effort S.
- No **settings schema versioning / migration**: a renamed or removed field
  silently orphans the old value. Low priority while the schema is additive.

### Error handling & user-facing feedback (this pass)

- Reviewed: error surfacing is solid overall. The view's `async void` event
  handlers (`OnOpenRequested`, `OnSettingsRequested`, `OnDrop`, reload) wrap the
  awaited work in try/catch and report `"... failed: {message}"` via the status
  bar; the view-model's load/save/reload/recent paths catch typed exceptions
  (IO / UnauthorizedAccess / cancellation) and set a clear status. No obvious
  fix needed this pass.
- WATCH (minor): `MainWindowViewModel.LoadFileAsync` has no internal try/catch
  and relies on every caller to wrap it. Today all callers do (the view's open
  handler, drag-drop, and App's guarded loaders), but a future caller could
  forget and let an open failure escape. Optional hardening: surface the error
  inside a `LoadFileGuardedAsync`-style wrapper so callers cannot miss it.
- WATCH (minor): when `mmdr` is unavailable, mermaid blocks silently render as
  plain code with no hint that diagram rendering is disabled. A one-time status
  note ("Mermaid CLI not found; showing source") would explain the difference.

### P4 — Robustness / fidelity (needs care)

- **Encoding/BOM/line-endings not preserved** load → save: a UTF-16 or
  BOM/CRLF/Windows-1252 file is silently rewritten as UTF-8/no-BOM/LF. Detect and
  remember the source encoding + line-ending at load, reuse on save.
  `MainWindowViewModel.LoadFileAsync`/`SaveToFileAsync`. Effort M.
- **No guard for very large / binary files**: a multi-MB or binary file freezes
  the UI building thousands of preview controls and loads as replacement-char
  soup. Add a size threshold + binary sniff before loading. Effort M.

### P5 — Native OS integration polish

- **macOS Window menu** (Minimize/Zoom/Enter Full Screen/Bring All to Front) and
  **Help menu** are absent. Full-screen already works; only the entry point is
  missing. Effort M / S.
- **macOS forced Light/Dark does not set `NSWindow.appearance`**, so the
  vibrancy chrome can mismatch the in-app theme. Set `appearance` for Light/Dark
  and `nil` for System. Effort M.
- **Windows dark title bar / Mica**: the chrome is tuned for macOS vibrancy; the
  Windows title bar color and Win11 backdrop are not handled
  (`DwmSetWindowAttribute`). Effort M.
- **Native notifications** (Windows toast / freedesktop / macOS UNUserNotification)
  for update-available instead of in-app banner only. Effort M.
- **Native installers / desktop integration**: Windows ships a bare zip and
  Linux a bare tar.gz with no installer, `.desktop`, MIME association, or icon;
  "Install update" just opens the archive. Effort L.
- **Windows jump list / taskbar progress** for recent files and update download.
  Effort M.

### P6 — Smaller UX

- **Caret readout goes stale in Preview-only mode** (no editor focused). Hide or
  blank `CaretPosition` when `IsPreviewOnly`. Effort S.
- **Command palette omits commands** (New, Save As, Find Next/Prev, Format
  Tables, the Markdown group). Add the missing entries. Effort S.
- **Recent files: no Clear option**, and the welcome list does not refresh after
  a stale entry is pruned (`List<string>` is not observable). Effort S-M.
- **Close document / return-to-welcome** command is missing. Effort S.
