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
    // Points. Matches macOS Tahoe's native window corner (tuned against a
    // side-by-side Finder window).
    private const double CornerRadius = 16;

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
        if (handle is null || handle.Handle == IntPtr.Zero)
        {
            return;
        }
        // Avalonia 12 hands back the NSWindow; older versions gave the NSView.
        var nsWindow = string.Equals(handle.HandleDescriptor, "NSWindow", StringComparison.Ordinal)
            ? handle.Handle
            : ObjC.Send(handle.Handle, ObjC.Sel("window"));
        var contentView = nsWindow != IntPtr.Zero ? ObjC.Send(nsWindow, ObjC.Sel("contentView")) : handle.Handle;
        if (contentView == IntPtr.Zero)
        {
            return;
        }
        ApplyTahoeMaterial(contentView, material);

        // Every layer in the window reports cornerRadius 0, so the visible
        // rounding is the window server's default shape, which on Tahoe reads
        // smaller and harder-edged than the system squircle. Clip the window's
        // content view to a continuous (squircle) corner so the app matches the
        // OS. The window is transparent, so the area outside the clip is the
        // desktop and the rounded shape is what shows.
        //
        // The content-view layer keeps its corner radius across a fullscreen
        // transition, where a rounded corner would clip the edge-to-edge content.
        // Square it off in fullscreen; the caller re-runs this when WindowState
        // changes. (Re-finds the content view each call so it is robust if the
        // view is ever recreated.)
        var radius = window.WindowState == WindowState.FullScreen ? 0 : CornerRadius;
        ApplyCornerRadius(contentView, radius);
    }

    private static void ApplyTahoeMaterial(IntPtr contentView, Material material)
    {
        var effectClass = ObjC.GetClass("NSVisualEffectView");
        if (effectClass == IntPtr.Zero)
        {
            return;
        }
        var setMaterial = ObjC.Sel("setMaterial:");
        var isKindOfClass = ObjC.Sel("isKindOfClass:");
        var objectAtIndex = ObjC.Sel("objectAtIndex:");
        var subviews = ObjC.Send(contentView, ObjC.Sel("subviews"));
        var subviewCount = ObjC.SendReturnLong(subviews, ObjC.Sel("count"));
        for (long i = 0; i < subviewCount; i++)
        {
            var view = ObjC.SendIdLong(subviews, objectAtIndex, i);
            if (view == IntPtr.Zero || !ObjC.SendBoolFromClass(view, isKindOfClass, effectClass))
            {
                continue;
            }
            ObjC.SendLong(view, setMaterial, (long)material);
            ObjC.SendLong(view, ObjC.Sel("setBlendingMode:"), 0L);
            ObjC.SendLong(view, ObjC.Sel("setState:"), 0L);
        }
    }

    private static void ApplyCornerRadius(IntPtr view, double radius)
    {
        if (view == IntPtr.Zero)
        {
            return;
        }
        ObjC.SendLong(view, ObjC.Sel("setWantsLayer:"), 1L);
        var layer = ObjC.Send(view, ObjC.Sel("layer"));
        if (layer == IntPtr.Zero)
        {
            return;
        }
        ObjC.SendDouble(layer, ObjC.Sel("setCornerRadius:"), radius);
        ObjC.SendLong(layer, ObjC.Sel("setMasksToBounds:"), 1L);
        // CACornerCurve.continuous gives the macOS squircle instead of a plain arc.
        var continuous = ObjC.MakeString("continuous");
        if (continuous != IntPtr.Zero)
        {
            ObjC.SendId(layer, ObjC.Sel("setCornerCurve:"), continuous);
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

        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        public static extern void SendDouble(IntPtr receiver, IntPtr selector, double value);

        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        public static extern IntPtr SendId(IntPtr receiver, IntPtr selector, IntPtr arg);

        public static IntPtr MakeString(string value)
        {
            var cls = GetClass("NSString");
            if (cls == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }
            var ptr = Marshal.StringToHGlobalAnsi(value);
            try
            {
                return SendId(cls, Sel("stringWithUTF8String:"), ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }
}
