// SBG VR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace SbgVr
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(UuvrGuid, BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.sbgvr";
        public const string Name = "SBG VR";
        public const string Version = "0.1.0";

        /// <summary>UUVR's BepInEx plugin ID (the Unity 2020+ Mono build).</summary>
        internal const string UuvrGuid = "raicuparta.uuvr-modern";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> DiagnosticsEnabled;
        internal static ConfigEntry<float> PerformanceLogInterval;

        private void Awake()
        {
            Log = Logger;
            BindConfig();

            if (DiagnosticsEnabled.Value)
            {
                var host = new GameObject("SBG VR");
                host.hideFlags = HideFlags.HideAndDontSave;
                DontDestroyOnLoad(host);
                host.AddComponent<Diagnostics.VrDiagnostics>();
                Log.LogInfo("[ok] Diagnostics");
            }

            Log.LogInfo(DiagnosticsEnabled.Value
                ? $"{Name} {Version} loaded. Diagnostics are on: VR, camera and renderer details go to this log."
                : $"{Name} {Version} loaded.");
        }

        private void BindConfig()
        {
            DiagnosticsEnabled = Config.Bind("Diagnostics", "Enabled", true,
                "Write VR, camera and renderer details to the BepInEx log (LogOutput.log), so problems in the " +
                "headset can be tracked down from the log. Only changes are logged, not every frame.");
            PerformanceLogInterval = Config.Bind("Diagnostics", "PerformanceLogInterval", 30f,
                new ConfigDescription(
                    "Seconds between frame rate lines in the log while VR is running. 0 = never.",
                    new AcceptableValueRange<float>(0f, 600f)));
        }
    }
}
