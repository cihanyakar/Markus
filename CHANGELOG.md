# Changelog

## [0.2.1](https://github.com/cihanyakar/Markus/compare/v0.2.0...v0.2.1) (2026-05-19)


### Build

* **macos:** standalone .app build script without hardened runtime ([863ef55](https://github.com/cihanyakar/Markus/commit/863ef55cbed1f78052af3c52064274c38b8b45ac))


### Continuous Integration

* **deps:** bump actions/checkout to v6 and actions/setup-dotnet to v5 ([ed745f5](https://github.com/cihanyakar/Markus/commit/ed745f543549752267cc65b3b991297c5df05a27))
* **deps:** Bump actions/upload-artifact from 4 to 7 ([#9](https://github.com/cihanyakar/Markus/issues/9)) ([4964ee4](https://github.com/cihanyakar/Markus/commit/4964ee4689232e7760514884ef7ced9603aa0aae))
* **release:** ad-hoc codesign macOS .app bundles without hardened runtime ([f2064f4](https://github.com/cihanyakar/Markus/commit/f2064f4110a1b46dedcd9124a4b271e5dfa998d8))

## [0.2.0](https://github.com/cihanyakar/Markus/compare/v0.1.1...v0.2.0) (2026-05-18)


### Features

* add Settings window, view modes scaffold, and app icon ([27b700d](https://github.com/cihanyakar/Markus/commit/27b700dcd93f87fecb5920c7cc35074c06d5a47f))
* **chrome:** native macOS Tahoe title bar with Liquid Glass tool platters ([780c760](https://github.com/cihanyakar/Markus/commit/780c760be846da201d904c78d61ad35d476103e6))
* **editor:** AvaloniaEdit + TextMate syntax highlighting for source view ([0d5912e](https://github.com/cihanyakar/Markus/commit/0d5912e696efd18c3b37506bca2736236d5a3e43))
* **input:** drag-and-drop a markdown file onto the window opens it ([c85ce28](https://github.com/cihanyakar/Markus/commit/c85ce28a77368ce20443b6b54d6d49cd53e69306))
* macOS-style settings, live reload, file watcher ([5f60481](https://github.com/cihanyakar/Markus/commit/5f6048176afb27e41d73d3c5bed99f0c3a501bbf))
* **menu:** native File / Edit / View menu bar, source editor rollback ([26da060](https://github.com/cihanyakar/Markus/commit/26da060a376088a14114e906080c2a941953b5ca))
* native Markdig preview rendering ([12f2d10](https://github.com/cihanyakar/Markus/commit/12f2d10330023d2419ce0f78365381d21db51a82))
* outline tree from heading structure ([26b4131](https://github.com/cihanyakar/Markus/commit/26b4131bfd1990ea77a5e4327b862d4122f0a4f9))
* **outline:** scroll preview to heading on tree selection ([87e42bf](https://github.com/cihanyakar/Markus/commit/87e42bf6549eed5e5ae6255f2874caf2d843c649))
* **perf:** async preview render, sync scroll, soft-wrap ([da1b316](https://github.com/cihanyakar/Markus/commit/da1b316bf3697e64bacffc7dded7127156b711ab))
* **recent:** persisted recent files menu in toolbar ([97aa9be](https://github.com/cihanyakar/Markus/commit/97aa9be3f6da4a137400077e28cde91d93886cd6))
* **render:** math and mermaid placeholder rendering ([028e773](https://github.com/cihanyakar/Markus/commit/028e773d101ce439590844ce28609f8527e90a53))
* **render:** wire markdown theme picker to renderer ([0c36188](https://github.com/cihanyakar/Markus/commit/0c36188615a4ba4680fef765aa8266c443c0ae8a))
* search, command palette, file association, detached redesign ([0ecbea2](https://github.com/cihanyakar/Markus/commit/0ecbea2331ce421f50f1f1add4c64093638e5a39))
* **settings:** Light/Dark/System theme mode override ([a18c2e8](https://github.com/cihanyakar/Markus/commit/a18c2e8f6bbf7d1c0ce4256b767fe5b6378bfd20))
* **settings:** Light/Dark/System theme mode override ([6d098be](https://github.com/cihanyakar/Markus/commit/6d098be2c5409c8177e96a8f70d4dbe903a7579e))
* **theming:** syntax highlighting + independent code & preview themes ([9783359](https://github.com/cihanyakar/Markus/commit/978335971205fa8d18227f19c4d0e6581eb587ec))
* **ui:** glassmorphic redesign with theme-aware atmosphere ([4a52b09](https://github.com/cihanyakar/Markus/commit/4a52b09975a9d65a53796b4eb1c51054d43dc5f6))
* **ui:** liquid-glass look + collapsing outline column ([9230137](https://github.com/cihanyakar/Markus/commit/9230137c3df577eeb192ce484668c0778a2d4655))
* UX overhaul, AOT-ready bundle, macOS-native settings ([4caab83](https://github.com/cihanyakar/Markus/commit/4caab839aecfe59aeead90d9b339cec6203e8599))


### Bug Fixes

* **editor:** use TextEditor inheritance + StyledProperty for binding ([4499449](https://github.com/cihanyakar/Markus/commit/4499449ae003dd29ed5e4930c3d2b4d6a02bec97))
* source editor renders, app menu, outline shortcut ([c98b4ac](https://github.com/cihanyakar/Markus/commit/c98b4ac5ec1284c87d98f4bcb66d40bc43a7e74c))
* **views:** silence IDE0031 in SearchOverlay and CommandPalette ([da07c92](https://github.com/cihanyakar/Markus/commit/da07c920cab4c703a248f2601c9662ba23fd25df))


### Refactoring

* **ui:** replace unicode emoji with Material Icons, refine styling ([bf8fdb0](https://github.com/cihanyakar/Markus/commit/bf8fdb075d2351648b7d28f4382fad846b63f358))

## [0.1.1](https://github.com/cihanyakar/Markus/compare/v0.1.0...v0.1.1) (2026-05-17)


### Bug Fixes

* **release:** build macOS .app bundle and compress single-file binaries ([8b1e617](https://github.com/cihanyakar/Markus/commit/8b1e617c67b5007c397657ee2285751c1e0e5e15))


### Documentation

* add Markus product and engineering plan (draft) ([1579280](https://github.com/cihanyakar/Markus/commit/1579280f7f30fc8facd02b046a751b779113133a))


### Continuous Integration

* **deps:** Bump actions/cache from 4 to 5 ([#1](https://github.com/cihanyakar/Markus/issues/1)) ([f04344f](https://github.com/cihanyakar/Markus/commit/f04344f9096e7d010f9cde9ae8b7519c07a7cfc1))
* **deps:** Bump actions/checkout from 4 to 6 ([#3](https://github.com/cihanyakar/Markus/issues/3)) ([0262961](https://github.com/cihanyakar/Markus/commit/02629615d45d8fb81bec68af5d0739276ba8e7e4))
* **deps:** Bump actions/setup-dotnet from 4 to 5 ([#2](https://github.com/cihanyakar/Markus/issues/2)) ([fbae1c8](https://github.com/cihanyakar/Markus/commit/fbae1c877b645092e73390805daed1f7c2c77448))

## 0.1.0 (2026-05-17)


### Features

* bootstrap Markus with .NET 10, Avalonia 12 and quality stack ([bc99fd8](https://github.com/cihanyakar/Markus/commit/bc99fd8c9f7b26aecaf71246314a759fb244ffb0))


### Documentation

* add C# / .NET 10 / Avalonia boilerplate guide ([4b577f0](https://github.com/cihanyakar/Markus/commit/4b577f0b5a628ec84bf75b0dfd55af4a5cc5332b))
* **boilerplate:** cover release pipeline (MinVer + release-please + binaries) ([f8c29e1](https://github.com/cihanyakar/Markus/commit/f8c29e11818a314674373d6aacdf36bcb79bd052))


### Continuous Integration

* add release pipeline with MinVer and release-please ([b76fc8a](https://github.com/cihanyakar/Markus/commit/b76fc8adab9dfc91506487004c79ca4ac6bab524))
* drop custom release-please title pattern (use default) ([a23635a](https://github.com/cihanyakar/Markus/commit/a23635af0fc82922101aec0df19111529d5c1cdd))
* pin initial release version to 0.1.0 ([05a666b](https://github.com/cihanyakar/Markus/commit/05a666bf4c7f4cffc1fb1a240bab724fb4ff591b))


### Tests

* bootstrap xUnit v3 test project with Shouldly and NSubstitute ([8271a2c](https://github.com/cihanyakar/Markus/commit/8271a2c3ec3d0c548663e55d4830092c5fb4b535))

## Changelog

All notable changes to this project will be documented in this file.

This file is maintained automatically by [release-please](https://github.com/googleapis/release-please).
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
