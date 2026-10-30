// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using HarmonyLib;
using UnityEngine;

namespace SBGVR.Controls
{
    /// <summary>
    /// VR Controls.MotionCamera for weapons: while you aim an item with the controllers, in third person or in
    /// SBGVR's first person, turning your DominantHand controller turns the camera by the same angle, left and
    /// right and up and down, and weapons aim along the camera, as the golf club's swing does. When the shot is
    /// done, or you stop aiming, the camera's up and down tilt eases back to where it was before you aimed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It works like <see cref="MotionCamera"/> does for golf swings: when you start aiming, the controller's
    /// heading and tilt in tracking space (the room) are remembered, and each frame the change since then turns
    /// the camera, adding only the change since the last frame, so the thumbstick keeps working alongside.
    /// Unlike golf, the turn is 1:1, with no scaling, dead zone, limit or smoothing (the game's own pitch limit,
    /// <c>maxPitch</c>, still applies), and sideways hand movement doesn't count.
    /// </para>
    /// <para>
    /// The camera is the game's camera module for the view you're in. In third person that's
    /// <c>OrbitCameraModule</c>: <c>SetYaw</c> and <c>SetPitch</c>. In first person it's
    /// <c>FirstPersonCameraModule</c>: <c>SetYaw</c>, and the pitch through <see cref="FirstPersonPitch"/>, which
    /// <see cref="View.FirstPersonView"/> applies each frame in place of the level pitch it otherwise keeps there
    /// (your head does the looking up and down). Both modules turn the camera by <c>Euler(pitch, yaw, roll)</c>.
    /// </para>
    /// <para>
    /// While it steers, weapons aim along the camera (<see cref="ControllerAim.TryGetAimRay"/> asks
    /// <see cref="TryGetCameraAim"/>). Otherwise the controller's turn would count twice: once through the camera,
    /// and again through the controller's ray, which turns with the camera. On a flat screen the camera is the
    /// aim too, and your character faces it while aiming (<c>PlayerMovement.UpdateTargetYaw</c>; in first person
    /// <see cref="View.FirstPersonView.YawOf"/> returns the camera's yaw while this steers, instead of your
    /// head's), so the camera, your character and the shot line up.
    /// </para>
    /// <para>
    /// Tilt is restored, turning isn't. The pitch from before you started aiming is kept (in first person it's
    /// always level). While the item is in use (the shot), the tilt holds still, so the shot goes where you aimed;
    /// once the use is over, the pitch eases back to the kept value (<see cref="RestoreSeconds"/>), and if you're
    /// still aiming, tilting carries on from there. When you stop aiming, it eases back the same way. Restoring
    /// sets the kept pitch itself rather than undoing the changes, so repeated aiming and firing can't build up an
    /// error. If the camera it was restoring isn't showing any more (you switched between first and third person,
    /// say), or a golf club, rocket club or Jumbo Burger swing starts, the kept pitch is set at once instead, so no
    /// tilt is left behind. The camera's yaw stays where you turned it, as it does after the thumbstick.
    /// </para>
    /// <para>
    /// Only the camera's yaw and pitch change: nothing moves your character, and UUVR's recentre rotation is left
    /// alone. Nothing changes for golf swings, the rocket club or the Jumbo Burger's giant swing
    /// (<see cref="MotionCamera"/>), during camera transitions, in the railgun's own scope outside SBGVR's first
    /// person, while the game locks the camera's yaw (a golf cart), or with a gamepad, keyboard or mouse.
    /// </para>
    /// </remarks>
    internal sealed class WeaponMotionCamera : MonoBehaviour
    {
        private enum CameraView
        {
            None,
            ThirdPerson,
            FirstPerson,
        }

        /// <summary>Time constant of the tilt easing back after a shot or aiming, in seconds.</summary>
        private const float RestoreSeconds = 0.12f;

        /// <summary>Within this many degrees of the kept pitch, the restore finishes on it exactly.</summary>
        private const float RestoreSnapDegrees = 0.05f;

        private static AccessTools.FieldRef<OrbitCameraModule, bool> _yawLocked;
        private static AccessTools.FieldRef<FirstPersonCameraModule, float> _firstPersonMaxPitch;
        private static bool _steering;
        private static int _steeringFrame = -1;
        private static CameraView _steeringView;

        /// <summary>
        /// The first-person camera's pitch while this tilts it, 0 (level) otherwise. Applied by
        /// <see cref="View.FirstPersonView"/> each frame, just before the camera places itself.
        /// </summary>
        internal static float FirstPersonPitch { get; private set; }

        /// <summary>True while this turns SBGVR's first-person camera for a weapon.</summary>
        internal static bool SteersFirstPerson => IsSteering && _steeringView == CameraView.FirstPerson;

