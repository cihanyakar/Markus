using Avalonia.Input;
using Markus.Services;

namespace Markus.Tests.Services;

public sealed class KeyBindingServiceTests : IDisposable
{
    private readonly string _tempDir;

    public KeyBindingServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "markus-test-keys-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup; not critical for test correctness.
        }
    }

    [Fact]
    public void Returns_Default_For_Unmodified_Action()
    {
        var svc = new KeyBindingService(_tempDir);

        var gesture = svc.GetGesture(ShortcutActions.OpenFile);

        gesture.ShouldNotBeNull();
        gesture!.Key.ShouldBe(Key.O);
        gesture.KeyModifiers.ShouldBe(KeyModifiers.Meta);
    }

    [Fact]
    public void Set_Custom_Gesture_Overrides_Default()
    {
        var svc = new KeyBindingService(_tempDir);
        var custom = new KeyGesture(Key.P, KeyModifiers.Meta | KeyModifiers.Shift);

        svc.SetGesture(ShortcutActions.OpenFile.Id, custom);

        svc.GetGesture(ShortcutActions.OpenFile).ShouldBe(custom);
    }

    [Fact]
    public void Set_Null_Gesture_Clears_Action()
    {
        var svc = new KeyBindingService(_tempDir);

        svc.SetGesture(ShortcutActions.OpenFile.Id, null);

        svc.GetGesture(ShortcutActions.OpenFile).ShouldBeNull();
    }

    [Fact]
    public void Reset_Restores_Default()
    {
        var svc = new KeyBindingService(_tempDir);
        svc.SetGesture(ShortcutActions.OpenFile.Id, new KeyGesture(Key.X, KeyModifiers.Meta));

        svc.ResetGesture(ShortcutActions.OpenFile.Id);

        svc.GetGesture(ShortcutActions.OpenFile).ShouldBe(ShortcutActions.OpenFile.DefaultGesture);
    }

    [Fact]
    public void ResetAll_Clears_Every_Override()
    {
        var svc = new KeyBindingService(_tempDir);
        svc.SetGesture(ShortcutActions.OpenFile.Id, new KeyGesture(Key.X, KeyModifiers.Meta));
        svc.SetGesture(ShortcutActions.Find.Id, new KeyGesture(Key.Y, KeyModifiers.Meta));

        svc.ResetAll();

        svc.GetGesture(ShortcutActions.OpenFile).ShouldBe(ShortcutActions.OpenFile.DefaultGesture);
        svc.GetGesture(ShortcutActions.Find).ShouldBe(ShortcutActions.Find.DefaultGesture);
    }

    [Fact]
    public void Overrides_Persist_Across_Service_Instances()
    {
        var custom = new KeyGesture(Key.J, KeyModifiers.Meta | KeyModifiers.Control);
        var first = new KeyBindingService(_tempDir);
        first.SetGesture(ShortcutActions.OpenFile.Id, custom);

        // Fresh service reads the same JSON file from disk.
        var second = new KeyBindingService(_tempDir);

        var loaded = second.GetGesture(ShortcutActions.OpenFile);
        loaded.ShouldNotBeNull();
        loaded!.Key.ShouldBe(custom.Key);
        loaded.KeyModifiers.ShouldBe(custom.KeyModifiers);
    }

    [Fact]
    public void FindConflict_Returns_Owner_When_Gesture_Already_Used()
    {
        var svc = new KeyBindingService(_tempDir);

        // Cmd+O is OpenFile's default. Probing it from a different action's
        // perspective should flag the conflict.
        var conflict = svc.FindConflict(ShortcutActions.Find.Id, new KeyGesture(Key.O, KeyModifiers.Meta));

        conflict.ShouldBe(ShortcutActions.OpenFile.DisplayName);
    }

    [Fact]
    public void FindConflict_Returns_Null_When_Gesture_Free()
    {
        var svc = new KeyBindingService(_tempDir);

        var conflict = svc.FindConflict(
            ShortcutActions.Find.Id,
            new KeyGesture(Key.Q, KeyModifiers.Meta | KeyModifiers.Shift | KeyModifiers.Alt)
        );

        conflict.ShouldBeNull();
    }

    [Fact]
    public void Changed_Event_Fires_On_Set()
    {
        var svc = new KeyBindingService(_tempDir);
        var fired = 0;
        svc.Changed += (_, _) => fired++;

        svc.SetGesture(ShortcutActions.OpenFile.Id, new KeyGesture(Key.P, KeyModifiers.Meta));

        fired.ShouldBe(1);
    }

    [Fact]
    public void Changed_Event_Fires_On_Reset()
    {
        var svc = new KeyBindingService(_tempDir);
        svc.SetGesture(ShortcutActions.OpenFile.Id, new KeyGesture(Key.P, KeyModifiers.Meta));
        var fired = 0;
        svc.Changed += (_, _) => fired++;

        svc.ResetGesture(ShortcutActions.OpenFile.Id);

        fired.ShouldBe(1);
    }

    [Fact]
    public void ResetAll_Without_Overrides_Does_Not_Fire_Changed()
    {
        var svc = new KeyBindingService(_tempDir);
        var fired = 0;
        svc.Changed += (_, _) => fired++;

        svc.ResetAll();

        fired.ShouldBe(0);
    }

    [Fact]
    public void Corrupted_Json_Falls_Back_To_Defaults()
    {
        File.WriteAllText(Path.Combine(_tempDir, "shortcuts.json"), "{not valid json");

        var svc = new KeyBindingService(_tempDir);

        svc.GetGesture(ShortcutActions.OpenFile).ShouldBe(ShortcutActions.OpenFile.DefaultGesture);
    }
}
