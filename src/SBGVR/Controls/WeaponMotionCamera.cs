// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using HarmonyLib;
using UnityEngine;

namespace SBGVR.Controls
{
    /// <summary>
    /// VR Controls.MotionCamera for weapons: while you aim an item with the controllers in third person, turning
    /// your DominantHand controller left or right turns the camera by the same angle, and weapons aim along the
    /// camera, as the golf club's swing does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It works like <see cref="MotionCamera"/> does for golf swings: when you start aiming, the controller's
    /// heading in tracking space (the room) is remembered, and each frame the change since then turns the orbit
    /// camera's yaw (<c>OrbitCameraModule.SetYaw</c>), adding only the change since the last frame, so the
    /// thumbstick keeps working alongside and the camera stays where it is when you stop aiming. Unlike golf,
    /// the turn is 1:1, with no scaling, dead zone, limit or smoothing, and sideways hand movement doesn't count.
    /// Only yaw: tilting the controller doesn't tilt the camera.
    /// </para>
    /// <para>
    /// While it steers, weapons aim along the camera's heading instead of the controller's
    /// (<see cref="ControllerAim.TryGetAimRay"/> asks <see cref="TryGetHeading"/>), with the controller's own up
    /// and down tilt. Otherwise the controller's turn would count twice: once through the camera, and again
    /// through the controller's ray, which turns with the camera. On a flat screen the camera is the aim too, and
    /// your character faces it while aiming (<c>PlayerMovement.UpdateTargetYaw</c>), so the camera, your
    /// character and the shot line up.
    /// </para>
    /// <para>
    /// Only the camera's yaw changes: nothing moves your character, and UUVR's recentre rotation is left alone.
    /// Nothing changes for golf swings (<see cref="MotionCamera"/>), in first person, during camera transitions,
    /// while the game locks the camera's yaw (in a golf cart, say), or with a gamepad, keyboard or mouse.
    /// </para>
    /// </remarks>
    internal sealed class WeaponMotionCamera : MonoBehaviour
    {
        private static AccessTools.FieldRef<OrbitCameraModule, bool> _yawLocked;
        private static bool _steering;
        private static int _steeringFrame = -1;

        private bool _hasReference;
        private Vector3 _referenceForward;
        private float _appliedYaw;

        private void Awake()
        {
            try
            {
                _yawLocked = AccessTools.FieldRefAccess<OrbitCameraModule, bool>("yawLockedToSubjectForward");
            }
            catch (System.Exception exception)
            {
                Plugin.Log.LogError($"[FAILED] Motion camera for weapons: {exception.GetBaseException().Message}. It's off.");
                enabled = false;
            }
        }

        private void OnDisable() => Stop();

        /// <summary>
        /// The heading weapons aim along while this steers the camera: the orbit camera's yaw, in degrees.
        /// False otherwise, and the controller's own ray is used.
        /// </summary>
        internal static bool TryGetHeading(out float yaw)
        {
            yaw = 0f;
            // Steering is decided once a frame in Update; after the frame it was decided for, it lapses.
            if (!_steering || Time.frameCount - _steeringFrame > 1) return false;
            if (!CameraModuleController.TryGetOrbitModule(out var orbit) || orbit == null) return false;
            yaw = orbit.Yaw;
            return true;
        }

        private void Update()
        {
            var orbit = SteeredOrbit(_hasReference);
            if (orbit == null || !ControllerAim.TryGetControllerPose(out _, out var rotation))
            {
                Stop();
                return;
            }

            // Pointing almost straight up or down has no useful heading: hold the camera where it is.
            var pointing = rotation * Vector3.forward;
            if (new Vector2(pointing.x, pointing.z).sqrMagnitude < 0.01f)
            {
                if (_hasReference) MarkSteering();
                return;
            }

            var forward = Horizontal(pointing);
            if (!_hasReference)
            {
                _hasReference = true;
                _referenceForward = forward;
                _appliedYaw = 0f;
            }

            // 1:1: the controller's turn since you started aiming, in the room, is the camera's turn.
            var yaw = Vector3.SignedAngle(_referenceForward, forward, Vector3.up);
            var change = Mathf.DeltaAngle(_appliedYaw, yaw);
            if (change != 0f) orbit.SetYaw(orbit.Yaw + change);
            _appliedYaw = yaw;
            MarkSteering();
        }

        private static void MarkSteering()
        {
            _steering = true;
            _steeringFrame = Time.frameCount;
        }

        /// <summary>
        /// The orbit camera while the controller should steer it for a weapon, or null. It starts when you aim an
        /// item and carries on while the item is in use, so firing doesn't switch the aim back to the controller.
        /// </summary>
        private static OrbitCameraModule SteeredOrbit(bool alreadySteering)
        {
            if (!VrState.IsRunning || !Settings.MotionControls.Value || !Settings.MotionCamera.Value ||
                !Settings.ControllerAiming.Value || !MotionControls.InUse) return null;

            var inventory = GameManager.LocalPlayerInventory;
            if (inventory == null) return null;
            if (!(inventory.IsAimingItem || (alreadySteering && inventory.IsUsingItemAtAll))) return null;

            if (CameraModuleController.IsTransitioning || CameraModuleController.CurrentModuleType != CameraModuleType.Orbit)
                return null;
            if (!CameraModuleController.TryGetOrbitModule(out var orbit) || orbit == null || _yawLocked(orbit)) return null;
            return orbit;
        }

        private void Stop()
        {
            if (_hasReference && Settings.DiagnosticsEnabled.Value && Mathf.Abs(_appliedYaw) >= 0.5f)
                Plugin.Log.LogInfo($"Motion camera: turned the camera {_appliedYaw:+0.0;-0.0;0}° while aiming an item.");
            _hasReference = false;
            _appliedYaw = 0f;
            _steering = false;
        }

        private static Vector3 Horizontal(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
        }
    }
}
