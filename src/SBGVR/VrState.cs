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
    /// F3 toggle, so every VR-only change in SBGVR switches off with it.
    /// </summary>
    internal static class VrState
    {
        private static readonly List<XRDisplaySubsystem> Displays = new List<XRDisplaySubsystem>();

        internal static bool IsRunning { get; private set; }

        /// <summary>Raised once when VR starts, and once when it stops.</summary>
        internal static event Action<bool> Changed;

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

            try
            {
                Changed?.Invoke(running);
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError($"A VR start/stop handler failed: {exception}");
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
