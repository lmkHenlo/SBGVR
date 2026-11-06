// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace SBGVR
{
    /// <summary>
    /// Whether VR is running right now: a headset display is up and being rendered to. Follows UUVR's
    /// headset session, so every VR-only change in SBGVR is off until VR is running.
    /// </summary>
    internal static class VrState
    {
        private static readonly List<XRDisplaySubsystem> Displays = new List<XRDisplaySubsystem>();

        internal static bool IsRunning { get; private set; }

        /// <summary>Raised once when VR starts, and once when it stops.</summary>
        internal static event Action<bool> Changed;

        /// <summary>
        /// The camera the headset sees through: the one UUVR's floating screen follows. The fades, markers,
        /// reticles, aim line and menu controls panel are all placed relative to it.
        /// </summary>
        internal static Camera ViewCamera()
        {
            var vrCamera = Uuvr.VrCamera.VrCamera.HighestDepthVrCamera;
            if (vrCamera != null && vrCamera.isActiveAndEnabled && vrCamera.ParentCamera != null)
                return vrCamera.ParentCamera;
            return Camera.main;
        }

        internal static void Refresh()
        {
            SubsystemManager.GetSubsystems(Displays);
            var running = false;
            foreach (var display in Displays)
            {
                if (display.running)
                {
                    running = true;
                    break;
                }
            }

            if (running == IsRunning) return;
            IsRunning = running;
            Plugin.Log.LogInfo(running ? "VR started: VR settings are on." : "VR stopped: the game is back to normal.");

            var handlers = Changed;
            if (handlers == null) return;

            // Each feature's handler runs on its own, so one that fails doesn't stop the rest: VR starts once and
            // stays on, so a skipped handler would stay skipped for the whole session.
            foreach (Action<bool> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(running);
                }
                catch (Exception exception)
                {
                    Plugin.Log.LogError($"VR {(running ? "start" : "stop")}: {handler.Method.DeclaringType?.Name ?? "a feature"} " +
                                        $"failed; the other features carry on. {exception}");
                }
            }
        }
    }

    /// <summary>Updates <see cref="VrState"/> before anything else runs each frame.</summary>
    [DefaultExecutionOrder(-30000)]
    internal sealed class VrStateTracker : MonoBehaviour
    {
        private void Update()
        {
            VrState.Refresh();
        }
    }
}
