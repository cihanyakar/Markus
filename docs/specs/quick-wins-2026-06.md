# Markus Quick Wins Mini-Spec

**Tarih.** 2026-06-15
**Sürüm hedefi.** Önümüzdeki 2-3 minor release
**Toplam efor.** ~5-7 hafta (tek geliştirici, part-time)
**Kapsam.** Üç bağımsız özellik. Sıralı veya paralel ilerleyebilir, hiçbir özelliği bloklamıyor.

## Genel Bağlam

Deep-research turunda doğrulanmış üç pain point.

1. **Akıllı tablo editing** — MarkText ve MarkEdit'te eksik, MarkEdit issue `#627` maintainer "frustrating" kabulüyle plugin'e itildi
2. **Wiki-link autocomplete** — MarkText `#2018` (4+ yıl açık), Obsidian/Zettlr'da var, plain-`.md` ekosistemi için differentiation alanı
3. **Proje çapında find-replace** — Typora `#2824, #2554, #2822, #766`, sürekli açık talep

Üç özellik plain `.md` filesystem felsefesini korur. Hiçbiri state dizinine (`.obsidian/` benzeri) ihtiyaç duymaz; tüm index'ler in-memory ve regenerable.

---

## QW1. Akıllı Tablo Editing

### Hedef
GFM tabloları içinde Tab/Shift-Tab navigation, auto-row creation ve inline reflow. `MarkdownTableFormatter`'ın formatlama logic'i üzerine sadece editing UX katmanı.

### Kullanıcı Hikayesi
> Tablo cell'inde Tab tuşuna basıyorum, sonraki cell'e geçiyor. Son cell'de Tab basınca yeni satır otomatik oluşuyor. Cell içeriğini yazdıkça kolonlar canlı olarak hizalanıyor (veya en azından imleç tabloyu terk ettiğinde).

### UX Davranışı

| Olay | Davranış |
|---|---|
| Imleç tablo cell'inde, Tab | Sonraki cell'in başına git, içeriği seç |
| Imleç tablo cell'inde, Shift+Tab | Önceki cell'in sonuna git |
| Son cell'de Tab | Yeni satır oluştur (önceki satırla aynı kolon sayısı, hepsi boş), ilk cell'e git |
| İlk cell'de Shift+Tab | Önceki satırın son cell'ine git (tabloda ilk satırsa noop) |
| Enter cell içinde | Yeni satır oluştur (Tab davranışı yerine, sadece header satırı altında) |
| Imleç tabloyu terk ediyor (cursor row değişimi) | O tabloyu `MarkdownTableFormatter.Format` ile reflow et |
| Cell yazımı sırasında `|` karakteri | Otomatik escape (`\|`) tekliftir, ama default'ta yapma (kullanıcı kararı) |

### Teknik Tasarım

**Mevcut altyapı.**
- `Services/MarkdownTableFormatter.cs`, formatlama logic'i hazır
- `Views/MarkdownTextEditor.cs`, TextEditor wrapper (AvaloniaEdit varsayımı)

**Yeni kod.**
- `Services/TableCellNavigator.cs` (yeni)
  - `bool TryFindTableAt(string source, int offset, out TableRegion region)`
  - `TableRegion { int StartLine; int EndLine; int HeaderLine; int DelimiterLine; List<List<CellRange>> Cells; }`
  - `CellRange? NextCell(TableRegion, int currentOffset, bool forward)`
  - `string InsertEmptyRow(TableRegion, int afterRowIndex)`
- `Views/MarkdownTextEditor.cs`'e `KeyDown` handler ekle
  - Tab/Shift-Tab/Enter eventlerini intercept et
  - Önce `TableCellNavigator.TryFindTableAt` ile cursor'un tablo içinde olup olmadığını kontrol et
  - Tablo içindeyse cell navigation, değilse default davranışa düş
- Reflow trigger, `TextEditor.Document.LineChanged` veya `CaretChanged` event'i. Cursor önceki satırda tabloda, yeni satırda değil ise reflow.

**Alternatif tasarım, full-on grid editor.**
GitHub Desktop'taki gibi tabloyu görsel grid'e dönüştürmek. Reddedildi, çünkü
- Markus'un Source-mode felsefesini bozar
- AvaloniaEdit altyapısıyla karmaşık integration
- 5-7 günlük spec'i 4-6 haftaya çıkarır

