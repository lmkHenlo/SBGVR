// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using HarmonyLib;
using UnityEngine;

namespace SBGVR.Hud
{
    /// <summary>
    /// The rest of the game's HUD that it places over something in the world (with
    /// CameraModuleController.WorldToScreenPoint or Camera.WorldToScreenPoint): drawn in the world at that
    /// thing by <see cref="WorldMarker"/>, like the icons and name tags in <see cref="MarkerPatches"/>, so it
    /// isn't doubled on the floating screen. Each is placed after the game has laid it out for the frame.
    /// </summary>
    [HarmonyPatch]
    internal static class MoreMarkerPatches
    {
        // The orange lock-on reticle on a target: homing shots (a full-power swing) and the rocket launcher.
        [HarmonyPatch(typeof(LockOnTargetUi), "OnLateUpdate")]
        [HarmonyPostfix]
        private static void LockOnTargetLateUpdate(Transform ___container, Vector3 ___worldPosition, bool ___isVisible, LockOnTarget ___target)
        {
            var inWorld = WorldMarker.Enabled && ___isVisible;
            WorldMarker.Place(___container, ___worldPosition, inWorld, inWorld ? PlayerOf(___target) : null,
                alwaysOnTop: true);
        }

        // The airhorn's indicator over each player it will hit.
        [HarmonyPatch(typeof(AirhornTargetIndicator), "OnBUpdate")]
        [HarmonyPostfix]
        private static void AirhornTarget(RectTransform ___rectTransform, Transform ___target, Vector3 ___targetWorldOffset)
        {
            var inWorld = WorldMarker.Enabled && ___target != null;
            var position = ___target != null ? ___target.position + ___targetWorldOffset : Vector3.zero;
            WorldMarker.Place(___rectTransform, position, inWorld, inWorld ? PlayerOf(___target) : null);
        }

        // The timer over a player in Jumbo Burger giant form.
        [HarmonyPatch(typeof(JumboBurgerGiantFormTimerUi), "LateUpdate")]
        [HarmonyPostfix]
        private static void GiantFormTimer(
            RectTransform ___rectTransform, Vector3 ___worldPosition, Transform ___parent, PlayerInfo ___parentPlayer)
        {
            WorldMarker.Place(___rectTransform, ___worldPosition, WorldMarker.Enabled && ___parent != null, ___parentPlayer);
        }

        // Text that pops up over something (points, hits and the like).
        [HarmonyPatch(typeof(TextPopupUi), "LateUpdate")]
        [HarmonyPostfix]
        private static void TextPopup(RectTransform ___rectTransform, Vector3 ___worldPosition, Transform ___parent)
        {
            var inWorld = WorldMarker.Enabled && ___parent != null;
            WorldMarker.Place(___rectTransform, ___worldPosition, inWorld, inWorld ? PlayerOf(___parent) : null);
        }

        /// <summary>The player a target belongs to, whose own character shouldn't cover its marker.</summary>
        private static PlayerInfo PlayerOf(Component target)
        {
            return target != null ? target.GetComponentInParent<PlayerInfo>() : null;
        }
    }
}
