using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace Markus.Views.Platform;

/// <summary>
/// Bridges document state to the native macOS window so Markus behaves like a
/// real document app: the title bar shows the file's proxy icon (draggable,
/// Cmd-clickable for the path), the close button shows the unsaved dot, and
/// opened files land in the Dock / Apple menu "Open Recent". No-ops off macOS.
/// </summary>
internal static class MacWindowIntegration
{
    /// <summary>
    /// Sets the window's represented file (the title-bar proxy icon). Pass null
    /// or empty for an untitled/scratch buffer to clear it.
    /// </summary>
    public static void SetRepresentedFile(Window window, string? path)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }
        var nsWindow = GetNSWindow(window);
        if (nsWindow == IntPtr.Zero)
        {
            return;
        }
        var ns = ObjC.MakeString(path ?? string.Empty);
        if (ns == IntPtr.Zero)
        {
            return;
        }
        ObjC.SendId(nsWindow, ObjC.Sel("setRepresentedFilename:"), ns);
    }

    /// <summary>Drives the unsaved dot inside the red close button.</summary>
    public static void SetDocumentEdited(Window window, bool edited)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }
        var nsWindow = GetNSWindow(window);
        if (nsWindow == IntPtr.Zero)
        {
            return;
        }
        ObjC.SendBool(nsWindow, ObjC.Sel("setDocumentEdited:"), edited);
    }

    /// <summary>
    /// Records an opened/saved file with the shared NSDocumentController so the
    /// Dock menu and Apple menu "Open Recent" reflect it.
    /// </summary>
    public static void NoteRecentDocument(string? path)
    {
        if (!OperatingSystem.IsMacOS() || string.IsNullOrEmpty(path))
        {
            return;
        }
        var controllerClass = ObjC.GetClass("NSDocumentController");
        if (controllerClass == IntPtr.Zero)
        {
            return;
        }
        var controller = ObjC.Send(controllerClass, ObjC.Sel("sharedDocumentController"));
        var urlClass = ObjC.GetClass("NSURL");
        if (controller == IntPtr.Zero || urlClass == IntPtr.Zero)
        {
            return;
        }
        var ns = ObjC.MakeString(path);
        if (ns == IntPtr.Zero)
        {
            return;
        }
        var url = ObjC.SendId(urlClass, ObjC.Sel("fileURLWithPath:"), ns);
        if (url == IntPtr.Zero)
        {
            return;
        }
        ObjC.SendId(controller, ObjC.Sel("noteNewRecentDocumentURL:"), url);
    }

    private static IntPtr GetNSWindow(Window window)
    {
        var handle = window.TryGetPlatformHandle();
        if (handle is null || handle.Handle == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }
        // Avalonia 12 hands back the NSWindow; older versions gave the NSView.
        return string.Equals(handle.HandleDescriptor, "NSWindow", StringComparison.Ordinal)
            ? handle.Handle
            : ObjC.Send(handle.Handle, ObjC.Sel("window"));
    }

    private static class ObjC
    {
        private const string Lib = "/usr/lib/libobjc.dylib";

        [DllImport(
            Lib,
            EntryPoint = "objc_getClass",
            CharSet = CharSet.Ansi,
            BestFitMapping = false,
            ThrowOnUnmappableChar = true
        )]
        public static extern IntPtr GetClass(string name);

        [DllImport(
            Lib,
            EntryPoint = "sel_registerName",
            CharSet = CharSet.Ansi,
            BestFitMapping = false,
            ThrowOnUnmappableChar = true
        )]
        public static extern IntPtr Sel(string name);

        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        public static extern IntPtr Send(IntPtr receiver, IntPtr selector);

        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        public static extern IntPtr SendId(IntPtr receiver, IntPtr selector, IntPtr arg);

        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        public static extern void SendBool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool value);

        public static IntPtr MakeString(string value)
        {
            var cls = GetClass("NSString");
            if (cls == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }
            // UTF-8 (not ANSI) so non-ASCII file paths survive intact.
            var ptr = Marshal.StringToCoTaskMemUTF8(value);
            try
            {
                return SendId(cls, Sel("stringWithUTF8String:"), ptr);
            }
            finally
            {
                Marshal.FreeCoTaskMem(ptr);
            }
        }
    }
}