**Reflow stratejisi, anında vs cursor-leaves.**
*Anında reflow* (her keystroke) latency yaratır ve cursor positioning'i kırar.
*Cursor-leaves* (kullanıcı satır değiştirince) tutarlı ve ucuz. Seçilen.

### Acceptance Criteria
1. Tablo cell'inde Tab → sonraki cell, içerik selected
2. Son cell'de Tab → yeni satır, ilk cell'inde imleç
3. Shift+Tab → önceki cell, edge case (ilk satır ilk cell) noop
4. Imleç tabloyu terk edince reflow `MarkdownTableFormatter.Format` ile tutarlı
5. Tablo dışındayken Tab default davranış (indent veya kullanıcı kısayolu)
6. CJK/emoji içeren cell'lerde reflow doğru (mevcut `MarkdownTableFormatter` testleri zaten kapsıyor)
7. Multi-table doc'ta sadece imleçteki tablo etkileniyor
8. Undo/Redo tek bir mantıklı step olarak çalışıyor (auto-row + reflow ayrı undo step olmamalı)

### Efor Tahmini
**5-8 gün.** Çoğu efor `TableCellNavigator` implementasyonu ve AvaloniaEdit event handling. Reflow logic'i hazır.

### Riskler
- AvaloniaEdit Tab davranışı default ile çatışma (indent). Mitigation, Tab handler'ı tablo context'iyle conditional.
- Undo grouping. AvaloniaEdit `UndoStack.StartContinuedUndoGroup` ile çözülür ama test gerekli.
- Wide character cursor offset. UTF-16 vs display width karışıklığı. Mitigation, navigator UTF-16 offset'leri kullanır, sadece reflow display width düşünür.

