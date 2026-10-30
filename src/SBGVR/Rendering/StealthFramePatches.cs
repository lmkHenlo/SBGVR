// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using Game.Rendering.StealthFrame;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Uuvr.VrUi.PatchModes;

namespace SBGVR.Rendering
{
    /// <summary>
    /// Keeps the smoke bomb's purple frame off UUVR's floating screen.
    /// </summary>
    /// <remarks>
    /// While your smoke bomb hides you, <c>StealthOverlayController</c> turns on the game's
    /// <see cref="StealthFrameFeature"/>, a URP pass that recolours the picture purple and adds a noisy purple
    /// edge. It runs on every camera using the game's default renderer: each eye, which gives the purple view,
    /// and also the UUVR camera that draws the floating screen's picture. On a flat screen that camera doesn't
    /// exist; in VR the pass painted its edge over the floating screen's transparent picture, so a purple frame
    /// showed around the floating screen. This skips the pass only for the cameras that draw the floating
    /// screen's picture (the screen-clearing camera in UUVR's Mirror mode and the UI capture camera in its
    /// Canvas Redirect mode). The eyes, and so the purple view, are unchanged.
    /// </remarks>
    [HarmonyPatch]
    internal static class StealthFramePatches
    {
        [HarmonyPatch(typeof(StealthFrameFeature), nameof(StealthFrameFeature.AddRenderPasses))]
        [HarmonyPrefix]
        private static bool AddRenderPasses(ref RenderingData renderingData)
        {
            if (!VrState.IsRunning || !StealthFrameFeature.Enabled) return true;
            return !DrawsFloatingScreen(renderingData.cameraData.camera);
        }

        private static bool DrawsFloatingScreen(Camera camera)
        {
            if (camera == null) return false;
            if (camera.GetComponent<ScreenMirrorPatchMode>() != null) return true;

            var parent = camera.transform.parent;
            return parent != null && parent.GetComponent<CanvasRedirectPatchMode>() != null;
        }
    }
}
