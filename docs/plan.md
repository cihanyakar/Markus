# Markus — Product & Engineering Plan

> Status: draft, awaiting approval. Once approved, this becomes the source of truth for v0.2 → v1.0.

## 1. Vision

A fast, elegant **markdown viewer for developers**. Optimized for reading README files, technical documentation, and code-block-heavy content. Native desktop feel on macOS, Windows, and Linux. Keyboard-friendly, theme-able, low-friction.

**Explicit non-goals (for at least v1.x)**
- Not an editor. Open in your editor of choice, view here.
- Not a note-taking app. No backlinks, no graph, no daily notes.
- Not a presentation tool. No slide mode.

## 2. Target audience

Developers reading and reviewing markdown:
- GitHub READMEs (open `~/repos/foo/README.md` and just look at it)
- Technical documentation (long-form, deeply nested, heavy code blocks, tables)
- Blog drafts and proposals
- ADRs / RFCs

Primary task pattern: *"I have a `.md` file. I want to see it rendered, quickly, beautifully, without a browser and without a heavy IDE."*

## 3. MVP scope (v0.2)

- Single-file viewer
- Live reload via OS file-system watcher
- Drag-and-drop a `.md` file onto the window
- Open dialog (`Cmd/Ctrl+O`)
- Recent files (up to 10)
- Theme picker (multiple markdown styles)
- Toolbar + status bar shell

Out of scope for v0.2 (postponed to v0.3+):
- Folder/workspace mode
- TOC sidebar
- Math (LaTeX) — **placeholder rendering only**
- Mermaid diagrams — **placeholder rendering only**
- Search within document
- Print / Export (HTML, PDF)
- Plugin system

## 4. Tech stack

| Layer | Choice |
|---|---|
| SDK | .NET 10 (already pinned via `global.json`) |
| UI | Avalonia 12 |
| MVVM | CommunityToolkit.Mvvm |
| Markdown parser | Markdig (vanilla) + selected extensions |
| Code highlighting | AvaloniaEdit + TextMateSharp (TextMate grammars) |
| DI / Hosting | Microsoft.Extensions.DependencyInjection + Hosting |
| Logging | Microsoft.Extensions.Logging |
| File watching | `System.IO.FileSystemWatcher` + debounce |
| Settings/state | JSON files in per-OS standard dir (see §9) |
| Tests | xUnit v3 + Shouldly + NSubstitute (already wired) |
| Versioning | MinVer (already wired) |
| Release | release-please + cross-platform binaries (already wired) |

## 5. Render architecture

**Native Avalonia renderer.** Markdig parses to AST, a custom renderer walks the AST and emits Avalonia controls. No WebView dependency.

```
.md file
   │
   ▼
[ Markdig pipeline ]
   │   - CommonMark
   │   - GFM (tables, task lists, autolinks, strikethrough)
   │   - footnotes
   │   - YAML frontmatter (extracted, displayed as metadata strip)
   │   - math + mermaid (parsed, rendered as placeholder)
   │
   ▼ MarkdownDocument (AST)
   │
   ▼
[ MarkusRenderer : IMarkdownRenderer ]
   │   - HeadingBlock → styled TextBlock
   │   - ParagraphBlock → TextBlock with inlines
   │   - FencedCodeBlock → AvaloniaEdit TextEditor (read-only) + TextMate grammar
   │   - Table → Avalonia DataGrid (or custom Grid)
   │   - ListBlock → ItemsControl
   │   - QuoteBlock → bordered StackPanel
   │   - LinkInline → Hyperlink (Avalonia inline)
   │   - InlineMath / MermaidBlock → PlaceholderRenderer
   │
   ▼
[ DocumentView ScrollViewer ]
```

### Placeholder rendering (math + mermaid)

Until a proper renderer is implemented, math and mermaid blocks are shown as **a distinct red-tinted block** with the raw source visible. Example:

```
┌──────────────────────────────────────┐
│ ⚠ math (not yet rendered)            │  ← styled badge, accent color
│ $\int_0^\infty e^{-x^2} dx$          │  ← monospaced raw source
└──────────────────────────────────────┘
```

This both signals "we know this isn't done" and lets the reader still see the source.

## 6. UI / UX

### Window shell

