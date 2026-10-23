// SBG VR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using HarmonyLib;
using HTraceAO.Rendering.URP.Infrastructure;
using UnityEngine;

namespace SbgVr.Rendering
{
    /// <summary>
    /// Turns off the game's ambient occlusion (HTrace AO) while VR runs.
    ///
    /// HTrace AO sizes its passes from <c>Camera.scaledPixelWidth</c> and <c>scaledPixelHeight</c>, which in VR
    /// are still the game window's size (2560x1440 here), not the eye's (2528x2704). The rest of each eye's
    /// image got no occlusion values, read as fully occluded, and nearby surfaces there drew black: a black
    /// band across the top of the view that followed the player and moved with the head.
    ///
    /// The prefix does what the feature itself does for cameras it skips: it turns the occlusion keyword off,
    /// so lit shaders stop sampling the occlusion texture, and adds none of its passes. Rendering for flat
    /// screens is unchanged, and the feature takes over again as soon as VR stops.
    /// </summary>
    [HarmonyPatch]
    internal static class AmbientOcclusionPatches
    {
        private const string OcclusionKeyword = "_SCREEN_SPACE_OCCLUSION";

        [HarmonyPatch(typeof(HTraceAORendererFeature), nameof(HTraceAORendererFeature.AddRenderPasses))]
        [HarmonyPrefix]
        private static bool AddRenderPasses()
        {
            if (!VrState.IsRunning) return true;

            Shader.DisableKeyword(OcclusionKeyword);
            return false;
        }
    }
}
