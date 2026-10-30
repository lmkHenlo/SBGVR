// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using HarmonyLib;
using UnityEngine;

namespace SBGVR.Controls
{
    /// <summary>
    /// VR Controls.MotionCamera for weapons: while you aim an item with the controllers in third person, turning
    /// your DominantHand controller turns the camera by the same angle, left and right and up and down, and
    /// weapons aim along the camera, as the golf club's swing does. When the shot is done, or you stop aiming,
    /// the camera's up and down tilt eases back to where it was before you aimed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It works like <see cref="MotionCamera"/> does for golf swings: when you start aiming, the controller's
    /// heading and tilt in tracking space (the room) are remembered, and each frame the change since then turns
    /// the orbit camera (<c>OrbitCameraModule.SetYaw</c> and <c>SetPitch</c>), adding only the change since the
    /// last frame, so the thumbstick keeps working alongside. Unlike golf, the turn is 1:1, with no scaling,
    /// dead zone, limit or smoothing (the game's own pitch limit, <c>maxPitch</c>, still applies), and sideways
    /// hand movement doesn't count.
    /// </para>
    /// <para>
    /// While it steers, weapons aim along the camera (<see cref="ControllerAim.TryGetAimRay"/> asks
    /// <see cref="TryGetCameraAim"/>). Otherwise the controller's turn would count twice: once through the
    /// camera, and again through the controller's ray, which turns with the camera. On a flat screen the camera
    /// is the aim too, and your character faces it while aiming (<c>PlayerMovement.UpdateTargetYaw</c>), so the
    /// camera, your character and the shot line up.
    /// </para>
    /// <para>
    /// Tilt is restored, turning isn't. The camera's pitch from before you started aiming is kept. While the item
    /// is in use (the shot), the tilt holds still, so the shot goes where you aimed; once the use is over, the
    /// pitch eases back to the kept value (<see cref="RestoreSeconds"/>), and if you're still aiming, tilting
    /// carries on from there. When you stop aiming, it eases back the same way. Restoring sets the kept pitch
    /// itself rather than undoing the changes, so repeated aiming and firing can't build up an error. The
    /// camera's yaw stays where you turned it, as it does after turning with the thumbstick.
    /// </para>
    /// <para>
    /// Only the camera's yaw and pitch change: nothing moves your character, and UUVR's recentre rotation is left
    /// alone. Nothing changes for golf swings, the rocket club or the Jumbo Burger's giant swing
    /// (<see cref="MotionCamera"/>; a restore still under way stops when one starts), in first person, during
    /// camera transitions, while the game locks the camera's yaw (in a golf cart, say), or with a gamepad,
    /// keyboard or mouse.
    /// </para>
    /// </remarks>
    internal sealed class WeaponMotionCamera : MonoBehaviour
    {
        /// <summary>Time constant of the tilt easing back after a shot or aiming, in seconds.</summary>
        private const float RestoreSeconds = 0.12f;

        /// <summary>Within this many degrees of the kept pitch, the restore finishes on it exactly.</summary>
        private const float RestoreSnapDegrees = 0.05f;

        private static AccessTools.FieldRef<OrbitCameraModule, bool> _yawLocked;
        private static bool _steering;
        private static int _steeringFrame = -1;

        private bool _hasReference;
        private Vector3 _referenceForward;
        private float _appliedYaw;
        private float _referenceTilt;
        private float _appliedTilt;
        private bool _wasUsing;

        // The camera's pitch from before you started aiming, and whether it's being eased back to.
        private bool _hasKeptPitch;
        private float _keptPitch;
        private bool _restoring;

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

        private void OnDisable()
        {
            StopSteering();
            _restoring = false;
            _hasKeptPitch = false;
        }

        /// <summary>
        /// The rotation weapons aim along while this steers the camera: the orbit camera's
        /// (<c>OrbitCameraModule.GetRotation</c> is <c>Euler(pitch, yaw, roll)</c>; roll is left out). False
        /// otherwise, and the controller's own ray is used.
        /// </summary>
        internal static bool TryGetCameraAim(out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            // Steering is decided once a frame in Update; after the frame it was decided for, it lapses.
            if (!_steering || Time.frameCount - _steeringFrame > 1) return false;
            if (!CameraModuleController.TryGetOrbitModule(out var orbit) || orbit == null) return false;
            rotation = Quaternion.Euler(orbit.Pitch, orbit.Yaw, 0f);
            return true;
        }

