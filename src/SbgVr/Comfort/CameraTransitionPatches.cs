// SBG VR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using HarmonyLib;
using UnityEngine;

namespace SbgVr.Comfort
{
    /// <summary>
    /// Shortens or removes the camera's sweeps between views in VR (VR Comfort.CameraTransitions). A long
    /// sweep that you don't control is a common cause of VR discomfort.
    /// </summary>
    [HarmonyPatch]
    internal static class CameraTransitionPatches
    {
        private const float FastFactor = 0.35f;

        [HarmonyPatch(typeof(CameraModuleController), "TransitionToInternal")]
        [HarmonyPrefix]
        private static void TransitionToInternal(ref float duration, ref AnimationCurve explicitTransitionCurve)
        {
            if (!VrState.IsRunning) return;

            switch (Settings.CameraTransitions.Value)
            {
                case CameraTransitionMode.Instant:
                    duration = 0f;
                    explicitTransitionCurve = null;
                    break;

                case CameraTransitionMode.Fast:
                    if (explicitTransitionCurve != null && explicitTransitionCurve.length > 0)
                    {
                        // A custom curve sets its own length; use the game's standard ease over a shorter time.
                        duration = explicitTransitionCurve[explicitTransitionCurve.length - 1].time;
                        explicitTransitionCurve = null;
                    }

                    duration *= FastFactor;
                    break;
            }
        }
    }
}
