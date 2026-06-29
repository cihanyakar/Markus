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
    private static bool _terminationGuardInstalled;

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
        _installed = InstallDelegateMethods();
    }

    /// <summary>
    /// Stops a NativeAOT shutdown crash on macOS. AppKit's Quit path
    /// (Cmd+Q, Dock &gt; Quit, logout) ends in <c>exit()</c>, whose C++ atexit
    /// chain destroys a global <c>ComPtr&lt;IAvnDispatcher&gt;</c> in
    /// libAvaloniaNative by calling its managed <c>Release</c> after the
    /// runtime has already torn down, which fail-fasts (abort). Avalonia has
    /// already closed every window and persisted the session by the time
    /// <c>applicationWillTerminate:</c> fires, so we end the process here with
    /// <c>_exit</c>, skipping the atexit teardown that crashes. Closing the
    /// window normally takes Avalonia's own shutdown path and never reaches
    /// this, so it is unaffected.
    /// </summary>
    public static void InstallTerminationGuard()
    {
        if (!OperatingSystem.IsMacOS() || _terminationGuardInstalled)
        {
            return;
        }
        var nsApp = ObjC.SendId(ObjC.GetClass("NSApplication"), ObjC.Sel("sharedApplication"));
        if (nsApp == IntPtr.Zero)
        {
            return;
        }
        var del = ObjC.SendId(nsApp, ObjC.Sel("delegate"));
        if (del == IntPtr.Zero)
        {
            return;
        }
        var cls = ObjC.SendId(del, ObjC.Sel("class"));
        if (cls == IntPtr.Zero)
        {
            return;
        }
#pragma warning disable SA1011
        var imp = (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void>)&WillTerminateTrampoline;
#pragma warning restore SA1011
        InstallOrSwap(cls, ObjC.Sel("applicationWillTerminate:"), imp, "v@:@");
        _terminationGuardInstalled = true;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void WillTerminateTrampoline(IntPtr self, IntPtr cmd, IntPtr notification)
    {
        // End the process before AppKit's exit() runs the crashing atexit
        // teardown (see InstallTerminationGuard). _exit skips atexit and
        // C++ static destructors, so the global ComPtr<IAvnDispatcher> is
        // never released into a torn-down runtime.
        ObjC.Exit(0);
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

    // macOS 11+ delivers Finder "open" events via -[NSApplicationDelegate
    // application:openURLs:] instead of the deprecated openFiles:. We bridge
    // both for compatibility across macOS versions.
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void OpenUrlsTrampoline(IntPtr self, IntPtr cmd, IntPtr sender, IntPtr urlsArray)
    {
        if (_callback is null || urlsArray == IntPtr.Zero)
        {
            return;
        }
        var count = ObjC.SendReturnLong(urlsArray, ObjC.Sel("count"));
        for (long i = 0; i < count; i++)
        {
            var nsUrl = ObjC.SendIdLong(urlsArray, ObjC.Sel("objectAtIndex:"), i);
            if (nsUrl == IntPtr.Zero)
            {
                continue;
            }
            var nsString = ObjC.SendId(nsUrl, ObjC.Sel("path"));
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

    private static bool InstallDelegateMethods()
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
#pragma warning disable SA1011 // Function-pointer modifier `]<` can't be separated by a space.
        var filesImp = (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, void>)&OpenFilesTrampoline;
        var urlsImp = (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, void>)&OpenUrlsTrampoline;
#pragma warning restore SA1011

        // "v@:@@" means: void method on (self, _cmd, sender, NSArray*).
        InstallOrSwap(cls, ObjC.Sel("application:openFiles:"), filesImp, "v@:@@");
        InstallOrSwap(cls, ObjC.Sel("application:openURLs:"), urlsImp, "v@:@@");
        return true;
    }

    private static void InstallOrSwap(IntPtr cls, IntPtr selector, IntPtr imp, string typeEncoding)
    {
        if (ObjC.AddMethod(cls, selector, imp, typeEncoding))
        {
            return;
        }
        var existing = ObjC.GetInstanceMethod(cls, selector);
        if (existing == IntPtr.Zero)
        {
            return;
        }
        ObjC.SetImplementation(existing, imp);
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

        [DllImport("/usr/lib/libSystem.dylib", EntryPoint = "_exit")]
        public static extern void Exit(int status);
    }
}