```
┌─────────────────────────────────────┐
│ README.md — Markus            ⊕ ⋮ │  ← title bar (file • app)
│─────────────────────────────────────│
│ [⊕ Open] [↻ Reload] [☽ Theme]      │  ← toolbar (compact, icon+text)
│─────────────────────────────────────│
│                                     │
│   # Markus                          │
│   …rendered content…                │  ← DocumentView (ScrollViewer)
│                                     │
│─────────────────────────────────────│
│ README.md • UTF-8 • LF • 327 lines  │  ← status bar
└─────────────────────────────────────┘
```

### Keyboard shortcuts (v0.2)

| Shortcut | Action |
|---|---|
| `Cmd/Ctrl+O` | Open file |
| `Cmd/Ctrl+R` | Reload current file |
| `Cmd/Ctrl+W` | Close (or quit if last window) |
| `Cmd/Ctrl+,` | Settings |
| `Cmd/Ctrl+Shift+T` | Cycle theme |
| `F5` | Reload (alternate) |
| `Cmd/Ctrl+Plus / Minus / 0` | Zoom in / out / reset |
| `Cmd/Ctrl+Q` | Quit (macOS / Linux) |

A command palette (`Cmd/Ctrl+K`) is a v0.3 addition.

### Drag and drop

Drop any `.md`/`.markdown` file onto the window → opens immediately. Drop on the app icon (Dock/Taskbar) → also opens.

### Empty state

When no file is open: large drop zone with hint text, recent files list below it.

## 7. Theming

Theme = `theme.json` (versioned, validated). Schema:

```json
{
  "name": "GitHub Dark",
  "base": "dark",
  "shell": {
    "accent": "#58a6ff"
  },
  "content": {
    "bg": "#0d1117",
    "fg": "#c9d1d9",
    "muted": "#7d8590",
    "link": "#58a6ff",
    "heading": "#c9d1d9",
    "border": "#30363d"
  },
  "code": {
    "bg": "#161b22",
    "fg": "#e6edf3",
    "grammar": "github-dark"
  },
  "font": {
    "body": "Inter",
    "mono": "JetBrains Mono",
    "size": 16,
    "lineHeight": 1.6
  }
}
```

### Bundled themes (v0.2)

- **GitHub Light** (default for light system)
- **GitHub Dark** (default for dark system)
- **Solarized Light**
- **Solarized Dark**
- **Nord**
- **Tokyo Night**

### Loading

- Built-in themes embedded as `AvaloniaResource`.
- User themes from `<settings dir>/themes/*.json` discovered on startup.
- Each token is exposed as an Avalonia `DynamicResource` so any control that binds to a token updates live when the theme changes.

## 8. Architecture

### Project layout (v0.2)

```
src/
├── Markus/                      # UI host (Avalonia app)
│   ├── Views/                   # Window, DocumentView, dialogs
│   ├── ViewModels/              # MainWindowViewModel, DocumentViewModel, SettingsViewModel
│   ├── Themes/                  # built-in theme.json files (AvaloniaResource)
│   ├── App.axaml(+.cs)
│   ├── Program.cs               # builds IHost, hands MainWindow off to Avalonia
│   └── ServiceCollectionExtensions.cs
│
├── Markus.Core/                 # Pure .NET; no Avalonia references
│   ├── Markdown/                # Pipeline, AST helpers, frontmatter extraction
│   ├── Theming/                 # Theme model, loader, registry
│   ├── Settings/                # ISettingsStore, JSON-backed implementation
│   └── Files/                   # FileService, RecentFilesService, FileWatcher
│
└── Markus.Rendering/            # Avalonia-aware AST → controls
    ├── MarkdownRenderer.cs
    ├── BlockRenderers/          # HeadingRenderer, CodeBlockRenderer, TableRenderer, ...
    ├── InlineRenderers/         # LinkRenderer, EmphasisRenderer, ...
    └── Placeholders/            # MathPlaceholder, MermaidPlaceholder

tests/
├── Markus.Core.Tests/
└── Markus.Rendering.Tests/      # snapshot-ish tests of AST → control tree
```

Splitting Core (pure .NET) from UI and Rendering lets us test parser, theme loader, frontmatter extraction, file watching, settings, and renderer-input fixtures without standing up an Avalonia app.

### Composition root

In `Program.Main`:

