using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace Markus.Views.Platform;

/// <summary>
/// Intercepts macOS Finder file-open events so double-clicking an .md (or any
/// associated file) routes the path into Markus. Avalonia 12 dropped its
/// public <c>UrlsOpened</c> surface, so we override the running NSApp's
/// delegate <c>application:openFiles:</c> method at the Objective-C runtime
/// level. This catches both initial-launch documents (queued by macOS until
/// the delegate is ready) and "while running" double-clicks.
/// </summary>
internal static unsafe class MacosAppleEventHandler
{
    private static Action<string>? _callback;
    private static bool _installed;

    public static void Register(Action<string> onFileOpen)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }
        _callback = onFileOpen;
        if (_installed)
        {
            return;
        }
        _installed = InstallDelegateMethod();
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void OpenFilesTrampoline(IntPtr self, IntPtr cmd, IntPtr sender, IntPtr filesArray)
    {
        if (_callback is null || filesArray == IntPtr.Zero)
        {
            return;
        }
        var count = ObjC.SendReturnLong(filesArray, ObjC.Sel("count"));
        for (long i = 0; i < count; i++)
        {
            var nsString = ObjC.SendIdLong(filesArray, ObjC.Sel("objectAtIndex:"), i);
            if (nsString == IntPtr.Zero)
            {
                continue;
            }
            var utf8 = ObjC.SendId(nsString, ObjC.Sel("UTF8String"));
            if (utf8 == IntPtr.Zero)
            {
                continue;
            }
            var path = Marshal.PtrToStringUTF8(utf8);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }
            var capture = _callback;
            Dispatcher.UIThread.Post(() => capture(path));
        }
    }

    private static bool InstallDelegateMethod()
    {
        var nsApp = ObjC.SendId(ObjC.GetClass("NSApplication"), ObjC.Sel("sharedApplication"));
        if (nsApp == IntPtr.Zero)
        {
            return false;
        }
        var del = ObjC.SendId(nsApp, ObjC.Sel("delegate"));
        if (del == IntPtr.Zero)
        {
            return false;
        }
        var cls = ObjC.SendId(del, ObjC.Sel("class"));
        if (cls == IntPtr.Zero)
        {
            return false;
        }
        var selector = ObjC.Sel("application:openFiles:");
#pragma warning disable SA1011 // Function-pointer modifier `]<` can't be separated by a space.
        var imp = (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, void>)&OpenFilesTrampoline;
#pragma warning restore SA1011

        // The type encoding "v@:@@" means: void method on (self id, _cmd SEL,
        // sender id, files NSArray*). Same shape as the AppKit declaration.
        var added = ObjC.AddMethod(cls, selector, imp, "v@:@@");
        if (added)
        {
            return true;
        }
        var existing = ObjC.GetInstanceMethod(cls, selector);
        if (existing == IntPtr.Zero)
        {
            return false;
        }
        ObjC.SetImplementation(existing, imp);
        return true;
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
        public static extern IntPtr SendId(IntPtr receiver, IntPtr selector);

        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        public static extern long SendReturnLong(IntPtr receiver, IntPtr selector);

        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        public static extern IntPtr SendIdLong(IntPtr receiver, IntPtr selector, long arg);

        [DllImport(
            Lib,
            EntryPoint = "class_addMethod",
            CharSet = CharSet.Ansi,
            BestFitMapping = false,
            ThrowOnUnmappableChar = true
        )]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool AddMethod(IntPtr cls, IntPtr name, IntPtr imp, string types);

        [DllImport(Lib, EntryPoint = "class_getInstanceMethod")]
        public static extern IntPtr GetInstanceMethod(IntPtr cls, IntPtr name);

        [DllImport(Lib, EntryPoint = "method_setImplementation")]
        public static extern IntPtr SetImplementation(IntPtr method, IntPtr imp);
    }
}
