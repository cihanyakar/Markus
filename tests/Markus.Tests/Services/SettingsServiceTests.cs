using Markus.Models;
using Markus.Services;

namespace Markus.Tests.Services;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir;

    public SettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"markus_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var sut = CreateService("nonexistent");

        var result = sut.Load();

        // Verify all default values, not just non-null.
        // A mutation that changes any default should break this test.
        result.Renderer.ShouldBe(RendererKind.Native);
        result.Language.ShouldBe("en");
        result.Theme.ShouldBe("GitHubDark");
        result.CodeTheme.ShouldBe("Auto");
        result.DefaultViewMode.ShouldBe(ViewMode.Preview);
        result.ShowOutline.ShouldBeFalse();
        result.OutlinePlacement.ShouldBe(OutlinePlacement.Right);
        result.FontSize.ShouldBe(16.0);
        result.MonoFont.ShouldBe("Menlo");
        result.ThemeMode.ShouldBe("System");
        result.IsSourceSoftWrap.ShouldBeFalse();
        result.IsPreviewSoftWrap.ShouldBeTrue();
        result.MermaidScale.ShouldBe(1.0);
        result.RecentFiles.ShouldBeEmpty();
        result.LastOpenedFile.ShouldBeNull();
        result.LastScrollLine.ShouldBe(0);
        result.RestoreSessionOnLaunch.ShouldBeFalse();
    }

    [Fact]
    public void Load_CorruptedJson_ReturnsDefaults()
    {
        var dir = Path.Combine(_tempDir, "corrupt");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "settings.json"), "{{not valid json}}");

        var sut = CreateService(dir);

        var result = sut.Load();

        result.Theme.ShouldBe("GitHubDark");
        result.FontSize.ShouldBe(16.0);
        result.IsPreviewSoftWrap.ShouldBeTrue();
    }

    [Fact]
    public void Load_NullJson_ReturnsDefaults()
    {
        var dir = Path.Combine(_tempDir, "null");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "settings.json"), "null");

        var sut = CreateService(dir);

        var result = sut.Load();

        result.ShouldNotBeNull();
        result.FontSize.ShouldBe(16.0);
        result.Theme.ShouldBe("GitHubDark");
    }

    [Fact]
    public void SaveAndLoad_RoundTrips_AllPropertyTypes()
    {
        var dir = Path.Combine(_tempDir, "roundtrip");
        var sut = CreateService(dir);

        // Cover every property type: string, double, bool, enum, int, list, nullable string
        var settings = new AppSettings
        {
            Renderer = RendererKind.Placeholder,
            Language = "tr",
            Theme = "Nord",
            CodeTheme = "Dracula",
            DefaultViewMode = ViewMode.SplitHorizontal,
            ShowOutline = true,
            OutlinePlacement = OutlinePlacement.Left,
            FontSize = 22.0,
            MonoFont = "Cascadia Code",
            ThemeMode = "Dark",
            IsSourceSoftWrap = true,
            IsPreviewSoftWrap = false,
            MermaidScale = 1.5,
            RecentFiles = new List<string> { "/tmp/a.md", "/tmp/b.md" },
            LastOpenedFile = "/tmp/a.md",
            LastScrollLine = 42,
            RestoreSessionOnLaunch = true,
        };

        sut.Save(settings);
        var loaded = sut.Load();

        loaded.Renderer.ShouldBe(RendererKind.Placeholder);
        loaded.Language.ShouldBe("tr");
        loaded.Theme.ShouldBe("Nord");
        loaded.CodeTheme.ShouldBe("Dracula");
        loaded.DefaultViewMode.ShouldBe(ViewMode.SplitHorizontal);
        loaded.ShowOutline.ShouldBeTrue();
        loaded.OutlinePlacement.ShouldBe(OutlinePlacement.Left);
        loaded.FontSize.ShouldBe(22.0);
        loaded.MonoFont.ShouldBe("Cascadia Code");
        loaded.ThemeMode.ShouldBe("Dark");
        loaded.IsSourceSoftWrap.ShouldBeTrue();
        loaded.IsPreviewSoftWrap.ShouldBeFalse();
        loaded.MermaidScale.ShouldBe(1.5);
        loaded.RecentFiles.Count.ShouldBe(2);
        loaded.RecentFiles[0].ShouldBe("/tmp/a.md");
        loaded.RecentFiles[1].ShouldBe("/tmp/b.md");
        loaded.LastOpenedFile.ShouldBe("/tmp/a.md");
        loaded.LastScrollLine.ShouldBe(42);
        loaded.RestoreSessionOnLaunch.ShouldBeTrue();
    }

    [Fact]
    public void Save_CreatesDirectoryIfMissing()
    {
        var dir = Path.Combine(_tempDir, "auto", "nested");
        var sut = CreateService(dir);

        sut.Save(new AppSettings());

        Directory.Exists(dir).ShouldBeTrue();
        File.Exists(Path.Combine(dir, "settings.json")).ShouldBeTrue();
    }

    [Fact]
    public void Save_FiresChangedEvent_WithSameSettings()
    {
        var dir = Path.Combine(_tempDir, "event");
        var sut = CreateService(dir);
        AppSettings? received = null;
        object? receivedSender = null;
        sut.Changed += (sender, e) =>
        {
            received = e.Settings;
            receivedSender = sender;
        };

        var settings = new AppSettings { Theme = "Custom", ShowOutline = true };
        sut.Save(settings);

        received.ShouldNotBeNull();
        received.ShouldBeSameAs(settings);
        receivedSender.ShouldBeSameAs(sut);
    }

    [Fact]
    public void Save_OverwritesPreviousFile()
    {
        var dir = Path.Combine(_tempDir, "overwrite");
        var sut = CreateService(dir);

        sut.Save(new AppSettings { Theme = "First" });
        sut.Save(new AppSettings { Theme = "Second" });

        var loaded = sut.Load();
        loaded.Theme.ShouldBe("Second");
    }

    [Fact]
    public void Load_PartialJson_FillsDefaultsForMissingFields()
    {
        var dir = Path.Combine(_tempDir, "partial");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "settings.json"), """{"theme":"Custom"}""");

        var sut = CreateService(dir);

        var result = sut.Load();

        result.Theme.ShouldBe("Custom");
        result.FontSize.ShouldBe(16.0);
        result.MermaidScale.ShouldBe(1.0);
        result.IsPreviewSoftWrap.ShouldBeTrue();
    }

    [Fact]
    public void Save_StillWritesFile_WhenChangedHandlerThrows()
    {
        var dir = Path.Combine(_tempDir, "handler_throws");
        var sut = CreateService(dir);
        sut.Changed += (_, _) => throw new InvalidOperationException("handler boom");

        var settings = new AppSettings { Theme = "Resilient" };

        // Save raises the event after writing, so the file should exist even though the handler throws.
        // The exception itself should propagate (we are not swallowing it).
        Should.Throw<InvalidOperationException>(() => sut.Save(settings));

        File.Exists(Path.Combine(dir, "settings.json")).ShouldBeTrue();
        var loaded = sut.Load();
        loaded.Theme.ShouldBe("Resilient");
    }

    [Fact]
    public void Load_ExtraUnknownProperties_DoesNotFail()
    {
        var dir = Path.Combine(_tempDir, "forward_compat");
        Directory.CreateDirectory(dir);
        var json = """
            {
                "theme": "Nord",
                "fontSize": 20.0,
                "futureFeatureFlag": true,
                "nestedFuture": { "x": 1 },
                "anotherNewField": "hello"
            }
            """;
        File.WriteAllText(Path.Combine(dir, "settings.json"), json);

        var sut = CreateService(dir);

        var result = sut.Load();

        result.Theme.ShouldBe("Nord");
        result.FontSize.ShouldBe(20.0);
        // Known defaults still apply for fields absent from the JSON
        result.MermaidScale.ShouldBe(1.0);
    }

    [Fact]
    public void Constructor_DirectoryWithSpacesAndSpecialChars_WorksCorrectly()
    {
        var dir = Path.Combine(_tempDir, "path with spaces", "special (chars) & more");
        var sut = CreateService(dir);

        var settings = new AppSettings { Theme = "SpaceTest", FontSize = 14.0 };
        sut.Save(settings);
        var loaded = sut.Load();

        loaded.Theme.ShouldBe("SpaceTest");
        loaded.FontSize.ShouldBe(14.0);
    }

    [Fact]
    public void TryLoad_MissingFile_ReturnsFalseAndDefaults()
    {
        var sut = CreateService(Path.Combine(_tempDir, "tryload-missing"));

        var succeeded = sut.TryLoad(out var loaded);

        succeeded.ShouldBeFalse();
        loaded.Theme.ShouldBe("GitHubDark"); // defaults still populated
    }

    [Fact]
    public void TryLoad_CorruptedJson_ReturnsFalseAndDefaults()
    {
        var dir = Path.Combine(_tempDir, "tryload-corrupt");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "settings.json"), "{{not valid json}}");
        var sut = CreateService(dir);

        var succeeded = sut.TryLoad(out var loaded);

        succeeded.ShouldBeFalse();
        loaded.Theme.ShouldBe("GitHubDark");
    }

    [Fact]
    public void TryLoad_ValidFile_ReturnsTrueAndLoaded()
    {
        var dir = Path.Combine(_tempDir, "tryload-valid");
        var sut = CreateService(dir);
        sut.Save(new AppSettings { Theme = "TryLoadTheme", FontSize = 18.0 });

        var succeeded = sut.TryLoad(out var loaded);

        succeeded.ShouldBeTrue();
        loaded.Theme.ShouldBe("TryLoadTheme");
        loaded.FontSize.ShouldBe(18.0);
    }

    [Fact]
    public async Task SaveDebounced_CoalescesRapidCallsIntoOneWrite()
    {
        // Slider-bound properties (FontSize, EditorFontSize, MermaidScale)
        // fire many ValueChanged events per drag; routing each through Save
        // produces one fsync per tick. SaveDebounced collapses a burst into
        // a single trailing write — the file's content reflects the LAST
        // call, and the number of physical writes is bounded.
        var dir = Path.Combine(_tempDir, "debounce");
        var sut = CreateService(dir);

        for (var i = 0; i < 25; i++)
        {
            sut.SaveDebounced(new AppSettings { FontSize = 10 + i }, TimeSpan.FromMilliseconds(50));
        }
        // Before the debounce delay elapses no write should have happened.
        File.Exists(Path.Combine(dir, "settings.json")).ShouldBeFalse();

        await Task.Delay(200, TestContext.Current.CancellationToken);

        // After the delay, exactly one write reflects the last call.
        sut.Load().FontSize.ShouldBe(10 + 24);
    }

    [Fact]
    public void FlushPendingSave_WritesImmediately_AndCancelsTimer()
    {
        // Shutdown / PersistSession paths cannot wait for the debounce to
        // elapse; they must be able to force a flush so the latest slider
        // value lands on disk before the app exits.
        var dir = Path.Combine(_tempDir, "flush");
        var sut = CreateService(dir);

        sut.SaveDebounced(new AppSettings { FontSize = 99 }, TimeSpan.FromSeconds(60));
        sut.FlushPendingSave();

        sut.Load().FontSize.ShouldBe(99);
    }

    [Fact]
    public void Save_SwallowsIoExceptionFromAtomicWriter()
    {
        // Save sits on a hot path (slider drags, partial-property setters,
        // PersistSession at close) where callers cannot reasonably catch
        // every IO failure mode AtomicFileWriter now surfaces (Windows
        // sharing violation, transient kernel-level failure). The service
        // owns the persistence contract; Save must be best-effort so a
        // transient failure does not crash the UI thread.
        var dir = Path.Combine(_tempDir, "save-swallows");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "settings.json");
        // Pre-occupy the destination with a directory so AtomicFileWriter's
        // File.Move fails. This stand-in for the runtime failure modes
        // (sharing violation, permission denied) is the same shape: an
        // IOException out of File.Move.
        Directory.CreateDirectory(path);
        var sut = CreateService(dir);

        Should.NotThrow(() => sut.Save(new AppSettings { Theme = "Swallow" }));
    }

    private static SettingsService CreateService(string directory)
    {
        return new SettingsService(directory);
    }
}
