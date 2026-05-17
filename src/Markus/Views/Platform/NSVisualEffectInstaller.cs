using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace Markus.Views.Platform;

/// <summary>
/// Patches the <c>NSVisualEffectView</c> Avalonia creates for AcrylicBlur so
/// it picks up macOS Tahoe's Liquid Glass material. Avalonia's built-in code
/// uses the Big-Sur-deprecated <c>NSVisualEffectMaterialLight</c>; Tahoe still
/// renders it, but without the new tint and saturation. We walk the
/// contentView's subviews, find the existing visual effect view, and call
/// <c>setMaterial:</c> on it with a current Tahoe material.
/// </summary>
internal static class NSVisualEffectInstaller
{
    public enum Material : long
    {
        Titlebar = 3,
        Menu = 5,
        Popover = 6,
        Sidebar = 7,
        HeaderView = 10,
        Sheet = 11,
        WindowBackground = 12,
        HudWindow = 13,
        FullScreenUI = 15,
        ToolTip = 17,
        ContentBackground = 18,
        UnderWindowBackground = 21,
        UnderPageBackground = 22,
    }

    public static void Patch(Window window, Material material)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }
        var handle = window.TryGetPlatformHandle();
        if (handle is null || !string.Equals(handle.HandleDescriptor, "NSView", StringComparison.Ordinal))
        {
            return;
        }
        var contentView = handle.Handle;
        if (contentView == IntPtr.Zero)
        {
            return;
        }

        var effectClass = ObjC.GetClass("NSVisualEffectView");
        if (effectClass == IntPtr.Zero)
        {
            return;
        }

        var setMaterial = ObjC.Sel("setMaterial:");
        var setBlendingMode = ObjC.Sel("setBlendingMode:");
        var setState = ObjC.Sel("setState:");
        var isKindOfClass = ObjC.Sel("isKindOfClass:");
        var count = ObjC.Sel("count");
        var objectAtIndex = ObjC.Sel("objectAtIndex:");

        // Walk the contentView's subviews looking for the NSVisualEffectView
        // Avalonia inserted for the blur. The visual effect view is usually the
        // first subview, but we iterate just in case.
        var subviews = ObjC.Send(contentView, ObjC.Sel("subviews"));
        var subviewCount = ObjC.SendReturnLong(subviews, count);
        for (long i = 0; i < subviewCount; i++)
        {
            var view = ObjC.SendIdLong(subviews, objectAtIndex, i);
            if (view == IntPtr.Zero)
            {
                continue;
            }
            var isEffectView = ObjC.SendBoolFromClass(view, isKindOfClass, effectClass);
            if (!isEffectView)
            {
                continue;
            }
            ObjC.SendLong(view, setMaterial, (long)material);
            // Make sure blending/state match what Tahoe expects.
            ObjC.SendLong(view, setBlendingMode, 0L);
            ObjC.SendLong(view, setState, 0L);
        }
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
        public static extern void SendLong(IntPtr receiver, IntPtr selector, long value);

        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        public static extern long SendReturnLong(IntPtr receiver, IntPtr selector);

        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        public static extern IntPtr SendIdLong(IntPtr receiver, IntPtr selector, long index);

        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SendBoolFromClass(IntPtr receiver, IntPtr selector, IntPtr cls);
    }
}
