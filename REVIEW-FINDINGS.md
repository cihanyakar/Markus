# Markus — Review Findings Log

Living backlog from recurring whole-app reviews. Each pass takes a different
angle, fixes the obvious low-risk items inline (see git log on `fix/ux-pass`),
and records the larger or riskier findings here for later.

## Angles covered so far

- Behavioral UX dead-ends and data-loss paths (fixed in batch).
- OS-capability + best-practice + code-robustness sweep (partly fixed).
- Markdown authoring ergonomics (this pass).

---

## Open findings (not yet fixed)

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
