// SBG VR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SbgVr
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(UuvrGuid, BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.sbgvr";
        public const string Name = "SBG VR";
        public const string Version = "0.2.0";

        /// <summary>UUVR's BepInEx plugin ID (the Unity 2020+ Mono build).</summary>
        internal const string UuvrGuid = "raicuparta.uuvr-modern";

        internal static ManualLogSource Log;
        internal static Harmony Harmony;

        private void Awake()
        {
            Log = Logger;
            Settings.Bind(Config);
            Harmony = new Harmony(Guid);

            // One persistent object for SBG VR's own components.
            var host = new GameObject("SBG VR");
            host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(host);
            host.AddComponent<VrStateTracker>();

            Patch("Screen shake", typeof(Comfort.ScreenShakePatches));
            Patch("Impact and flash frames, CRT effect, menu blur", typeof(Comfort.ScreenEffectPatches));
            Patch("Camera transitions", typeof(Comfort.CameraTransitionPatches));
            if (Patch("Full-view fades", typeof(Fades.FullViewFadePatches)))
                host.AddComponent<Fades.FullViewFade>();
            if (Patch("World-space markers", typeof(Hud.MarkerPatches)))
                Hud.WorldMarker.Initialize();
            Add(host, "Main menu background", typeof(Comfort.MenuBackground));
            Add(host, "Recenter key", typeof(Comfort.Recenter));
            if (Settings.DiagnosticsEnabled.Value)
                Add(host, "Diagnostics", typeof(Diagnostics.VrDiagnostics));

            Log.LogInfo(Settings.DiagnosticsEnabled.Value
                ? $"{Name} {Version} loaded. Diagnostics are on: VR, camera and renderer details go to this log."
                : $"{Name} {Version} loaded.");
        }

        /// <summary>
        /// Applies one feature's Harmony patches. A feature whose patch fails is removed again and logged, and
        /// the other features keep working.
        /// </summary>
        private static bool Patch(string feature, Type patchClass)
        {
            try
            {
                Harmony.CreateClassProcessor(patchClass).Patch();
                Log.LogInfo($"[ok] {feature}");
                return true;
            }
            catch (Exception exception)
            {
                Log.LogError($"[FAILED] {feature}: {exception.GetBaseException().Message}. This feature is off; the rest of SBG VR works as before.");
                try
                {
                    UnpatchClass(patchClass);
                }
                catch (Exception unpatchException)
                {
                    Log.LogWarning($"Couldn't fully remove the patches for {feature}: {unpatchException.Message}");
                }

                return false;
            }
        }

        private static void UnpatchClass(Type patchClass)
        {
            foreach (var original in Harmony.GetPatchedMethods())
            {
                var info = Harmony.GetPatchInfo(original);
                foreach (var patch in info.Prefixes) Remove(original, patch);
                foreach (var patch in info.Postfixes) Remove(original, patch);
                foreach (var patch in info.Transpilers) Remove(original, patch);
            }

            void Remove(System.Reflection.MethodBase original, HarmonyLib.Patch patch)
            {
                if (patch.owner == Guid && patch.PatchMethod.DeclaringType == patchClass)
                    Harmony.Unpatch(original, patch.PatchMethod);
            }
        }

        private static void Add(GameObject host, string feature, Type component)
        {
            try
            {
                host.AddComponent(component);
                Log.LogInfo($"[ok] {feature}");
            }
            catch (Exception exception)
            {
                Log.LogError($"[FAILED] {feature}: {exception.GetBaseException().Message}");
            }
        }
    }
}
