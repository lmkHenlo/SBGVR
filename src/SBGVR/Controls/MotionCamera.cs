// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System;
using HarmonyLib;
using UnityEngine;

namespace SBGVR.Controls
{
    /// <summary>
    /// VR Controls.MotionCamera: while you aim and charge a golf club or rocket club swing, small movements of
    /// your dominant hand's controller turn the game camera, as the right thumbstick does.
    /// </summary>
    /// <remarks>
    /// When you start aiming, the controller's pose is remembered. From then on, turning it left or right and
    /// moving it sideways turn the camera left or right, and tilting it tilts the camera up or down, each scaled
    /// down and capped, so big hand movements make small, precise changes. Each frame only the change since the
    /// last frame is added to the orbit camera's yaw and pitch (<c>OrbitCameraModule.SetYaw</c> and
    /// <c>SetPitch</c>), so the thumbstick keeps working alongside, and the camera stays where it is when you
    /// stop aiming. Nothing moves once the swing starts, so pulling the trigger or following through doesn't
    /// turn it. Poses are read in tracking space (the room), so turning your head doesn't count.
    ///
    /// The swing itself is the game's own: while you aim, your character faces the orbit camera's direction,
    /// and the swing goes that way, exactly as when you turn the camera with the thumbstick.
    /// </remarks>
    internal sealed class MotionCamera : MonoBehaviour
    {
        private const float RotationDeadZone = 1f;
        private const float MovementDeadZone = 0.01f;

        /// <summary>The game's own (private) PlayerGolfer.GetSwingType.</summary>
        private static Func<PlayerGolfer, SwingType> _getSwingType;

        private bool _hasReference;
        private Vector3 _referencePosition;
        private Vector3 _referenceForward;
        private float _referencePitch;

        /// <summary>The yaw and pitch already added to the camera since aiming started.</summary>
        private float _appliedYaw;
        private float _appliedPitch;

        private void Awake()
        {
            var method = AccessTools.Method(typeof(PlayerGolfer), "GetSwingType");
            if (method != null) _getSwingType = AccessTools.MethodDelegate<Func<PlayerGolfer, SwingType>>(method);
            if (_getSwingType != null) return;

            Plugin.Log.LogError("[FAILED] Motion camera: PlayerGolfer.GetSwingType is missing. Motion camera is off.");
            enabled = false;
        }

        private void OnDisable() => Reset();

        private void Update()
        {
            var golfer = LocalAimingGolfer();
            if (golfer == null)
            {
                LogSwing();
                Reset();
                return;
            }

            // Leave the camera alone while the swing plays; aiming again afterwards starts afresh.
            if (golfer.IsSwinging)
            {
                LogSwing();
                Reset();
                return;
            }

            if (CameraModuleController.CurrentModuleType != CameraModuleType.Orbit || CameraModuleController.IsTransitioning) return;
            if (!CameraModuleController.TryGetOrbitModule(out var orbit) || orbit == null) return;
            if (!ControllerAim.TryGetControllerPose(out var position, out var rotation)) return;

            var forward = rotation * Vector3.forward;
            if (!_hasReference)
            {
                _hasReference = true;
                _referencePosition = position;
                _referenceForward = Horizontal(forward);
                _referencePitch = PitchOf(forward);
                _appliedYaw = 0f;
                _appliedPitch = 0f;
                return;
            }

            var strength = Settings.MotionCameraStrength.Value;
            var turn = Vector3.SignedAngle(_referenceForward, Horizontal(forward), Vector3.up);
            var right = Vector3.Cross(Vector3.up, _referenceForward);
            var sideways = Vector3.Dot(position - _referencePosition, right);
            var maxYaw = Settings.MotionCameraMaxYaw.Value;
            var yaw = Mathf.Clamp(
                DeadZone(turn, RotationDeadZone) * strength +
                DeadZone(sideways, MovementDeadZone) * 10f * Settings.MotionCameraMovement.Value,
                -maxYaw, maxYaw);

            // Tilting the controller up looks up; the orbit camera's pitch grows as it looks down.
            var tilt = Mathf.DeltaAngle(_referencePitch, PitchOf(forward));
            var maxPitch = Settings.MotionCameraMaxPitch.Value;
            var pitch = Mathf.Clamp(DeadZone(tilt, RotationDeadZone) * strength, -maxPitch, maxPitch);

            var yawChange = yaw - _appliedYaw;
            var pitchChange = pitch - _appliedPitch;
            if (yawChange != 0f) orbit.SetYaw(orbit.Yaw + yawChange);
            if (pitchChange != 0f) orbit.SetPitch(orbit.Pitch - pitchChange);
            _appliedYaw = yaw;
            _appliedPitch = pitch;
        }

        /// <summary>The local player's golfer while it aims or charges a golf club or rocket club swing.</summary>
        private static PlayerGolfer LocalAimingGolfer()
        {
            if (!VrState.IsRunning || !Settings.MotionControls.Value || !Settings.MotionCamera.Value) return null;

            var player = GameManager.LocalPlayerInfo;
            var golfer = player != null ? player.AsGolfer : null;
            if (golfer == null) return null;
            if (!(golfer.IsAimingSwing || golfer.IsChargingSwing || golfer.IsSwinging)) return null;

            // The golf club and the rocket club, not the Jumbo Burger's giant swing.
            var type = _getSwingType(golfer);
            return type == SwingType.Regular || type == SwingType.RocketDriver ? golfer : null;
        }

        /// <summary>Turns smaller than this, in degrees, aren't logged, so ordinary hand wobble stays out of the log.</summary>
        private const float LogThreshold = 0.5f;

        private void LogSwing()
        {
            if (!_hasReference || !Settings.DiagnosticsEnabled.Value) return;
            if (Mathf.Abs(_appliedYaw) < LogThreshold && Mathf.Abs(_appliedPitch) < LogThreshold) return;
            Plugin.Log.LogInfo($"Motion camera: turned the camera {_appliedYaw:+0.0;-0.0;0}° sideways and " +
                               $"{_appliedPitch:+0.0;-0.0;0}° up while aiming.");
        }

        private void Reset()
        {
            _hasReference = false;
            _appliedYaw = 0f;
            _appliedPitch = 0f;
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
}
