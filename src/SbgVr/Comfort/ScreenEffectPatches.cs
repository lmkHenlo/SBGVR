// SBG VR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using Game.Rendering.CRT;
using Game.Rendering.FlashFrame;
using HarmonyLib;

namespace SbgVr.Comfort
{
    /// <summary>
    /// Full-screen effects that don't suit a headset. Each is skipped while VR runs rather than switched off,
    /// so nothing has to be restored when VR stops:
    /// <list type="bullet">
    /// <item>Impact frames (VR Comfort.ImpactFrames): a distorted, recoloured frame centred on the flat
    /// camera's view of the explosion.</item>
    /// <item>Flash frames (VR Comfort.FlashFrames): a full-view flash with a doubled picture.</item>
    /// <item>The CRT effect (photo camera and the CRT graphics option), always: its bulge warps each eye
    /// separately.</item>
    /// </list>
    /// </summary>
    [HarmonyPatch]
    internal static class ScreenEffectPatches
    {
        [HarmonyPatch(typeof(ImpactFrameController), nameof(ImpactFrameController.PlayImpactFrame))]
        [HarmonyPrefix]
        private static bool PlayImpactFrame()
        {
            return !VrState.IsRunning || Settings.ImpactFrames.Value;
        }

        [HarmonyPatch(typeof(FlashFrameFeature), nameof(FlashFrameFeature.AddRenderPasses))]
        [HarmonyPrefix]
        private static bool FlashFrameAddRenderPasses()
        {
            return !VrState.IsRunning || Settings.FlashFrames.Value;
        }

        [HarmonyPatch(typeof(CRTFeature), nameof(CRTFeature.AddRenderPasses))]
        [HarmonyPrefix]
        private static bool CrtAddRenderPasses()
        {
            return !VrState.IsRunning;
        }
    }
}
