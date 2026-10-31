// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SBGVR.Hud
{
    /// <summary>
    /// Where the local player's equipped gun (dueling pistol, elephant gun, rocket launcher or railgun) will
    /// shoot this frame, from the game's own <c>PlayerInventory.GetFirearmAimPoint</c>, so
    /// <see cref="WorldReticles"/> can put the crosshair exactly there.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game casts a ray from <c>GameManager.Camera</c> (the controller or your head, through
    /// <see cref="Controls.ControllerAimPatches"/>) with each gun's own range and layers, keeps the nearest hit
    /// in front of the gun that isn't your own character, and otherwise aims at the gun's range along the ray.
    /// If the point is more than 45 degrees to the side of where your character faces, it returns the point
    /// at the gun's range instead; when the gun fires, the game then turns your character to the aim and asks
    /// again (with the gun hittables layers, for the rocket launcher too). For that case this class repeats the
    /// game's ray without the 45 degree rule, so the crosshair shows where the shot will go after the turn.
    /// </para>
    /// <para>
    /// The arguments are the ones the game's own shots use. With Diagnostics on, the repeated ray is checked
    /// against the game's result about once a second while within 45 degrees, and any difference is logged,
    /// so a game update that changes the aiming shows up in the log.
    /// </para>
    /// <para>
    /// If a game update removes or renames one of the methods used here, the first call fails, that's logged
    /// once, and from then on <see cref="TryGet"/> returns false, so the crosshairs fall back to SBGVR's own
    /// raycast instead of breaking.
    /// </para>
    /// </remarks>
    internal static class GameAimPoint
    {
        /// <summary>The game turns your character before shooting when the aim is further to the side than this.</summary>
        private const float MaxLocalYaw = 45f;

        private const float CrossCheckInterval = 1f;
        private const float CrossCheckTolerance = 0.01f;

        // The size of the game's own hit buffer (PlayerGolfer.raycastHitBuffer).
        private static readonly RaycastHit[] Hits = new RaycastHit[100];

        private static readonly HashSet<ItemType> LoggedWeapons = new HashSet<ItemType>();
        private static readonly HashSet<ItemType> LoggedMismatches = new HashSet<ItemType>();
        private static readonly HashSet<string> LoggedErrors = new HashSet<string>();
        private static float _nextCrossCheck;
        private static bool _unavailable;

        /// <summary>
        /// The point the equipped gun will shoot at, and the gun's range. False when no gun is equipped, the
        /// game isn't ready, or the game's aiming code isn't what SBGVR expects.
        /// </summary>
        internal static bool TryGet(out Vector3 point, out float range)
        {
            point = default;
            range = 0f;
            if (_unavailable) return false;

            try
            {
                return TryGetFromGame(out point, out range);
            }
            catch (Exception exception) when (exception is MissingMemberException || exception is TypeLoadException)
            {
                _unavailable = true;
                Plugin.Log.LogError($"Gun crosshairs: the game's aiming code has changed ({exception.GetBaseException().Message}). " +
                                    "Gun crosshairs use SBGVR's own raycast instead.");
                return false;
            }
            catch (Exception exception)
            {
                if (LoggedErrors.Add(exception.GetType().FullName + exception.Message))
                    Plugin.Log.LogWarning($"Gun crosshairs: couldn't get the game's aim point: {exception}");
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryGetFromGame(out Vector3 point, out float range)
        {
            point = default;
            range = 0f;

            var player = GameManager.LocalPlayerInfo;
            var items = GameManager.ItemSettings;
            var layers = GameManager.LayerSettings;
            if (player == null || items == null || layers == null) return false;

            var inventory = player.Inventory;
            if (inventory == null) return false;

            // The arguments each gun's shot passes (PlayerInventory's Shoot routines and CastRailgunAim).
            var weapon = player.NetworkedEquippedItem;
            Vector3 backmost, forward;
            int layerMask, layerMaskAfterTurning;
            switch (weapon)
            {
                case ItemType.DuelingPistol:
                    backmost = inventory.GetDuelingPistolBarrelEndPosition();
                    forward = inventory.GetDuelingPistolBarrelForward();
                    range = items.DuelingPistolMaxAimingDistance;
                    layerMask = layerMaskAfterTurning = layers.GunHittablesMask;
                    break;
                case ItemType.ElephantGun:
                    backmost = inventory.GetElephantGunBarrelEndPosition();
                    forward = inventory.GetElephantGunBarrelForward();
                    range = items.ElephantGunMaxAimingDistance;
                    layerMask = layerMaskAfterTurning = layers.GunHittablesMask;
                    break;
                case ItemType.RocketLauncher:
                    backmost = inventory.GetRocketLauncherBarrelFrontEndPosition();
                    forward = inventory.GetRocketLauncherBarrelForward();
                    range = items.RocketLauncherMaxAimingDistance;
                    layerMask = layers.RocketHittablesMask;
                    layerMaskAfterTurning = layers.GunHittablesMask;
                    break;
                case ItemType.Railgun:
                    backmost = inventory.GetRailgunBeamOriginPosition();
                    forward = inventory.GetRailgunBarrelForward();
                    range = items.RailgunMaxAimingDistance;
                    layerMask = layerMaskAfterTurning = layers.RailgunAimablesMask;
                    break;
                default:
                    return false;
            }

            LogWeapon(weapon, range, layerMask, layerMaskAfterTurning);

            point = inventory.GetFirearmAimPoint(backmost, forward, range, layerMask, out var localYaw);
            if (Mathf.Abs(localYaw) <= MaxLocalYaw)
            {
                CrossCheck(weapon, inventory, point, backmost, forward, range, layerMask);
                return true;
            }

            // The shot will turn your character first and aim again: show where that goes.
            point = AimWithoutTurnLimit(inventory, backmost, forward, range, layerMaskAfterTurning);
            return true;
        }

        /// <summary>
        /// The game's aim (GetFirearmAimPoint) without its 45 degree rule: the nearest hit along the ray, in front
        /// of the gun and not on your own character, or the point at the gun's range.
        /// </summary>
        private static Vector3 AimWithoutTurnLimit(PlayerInventory inventory, Vector3 backmost, Vector3 forward,
            float range, int layerMask)
        {
            // The game's ray: GameManager.Camera, which the controller-aiming patches point along the controller
            // or your head.
            var view = Controls.ControllerAim.TransformOf(GameManager.Camera);
            var origin = view.position;
            var direction = view.forward;

            var count = Physics.RaycastNonAlloc(new Ray(origin, direction), Hits, range, layerMask, QueryTriggerInteraction.Ignore);
            var nearest = float.MaxValue;
            var point = origin + direction * range;
            for (var i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (hit.distance >= nearest || hit.collider == null) continue;
                if (Vector3.Dot(hit.point - backmost, forward) <= 0f) continue;
                if (hit.collider.GetComponentInParent<PlayerInventory>(true) == inventory) continue;

                nearest = hit.distance;
                point = hit.point;
            }

            return point;
        }

        /// <summary>With Diagnostics on, about once a second: does the repeated ray still match the game's?</summary>
        private static void CrossCheck(ItemType weapon, PlayerInventory inventory, Vector3 gamePoint, Vector3 backmost,
            Vector3 forward, float range, int layerMask)
        {
            if (!Settings.DiagnosticsEnabled.Value || Time.unscaledTime < _nextCrossCheck || LoggedMismatches.Contains(weapon)) return;
            _nextCrossCheck = Time.unscaledTime + CrossCheckInterval;

            var ownPoint = AimWithoutTurnLimit(inventory, backmost, forward, range, layerMask);
            var difference = Vector3.Distance(ownPoint, gamePoint);
            if (difference <= CrossCheckTolerance) return;

            LoggedMismatches.Add(weapon);
            Plugin.Log.LogWarning($"[diag] Gun crosshairs: SBGVR's copy of the game's aim for the {weapon} is {difference:0.00} m off " +
                                  "the game's own; while aiming more than 45 degrees to the side, its crosshair may not be exact.");
        }

        private static void LogWeapon(ItemType weapon, float range, int layerMask, int layerMaskAfterTurning)
        {
            if (!Settings.DiagnosticsEnabled.Value || !LoggedWeapons.Add(weapon)) return;
            Plugin.Log.LogInfo($"[diag] Gun crosshairs: the {weapon} aims up to {range:0.#} m, layers 0x{layerMask:X8}" +
                               (layerMaskAfterTurning != layerMask ? $" (0x{layerMaskAfterTurning:X8} after turning)" : "") + ".");
        }
    }
}