        private void Update()
        {
            var orbit = SteeredOrbit(_hasReference, out var inventory);
            if (orbit == null || !ControllerAim.TryGetControllerPose(out _, out var rotation))
            {
                StopSteering();
                UpdateRestore(RestorableOrbit());
                return;
            }

            var pointing = rotation * Vector3.forward;
            var tilt = TiltOf(pointing);
            if (!_hasReference)
            {
                _hasReference = true;
                _referenceForward = Horizontal(pointing);
                _appliedYaw = 0f;
                _referenceTilt = tilt;
                _appliedTilt = 0f;
                _wasUsing = false;
                // A restore still under way keeps the pitch from before the earlier aim: that's the one to return to.
                if (!_hasKeptPitch)
                {
                    _hasKeptPitch = true;
                    _keptPitch = orbit.Pitch;
                }

                _restoring = false;
            }

            // Left and right, 1:1. Pointing almost straight up or down has no useful heading: hold the yaw.
            if (new Vector2(pointing.x, pointing.z).sqrMagnitude >= 0.01f)
            {
                var yaw = Vector3.SignedAngle(_referenceForward, Horizontal(pointing), Vector3.up);
                var yawChange = Mathf.DeltaAngle(_appliedYaw, yaw);
                if (yawChange != 0f) orbit.SetYaw(orbit.Yaw + yawChange);
                _appliedYaw = yaw;
            }

            // Up and down, 1:1, except while the item is in use (the shot holds still) and while the tilt eases
            // back after it. The orbit camera's pitch grows as it looks down, so tilting up lowers it.
            var inUse = inventory.IsUsingItemAtAll;
            if (_wasUsing && !inUse) _restoring = true;
            _wasUsing = inUse;

            if (inUse)
            {
                // Hold the camera's tilt for the shot.
            }
            else if (_restoring)
            {
                UpdateRestore(orbit);
                if (!_restoring)
                {
                    // Carry on tilting from the restored pitch.
                    _referenceTilt = tilt;
                    _appliedTilt = 0f;
                }
            }
            else
            {
                var tiltNow = Mathf.DeltaAngle(_referenceTilt, tilt);
                var tiltChange = tiltNow - _appliedTilt;
                if (tiltChange != 0f) orbit.SetPitch(orbit.Pitch - tiltChange);
                _appliedTilt = tiltNow;
            }

            _steering = true;
            _steeringFrame = Time.frameCount;
        }

        /// <summary>
        /// The orbit camera while the controller should steer it for a weapon, or null. It starts when you aim an
        /// item and carries on while the item is in use, so firing doesn't switch the aim back to the controller.
        /// </summary>
        private static OrbitCameraModule SteeredOrbit(bool alreadySteering, out PlayerInventory inventory)
        {
            inventory = null;
            if (!VrState.IsRunning || !Settings.MotionControls.Value || !Settings.MotionCamera.Value ||
                !Settings.ControllerAiming.Value || !MotionControls.InUse) return null;

            inventory = GameManager.LocalPlayerInventory;
            if (inventory == null) return null;
            if (!(inventory.IsAimingItem || (alreadySteering && inventory.IsUsingItemAtAll))) return null;

            if (CameraModuleController.IsTransitioning || CameraModuleController.CurrentModuleType != CameraModuleType.Orbit)
                return null;
            if (!CameraModuleController.TryGetOrbitModule(out var orbit) || orbit == null || _yawLocked(orbit)) return null;
            return orbit;
        }

        /// <summary>
        /// The orbit camera while a restore may carry on after aiming, or null, which ends it: not while the game
        /// has another camera or is moving between them, and never during a golf club, rocket club or Jumbo
        /// Burger swing, which <see cref="MotionCamera"/> and the game control.
        /// </summary>
        private static OrbitCameraModule RestorableOrbit()
        {
            if (!VrState.IsRunning || CameraModuleController.IsTransitioning ||
                CameraModuleController.CurrentModuleType != CameraModuleType.Orbit) return null;

            var golfer = GameManager.LocalPlayerAsGolfer;
            if (golfer != null && (golfer.IsAimingSwing || golfer.IsChargingSwing || golfer.IsSwinging)) return null;

            return CameraModuleController.TryGetOrbitModule(out var orbit) ? orbit : null;
        }

        /// <summary>Eases the camera's pitch back to the kept pitch, ending on it exactly.</summary>
        private void UpdateRestore(OrbitCameraModule orbit)
        {
            if (!_restoring) return;
            if (orbit == null || !_hasKeptPitch)
            {
                // The game took the camera (or a swing started): leave it to the game.
                _restoring = false;
                _hasKeptPitch = false;
                return;
            }

            var next = Mathf.Lerp(orbit.Pitch, _keptPitch, 1f - Mathf.Exp(-Time.unscaledDeltaTime / RestoreSeconds));
            if (Mathf.Abs(next - _keptPitch) <= RestoreSnapDegrees)
            {
                next = _keptPitch;
                _restoring = false;
                // Still aiming (after a shot): keep it for when aiming ends.
                if (!_hasReference) _hasKeptPitch = false;
            }

            orbit.SetPitch(next);
        }

        private void StopSteering()
        {
            if (!_hasReference) return;
            if (Settings.DiagnosticsEnabled.Value && (Mathf.Abs(_appliedYaw) >= 0.5f || _hasKeptPitch))
                Plugin.Log.LogInfo($"Motion camera: turned the camera {_appliedYaw:+0.0;-0.0;0}° while aiming an item" +
                                   (_hasKeptPitch ? $"; tilt eases back to {_keptPitch:0.0}°." : "."));
            _hasReference = false;
            _appliedYaw = 0f;
            _appliedTilt = 0f;
            _wasUsing = false;
            _steering = false;
            if (_hasKeptPitch) _restoring = true;
        }

        private static Vector3 Horizontal(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
        }

        /// <summary>Degrees above the horizon.</summary>
        private static float TiltOf(Vector3 forward) => Mathf.Asin(Mathf.Clamp(forward.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
    }
}