### Out of Scope
- Tablo görsel grid editor
- Satır/kolon ekleme/silme komutları (right-click menu), sonraki spec
- Cell birleştirme (markdown'da yok)
- CSV import/export

---

## QW2. Wiki-link Autocomplete

### Hedef
`[[` yazıldığında proje dosyalarından öneri popup'ı. Plain `.md` felsefesini korumak için state dizini yok, in-memory index regenerable.

### Kullanıcı Hikayesi
> Bir dosyada yazıyorum, `[[` yazıyorum, anında proje dizinindeki `.md` dosyalarının fuzzy-matched listesi açılıyor. Bir tane seçip Enter'a basıyorum, `[[file-name]]` formatında link yapışıyor. Daha sonra link üzerine `Cmd+Click` yapınca o dosya açılıyor.

### UX Davranışı

| Olay | Davranış |
|---|---|
| Kullanıcı `[[` yazdı | Popup açıl, mevcut proje dosyaları list, ilk eşleşme seçili |
| Popup açıkken harf yazımı | Fuzzy filtre, en iyi eşleşme seçili |
| Popup açıkken ↑/↓ | Seçim değiştir |
| Popup açıkken Enter veya Tab | Seçili dosyayı `[[file-name]]` formatında yapıştır, popup kapat |
| Popup açıkken Esc | Popup kapat, `[[` olduğu gibi kalsın |
| Popup açıkken `]]` yazımı | Popup kapat, kullanıcının manuel yazımına izin ver |
| `[[partial]]` link üzerinde Cmd+Click (macOS) / Ctrl+Click (Win/Linux) | Eşleşen dosyayı yeni sekme/aynı pencerede aç (sekme yoksa replace) |
| `[[partial]]` linki eşleşmiyor | Subtle underline kırmızı, hover'da "no match" tooltip |

### Teknik Tasarım

**Mevcut altyapı.**
- `Services/FileWatcherService.cs`, file change tracking
- `Services/MarkdownPipeline.cs`, Markdig pipeline
- `Views/CommandPalette.axaml.cs`, fuzzy match + popup pattern referansı (oradan ödünç al)

**Yeni kod.**
- `Services/WorkspaceIndex.cs` (yeni)
  - Proje kökü ne demek? İlk açılan dosyanın klasörü (deep-detect değil, basit). User explicit `Open Folder` komutuyla override edebilir.
  - `IReadOnlyList<WorkspaceFile> Files { get; }`
  - `IEnumerable<WorkspaceFile> Match(string query)`, fuzzy match (CommandPalette algoritmasını paylaş)
  - `WorkspaceFile { string AbsolutePath; string DisplayName; string RelativePath; }`
  - `FileWatcherService` ile sync olur, dosya eklenir/silinirse index güncellenir
- `Services/WikiLinkResolver.cs` (yeni)
  - `WorkspaceFile? Resolve(string linkText, string sourceFilePath)`
  - Eşleşme stratejisi (öncelik sırası)
    1. Tam dosya adı match (`[[notes]]` → `notes.md`)
    2. Aynı dizinde relative match
    3. Proje çapında unique basename match
    4. Çoklu eşleşme varsa ilk alphabetic (deterministik)
- `Views/WikiLinkPopup.axaml(.cs)` (yeni), CommandPalette tarzı popup
- `Views/MarkdownTextEditor.cs`'e
  - `TextEntered` event'inde `[[` algılama, popup tetikleme
  - `[[...]]` token'ları için inline rendering (clickable link + dim non-match)

**Wiki-link sözdizimi.**
Obsidian'la uyumlu kalmak için `[[file-name]]`. Display text alias için `[[file-name|Display]]` (sonraki sürüm).

**Indexing yaklaşımı, lazy vs eager.**
*Lazy* (sadece popup açıldığında scan) cold-path slow.
*Eager* (workspace açıldığında full scan) startup +50ms gibi bir maliyet, sonrasında sıfır latency.
Seçilen, **eager + watcher**. Bir defa scan, sonra `FileWatcherService` ile delta güncelleme.

**State dizini yok prensibi.**
Index process memory'de yaşar. Markus kapanıp açıldığında yeniden scan. Persist edilirse `.obsidian/` benzeri lock-in yaratır. Reddedildi. Scan maliyeti ~10K dosya için ~100ms (sadece dizin listeleme + file size, içerik değil).

### Acceptance Criteria
1. `[[` yazınca popup ≤100ms açılıyor
2. Boş query'de tüm `.md` dosyaları (yakın zamanda değiştirilenler önce)
3. Fuzzy match CommandPalette ile aynı algoritma (kullanıcı tutarlılığı)
4. Enter → `[[file-name]]` insert, popup kapat
5. Esc → noop, `[[` kalır
6. `]]` yazımı popup'ı kapatır
7. Cmd/Ctrl+Click eşleşen dosyayı açar
8. Eşleşmeyen link kırmızı underline (preview ve source mode'da)
9. `FileWatcherService` ile yeni dosya eklendiğinde 1 saniye içinde popup'ta görünüyor
10. Workspace dışı dosya açılırsa popup boş ama crash yok

### Efor Tahmini
**6-10 gün.** `WorkspaceIndex` + popup UI + AvaloniaEdit integration + link rendering. CommandPalette zaten pattern verdiği için popup kısmı ~1.5 gün.

### Riskler
- Workspace kökü algılama kullanıcı için belirsiz olabilir. Mitigation, status bar'da workspace path göster, "Change Workspace..." komutu ekle.
- Çoklu match deterministik değil kullanıcıyı şaşırtabilir. Mitigation, çoklu match varsa popup'ta tüm seçenekleri göster (zaten popup açık olduğu için doğal).
- `[[` literal kullanan kullanıcılar (örn. matematik notasyon `[[1, 2], [3, 4]]`). Mitigation, popup açılır ama Esc ile kapatılır, `[[` literal kalır.
- Performance büyük workspace (~100K dosya). Mitigation, scan'i `glob` ile `*.md` filtreli yap, mtime sort, top 1000 göster.

### Out of Scope
- `[[file-name|Display]]` alias syntax (sonraki spec)
- `[[file-name#heading]]` heading-level link
- `[[file-name^block]]` block reference
- Backlinks panel (graph view), bu daha büyük bir Strategic Bet
- Non-`.md` dosya linking (resim, PDF)

---

## QW3. Proje Çapında Find-Replace

### Hedef
`SearchOverlay`'in kazandığı find/replace UX'i çoklu dosya scope'una genişletmek. VS Code'un "Search in Files" UX'i referans.

### Kullanıcı Hikayesi
> Cmd+Shift+F basıyorum, side panel açılıyor. Aradığım metni yazıyorum, tüm proje dosyalarında eşleşmeler gruplu olarak görünüyor (dosya başına expandable). Bir eşleşmeyi tıklayınca dosya açılıyor o satıra gidiyor. Replace metni yazıp 'Replace All' diyorum, önce preview, sonra confirm dialog, sonra apply.

### UX Davranışı

| Olay | Davranış |
|---|---|
| Cmd+Shift+F (macOS) / Ctrl+Shift+F (Win/Linux) | Side panel aç (sol veya sağ, kullanıcı setting'i) |
| Search input'a yazım | Debounced (300ms) workspace scan, sonuçlar canlı |
| Sonuç grouplaması | Dosya yolu header, altında eşleşen satırlar (line number + snippet, match highlighted) |
| Bir sonuca çift tıklama | Dosyayı aç, o satıra git, o eşleşmeyi seçili bırak |
| Case sensitive / Whole word / Regex checkbox'ları | Mevcut SearchOverlay ile aynı semantik |
| Replace alanı görünür | Her sonucun yanında "replace this" butonu, en üstte "Replace All" butonu |
| Replace All tıklandı | Confirm dialog, "Replace 47 occurrences across 12 files. This action can be undone per-file." Confirm sonrası apply. |
| Replace sonrası | Her etkilenen dosyada per-file undo entry, ek olarak global "Undo Replace All" komutu (state Markus session boyunca tutulur) |

### Teknik Tasarım

**Mevcut altyapı.**
- `Views/SearchOverlay.axaml(.cs)`, find/replace UX patterns, regex/case settings
- `Services/FileWatcherService.cs`, değişiklikleri yakalama
- `Views/OutlinePanel.axaml(.cs)`, side panel UX patterns
- `Services/AtomicFileWriter.cs`, replace sırasında güvenli yazım

**Yeni kod.**
- `Services/WorkspaceSearch.cs` (yeni)
  - `IAsyncEnumerable<SearchHit> SearchAsync(WorkspaceSearchQuery query, CancellationToken)`
  - `WorkspaceSearchQuery { string Pattern; bool CaseSensitive; bool WholeWord; bool Regex; }`
  - `SearchHit { string FilePath; int LineNumber; int Column; string LineText; (int Start, int Length) MatchRange; }`
  - Streaming sonuç (büyük workspace için)
  - File walker `*.md` filter (sonraki spec'te include/exclude pattern)
- `Services/WorkspaceReplace.cs` (yeni)
  - `ReplacePlan PreparePlan(IReadOnlyList<SearchHit> hits, string replacement)`
  - `ReplaceResult Apply(ReplacePlan)`, `AtomicFileWriter` üzerinden
  - Per-file undo entries
- `Views/WorkspaceSearchPanel.axaml(.cs)` (yeni), side panel
- `Views/MainWindow.axaml.cs`'e panel toggle entegrasyonu

**Search engine, in-memory vs ripgrep bridge.**
*ripgrep external process* hızlı ama dependency, NativeAOT bundling karmaşıklığı.
*Pure C#* implementasyonu yavaş ama kontrol bizde, NativeAOT-safe.
Seçilen, **pure C#**. Markus'un kapsamı (typical workspace <10K dosya) için yeterli hızlı (~50-200ms search). Sonraki sürümde performans sorun olursa ripgrep optional opt-in eklenir.

**Streaming UI yaklaşımı.**
Sonuçlar geldikçe panel güncellenir (yıldırım hızında ilk feedback). Total count en sonda netleşir.

**Replace güvenlik stratejisi.**
1. Önizleme zorunlu. Replace all'a doğrudan apply yok, confirm dialog şart.
2. Atomic yazım her dosya için (`AtomicFileWriter` zaten var).
3. Per-file undo. Açık dosyada AvaloniaEdit undo stack'inde, kapalı dosyada session-scoped backup (`%TEMP%/Markus/replace-backups/{session-id}/{file-hash}.bak`). Markus kapanışında temizle.
4. External değişiklik tespit. Apply öncesi her dosyanın mtime'ı doğrula; değişmişse o dosyayı atla, kullanıcıya bildir.

### Acceptance Criteria
1. Cmd/Ctrl+Shift+F side panel açıyor
2. Search input typing → 300ms debounce sonrası canlı sonuçlar
3. Sonuçlar dosya-grouplu, snippet'lerde match highlighted
4. Çift tıklama dosya açma + satır navigation çalışıyor
5. Case sensitive / whole word / regex flag'leri SearchOverlay ile aynı semantik
6. Regex pattern hatalı ise input'ta inline error message, search çalışmaz
7. Replace All önce confirm dialog (sayı + dosya sayısı)
8. Apply sonrası açık dosyalar refresh, kapalı dosyalar disk'te güncel
9. External değişiklik olan dosyalar atlanır, kullanıcı bildirilir
10. Apply sonrası "Undo Replace All" komutu çalışır (session boyunca)
11. 10K `.md` dosyalı workspace'de ilk sonuç ≤500ms, total ≤2s
12. Cancel butonu in-flight search'ü iptal eder

### Efor Tahmini
**10-15 gün.** En büyük scope. Search engine + replace safety + UI panel + undo state. SearchOverlay UX zaten var, oradan referans.

### Riskler
- Streaming UI thread safety. Mitigation, `Dispatcher.UIThread.Post` ile batched updates (her 50ms).
- Undo state büyüyebilir. Mitigation, session-scoped backup `%TEMP%`'te, Markus kapanışında temizle. Disk usage cap ~100MB, aşılırsa eski backup'lar düşer.
- External edit conflict. Yukarıdaki mtime check çözer ama edge case, Markus yazarken external edit. Mitigation, file lock yok, en kötü ihtimal "skipped" raporu, veri kaybı yok.
- Regex DOS (catastrophic backtracking). Mitigation, `Regex` constructor'a `TimeSpan` timeout (1 saniye).

### Out of Scope
- Include/exclude glob patterns (`!node_modules/**`), sonraki spec
- Search history
- Saved searches
- Search non-`.md` files
- Replace preview diff view (sadece confirm dialog sayısı)

---

## Test Stratejisi

Markus'un `tests/Markus.Tests/` saf-logic test felsefesini takip et. UI integration test yok (Avalonia headless infra zaten yok).

| Özellik | Test alanı |
|---|---|
| QW1 | `TableCellNavigatorTests`, Tab/Shift-Tab navigation, auto-row, edge cases (ilk/son cell), CJK widths |
| QW2 | `WorkspaceIndexTests`, fuzzy match deterministik, FileWatcher delta, eager scan idempotent; `WikiLinkResolverTests`, match priority |
| QW3 | `WorkspaceSearchTests`, case/whole-word/regex semantics, streaming order, cancellation; `WorkspaceReplaceTests`, atomic write, mtime guard, undo stack |

**Conformance.** `MarkdownTableFormatter` zaten conformance test'lere sahip, QW1 reflow için ekstra test gerekmez. QW2'de Obsidian wiki-link parser ile karşılaştırmalı conformance (en az 20 örnek).

**Performans.** `benchmarks/Markus.Benchmarks/` zaten var. QW3 için 10K dosyalı sentetik workspace benchmark ekle.

---

## Release Sırası

Bağımlılık yok, paralel çıkabilir ama önerilen sıra.

1. **QW1 (Tablo editing)**, en küçük scope, mevcut altyapı %80 hazır, immediate user impact, marketing demo değeri yüksek
2. **QW3 (Project find-replace)**, bağımsız çalışabilir ama UI patternleri QW1'in user-feedback'inden faydalanır
3. **QW2 (Wiki-link)**, en büyük conceptual değişim (workspace concept'i tanıtıyor), QW3'ün workspace altyapısını paylaşabilir

**`WorkspaceIndex` paylaşım fırsatı.**
QW2 ve QW3 ikisi de `WorkspaceIndex` ihtiyacında. QW3'ten önce başlanırsa o spec'te `WorkspaceIndex` temel scaffold'unu yap, QW2 üzerine inşa etsin. Bu efor tahminini ~3 gün düşürür.

Birleşik tahmin, **~5-7 hafta** (3 gün overlap çıkartılarak).

---

## Açık Sorular (Kullanıcı Kararı)

1. **Workspace kavramı.** İlk açılan dosyanın klasörü mü, yoksa explicit "Open Folder" komutu mu varsayılan? (Obsidian explicit, VS Code her ikisi.)
2. **Wiki-link sözdizimi.** Obsidian-uyumlu `[[file-name]]` mi, yoksa standart markdown `[text](file.md)` mi? Birincisi pazar-uyumlu, ikincisi spec-pure.
3. **Project find-replace panel konumu.** Sol mu, sağ mı, veya kullanıcı seçimi? (OutlinePanel zaten bir panel kullandığı için layout decision matter.)
4. **Replace backup retention.** Session-scoped (Markus kapanışında sil) yeterli mi, yoksa N-gün persistent backup mı?
5. **Non-markdown dosya search?** QW3 sadece `.md` mi taramalı, yoksa `.txt`, `.json`, frontmatter dosyaları dahil mi? Default `.md`, opt-in genişletme önerim.

---

## Kaynak Araştırma

Bu spec'in talep tarafı kanıtı için bkz `docs/research/markdown-editor-pain-points-2026-06.md` (deep-research turunda 28 kaynak, 16 onaylı bulgu).
