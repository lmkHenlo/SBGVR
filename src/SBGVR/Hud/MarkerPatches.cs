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
    [HarmonyPatch]
    internal static class MarkerPatches
    {
        // Ball, hole, objective, teammate and warning icons.
        [HarmonyPatch(typeof(WorldspaceIconUi), "LateUpdate")]
        [HarmonyPostfix]
        private static void IconLateUpdate(
            WorldspaceIconUi __instance, Vector3 ___worldPosition, bool ___isOffscreen, Transform ___parent,
            WorldspaceIconUiSettings ___settings)
        {
            var inWorld = WorldMarker.Enabled && !___isOffscreen;
            // A teammate's icon sits on that player, whose own character shouldn't cover it. Only teammate
            // icons: a ball (or a tee) can be attached to its player, and the player should still cover it.
            var owner = inWorld && ___parent != null && ___settings == WorldspaceIconManager.TeammateIconSettings
                ? ___parent.GetComponentInParent<PlayerInfo>()
                : null;
            WorldMarker.Place(__instance, ___worldPosition, inWorld, owner,
                alwaysOnTop: inWorld && IsBallOrHoleIconOnTop(___settings));
        }

        /// <summary>
        /// VR HUD.BallAndHoleIconsOnTop: the golf ball and hole icons are drawn like weapon reticles, over scenery,
        /// characters and effects (see <see cref="WorldMarker.AlwaysOnTop"/>), as the game's flat-screen HUD
        /// draws them. An icon is told which it is by its settings, which stay the hole's while the tutorial
        /// shows its objective sprite on the hole.
        /// </summary>
        private static bool IsBallOrHoleIconOnTop(WorldspaceIconUiSettings settings)
        {
            if (!Settings.BallAndHoleIconsOnTop.Value || settings == null ||
                !SingletonBehaviour<WorldspaceIconManager>.HasInstance) return false;

            return ReferenceEquals(settings, WorldspaceIconManager.HoleIconSettings) ||
                   ReferenceEquals(settings, WorldspaceIconManager.BallIconSettings);
        }

        // Player name tags.
        [HarmonyPatch(typeof(NameTagUi), "LateUpdate")]
        [HarmonyPostfix]
        private static void NameTagLateUpdate(
            NameTagUi __instance, Vector3 ___worldPosition, PlayerInfo ___playerInfo, bool ___nameTagIsPlayer)
        {
            // A player's name tag isn't covered by that player; a ball's name tag is covered by its owner.
            WorldMarker.Place(__instance, ___worldPosition, WorldMarker.Enabled, ___nameTagIsPlayer ? ___playerInfo : null);
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

    /// <summary>
    /// The game shows an icon as an arrow at the screen edge when its target is outside the flat view. In VR
    /// the headset shows more than that, so icons stay in the world up to VR HUD.MarkerViewAngle from where
    /// the camera faces; only beyond that do they become edge arrows on the floating screen.
    /// </summary>
    /// <remarks>
    /// The check is a local function in WorldspaceIconUi.LateUpdate, whose compiler-made name ends in numbers
    /// that change when the game's code does ("&lt;LateUpdate&gt;g__IsOffscreen|18_5"), so it's found by the
    /// rest of its name. It's patched on its own: if a game update moves it, only this wider view angle is
    /// lost, not the world-space markers.
    /// </remarks>
    [HarmonyPatch]
    internal static class MarkerViewAnglePatches
    {
        private const string LocalFunctionPrefix = "<LateUpdate>g__IsOffscreen|";

        private static MethodBase TargetMethod() =>
            AccessTools.GetDeclaredMethods(typeof(WorldspaceIconUi))
                .FirstOrDefault(m => m.Name.StartsWith(LocalFunctionPrefix, StringComparison.Ordinal) && m.ReturnType == typeof(bool))
            ?? throw new MissingMethodException(nameof(WorldspaceIconUi), LocalFunctionPrefix + "...");

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