```
var builder = Host.CreateApplicationBuilder(args);
builder.Services
    .AddMarkusCore()
    .AddMarkusRendering()
    .AddMarkusUi();

var host = builder.Build();
BuildAvaloniaApp(host).StartWithClassicDesktopLifetime(args);
```

Each layer exposes one `Add<Layer>` extension. Avalonia `App` resolves `MainWindowViewModel` from the host's `IServiceProvider`.

### Logging

`Microsoft.Extensions.Logging` with a file sink in the settings directory (rolling, max 5 MB, 5 files) and console in DEBUG.

## 9. Persistence

Per-OS standard directory:

| OS | Path |
|---|---|
| Windows | `%APPDATA%\Markus\` |
| macOS | `~/Library/Application Support/Markus/` |
| Linux | `${XDG_CONFIG_HOME:-~/.config}/Markus/` |

Files:
- `settings.json` — theme, font overrides, behavior toggles
- `state.json` — window position/size, recent files, last opened
- `themes/*.json` — user-supplied themes
- `logs/markus-*.log` — rolling log files

Loaded via `Microsoft.Extensions.Configuration.Json` + an `IOptionsMonitor<MarkusSettings>` so changes outside the app are picked up.

## 10. File watcher behavior

- `System.IO.FileSystemWatcher` on the directory of the currently open file, filtered to that filename.
- Debounce of **150 ms** so editors that save through atomic rename (Vim, VS Code) don't double-fire.
- On `Renamed`: track new path automatically.
- On `Deleted`: keep the rendered content, show a "file deleted" banner at the top.
- Manual reload (`Cmd/Ctrl+R`) always works regardless of watcher.

## 11. Localization

**English only for v0.2.** Strings are still kept in a `Resources.resx` so we can add languages without a refactor. Turkish is a likely second locale; if added, it goes in v0.4.

## 12. Roadmap

### v0.2 — Viewer (next release)
Everything in §3 plus six bundled themes.

### v0.3 — Comfort
- Math rendering (CSharpMath.Avalonia integration, native LaTeX → SVG)
- Mermaid rendering (still TBD; either a small bundled Node or a `markdig-mermaid` SVG renderer)
- Command palette (`Cmd/Ctrl+K`)
- In-document search (`Cmd/Ctrl+F`)
- TOC sidebar (auto from headings)

### v0.4 — Workspace
- Open a folder; left sidebar with `.md` files
- Quick switcher (`Cmd/Ctrl+P`)
- Per-folder watched live reload
- Turkish localization

### v0.5 — Sharing
- Export to HTML, PDF
- Copy as HTML / rich text
- Print

### v1.0 — Stable API
- Plugin system (custom renderers)
- Settled theme schema (v1)
- API freeze; SemVer kicks in for real

## 13. Open questions (to revisit later)

- **Code highlighting grammar source**: TextMateSharp ships a curated set; do we accept user-supplied `.tmLanguage`? (v0.3 question.)
- **Image handling**: local images fine, remote images = privacy concern. Add a "load remote images" toggle? (v0.3 question.)
- **File size limit**: at what size do we switch to virtualized rendering? Empirical bench needed.
- **Mermaid strategy**: native SVG render vs bundled WebView for mermaid only. (v0.3 question.)
- **Accessibility**: screen reader walkthrough, high-contrast mode. (Continuous; aim for full support by v1.0.)

## 14. Acceptance criteria for v0.2

To call v0.2 done:
- [ ] Opens any `.md` file from drag-and-drop, file association, or `Cmd/Ctrl+O`.
- [ ] Live-reloads within 200 ms of an external save.
- [ ] Renders CommonMark + GFM + frontmatter correctly against a fixture suite of at least 20 real-world README files (we'll vendor some from popular OSS).
- [ ] Code blocks highlight with TextMate grammar; copy button works.
- [ ] Math/mermaid blocks render as placeholders with raw source visible.
- [ ] All six bundled themes selectable from a toolbar dropdown; choice persists across sessions.
- [ ] Recent files list, up to 10, with click-to-open.
- [ ] All quality gates still green (CSharpier, analyzers, tests on 3 OSes).
- [ ] Self-contained binaries published via existing release pipeline.

---

*Next step after approval: spike `Markus.Core.Markdown` + a minimal `Markus.Rendering.MarkdownRenderer` rendering CommonMark text into a `StackPanel`. Headings + paragraphs + lists + code blocks first; tables and links second; placeholders last.*
