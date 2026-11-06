using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
#if LEGACY
using AssetsTools.NET;
using AssetsTools.NET.Extra;
#endif
#if CPP
using BepInEx.Preloader.Core.Patching;
#elif MONO
using Mono.Cecil;
#endif

#if CPP
[PatcherPluginInfo("com.raicuparta.uuvr", "UUVR", "0.1.0")]
#endif
public class Patcher
#if CPP
    : BasePatcher
#endif
{
#if LEGACY
    private static readonly List<string> GlobalSettingsFileNames =
        new()
        {
            "globalgamemanagers", "mainData", "data.unity3d"
        };
#endif

    public static IEnumerable<string> TargetDLLs { get; } = new[] { "Assembly-CSharp.dll" };

#if MONO
    public static void Patch(AssemblyDefinition assembly)
    {
    }
#endif

#if CPP
    public override void Initialize()
#elif MONO
    public static void Initialize()
#endif
    {
        var installerPath = Assembly.GetExecutingAssembly().Location;

        var gameExePath = Process.GetCurrentProcess().MainModule.FileName;

        var gamePath = Path.GetDirectoryName(gameExePath);
        var gameName = Path.GetFileNameWithoutExtension(gameExePath);
        var dataPath = Path.Combine(gamePath, $"{gameName}_Data/");
        var patcherPath = Path.GetDirectoryName(installerPath);
        
        // SBGVR: nothing is copied into the game folder; Unity's lookups of the VR runtime files are pointed at
        // VrRuntime, next to this patcher, instead (GameFolderRedirect).
        GameFolderRedirect.Install(Path.Combine(patcherPath, "VrRuntime"), dataPath);

#if LEGACY
        string globalSettingsFilePath = GetGlobalSettingsFilePath(dataPath);
        string globalSettingsBackupPath = CreateGlobalSettingsBackup(globalSettingsFilePath);
        string classDataPath = Path.Combine(patcherPath, "classdata.tpk");
        PatchVR(globalSettingsBackupPath, globalSettingsFilePath, classDataPath);
#endif
    }

#if LEGACY
    private static string GetGlobalSettingsFilePath(string dataPath)
    {
        foreach (var globalSettingsFielName in GlobalSettingsFileNames)
        {
            var path = Path.Combine(dataPath, globalSettingsFielName);
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new Exception("Failed to find global settings file path");
    }

    private static string CreateGlobalSettingsBackup(string globalSettingsFilePath)
    {
        Console.WriteLine($"Backing up '{globalSettingsFilePath}'...");
        var backupPath = globalSettingsFilePath + ".bak";
        if (File.Exists(backupPath))
        {
            Console.WriteLine($"Backup already exists.");
            return backupPath;
        }

        File.Copy(globalSettingsFilePath, backupPath);
        Console.WriteLine($"Created backup in '{backupPath}'");
        return backupPath;
    }

    private static void PatchVR(string globalSettingsBackupPath, string globalSettingsFilePath, string classDataPath)
    {
        Console.WriteLine($"Using classData file from path '{classDataPath}'");

        AssetsManager am = new();
        am.LoadClassPackage(classDataPath);
        var ggm = am.LoadAssetsFile(globalSettingsBackupPath, false);
        var ggmFile = ggm.file;
        var ggmTable = ggm.table;
        am.LoadClassDatabaseFromPackage(ggmFile.typeTree.unityVersion);

        List<AssetsReplacer> replacers = new();
        
        // TODO: Read inputs from globalgamemanagers, store map somewhere, patch in-game?
        // AssetFileInfoEx inputManager = ggmTable.GetAssetInfo(2);
        // AssetTypeValueField inputManagerBase = am.GetATI(ggmFile, inputManager).GetBaseField();
        // AssetTypeValueField axes = inputManagerBase.Get("m_Axes").Get("Array");
        // Console.WriteLine($"#### Found axes: {axes.children.Length}, looping...");
        //
        // foreach (AssetTypeValueField? child in axes.children)
        // {
        //     int axis = child.Get("axis").value.AsInt();
        //     int type = child.Get("type").value.AsInt();
        //     int joyNum = child.Get("joyNum").value.AsInt();
        //     string? name = child.Get("m_Name").value.AsString();
        //     string? positiveButton = child.Get("positiveButton").value.AsString();
        //     string? negativeButton = child.Get("negativeButton").value.AsString();
        //     string? altNegativeButton = child.Get("altNegativeButton").value.AsString();
        //     string? altPositiveButton = child.Get("altPositiveButton").value.AsString();
        //     float gravity = child.Get("gravity").value.AsFloat();
        //     float dead = child.Get("dead").value.AsFloat();
        //     float sensitivity = child.Get("sensitivity").value.AsFloat();
        //     bool snap = child.Get("snap").value.AsBool();
        //     bool invert = child.Get("invert").value.AsBool();
        //
        //     if (string.IsNullOrEmpty(positiveButton)) continue;
        //
        //     if (!positiveButton.StartsWith("joystick")) continue;
        //
        //     Console.WriteLine($"name:{name} | positiveButton:{positiveButton} ");
        // }
        
        var buildSettings = ggmTable.GetAssetInfo(11);
        #pragma warning disable CS0618 // Type or member is obsolete
        var buildSettingsBase = am.GetATI(ggmFile, buildSettings).GetBaseField();
        #pragma warning restore CS0618 // Type or member is obsolete
        var enabledVRDevices = buildSettingsBase.Get("enabledVRDevices").Get("Array");
        var stringTemplate = enabledVRDevices.templateField.children[1];
        
        // AssetTypeValueField[] vrDevicesList = { StringField("None", stringTemplate), StringField("OpenVR", stringTemplate), StringField("Oculus", stringTemplate) };
        AssetTypeValueField[] vrDevicesList = { StringField("OpenVR", stringTemplate), StringField("Oculus", stringTemplate) };
        enabledVRDevices.SetChildrenList(vrDevicesList);

        replacers.Add(new AssetsReplacerFromMemory(0, buildSettings.index, (int)buildSettings.curFileType, 0xffff,
            buildSettingsBase.WriteToByteArray()));

        using AssetsFileWriter writer = new(File.OpenWrite(globalSettingsFilePath));
        ggmFile.Write(writer, 0, replacers, 0);
    }

    private static AssetTypeValueField StringField(string str, AssetTypeTemplateField template)
    {
        return new AssetTypeValueField()
        {
            children = null,
            childrenCount = 0,
            templateField = template,
            value = new AssetTypeValue(EnumValueTypes.ValueType_String, str)
        };
    }
#endif

#if CPP
    public override void Finalizer() { }
#endif
}
