// SBGVR: prototype. Loads UUVR's VR runtime files from the patcher's own folder instead of copying them into
// the game's data folder, so the mod writes nothing outside BepInEx.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

/// <summary>
/// Unity only looks for XR plug-ins in the game's data folder: the subsystem manifests in
/// <c>&lt;Data&gt;\UnitySubsystems</c>, the native plug-ins in <c>&lt;Data&gt;\Plugins</c>, and the OpenVR plug-in
/// reads <c>&lt;Data&gt;\StreamingAssets\SteamVR\OpenVRSettings.asset</c>. Instead of copying UUVR's files there,
/// this points those lookups at the same files in <c>CopyToGame</c>, next to the patcher, by replacing the file
/// functions in the import tables of UnityPlayer.dll (which looks up the plug-ins), Mono (which loads them for
/// [DllImport]) and the two XR plug-ins once they load (which read their own files). Only lookups of those
/// paths change; every other file goes through untouched, and nothing in the game folder is created, changed or
/// deleted.
/// </summary>
internal static class GameFolderRedirect
{
    private const int MaxLoggedRedirects = 60;

    private static readonly string[] RuntimePlugins = { "openvr_api.dll", "openxr_loader.dll", "UnityOpenXR.dll", "XRSDKOpenVR.dll" };

    private static string _dataPath = "";      // <game>\<name>_Data, no trailing separator
    private static string _sourceData = "";    // CopyToGame\Data
    private static string _sourcePlugins = ""; // CopyToGame\Plugins\x64
    private static readonly HashSet<string> PluginNames = new(RuntimePlugins, StringComparer.OrdinalIgnoreCase);

    // The XR plug-ins read their own files (the OpenVR plug-in its settings in StreamingAssets\SteamVR).
    private static readonly HashSet<string> PluginsWithOwnLookups = new(StringComparer.OrdinalIgnoreCase) { "UnityOpenXR.dll", "XRSDKOpenVR.dll" };
    private static readonly HashSet<IntPtr> PatchedModules = new();
    private static readonly object Lock = new();
    private static readonly HashSet<string> Logged = new(StringComparer.OrdinalIgnoreCase);

    public static void Install(string copyToGamePath, string dataPath)
    {
        _dataPath = Path.GetFullPath(dataPath).TrimEnd('\\', '/');
        _sourceData = Path.Combine(copyToGamePath, "Data");
        _sourcePlugins = Path.Combine(Path.Combine(copyToGamePath, "Plugins"), IntPtr.Size == 8 ? "x64" : "x86");

        Console.WriteLine($"[Redirect] Game data folder: {_dataPath}");
        Console.WriteLine($"[Redirect] VR runtime files are read from: {copyToGamePath}");
        ReportGameFolderCopies();

        var unity = GetModuleHandleW("UnityPlayer.dll");
        if (unity == IntPtr.Zero)
        {
            Console.WriteLine("[Redirect] [FAILED] UnityPlayer.dll isn't loaded; nothing redirected.");
            return;
        }
        var count = PatchImports(unity, "UnityPlayer.dll", AllHooks);

        // Mono loads native plug-ins for [DllImport] itself.
        var mono = FindMono();
        if (mono != IntPtr.Zero) count += PatchImports(mono, "Mono", LibraryHooks);

        // Loaded now from the mod's folder, so the XR plug-ins that need them find them by name however Unity loads
        // the plug-ins.
        foreach (var loader in new[] { "openxr_loader.dll", "openvr_api.dll" })
        {
            var path = Path.Combine(_sourcePlugins, loader);
            var module = LoadLibraryExW(path, IntPtr.Zero, 0x8); // LOAD_WITH_ALTERED_SEARCH_PATH
            Console.WriteLine(module != IntPtr.Zero
                ? $"[Redirect] Loaded {path}"
                : $"[Redirect] [FAILED] Loading {path} failed with Windows error {Marshal.GetLastWin32Error()}.");
        }

        Console.WriteLine($"[Redirect] {count} imports redirected. Each redirected path is logged below as it's used " +
                          $"(the first {MaxLoggedRedirects}).");
    }

