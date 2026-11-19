// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SBGVR.Hud
{
    /// <summary>
    /// VR HUD.WorldSpaceMarkers: after the game lays out each marker for the frame, hands it to
    /// <see cref="WorldMarker"/> with the world position it points at.
    /// </summary>
    /// <remarks>
    /// The game lays these out in its BUpdate OnPostPostLateBUpdate stage (in LateUpdate before its 1.4.0
    /// update), after its camera has moved for the frame.
    /// </remarks>
    [HarmonyPatch]
    internal static class MarkerPatches
    {
        // Ball, hole, objective, teammate and warning icons.
        [HarmonyPatch(typeof(WorldspaceIconUi), nameof(WorldspaceIconUi.OnPostPostLateBUpdate))]
        [HarmonyPostfix]
        private static void IconLayout(
            WorldspaceIconUi __instance, Vector3 ___worldPosition, bool ___isOffscreen, Transform ___parent,
            WorldspaceIconUiSettings ___settings)
        {
            var inWorld = WorldMarker.Enabled && !___isOffscreen;
            // A teammate's icon sits on that player, whose own character shouldn't cover it. Only teammate
            // icons: a ball (or a tee) can be attached to its player, and the player should still cover it.
            var owner = inWorld && ___parent != null && ___settings == WorldspaceIconManager.TeammateIconSettings
                ? ___parent.GetComponentInParent<PlayerInfo>()
                : null;
            WorldMarker.Place(__instance, ___worldPosition, inWorld, owner);
        }

        // Player name tags.
        [HarmonyPatch(typeof(NameTagUi), nameof(NameTagUi.OnPostPostLateBUpdate))]
        [HarmonyPostfix]
        private static void NameTagLayout(
            NameTagUi __instance, Vector3 ___worldPosition, PlayerInfo ___playerInfo, bool ___nameTagIsPlayer)
        {
            // A player's name tag isn't covered by that player; a ball's name tag is covered by its owner.
            WorldMarker.Place(__instance, ___worldPosition, WorldMarker.Enabled, ___nameTagIsPlayer ? ___playerInfo : null);
        }

        // The reticle on whatever you can interact with, and its button prompt.
        [HarmonyPatch(typeof(TargetReticleUi), nameof(TargetReticleUi.OnPostPostLateBUpdate))]
        [HarmonyPostfix]
        private static void ReticleLayout(
            UiVisibilityController ___reticleVisibilityController,
            ButtonPrompt ___interactButtonPrompt,
            Vector3 ___lastInteractableReticleWorldPosition)
        {
            WorldMarker.Place(___reticleVisibilityController, ___lastInteractableReticleWorldPosition, WorldMarker.Enabled);
            WorldMarker.Place(___interactButtonPrompt, ___lastInteractableReticleWorldPosition, WorldMarker.Enabled);
        }
    }

    /// <summary>
    /// The game shows an icon as an arrow at the screen edge when its target is outside the flat view. In VR
    /// the headset shows more than that, so icons stay in the world up to VR HUD.MarkerViewAngle from where
    /// the camera faces; only beyond that do they become edge arrows on the floating screen.
    /// </summary>
    /// <remarks>
    /// The check is a local function in WorldspaceIconUi's layout method, whose compiler-made name holds that
    /// method's name and ends in numbers that change when the game's code does
    /// ("&lt;OnPostPostLateBUpdate&gt;g__IsOffscreen|18_5"), so it's found by the part in between, whichever
    /// method holds it. It's patched on its own: if a game update moves it, only this wider view angle is
    /// lost, not the world-space markers.
    /// </remarks>
    [HarmonyPatch]
    internal static class MarkerViewAnglePatches
    {
        private const string LocalFunctionName = ">g__IsOffscreen|";

        private static MethodBase TargetMethod() =>
            AccessTools.GetDeclaredMethods(typeof(WorldspaceIconUi))
                .FirstOrDefault(m => m.Name.Contains(LocalFunctionName) && m.ReturnType == typeof(bool))
            ?? throw new MissingMethodException(nameof(WorldspaceIconUi), "<...>g__IsOffscreen|...");

        private static bool Prefix(Vector3 ___worldPosition, ref bool __result)
        {
            if (!WorldMarker.Enabled) return true;

            var camera = GameManager.Camera;
            if (camera == null) return true;

            var toTarget = ___worldPosition - camera.transform.position;
            __result = Vector3.Angle(camera.transform.forward, toTarget) > Settings.MarkerViewAngle.Value;
            return false;
        }
    }
}
