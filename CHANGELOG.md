# Changelog

## [0.10.0](https://github.com/cihanyakar/Markus/compare/v0.9.4...v0.10.0) (2026-06-30)


### Features

* **macos:** native document window integration ([fa808c0](https://github.com/cihanyakar/Markus/commit/fa808c03206c2ec6fe34eeb9a1ba9dd4ecee502c))
* **markdown:** insert-link command (Cmd+K) and review-findings log ([af3f77f](https://github.com/cihanyakar/Markus/commit/af3f77f20a80c1c7353ff8704b43412a86f58b7d))
* **menu:** complete File menu (New, Save As, Close, Reveal) ([7988b6e](https://github.com/cihanyakar/Markus/commit/7988b6ecab699bee11ba374f863069c1d89fb04b))
* **scratch:** drop to source editor when new scratch starts in preview ([ff50fae](https://github.com/cihanyakar/Markus/commit/ff50fae58a8acb50c539f7d0bb3194d1b1a81357))


### Bug Fixes

* **macos:** harden quit guard with an explicit WillTerminate observer ([88459db](https://github.com/cihanyakar/Markus/commit/88459db58e40cc359d8e64d0935550d3c1a8b076))
* **robustness:** atomic document save and safer settings/watcher paths ([56f3a5c](https://github.com/cihanyakar/Markus/commit/56f3a5ce888c872a8ebfe732513fc24d3087ac59))
* **search:** drop the nonexistent replace shortcut from the tooltip ([ffc1ffa](https://github.com/cihanyakar/Markus/commit/ffc1ffad1683e48985a25c719670b8affdc9a826))
* **settings:** clamp numeric settings on load to valid ranges ([57b5444](https://github.com/cihanyakar/Markus/commit/57b54444dec34218a020767377a8b79d9ca1c257))
* **ux:** resolve behavioral dead-ends and data-loss paths ([baabfc7](https://github.com/cihanyakar/Markus/commit/baabfc7c66568da09f6594745d1f6bbc9344b4c3))


### Performance

* **mermaid:** cache rendered SVG by diagram source ([922699c](https://github.com/cihanyakar/Markus/commit/922699ccd3827898a413f2dcec48f76aaaccd70f))


### Documentation

* **findings:** log error-handling review pass (solid; two minor watches) ([0e5fae7](https://github.com/cihanyakar/Markus/commit/0e5fae799a908e6c8d0ef8d7a7c9ceeb0c617767))
* **findings:** log macOS quit crash (P0) and hardening proposal ([22f6ebc](https://github.com/cihanyakar/Markus/commit/22f6ebc44b8442fa1aad72a73f0a5b2382aff195))
* **findings:** mark macOS quit-guard hardening as applied, pending verify ([bd6e98b](https://github.com/cihanyakar/Markus/commit/bd6e98b4df20bd4c0b555544dd45142edeb2661d))

## [0.9.4](https://github.com/cihanyakar/Markus/compare/v0.9.3...v0.9.4) (2026-06-29)


### Continuous Integration

* **release-please:** drop package-name to fix release tagging ([27f8b94](https://github.com/cihanyakar/Markus/commit/27f8b94906104b4e3a323807cf08f8cb5fac930e))
* **release:** build binaries for bot-created releases ([abfe591](https://github.com/cihanyakar/Markus/commit/abfe59161ea683f0a0b5514dfb4482de8d3cfb08))

## [0.9.3](https://github.com/cihanyakar/Markus/compare/v0.9.2...v0.9.3) (2026-06-29)


### Bug Fixes

* **io:** retry atomic rename on Windows access-denied race ([3eb2236](https://github.com/cihanyakar/Markus/commit/3eb22369311eebf8ba9a4cfeb85041eb264d7a47))

## [0.9.2](https://github.com/cihanyakar/Markus/compare/v0.9.1...v0.9.2) (2026-06-29)


### Bug Fixes

* **about:** show the real version in the About window instead of 0.0.0
* **macos:** guard against NativeAOT shutdown crash on quit ([6c80904](https://github.com/cihanyakar/Markus/commit/6c80904eef3d2079ea4b977eef546272fdf58097))
* **menu:** open recent files via Click handler on macOS ([42f7b81](https://github.com/cihanyakar/Markus/commit/42f7b818d00e1b813c2babfe4ee0cf911cb20cba))

## [0.9.1](https://github.com/cihanyakar/Markus/compare/v0.9.0...v0.9.1) (2026-06-17)


### Tests

* satisfy MA0074 from bumped Meziantou.Analyzer ([37a7a60](https://github.com/cihanyakar/Markus/commit/37a7a60490c2a9ce8f391fde5171452efa40ce66))

## [0.9.0](https://github.com/cihanyakar/Markus/compare/v0.8.1...v0.9.0) (2026-06-17)


### Features

* **editor:** dispatch Tab/Shift-Tab inside tables to TableCellNavigator ([527cac4](https://github.com/cihanyakar/Markus/commit/527cac486cb321d5af930a299aff6d5d966e239f))
* **editor:** reflow GFM table when caret leaves it ([f3ef3f1](https://github.com/cihanyakar/Markus/commit/f3ef3f139950d3d18225000a370195ea4ba75ca1))
* **table:** add InsertEmptyRow for auto-row creation ([88a07a8](https://github.com/cihanyakar/Markus/commit/88a07a82b914c6dc320e4aad3ca801d94cf68fec))
* **table:** add IsCaretInTable helper for reflow trigger ([3b3c423](https://github.com/cihanyakar/Markus/commit/3b3c42331a867ca21b03e1465503edb3065b669b))
* **table:** add NextCell forward navigation (skips delimiter row) ([10a088c](https://github.com/cihanyakar/Markus/commit/10a088cab030e40733125e03d7b4d2050476e3f1))
* **table:** add TableCellNavigator scaffold with TryFindTableAt happy path ([8b21f6a](https://github.com/cihanyakar/Markus/commit/8b21f6a2dc6c0952e1774b711686074a0d5bf74e))


### Bug Fixes

* **editor:** group auto-row insertion as single undo step ([d1a5ea4](https://github.com/cihanyakar/Markus/commit/d1a5ea4382f7221f3c367c070a7323ca2b8c0641))
* **editor:** guard table reflow against re-entry and stale line index ([9a5e885](https://github.com/cihanyakar/Markus/commit/9a5e8855affe38d9cf7e012873a232734091947a))
* **renderer:** prevent multi-GB RAM blowup from emoji weight mix ([ef518db](https://github.com/cihanyakar/Markus/commit/ef518db9fa302282b774a39938a1a04f059b71ff))
* **table:** land InsertEmptyRow caret at first content position ([d0bd0d8](https://github.com/cihanyakar/Markus/commit/d0bd0d89410b023747e9971cd079ef95143741c3))
* **table:** StepForward returns null at end of header-only table ([216a9be](https://github.com/cihanyakar/Markus/commit/216a9bea6f96a229895a3c037dedfb15aae284ef))


### Refactoring

* **table:** apply code review fixes to TableCellNavigator ([a519d15](https://github.com/cihanyakar/Markus/commit/a519d15f8b4ca06e238c4b7b95d2cd3b284e1acd))


### Documentation

* **plan:** add convention 8 for [NotNullWhen(true)] out + Shouldly tests ([ecdc65f](https://github.com/cihanyakar/Markus/commit/ecdc65f7e0f3b63998c72a1083de99457a473ec5))
* **plan:** add deep-research-driven Quick Wins spec and QW1 plan ([97a599c](https://github.com/cihanyakar/Markus/commit/97a599cd5ef903fe47701c70ab8718954448ae58))


### Build

* **deps:** bump csharpier from 1.2.6 to 1.3.0 ([5da0db0](https://github.com/cihanyakar/Markus/commit/5da0db0b0ec1194f302fd1789762d4457cf18018))


### Tests

* **table:** cover NextCell backward navigation ([6846156](https://github.com/cihanyakar/Markus/commit/6846156e6a7571a0109f82f75e131e80bc888b0e))
* **table:** cover TryFindTableAt no-table and edge cases ([1f8b69f](https://github.com/cihanyakar/Markus/commit/1f8b69f448c914c18b254ae7c533e0b6f3cfda6f))
* **table:** TryFindTableAt picks correct table in multi-table doc ([a9fc3b4](https://github.com/cihanyakar/Markus/commit/a9fc3b4de2a85a4bdafb163be9fbbca2050c1ad7))

## [0.8.1](https://github.com/cihanyakar/Markus/compare/v0.8.0...v0.8.1) (2026-06-14)


### Continuous Integration

* **release-binaries:** drop osx-x64 from the build matrix ([a2aba7e](https://github.com/cihanyakar/Markus/commit/a2aba7e07773b93cbea16a08c6ebb99c02332729))

## [0.8.0](https://github.com/cihanyakar/Markus/compare/v0.7.4...v0.8.0) (2026-06-14)


### Features

* **settings:** atomic crash-safe writes with TryLoad and debounced hot-path saves ([6fdaa57](https://github.com/cihanyakar/Markus/commit/6fdaa57810057752e6f919528c5de1239003f530))


### Bug Fixes

* **file-watcher:** widen catch and dispose partially-constructed watcher ([a106a81](https://github.com/cihanyakar/Markus/commit/a106a81e0e952ba39d6dc8ddf5615aca69888598))
* **update:** prefer installer extensions over portable archives ([5fbc442](https://github.com/cihanyakar/Markus/commit/5fbc442c843b7fcd00d20fe4dd2ed187a1a690d0))
* **update:** preserve concurrent writes and channel changes during async check ([e685bd9](https://github.com/cihanyakar/Markus/commit/e685bd9aed390d618cb9b07d10f0db4e3c4ccd30))
* **update:** treat future-dated last check as stale ([b852752](https://github.com/cihanyakar/Markus/commit/b852752cf44f5b47f355d0d14a3cba0d2859719e))
* **views:** visual-tree lifecycle hygiene for preview and mermaid ([962cdf9](https://github.com/cihanyakar/Markus/commit/962cdf98a9b737b1aa1ce3dbfc0fa1995cff0a6e))


### Documentation

* refresh README and add CLAUDE.md with release and tooling notes ([a8153eb](https://github.com/cihanyakar/Markus/commit/a8153eb7ce10d9d7472f3b111fdea8722b1cb518))


### Continuous Integration

* sync release-please manifest with the manually published v0.7.4 ([b8ae7de](https://github.com/cihanyakar/Markus/commit/b8ae7de91aacf2ca0be50cdc7c1457e260e3d1bf))

## [0.5.0](https://github.com/cihanyakar/Markus/compare/v0.4.0...v0.5.0) (2026-06-04)


### Features

* **editor:** add dirty tracking, save flow, and discard guards ([a01b623](https://github.com/cihanyakar/Markus/commit/a01b623daa22cfc618c6590aa0f7d4705ddb461a))
* **math:** integrate CSharpMath for native LaTeX rendering ([7a59ff6](https://github.com/cihanyakar/Markus/commit/7a59ff697dcc3be6942d0ee284f33162fdfec1fa))
* **mermaid:** SVG-based diagram rendering with bundled mmdr ([4cb62a0](https://github.com/cihanyakar/Markus/commit/4cb62a077cf5b2af6d3fe5c4ce09faa1820fabd4))
* **preview:** scroll to anchor on in-document link click ([68ec073](https://github.com/cihanyakar/Markus/commit/68ec073387549a2387c4c54b16d86889c60b9c96))
* **status:** show file last-modified time in footer ([d95ac14](https://github.com/cihanyakar/Markus/commit/d95ac146de6e91870b0c8a476d51b751907d4d9b))
* **updates:** add artifact downloader ([b050380](https://github.com/cihanyakar/Markus/commit/b050380fd3e8aae6f3a34f1b3bbe1c0f45d59441))
* **updates:** add assembly version provider ([eee456c](https://github.com/cihanyakar/Markus/commit/eee456cd423600a8740c677a7561bb0002c0d41b))
* **updates:** add auto-check debounce policy ([c3743a6](https://github.com/cihanyakar/Markus/commit/c3743a623a46fab83627a58fb5f920ee2c7e8bde))
* **updates:** add check-for-updates button to about ([429bb0f](https://github.com/cihanyakar/Markus/commit/429bb0f9181daf56fab323fcbf156d9198af9317))
* **updates:** add check-for-updates menu item ([7061c92](https://github.com/cihanyakar/Markus/commit/7061c92f96b0b5e3e33415f2b37c2bb6fcbdb5a1))
* **updates:** add github release dtos and mapper ([3ef2bee](https://github.com/cihanyakar/Markus/commit/3ef2bee497a4162859b08066f47a6322a6408427))
* **updates:** add github release feed ([f6b9d63](https://github.com/cihanyakar/Markus/commit/f6b9d6315637c70c5193181b2ba193330141f10e))
* **updates:** add release asset selector ([82ea35d](https://github.com/cihanyakar/Markus/commit/82ea35d2c9f6b619874534a21da903e56e15b651))
* **updates:** add release domain models ([4f54f31](https://github.com/cihanyakar/Markus/commit/4f54f3169b33283c975bbf402fb78d9552b62c7c))
* **updates:** add runtime identifier helper ([ac73276](https://github.com/cihanyakar/Markus/commit/ac73276a974c4a168af2c9714e021ddfd9a3d2fb))
* **updates:** add SemVer value type ([2650beb](https://github.com/cihanyakar/Markus/commit/2650beb981a139f760dfa308e7d3e4c3560bb4da))
* **updates:** add sha256 verifier ([eea6ab0](https://github.com/cihanyakar/Markus/commit/eea6ab0bdfc947943c5b1d44a991d21d44aed1bb))
* **updates:** add update banner to main window ([c00ed11](https://github.com/cihanyakar/Markus/commit/c00ed11b35cda7f9228754c75b4343da820f85d8))
* **updates:** add update channel and on-launch settings ([920dd4b](https://github.com/cihanyakar/Markus/commit/920dd4bc4d5cb46a61fd1b255bc1a7eeacf1d644))
* **updates:** add update checker ([a790dfe](https://github.com/cihanyakar/Markus/commit/a790dfe5c8deaaba64c4803e805a7bf5e66d7bfc))
* **updates:** add update launcher ([cd735fb](https://github.com/cihanyakar/Markus/commit/cd735fb870e438e96994bf3a32b74560c9720409))
* **updates:** add update view model ([3764740](https://github.com/cihanyakar/Markus/commit/376474024721550a8a5e92973b465427212f8ca2))
* **updates:** add updates settings category ([eaac56e](https://github.com/cihanyakar/Markus/commit/eaac56e0757b4e18cafe01c83c88ecc0065701c8))
* **updates:** auto-update via GitHub Releases ([3cfdd9c](https://github.com/cihanyakar/Markus/commit/3cfdd9cc933e7f2dab8f915e5e380201af377669))
* **updates:** compose update services and launch check ([bceae5a](https://github.com/cihanyakar/Markus/commit/bceae5a733daa3cd19cd06c36dbf860c657a1e05))
* **updates:** expose update view model on main window ([1649eb7](https://github.com/cihanyakar/Markus/commit/1649eb781a914618f6c3d4a9ea02a7ec840d500b))


### Bug Fixes

* **open:** focus existing window and stop spawn cascade ([6b54532](https://github.com/cihanyakar/Markus/commit/6b545326cc6af6837bda5465b6f3ee78b2dfa5b6))
* resolve 7 bugs and expand test suite to 207 tests ([ce15f2b](https://github.com/cihanyakar/Markus/commit/ce15f2bb3d2312597aed112828431c742b933a30))
* **updates:** harden cancellation and json failure handling ([3971c3f](https://github.com/cihanyakar/Markus/commit/3971c3f61f68dbaca3c2e105ba598da32d386e8f))
* **updates:** reject leading-zero numeric identifiers in SemVer ([c08ac98](https://github.com/cihanyakar/Markus/commit/c08ac98e359a27216c68b5b860061dd091a6750f))


### Documentation

* **updates:** add auto-update implementation plan ([adace94](https://github.com/cihanyakar/Markus/commit/adace944f84a7771ba111dd899f7e241979c0088))
* **updates:** add auto-update via GitHub Releases design spec ([fb6f22c](https://github.com/cihanyakar/Markus/commit/fb6f22c415a5c41abf93de71f3777fa70f04f914))

## [0.4.0](https://github.com/cihanyakar/Markus/compare/v0.3.0...v0.4.0) (2026-05-20)


### Features

* **async:** cancellation token support for file I/O and render loop ([e2b2abb](https://github.com/cihanyakar/Markus/commit/e2b2abbbaf10e94990fdfcad9141b64085d5aceb))

## [0.3.0](https://github.com/cihanyakar/Markus/compare/v0.2.1...v0.3.0) (2026-05-20)


### Features

* **outline:** left/right placement, expand/collapse, quick filter ([5651f83](https://github.com/cihanyakar/Markus/commit/5651f830cc1346dbe2531519266264cccc8223ca))
* **ux:** welcome view, drag-drop polish, table formatter, custom shortcuts ([59771d5](https://github.com/cihanyakar/Markus/commit/59771d5811107f9e34cee6c989e1b04f5fc30fb9))


### Tests

* 34 tests for parsers/services; coverage 12.6% → 21.6% ([0751ec5](https://github.com/cihanyakar/Markus/commit/0751ec55760a89805aa6a16f73833699f9416170))

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