    /// <summary>Lists copies of the VR runtime that earlier versions put in the game folder: they'd hide whether the redirect works.</summary>
    private static void ReportGameFolderCopies()
    {
        var found = new List<string>();
        foreach (var dir in new[] { Path.Combine(_dataPath, "UnitySubsystems"), Path.Combine(Path.Combine(_dataPath, "StreamingAssets"), "SteamVR") })
            if (Directory.Exists(dir)) found.Add(dir);
        var plugins = Path.Combine(_dataPath, "Plugins");
        if (Directory.Exists(plugins))
            foreach (var file in Directory.GetFiles(plugins, "*.dll", SearchOption.AllDirectories))
                if (PluginNames.Contains(Path.GetFileName(file))) found.Add(file);

        if (found.Count == 0)
            Console.WriteLine("[Redirect] The game folder has no copies of the VR runtime: everything VR loads comes from the mod's folder.");
        else
            Console.WriteLine("[Redirect] WARNING: the game folder still has VR runtime files copied by an earlier version. " +
                              "They aren't used while the redirect works, but delete them to be sure the test shows " +
                              "the redirect alone:\n  " + string.Join("\n  ", found.ToArray()));
    }

    /// <summary>The file in the mod's folder that stands in for a path in the game's data folder, or null.</summary>
    internal static string? Redirect(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        var prefix = "";
        var p = path.Replace('/', '\\');
        if (p.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            prefix = @"\\?\";
            p = p.Substring(4);
        }
        if (p.Length < _dataPath.Length || !p.StartsWith(_dataPath, StringComparison.OrdinalIgnoreCase))
            return SteamVrFolderElsewhere(p, prefix);

        var rest = p.Substring(_dataPath.Length); // "" or "\..."
        if (rest.Length > 0 && rest[0] != '\\') return SteamVrFolderElsewhere(p, prefix);

        string? target = null;
        if (Under(rest, @"\UnitySubsystems") || Under(rest, @"\StreamingAssets\SteamVR"))
        {
            target = _sourceData + rest;
        }
        else if (Under(rest, @"\Plugins"))
        {
            var name = rest.Substring(rest.LastIndexOf('\\') + 1);
            if (!name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) name += ".dll";
            if (PluginNames.Contains(name)) target = _sourcePlugins + @"\" + name;
        }
        return target == null ? null : prefix + target;
    }