        // Steering is decided once a frame in Update; after the frame it was decided for, it lapses.
        private static bool IsSteering => _steering && Time.frameCount - _steeringFrame <= 1;

        private CameraView _view;
        private Vector3 _referenceForward;
        private float _appliedYaw;
        private float _referenceTilt;
        private float _appliedTilt;
        private bool _wasUsing;

        // The pitch from before you started aiming, for the camera it was kept for, and whether it's being eased
        // back to.
        private CameraView _keptView;
        private float _keptPitch;
        private bool _restoring;

        private void Awake()
        {
            try
            {
                _yawLocked = AccessTools.FieldRefAccess<OrbitCameraModule, bool>("yawLockedToSubjectForward");
                _firstPersonMaxPitch = AccessTools.FieldRefAccess<FirstPersonCameraModule, float>("maxPitch");
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
            FinishRestoreNow();
        }

        /// <summary>
        /// The rotation weapons aim along while this steers the camera: the camera's own, without roll. False
        /// otherwise, and the controller's own ray is used.
        /// </summary>
        internal static bool TryGetCameraAim(out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            if (!IsSteering) return false;

            switch (_steeringView)
            {
                case CameraView.ThirdPerson:
                    if (!CameraModuleController.TryGetOrbitModule(out var orbit) || orbit == null) return false;
                    rotation = Quaternion.Euler(orbit.Pitch, orbit.Yaw, 0f);
                    return true;
                case CameraView.FirstPerson:
                    // The pitch this frame is FirstPersonPitch; the module only takes it when it places itself.
                    if (!CameraModuleController.TryGetFirstPersonModule(out var firstPerson) || firstPerson == null) return false;
                    rotation = Quaternion.Euler(FirstPersonPitch, firstPerson.Yaw, 0f);
                    return true;
                default:
                    return false;
            }
        }

        private void Update()
        {
            var view = SteeredView(_view != CameraView.None, out var inventory, out var orbit, out var firstPerson);
            if (view != _view && _view != CameraView.None) StopSteering();

            if (view == CameraView.None || !ControllerAim.TryGetControllerPose(out _, out var rotation))
            {
                StopSteering();
                UpdateRestore();
                return;
            }

            var pointing = rotation * Vector3.forward;
            var tilt = TiltOf(pointing);
            if (_view == CameraView.None)
            {
                // A restore for the other camera can't carry on now: finish it at once.
                if (_restoring && _keptView != view) FinishRestoreNow();

                _view = view;
                _referenceForward = Horizontal(pointing);
                _appliedYaw = 0f;
                _referenceTilt = tilt;
                _appliedTilt = 0f;
                _wasUsing = false;
                // A restore still under way keeps the pitch from before the earlier aim: that's the one to return to.
                if (_keptView != view)
                {
                    _keptView = view;
                    _keptPitch = view == CameraView.ThirdPerson ? orbit.Pitch : 0f;
                }

                _restoring = false;
            }

            // Left and right, 1:1. Pointing almost straight up or down has no useful heading: hold the yaw.
            if (new Vector2(pointing.x, pointing.z).sqrMagnitude >= 0.01f)
            {
                var yaw = Vector3.SignedAngle(_referenceForward, Horizontal(pointing), Vector3.up);
                var yawChange = Mathf.DeltaAngle(_appliedYaw, yaw);
                if (yawChange != 0f)
                {
                    if (view == CameraView.ThirdPerson) orbit.SetYaw(orbit.Yaw + yawChange);
                    else firstPerson.SetYaw(firstPerson.Yaw + yawChange);
                }

                _appliedYaw = yaw;
            }

            // Up and down, 1:1, except while the item is in use (the shot holds still) and while the tilt eases
            // back after it. The cameras' pitch grows as they look down, so tilting up lowers it.
            var inUse = inventory.IsUsingItemAtAll;
            if (_wasUsing && !inUse) _restoring = true;
            _wasUsing = inUse;

            if (inUse)
            {
                // Hold the camera's tilt for the shot.
            }
            else if (_restoring)
            {
                UpdateRestore();
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
                if (tiltChange != 0f) SetPitch(view, GetPitch(view) - tiltChange);
                _appliedTilt = tiltNow;
            }

            _steering = true;
            _steeringFrame = Time.frameCount;
            _steeringView = view;
        }

        /// <summary>
        /// The camera the controller should steer for a weapon, or None. It starts when you aim an item and carries
        /// on while the item is in use, so firing doesn't switch the aim back to the controller.
        /// </summary>
        private static CameraView SteeredView(bool alreadySteering, out PlayerInventory inventory,
            out OrbitCameraModule orbit, out FirstPersonCameraModule firstPerson)
        {
            inventory = null;
            orbit = null;
            firstPerson = null;
            if (!VrState.IsRunning || !Settings.MotionControls.Value || !Settings.MotionCamera.Value ||
                !Settings.ControllerAiming.Value || !MotionControls.InUse) return CameraView.None;

            inventory = GameManager.LocalPlayerInventory;
            if (inventory == null) return CameraView.None;
            if (!(inventory.IsAimingItem || (alreadySteering && inventory.IsUsingItemAtAll))) return CameraView.None;
            if (CameraModuleController.IsTransitioning) return CameraView.None;

            switch (CameraModuleController.CurrentModuleType)
            {
                case CameraModuleType.Orbit:
                    if (!CameraModuleController.TryGetOrbitModule(out orbit) || orbit == null || _yawLocked(orbit))
                        return CameraView.None;
                    return CameraView.ThirdPerson;
                case CameraModuleType.FirstPerson:
                    // Only SBGVR's first person, and not in a golf cart, where it faces the cart's front.
                    if (!CameraModuleController.TryGetFirstPersonModule(out firstPerson) ||
                        !View.FirstPersonView.Shows(firstPerson) || View.FirstPersonView.InGolfCart())
                        return CameraView.None;
                    return CameraView.FirstPerson;
                default:
                    return CameraView.None;
            }
        }

        private static float GetPitch(CameraView view)
        {
            if (view == CameraView.FirstPerson) return FirstPersonPitch;
            return CameraModuleController.TryGetOrbitModule(out var orbit) && orbit != null ? orbit.Pitch : 0f;
        }

        private static void SetPitch(CameraView view, float pitch)
        {
            if (view == CameraView.None) return;
            if (view == CameraView.FirstPerson)
            {
                // Within the first-person camera's own limit, as its SetPitch would keep it.
                var limit = CameraModuleController.TryGetFirstPersonModule(out var firstPerson) && firstPerson != null
                    ? _firstPersonMaxPitch(firstPerson)
                    : 89f;
                FirstPersonPitch = Mathf.Clamp(pitch, -limit, limit);
                return;
            }

            if (CameraModuleController.TryGetOrbitModule(out var orbit) && orbit != null) orbit.SetPitch(pitch);
        }

        /// <summary>
        /// Whether the kept pitch's camera is showing and free to ease back: not during a camera transition or a
        /// golf club, rocket club or Jumbo Burger swing.
        /// </summary>
        private static bool CanEase(CameraView view)
        {
            if (!VrState.IsRunning || CameraModuleController.IsTransitioning) return false;

            var golfer = GameManager.LocalPlayerAsGolfer;
            if (golfer != null && (golfer.IsAimingSwing || golfer.IsChargingSwing || golfer.IsSwinging)) return false;

            switch (view)
            {
                case CameraView.ThirdPerson:
                    return CameraModuleController.CurrentModuleType == CameraModuleType.Orbit;
                case CameraView.FirstPerson:
                    return CameraModuleController.CurrentModuleType == CameraModuleType.FirstPerson &&
                           CameraModuleController.TryGetFirstPersonModule(out var firstPerson) &&
                           View.FirstPersonView.Shows(firstPerson);
                default:
                    return false;
            }
        }

        /// <summary>Eases the kept camera's pitch back to the kept pitch, ending on it exactly.</summary>
        private void UpdateRestore()
        {
            if (!_restoring) return;
            if (!CanEase(_keptView))
            {
                FinishRestoreNow();
                return;
            }

            var view = _keptView;
            var next = Mathf.Lerp(GetPitch(view), _keptPitch, 1f - Mathf.Exp(-Time.unscaledDeltaTime / RestoreSeconds));
            if (Mathf.Abs(next - _keptPitch) <= RestoreSnapDegrees)
            {
                next = _keptPitch;
                _restoring = false;
                // Still aiming (after a shot): keep it for when aiming ends.
                if (_view == CameraView.None) _keptView = CameraView.None;
            }

            SetPitch(view, next);
        }

        /// <summary>Sets the kept pitch at once and forgets it, so no tilt is left behind.</summary>
        private void FinishRestoreNow()
        {
            if (_keptView != CameraView.None && (_restoring || _view == CameraView.None)) SetPitch(_keptView, _keptPitch);
            if (_keptView == CameraView.FirstPerson) FirstPersonPitch = 0f;
            _restoring = false;
            _keptView = CameraView.None;
        }

        private void StopSteering()
        {
            if (_view == CameraView.None) return;
            if (Settings.DiagnosticsEnabled.Value)
                Plugin.Log.LogInfo($"Motion camera: turned the {(_view == CameraView.FirstPerson ? "first-person" : "third-person")} " +
                                   $"camera {_appliedYaw:+0.0;-0.0;0}° while aiming an item; tilt eases back to {_keptPitch:0.0}°.");
            _view = CameraView.None;
            _appliedYaw = 0f;
            _appliedTilt = 0f;
            _wasUsing = false;
            _steering = false;
            if (_keptView != CameraView.None) _restoring = true;
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
