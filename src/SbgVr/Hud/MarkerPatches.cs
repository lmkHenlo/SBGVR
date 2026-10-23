// SBG VR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using HarmonyLib;
using UnityEngine;

namespace SbgVr.Hud
{
    /// <summary>
    /// VR HUD.WorldSpaceMarkers: after the game lays out each marker for the frame, hands it to
    /// <see cref="WorldMarker"/> with the world position it points at.
    /// </summary>
    [HarmonyPatch]
    internal static class MarkerPatches
    {
        // Ball, hole, objective, teammate and warning icons.
        [HarmonyPatch(typeof(WorldspaceIconUi), "LateUpdate")]
        [HarmonyPostfix]
        private static void IconLateUpdate(WorldspaceIconUi __instance, Vector3 ___worldPosition, bool ___isOffscreen)
        {
            WorldMarker.Place(__instance, ___worldPosition, WorldMarker.Enabled && !___isOffscreen);
        }

        /// <summary>
        /// The game shows an icon as an arrow at the screen edge when its target is outside the flat view. In VR
        /// the headset shows more than that, so icons stay in the world up to VR HUD.MarkerViewAngle from where
        /// the camera faces; only beyond that do they become edge arrows on the floating screen.
        /// </summary>
        [HarmonyPatch(typeof(WorldspaceIconUi), "<LateUpdate>g__IsOffscreen|18_5")]
        [HarmonyPrefix]
        private static bool IconIsOffscreen(Vector3 ___worldPosition, ref bool __result)
        {
            if (!WorldMarker.Enabled) return true;

            var camera = GameManager.Camera;
            if (camera == null) return true;

            var toTarget = ___worldPosition - camera.transform.position;
            __result = Vector3.Angle(camera.transform.forward, toTarget) > Settings.MarkerViewAngle.Value;
            return false;
        }

        // Player name tags.
        [HarmonyPatch(typeof(NameTagUi), "LateUpdate")]
        [HarmonyPostfix]
        private static void NameTagLateUpdate(NameTagUi __instance, Vector3 ___worldPosition)
        {
            WorldMarker.Place(__instance, ___worldPosition, WorldMarker.Enabled);
        }

        // The reticle on whatever you can interact with, and its button prompt.
        [HarmonyPatch(typeof(TargetReticleUi), "LateUpdate")]
        [HarmonyPostfix]
        private static void ReticleLateUpdate(
            UiVisibilityController ___reticleVisibilityController,
            ButtonPrompt ___interactButtonPrompt,
            Vector3 ___lastInteractableReticleWorldPosition)
        {
            WorldMarker.Place(___reticleVisibilityController, ___lastInteractableReticleWorldPosition, WorldMarker.Enabled);
            WorldMarker.Place(___interactButtonPrompt, ___lastInteractableReticleWorldPosition, WorldMarker.Enabled);
        }
    }
}