    /// <summary>
    /// The OpenVR plug-in may find StreamingAssets\SteamVR relative to the working folder or to its own file, which
    /// is now in the mod's folder: any StreamingAssets\SteamVR outside the mod's Data folder is that one.
    /// </summary>
    private static string? SteamVrFolderElsewhere(string p, string prefix)
    {
        const string folder = @"StreamingAssets\SteamVR";
        var at = p.IndexOf(folder, StringComparison.OrdinalIgnoreCase);
        if (at < 0 || (at > 0 && p[at - 1] != '\\')) return null;
        var end = at + folder.Length;
        if (end < p.Length && p[end] != '\\') return null;
        if (p.StartsWith(_sourceData + @"\", StringComparison.OrdinalIgnoreCase)) return null; // already the mod's
        return prefix + _sourceData + @"\" + p.Substring(at);
    }

    private static bool Under(string rest, string folder) =>
        rest.Equals(folder, StringComparison.OrdinalIgnoreCase) ||
        rest.StartsWith(folder + @"\", StringComparison.OrdinalIgnoreCase);

    // ---------------------------------------------------------------------------------------------------------
    // Hooks. Each reads the path, swaps in the mod's file if it's one of the redirected ones, and calls the real
    // function. Errors never reach the caller: on any problem the real function runs with the original path.

    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    private delegate IntPtr CreateFileWFn(IntPtr name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    private delegate uint GetFileAttributesWFn(IntPtr name);
    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    private delegate int GetFileAttributesExWFn(IntPtr name, int level, IntPtr info);
    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    private delegate IntPtr FindFirstFileWFn(IntPtr name, IntPtr data);
    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    private delegate IntPtr FindFirstFileExWFn(IntPtr name, int level, IntPtr data, int op, IntPtr filter, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    private delegate IntPtr LoadLibraryWFn(IntPtr name);
    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    private delegate IntPtr LoadLibraryExWFn(IntPtr name, IntPtr file, uint flags);

    private static readonly CreateFileWFn RealCreateFileW = Real<CreateFileWFn>("CreateFileW");
    private static readonly GetFileAttributesWFn RealGetFileAttributesW = Real<GetFileAttributesWFn>("GetFileAttributesW");
    private static readonly GetFileAttributesExWFn RealGetFileAttributesExW = Real<GetFileAttributesExWFn>("GetFileAttributesExW");
    private static readonly FindFirstFileWFn RealFindFirstFileW = Real<FindFirstFileWFn>("FindFirstFileW");
    private static readonly FindFirstFileExWFn RealFindFirstFileExW = Real<FindFirstFileExWFn>("FindFirstFileExW");
    private static readonly LoadLibraryExWFn RealLoadLibraryExW = Real<LoadLibraryExWFn>("LoadLibraryExW");

    // Kept in fields so the delegates (and the native thunks to them) are never collected.
    private static readonly CreateFileWFn HookCreateFileW = (n, a, s, sec, d, f, t) =>
        WithPath(n, "CreateFileW", p => RealCreateFileW(p, a, s, sec, d, f, t));
    private static readonly GetFileAttributesWFn HookGetFileAttributesW = n =>
        WithPath(n, "GetFileAttributesW", p => RealGetFileAttributesW(p));
    private static readonly GetFileAttributesExWFn HookGetFileAttributesExW = (n, l, i) =>
        WithPath(n, "GetFileAttributesExW", p => RealGetFileAttributesExW(p, l, i));
    private static readonly FindFirstFileWFn HookFindFirstFileW = (n, d) =>
        WithPath(n, "FindFirstFileW", p => RealFindFirstFileW(p, d));
    private static readonly FindFirstFileExWFn HookFindFirstFileExW = (n, l, d, o, f, fl) =>
        WithPath(n, "FindFirstFileExW", p => RealFindFirstFileExW(p, l, d, o, f, fl));
    private static readonly LoadLibraryWFn HookLoadLibraryW = n => LoadLibrary(n, 0, "LoadLibraryW");
    private static readonly LoadLibraryExWFn HookLoadLibraryExW = (n, file, flags) => LoadLibrary(n, flags, "LoadLibraryExW");

    private static readonly Dictionary<string, Delegate> AllHooks = new()
    {
        ["CreateFileW"] = HookCreateFileW,
        ["GetFileAttributesW"] = HookGetFileAttributesW,
        ["GetFileAttributesExW"] = HookGetFileAttributesExW,
        ["FindFirstFileW"] = HookFindFirstFileW,
        ["FindFirstFileExW"] = HookFindFirstFileExW,
        ["LoadLibraryW"] = HookLoadLibraryW,
        ["LoadLibraryExW"] = HookLoadLibraryExW,
    };

    private static readonly Dictionary<string, Delegate> LibraryHooks = new()
    {
        ["LoadLibraryW"] = HookLoadLibraryW,
        ["LoadLibraryExW"] = HookLoadLibraryExW,
    };

    private static T WithPath<T>(IntPtr name, string function, Func<IntPtr, T> call)
    {
        IntPtr redirected = IntPtr.Zero;
        try
        {
            var target = name == IntPtr.Zero ? null : Redirect(Marshal.PtrToStringUni(name));
            if (target != null)
            {
                redirected = Marshal.StringToHGlobalUni(target);
                LogRedirect(function, Marshal.PtrToStringUni(name), target);
            }
        }
        catch
        {
            // Logging failed after the path was made: the redirect still applies.
        }

        try
        {
            var result = call(redirected != IntPtr.Zero ? redirected : name);
            SetLastError(Marshal.GetLastWin32Error());
            return result;
        }
        finally
        {
            if (redirected != IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                Marshal.FreeHGlobal(redirected);
                SetLastError(error);
            }
        }
    }

    /// <summary>
    /// Loads a library, from the mod's folder if it's one of the VR runtime's. Those load with their own folder
    /// searched first (LOAD_WITH_ALTERED_SEARCH_PATH), so the OpenVR plug-in finds openvr_api.dll next to it,
    /// and their own file lookups are redirected too.
    /// </summary>
    private static IntPtr LoadLibrary(IntPtr name, uint flags, string function)
    {
        string? target = null;
        try
        {
            if (name != IntPtr.Zero) target = Redirect(Marshal.PtrToStringUni(name));
        }
        catch
        {
            target = null;
        }

        if (target == null)
        {
            var plain = RealLoadLibraryExW(name, IntPtr.Zero, flags);
            SetLastError(Marshal.GetLastWin32Error());
            return plain;
        }

        LogRedirect(function, Marshal.PtrToStringUni(name), target);
        const uint searchFlags = 0x1F00; // LOAD_LIBRARY_SEARCH_*: can't be combined with an altered search path
        if ((flags & searchFlags) == 0) flags |= 0x8; // LOAD_WITH_ALTERED_SEARCH_PATH
        var path = Marshal.StringToHGlobalUni(target);
        IntPtr module;
        int error;
        try
        {
            module = RealLoadLibraryExW(path, IntPtr.Zero, flags);
            error = Marshal.GetLastWin32Error();
        }
        finally
        {
            Marshal.FreeHGlobal(path);
        }

        if (module != IntPtr.Zero && PluginsWithOwnLookups.Contains(Path.GetFileName(target)))
        {
            try
            {
                PatchImports(module, Path.GetFileName(target), AllHooks);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[Redirect] Couldn't redirect {Path.GetFileName(target)}'s own file lookups: {exception.Message}");
            }
        }
        if (module == IntPtr.Zero)
            Console.WriteLine($"[Redirect] [FAILED] Loading {target} failed with Windows error {error}.");
        SetLastError(error);
        return module;
    }

    private static void LogRedirect(string function, string from, string to)
    {
        lock (Lock)
        {
            if (Logged.Count >= MaxLoggedRedirects || !Logged.Add(function + "|" + from)) return;
            Console.WriteLine($"[Redirect] {function}: {from} -> {to}");
        }
    }

    // ---------------------------------------------------------------------------------------------------------
    // Import tables.

    /// <summary>Points a module's KERNEL32 imports of the hooked functions at the hooks. Returns how many it changed.</summary>
    private static int PatchImports(IntPtr module, string moduleName, Dictionary<string, Delegate> hooks)
    {
        lock (Lock)
        {
            if (!PatchedModules.Add(module)) return 0;
        }

        var ntHeaders = Add(module, Marshal.ReadInt32(module, 0x3C));
        if (Marshal.ReadInt32(ntHeaders) != 0x4550 || Marshal.ReadInt16(ntHeaders, 24) != 0x20B)
            throw new BadImageFormatException($"{moduleName} isn't a 64-bit Windows module");
        var importRva = Marshal.ReadInt32(ntHeaders, 24 + 112 + 8); // optional header, data directory 1
        if (importRva == 0) return 0;

        var changed = new List<string>();
        for (var descriptor = Add(module, importRva); ; descriptor = Add(descriptor, 20))
        {
            var lookupRva = Marshal.ReadInt32(descriptor, 0);
            var nameRva = Marshal.ReadInt32(descriptor, 12);
            var thunkRva = Marshal.ReadInt32(descriptor, 16);
            if (nameRva == 0 && thunkRva == 0) break;
            var dll = Marshal.PtrToStringAnsi(Add(module, nameRva));
            if (!string.Equals(dll, "KERNEL32.dll", StringComparison.OrdinalIgnoreCase)) continue;

            var names = Add(module, lookupRva != 0 ? lookupRva : thunkRva);
            for (var i = 0; ; i++)
            {
                var entry = Marshal.ReadInt64(names, i * 8);
                if (entry == 0) break;
                if (entry < 0) continue; // imported by ordinal
                var function = Marshal.PtrToStringAnsi(Add(module, (int)(entry & 0x7FFFFFFF) + 2));
                if (function == null || !hooks.TryGetValue(function, out var hook)) continue;

                var slot = Add(module, thunkRva + i * 8);
                if (!VirtualProtect(slot, (UIntPtr)8, 0x04, out var old)) continue; // PAGE_READWRITE
                Marshal.WriteIntPtr(slot, Marshal.GetFunctionPointerForDelegate(hook));
                VirtualProtect(slot, (UIntPtr)8, old, out _);
                changed.Add(function);
            }
        }

        Console.WriteLine($"[Redirect] {moduleName}: {(changed.Count == 0 ? "no file imports to redirect" : string.Join(", ", changed.ToArray()))}");
        return changed.Count;
    }

    private static IntPtr FindMono()
    {
        foreach (var name in new[] { "mono-2.0-bdwgc.dll", "mono-2.0-sgen.dll", "mono.dll" })
        {
            var module = GetModuleHandleW(name);
            if (module != IntPtr.Zero) return module;
        }
        Console.WriteLine("[Redirect] Mono's module wasn't found; [DllImport] loads aren't redirected.");
        return IntPtr.Zero;
    }

    private static T Real<T>(string function) where T : Delegate =>
        (T)Marshal.GetDelegateForFunctionPointer(GetProcAddress(GetModuleHandleW("kernel32.dll"), function), typeof(T));

    private static IntPtr Add(IntPtr pointer, int offset) => new IntPtr(pointer.ToInt64() + offset);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string name);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, BestFitMapping = false)]
    private static extern IntPtr GetProcAddress(IntPtr module, string name);

    [DllImport("kernel32.dll")]
    private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint protect, out uint oldProtect);

    [DllImport("kernel32.dll")]
    private static extern void SetLastError(int error);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryExW(string path, IntPtr file, uint flags);
}
