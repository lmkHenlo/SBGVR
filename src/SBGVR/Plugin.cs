// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SBGVR
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(UuvrGuid, BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.sbgvr";
        public const string Name = "SBGVR";
        public const string Version = "1.3.2";

        /// <summary>UUVR's BepInEx plugin ID (the Unity 2020+ Mono build).</summary>
        internal const string UuvrGuid = "raicuparta.uuvr-modern";

        internal static ManualLogSource Log;
        internal static Harmony Harmony;

        private void Awake()
        {
            Log = Logger;
            Settings.Bind(Config);
            Harmony = new Harmony(Guid);

            // One persistent object for SBGVR's own components.
            var host = new GameObject("SBGVR");
            host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(host);
            host.AddComponent<VrStateTracker>();

            Patch("Screen shake", typeof(Comfort.ScreenShakePatches));
            Patch("Impact and flash frames, CRT effect, menu blur", typeof(Comfort.ScreenEffectPatches));
            Patch("Camera transitions", typeof(Comfort.CameraTransitionPatches));
            Patch("Smoke bomb frame kept off the floating screen", typeof(Rendering.StealthFramePatches));
            if (Patch("Railgun scope zoom", typeof(View.RailgunScopeZoomPatches)))
                Add(host, "Railgun scope zoom: HUD and floating screen", typeof(View.RailgunScopeZoom));
            if (Patch("Ambient occlusion for each eye", typeof(Rendering.AmbientOcclusionEyePatches)))
                Rendering.AmbientOcclusionEyePatches.Verify();
            Patch("Ambient occlusion", typeof(Rendering.AmbientOcclusionPatches));
            if (Patch("Full-view fades", typeof(Fades.FullViewFadePatches)))
                host.AddComponent<Fades.FullViewFade>();
            if (Patch("World-space markers", typeof(Hud.MarkerPatches)))
            {
                Hud.WorldMarker.Initialize();
                Hud.MarkerOcclusion.Initialize();
                Patch("World-space markers: icons up to MarkerViewAngle", typeof(Hud.MarkerViewAnglePatches));
                Add(host, "World-space reticles", typeof(Hud.WorldReticles));
                Add(host, "Aim line", typeof(Hud.AimLine));
                Patch("World-space lock-on reticles, airhorn targets, timers and popups", typeof(Hud.MoreMarkerPatches));
            }
            Add(host, "Main menu background", typeof(Comfort.MenuBackground));
            Add(host, "Controller layout in the main menu", typeof(Hud.MenuControlsPanel));
            Add(host, "Recenter key", typeof(Comfort.Recenter));
            if (Settings.MotionControls.Value)
            {
                EnableOpenXrControllers();
                Add(host, "Motion controls", typeof(Controls.MotionControls));
                if (Settings.MotionCamera.Value)
                {
                    Add(host, "Motion camera", typeof(Controls.MotionCamera));
                    Add(host, "Motion camera for weapons", typeof(Controls.WeaponMotionCamera));
                }
            }

            // Weapons aim along the controller (VR Controls.ControllerAiming) or, in first person, the head. While
            // neither applies, the patched methods read the game camera exactly as before.
            if (Patch("Aiming with the controller or head", typeof(Controls.ControllerAimPatches)) &&
                Patch("Aiming with the controller or head: rocket launcher lock-on", typeof(Controls.ControllerAimLockOnPatches)))
            {
                Patch("Aiming with the controller or head: rocket launcher lock-on (scope)", typeof(Controls.ControllerAimLockOnPatches.RocketLauncherScope));
            }

            if (Patch("First person", typeof(View.FirstPersonPatches)))
            {
                Patch("First person: walking and aiming where you look", typeof(View.FirstPersonMovementPatches));
                Add(host, "First person toggle", typeof(View.FirstPersonView));
            }
            if (Settings.DiagnosticsEnabled.Value)
                Add(host, "Diagnostics", typeof(Diagnostics.VrDiagnostics));

            Log.LogInfo(Settings.DiagnosticsEnabled.Value
                ? $"{Name} {Version} loaded. Diagnostics are on: VR, camera and renderer details go to this log."
                : $"{Name} {Version} loaded.");
        }

        /// <summary>
        /// Gives UUVR's OpenXR loader the controller interaction profiles it starts without, so OpenXR reports
        /// controllers. Runs before UUVR starts VR (UUVR starts it from its first Start, after every plugin's
        /// Awake). Controllers don't work with OpenVR (a known issue).
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void EnableOpenXrControllers()
        {
            try
            {
                var profiles = UnityEngine.XR.OpenXR.Features.Interactions.ControllerProfiles.Enable();
                Log.LogInfo($"[ok] OpenXR controller profiles: {string.Join(", ", profiles)}");
            }
            catch (Exception exception)
            {
                Log.LogError($"[FAILED] OpenXR controller profiles: {exception.GetBaseException().Message}. " +
                             "Controllers won't work with OpenXR; OpenVR and the rest of SBGVR work as before.");
            }
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
                Log.LogError($"[FAILED] {feature}: {exception.GetBaseException().Message}. This feature is off; the rest of SBGVR works as before.");
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
