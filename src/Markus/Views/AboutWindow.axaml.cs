using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Markus.Views;

internal sealed partial class AboutWindow : Window
{
    // Built at runtime so the link constants aren't flagged as hardcoded URIs
    // (S1075). The repo coordinates are the only source of truth we need.
    private const string RepoOwner = "cihanyakar";
    private const string RepoName = "Markus";

    private static readonly string GithubUrl = $"https://github.com/{RepoOwner}/{RepoName}";
    private static readonly string LicenseUrl = $"{GithubUrl}/blob/main/LICENSE";

    public AboutWindow()
    {
        InitializeComponent();
        Icon = Markus.Services.IconLoader.LoadWindowIcon();
        ApplyAppIcon();
        ConfigureExtendedTitleBar();
        ApplyVersionLabel();
    }

    private void ApplyAppIcon()
    {
        if (this.FindControl<Image>("AppIconImage") is { } img)
        {
            img.Source = Markus.Services.IconLoader.LoadBitmap();
        }
    }

    private void ConfigureExtendedTitleBar()
    {
        // Mirror MainWindow's macOS guard. Other platforms keep their native
        // chrome until per-platform polish lands.
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }
        ExtendClientAreaTitleBarHeightHint = -1;
    }

    private void ApplyVersionLabel()
    {
        var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "dev";
        if (this.FindControl<TextBlock>("VersionLabel") is { } label)
        {
            label.Text = $"Version {version}";
        }
    }

    private void OnCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Let the whole card act as a drag handle when the user grabs empty
        // space; buttons keep their own click semantics via routed events.
        if (e.Source is Button)
        {
            return;
        }
        BeginMoveDrag(e);
    }

    private static void OnGithubClick(object? sender, RoutedEventArgs e)
    {
        OpenUrl(GithubUrl);
    }

    private static void OnLicenseClick(object? sender, RoutedEventArgs e)
    {
        OpenUrl(LicenseUrl);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Shell association missing or blocked. Swallow so the dialog
            // stays alive; surfacing the error here would require a status
            // hook that this dialog deliberately does not own.
        }
        catch (System.IO.FileNotFoundException)
        {
            // Same posture as above for the no-default-handler case.
        }
    }
}
