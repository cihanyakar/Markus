# Markus

[![CI](https://github.com/cihanyakar/Markus/actions/workflows/ci.yml/badge.svg)](https://github.com/cihanyakar/Markus/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/cihanyakar/Markus?display_name=tag&sort=semver)](https://github.com/cihanyakar/Markus/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-11-512BD4)](https://dotnet.microsoft.com)

A fast, elegant Markdown editor built with .NET 11 and Avalonia, shipped as small NativeAOT binaries (~30 MB).

## Features

- Source, Preview, vertical/horizontal Split, and Detached view modes (Cmd+1..5)
- TextMate syntax highlighting in the source editor with 15 color themes plus an Auto mode that follows the app theme
- Live GFM preview with tables, footnotes, task lists, Mermaid diagrams, and LaTeX math
- Document outline, in-document search and replace, folding, typewriter mode
- Editor and Preview customization (fonts, line numbers, tab width, full-width preview, math/Mermaid toggles)
- Built-in update check against GitHub releases

## Install

Download the latest build for macOS (arm64/x64), Windows, or Linux from the
[releases page](https://github.com/cihanyakar/Markus/releases/latest).

## Build and run

```bash
dotnet tool restore
dotnet build
dotnet run --project src/Markus
```

## Test

```bash
dotnet test
```

## Releasing

Releases are automated with [release-please](https://github.com/googleapis/release-please).
Conventional commits on `main` accumulate into a `chore: release main` PR; merging that PR
tags the version and triggers the binary build workflow. Do not create tags or releases by hand.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). All contributions must follow Conventional Commits and pass formatter, analyzers, and build (all warnings are errors).

## License

[MIT](LICENSE)
