// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System;
using HarmonyLib;
using UnityEngine;

namespace SBGVR.Controls
{
    /// <summary>
    /// VR Controls.FineSwingAim: while you aim and charge a golf club or rocket club swing, small movements of
    /// your dominant hand's controller fine-tune the swing's direction on top of the camera and swing angle.
    /// </summary>
    /// <remarks>
    /// When you start aiming, the controller's pose is remembered. From then on, turning it left or right and
    /// moving it sideways nudge the swing left or right, and tilting it nudges the swing up or down, each scaled
    /// down and capped, so big hand movements make small, precise changes. The adjustment freezes the moment the
    /// swing starts, so pulling the trigger or following through can't change where the ball goes, and resets
    /// when you stop aiming. Poses are read in tracking space (the room), so turning the camera or your head
    /// doesn't count as hand movement.
    ///
    /// The game takes every swing's direction, for the hit and for the trajectory preview, from
    /// <c>PlayerGolfer.GetSwingDirection</c>; <see cref="FineSwingAimPatches"/> turns its result by the
    /// adjustment. Your character and the camera don't turn. The game sends the resulting hit direction to the
    /// server itself, as with any swing.
    /// </remarks>
    internal sealed class FineSwingAim : MonoBehaviour
    {
        private const float RotationDeadZone = 1f;

        /// <summary>The game's own (private) PlayerGolfer.GetSwingType.</summary>
        private static Func<PlayerGolfer, SwingType> GetSwingType;
        private const float MovementDeadZone = 0.01f;

        /// <summary>Whether <see cref="Yaw"/> and <see cref="Pitch"/> apply to the local player's swing now.</summary>
        internal static bool Active { get; private set; }

        /// <summary>Degrees to turn the swing: positive is to the right.</summary>
        internal static float Yaw { get; private set; }

        /// <summary>Degrees to tilt the swing: positive is up.</summary>
        internal static float Pitch { get; private set; }

        private bool _hasReference;
        private Vector3 _referencePosition;
        private Vector3 _referenceForward;
        private float _referencePitch;
        private bool _frozen;

        private void Awake()
        {
            var method = AccessTools.Method(typeof(PlayerGolfer), "GetSwingType");
            if (method != null) GetSwingType = AccessTools.MethodDelegate<Func<PlayerGolfer, SwingType>>(method);
            if (GetSwingType != null) return;

            Plugin.Log.LogError("[FAILED] Fine swing aiming: PlayerGolfer.GetSwingType is missing. Fine swing aiming is off.");
            enabled = false;
        }

        private void OnDisable() => Reset();

        private void Update()
        {
            var golfer = LocalSwingingGolfer();
            if (golfer == null)
            {
                Reset();
                return;
            }

            if (golfer.IsSwinging)
            {
                // Keep the adjustment the swing was released with until the swing is over.
                if (!_frozen && _hasReference && Settings.DiagnosticsEnabled.Value)
                    Plugin.Log.LogInfo($"Fine swing aim: swung with {Yaw:+0.0;-0.0;0}° sideways and {Pitch:+0.0;-0.0;0}° up.");
                _frozen = true;
                Active = _hasReference;
                return;
            }

            if (!ControllerAim.TryGetControllerPose(out var position, out var rotation))
            {
                Active = _hasReference;
                return;
            }

            var forward = rotation * Vector3.forward;
            if (!_hasReference)
            {
                _hasReference = true;
                _referencePosition = position;
                _referenceForward = Horizontal(forward);
                _referencePitch = PitchOf(forward);
                Yaw = 0f;
                Pitch = 0f;
                Active = true;
                return;
            }

            var strength = Settings.FineSwingAimStrength.Value;
            var turn = Vector3.SignedAngle(_referenceForward, Horizontal(forward), Vector3.up);
            var right = Vector3.Cross(Vector3.up, _referenceForward);
            var sideways = Vector3.Dot(position - _referencePosition, right);
            var yaw = DeadZone(turn, RotationDeadZone) * strength +
                      DeadZone(sideways, MovementDeadZone) * 10f * Settings.FineSwingAimMovement.Value;
            Yaw = Mathf.Clamp(yaw, -Settings.FineSwingAimMaxYaw.Value, Settings.FineSwingAimMaxYaw.Value);

            var tilt = Mathf.DeltaAngle(_referencePitch, PitchOf(forward));
            var maxPitch = Settings.FineSwingAimMaxPitch.Value;
            Pitch = Mathf.Clamp(DeadZone(tilt, RotationDeadZone) * strength, -maxPitch, maxPitch);
            Active = true;
        }

        /// <summary>The local player's golfer while fine aiming applies to it, otherwise null.</summary>
        private static PlayerGolfer LocalSwingingGolfer()
        {
            if (!VrState.IsRunning || !Settings.MotionControls.Value || !Settings.FineSwingAim.Value) return null;

            var player = GameManager.LocalPlayerInfo;
            var golfer = player != null ? player.AsGolfer : null;
            if (golfer == null) return null;
            if (!(golfer.IsAimingSwing || golfer.IsChargingSwing || golfer.IsSwinging)) return null;

            // The golf club and the rocket club, not the Jumbo Burger's giant swing.
            var type = GetSwingType(golfer);
            if (type != SwingType.Regular && type != SwingType.RocketDriver) return null;

            // A homing swing with a lock-on target aims itself, as in the game.
            if (golfer.LockOnTarget != null) return null;
            return golfer;
        }

        private void Reset()
        {
            _hasReference = false;
            _frozen = false;
            Active = false;
            Yaw = 0f;
            Pitch = 0f;
        }

        private static Vector3 Horizontal(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
        }

        /// <summary>Degrees above the horizon.</summary>
        private static float PitchOf(Vector3 forward) => Mathf.Asin(Mathf.Clamp(forward.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;

        private static float DeadZone(float value, float deadZone) =>
            Mathf.Abs(value) <= deadZone ? 0f : value - Mathf.Sign(value) * deadZone;
    }

    [HarmonyPatch]
    internal static class FineSwingAimPatches
    {
        /// <summary>Turns the local player's swing direction by the fine aim adjustment.</summary>
        [HarmonyPatch(typeof(PlayerGolfer), "GetSwingDirection")]
        [HarmonyPostfix]
        private static void Adjust(PlayerGolfer __instance, float pitch, ref Vector3 __result)
        {
            if (!FineSwingAim.Active || (FineSwingAim.Yaw == 0f && FineSwingAim.Pitch == 0f)) return;

            var player = GameManager.LocalPlayerInfo;
            if (player == null || !ReferenceEquals(__instance, player.AsGolfer)) return;

            // Tilt within the swing angles the game allows, and never lift a putt.
            var extraPitch = 0f;
            if (pitch > 0.01f)
            {
                var max = GameManager.GolfSettings.MaxSwingPitch;
                extraPitch = Mathf.Clamp(pitch + FineSwingAim.Pitch, 0.01f, max) - pitch;
            }

            var transform = __instance.transform;
            var tilted = Quaternion.AngleAxis(-extraPitch, transform.right) * __result;
            __result = Quaternion.AngleAxis(FineSwingAim.Yaw, Vector3.up) * tilted;
        }
    }
}
