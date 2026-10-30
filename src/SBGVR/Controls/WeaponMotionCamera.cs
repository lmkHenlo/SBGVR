// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using HarmonyLib;
using UnityEngine;
using UnityEngine.XR;
using Uuvr.VrCamera;

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
    /// Unlike golf, the turn is 1:1, with no scaling, dead zone or limit (the game's own pitch limit,
    /// <c>maxPitch</c>, still applies), and sideways hand movement doesn't count.
    /// </para>
    /// <para>
    /// The controller's turn and tilt are smoothed with a One Euro filter (<see cref="OneEuroFilter"/>) before
    /// they turn the camera: hand tremor while you hold still is cut to about a tenth, while a quick turn is
    /// followed within a few milliseconds of your hand stopping. It doesn't change how far the camera turns.
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
    /// always level). While the item is being used (the shot; <see cref="IsFiring"/>, not while the railgun
    /// charges), the tilt holds still, so the shot goes where you aimed; once the use is over, the pitch eases back to the kept value (<see cref="RestoreSeconds"/>), and if you're
    /// still aiming, tilting carries on from there. When you stop aiming, it eases back the same way. Restoring
    /// sets the kept pitch itself rather than undoing the changes, so repeated aiming and firing can't build up an
    /// error. If the camera it was restoring isn't showing any more (you switched between first and third person,
    /// say), or a golf club, rocket club or Jumbo Burger swing starts, the kept pitch is set at once instead, so no
    /// tilt is left behind. The camera's yaw stays where you turned it, as it does after the thumbstick.
    /// </para>
    /// <para>
    /// The railgun's scope locks your view to the gun (<see cref="UpdateViewLock"/>): while it's open and this
    /// steers the camera, VR draws the view with the camera's own rotation instead of adding your head's
    /// (<see cref="VrCamera.IgnoreHeadRotation"/>), so the view looks exactly where the railgun aims, as the
    /// flat-screen scope does, and the controller turns it. Turning your head doesn't move it meanwhile. When the
    /// lock starts, the way your head faces is turned into the camera, so the view doesn't jump; when it ends,
    /// the way your head faces then is taken back out of the camera (the third-person camera, once the scope has
    /// closed), so it doesn't jump back either, and the tilt then eases back as usual.
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
            /// <summary>The railgun's scope from third person: the game's own first-person camera, not SBGVR's.</summary>
            RailgunScope,
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

        // The controller's turn since aiming started (unwrapped, so it can go past 180 degrees) and its tilt,
        // before and after smoothing.
        private float _rawYaw;
        private float _lastWrappedYaw;
        private OneEuroFilter _yawFilter;
        private OneEuroFilter _tiltFilter;
        private bool _wasUsing;

        // Whether the view is locked to the railgun's aim (its scope is open).
        private bool _locked;

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
                case CameraView.RailgunScope:
                    if (!CameraModuleController.TryGetFirstPersonModule(out var scope) || scope == null) return false;
                    rotation = Quaternion.Euler(scope.Pitch, scope.Yaw, 0f);
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
            var dt = Time.unscaledDeltaTime;
            var starting = _view == CameraView.None;
            // A new aim starts from where the controller is, with nothing to catch up.
            if (starting) _tiltFilter.Reset();
            var tilt = _tiltFilter.Filter(TiltOf(pointing), dt);
            if (starting)
            {
                ResolveKeptView();

                // The railgun's scope opened while the third-person camera was still easing back: the game copied
                // that camera's pitch into the scope, so the scope's kept pitch is the one from before, carried over.
                if (_keptView == CameraView.ThirdPerson && view == CameraView.RailgunScope) _keptView = view;

                // A restore for another camera can't carry on now: finish it at once.
                if (_restoring && _keptView != view) FinishRestoreNow();

                _view = view;
                _referenceForward = Horizontal(pointing);
                _appliedYaw = 0f;
                _rawYaw = 0f;
                _lastWrappedYaw = 0f;
                _yawFilter.Reset();
                _referenceTilt = tilt;
                _appliedTilt = 0f;
                _wasUsing = false;
                // A restore still under way keeps the pitch from before the earlier aim: that's the one to return to.
                if (_keptView != view)
                {
                    _keptView = view;
                    // Level in SBGVR's first person; the railgun's scope starts at the third-person camera's pitch,
                    // which the game copies into it.
                    _keptPitch = view == CameraView.ThirdPerson ? orbit.Pitch
                        : view == CameraView.RailgunScope ? firstPerson.Pitch
                        : 0f;
                }

                _restoring = false;
            }

            // Left and right, 1:1. Pointing almost straight up or down has no useful heading: hold the yaw.
            if (new Vector2(pointing.x, pointing.z).sqrMagnitude >= 0.01f)
            {
                var wrappedYaw = Vector3.SignedAngle(_referenceForward, Horizontal(pointing), Vector3.up);
                _rawYaw += Mathf.DeltaAngle(_lastWrappedYaw, wrappedYaw);
                _lastWrappedYaw = wrappedYaw;
                var yaw = _yawFilter.Filter(_rawYaw, dt);
                var yawChange = yaw - _appliedYaw;
                if (yawChange != 0f)
                {
                    if (view == CameraView.ThirdPerson) orbit.SetYaw(orbit.Yaw + yawChange);
                    else firstPerson.SetYaw(firstPerson.Yaw + yawChange);
                }

                _appliedYaw = yaw;
            }

            // Up and down, 1:1, except while the item is being used (the shot holds still) and while the tilt eases
            // back after it. The cameras' pitch grows as they look down, so tilting up lowers it.
            var inUse = IsFiring(inventory);
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

            UpdateViewLock(view, inventory, firstPerson);

            _steering = true;
            _steeringFrame = Time.frameCount;
            _steeringView = view;
        }

        /// <summary>Whether the railgun's scope is open: the local player aims the railgun.</summary>
        private static bool IsRailgunScoped(PlayerInventory inventory)
        {
            var player = GameManager.LocalPlayerInfo;
            return inventory != null && inventory.IsInFirstPersonAim && player != null &&
                   player.NetworkedEquippedItem == ItemType.Railgun;
        }

        /// <summary>
        /// Locks the view to the camera, and so to the railgun's aim, while the railgun's scope is open in SBGVR's
        /// first person or from third person; unlocks it otherwise. The camera is turned so the view doesn't
        /// jump either way.
        /// </summary>
        private void UpdateViewLock(CameraView view, PlayerInventory inventory, FirstPersonCameraModule module)
        {
            var lockView = (view == CameraView.RailgunScope || view == CameraView.FirstPerson) &&
                           module != null && IsRailgunScoped(inventory);
            if (lockView && !_locked) Lock(view, module);
            else if (!lockView && _locked) Unlock(view);
            VrCamera.IgnoreHeadRotation = _locked;
        }

        /// <summary>Turns the camera to where your head looks through it now, then stops adding your head's turn.</summary>
        private void Lock(CameraView view, FirstPersonCameraModule module)
        {
            var camera = Quaternion.Euler(GetPitch(view), module.Yaw, 0f);
            var forward = camera * HeadInCamera() * Vector3.forward;
            if (new Vector2(forward.x, forward.z).sqrMagnitude >= 1e-4f)
                module.SetYaw(module.Yaw + Mathf.DeltaAngle(module.Yaw, YawOf(forward)));
            SetPitch(view, -TiltOf(forward));
            _locked = true;
            if (Settings.DiagnosticsEnabled.Value)
                Plugin.Log.LogInfo($"Motion camera: railgun scope view locked to the aim (yaw {module.Yaw:0.0}°, pitch {GetPitch(view):0.0}°).");
        }

        /// <summary>
        /// Turns the camera that shows next so that, with your head's turn added again, the view stays where it
        /// was. Solved exactly rather than by subtracting your head's angles, so aiming again and again can't
        /// build up a turn.
        /// </summary>
        private void Unlock(CameraView view)
        {
            _locked = false;
            VrCamera.IgnoreHeadRotation = false;

            // The scope from third person has handed its yaw and pitch to the third-person camera once it closes.
            var inventory = GameManager.LocalPlayerInventory;
            var next = view == CameraView.RailgunScope && (inventory == null || !inventory.IsInFirstPersonAim)
                ? CameraView.ThirdPerson
                : view;
            float yaw;
            OrbitCameraModule orbit = null;
            FirstPersonCameraModule module = null;
            if (next == CameraView.ThirdPerson)
            {
                if (!CameraModuleController.TryGetOrbitModule(out orbit) || orbit == null) return;
                yaw = orbit.Yaw;
            }
            else
            {
                if (!CameraModuleController.TryGetFirstPersonModule(out module) || module == null) return;
                yaw = module.Yaw;
            }

            // Where the view looks now, and the camera that makes your head look there once its turn is added.
            var pitch = GetPitch(next);
            var looking = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
            var head = HeadInCamera() * Vector3.forward;
            if (!SolveCamera(head, looking, pitch - TiltOf(head), out var newPitch, out var newYaw)) return;

            newYaw = yaw + Mathf.DeltaAngle(yaw, newYaw);
            if (orbit != null) orbit.SetYaw(newYaw);
            else module.SetYaw(newYaw);
            SetPitch(next, newPitch);
            if (Settings.DiagnosticsEnabled.Value)
                Plugin.Log.LogInfo($"Motion camera: railgun scope view unlocked; camera turned {Mathf.DeltaAngle(yaw, newYaw):+0.0;-0.0;0}° and " +
                                   $"{newPitch - pitch:+0.0;-0.0;0}° so your head's turn doesn't move the view.");
        }

        /// <summary>
        /// The camera pitch and yaw (no roll) with <c>Euler(pitch, yaw, 0) * head == view</c>: the camera through
        /// which your head, facing <paramref name="head"/> relative to the camera, looks along
        /// <paramref name="view"/>. Of the two pitches that do, the one nearer <paramref name="guessPitch"/>. If your
        /// head is tilted so far that no level camera can, the nearest pitch.
        /// </summary>
        private static bool SolveCamera(Vector3 head, Vector3 view, float guessPitch, out float pitch, out float yaw)
        {
            pitch = guessPitch;
            yaw = 0f;
            // Euler(pitch, 0, 0) turns (y, z) to (y cos p - z sin p, y sin p + z cos p): the height is r cos(p - phi).
            var r = Mathf.Sqrt(head.y * head.y + head.z * head.z);
            if (r < 1e-4f || new Vector2(view.x, view.z).sqrMagnitude < 1e-6f) return false;

            var phi = Mathf.Atan2(-head.z, head.y) * Mathf.Rad2Deg;
            var offset = Mathf.Acos(Mathf.Clamp(view.y / r, -1f, 1f)) * Mathf.Rad2Deg;
            var first = phi + offset;
            var second = phi - offset;
            pitch = Mathf.Abs(Mathf.DeltaAngle(guessPitch, first)) <= Mathf.Abs(Mathf.DeltaAngle(guessPitch, second)) ? first : second;
            pitch = guessPitch + Mathf.DeltaAngle(guessPitch, pitch);

            var turned = Quaternion.Euler(pitch, 0f, 0f) * head;
            if (new Vector2(turned.x, turned.z).sqrMagnitude < 1e-6f) return false;
            yaw = YawOf(view) - YawOf(turned);
            return true;
        }

        /// <summary>Your head's rotation relative to the game camera, as UUVR applies it (its recentre rotation, then the headset's).</summary>
        private static Quaternion HeadInCamera()
        {
#pragma warning disable CS0618 // The same head tracking UUVR's cameras follow.
            return VrCameraOffset.RecenterRotation * InputTracking.GetLocalRotation(XRNode.CenterEye);
#pragma warning restore CS0618
        }

        private static float YawOf(Vector3 forward) => Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

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
                    // Aiming the railgun in third person switches to its scope: leave the third-person camera
                    // alone meanwhile, since the game copies its pitch and yaw into the scope.
                    var player = GameManager.LocalPlayerInfo;
                    if (player != null && player.NetworkedEquippedItem == ItemType.Railgun) return CameraView.None;
                    return CameraView.ThirdPerson;
                case CameraModuleType.FirstPerson:
                    if (!CameraModuleController.TryGetFirstPersonModule(out firstPerson) || firstPerson == null)
                        return CameraView.None;
                    // SBGVR's first person (its railgun scope included), not in a golf cart, where it faces the
                    // cart's front.
                    if (View.FirstPersonView.Shows(firstPerson))
                        return View.FirstPersonView.InGolfCart() ? CameraView.None : CameraView.FirstPerson;
                    // The railgun's scope from third person.
                    if (!View.FirstPersonView.Owns && inventory.IsInFirstPersonAim) return CameraView.RailgunScope;
                    return CameraView.None;
                default:
                    return CameraView.None;
            }
        }

        /// <summary>
        /// Whether the item is being used: its shot or other use (<c>ItemUseType.Regular</c> or <c>Alt</c>). Not
        /// while the railgun charges (<c>Charging</c>, then <c>FullyCharged</c> until you let go), which is still
        /// aiming: its shot is taken from the camera when it switches to <c>Regular</c>. The railgun is the only
        /// item that charges.
        /// </summary>
        private static bool IsFiring(PlayerInventory inventory)
        {
            var use = inventory.CurrentItemUse;
            return use == ItemUseType.Regular || use == ItemUseType.Alt;
        }

        private static float GetPitch(CameraView view)
        {
            if (view == CameraView.FirstPerson) return FirstPersonPitch;
            if (view == CameraView.RailgunScope)
                return CameraModuleController.TryGetFirstPersonModule(out var scope) && scope != null ? scope.Pitch : 0f;
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

            if (view == CameraView.RailgunScope)
            {
                if (CameraModuleController.TryGetFirstPersonModule(out var scope) && scope != null) scope.SetPitch(pitch);
                return;
            }

            if (CameraModuleController.TryGetOrbitModule(out var orbit) && orbit != null) orbit.SetPitch(pitch);
        }

        /// <summary>
        /// Whether the kept pitch's camera is showing and free to ease back: not during a golf club, rocket club or
        /// Jumbo Burger swing.
        /// </summary>
        private static bool CanEase(CameraView view)
        {
            if (!VrState.IsRunning) return false;

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
                case CameraView.RailgunScope:
                    var inventory = GameManager.LocalPlayerInventory;
                    return CameraModuleController.CurrentModuleType == CameraModuleType.FirstPerson &&
                           !View.FirstPersonView.Owns && inventory != null && inventory.IsInFirstPersonAim;
                default:
                    return false;
            }
        }

        /// <summary>Eases the kept camera's pitch back to the kept pitch, ending on it exactly.</summary>
        private void UpdateRestore()
        {
            if (!_restoring) return;

            // Wait for a camera transition to finish, then see which camera is showing.
            if (CameraModuleController.IsTransitioning) return;

            ResolveKeptView();
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

        /// <summary>
        /// Leaving the railgun's scope, the game copies the scope's pitch and yaw into the third-person camera: once
        /// that camera is back, it's the one to ease back to the pitch from before the scope.
        /// </summary>
        private void ResolveKeptView()
        {
            if (_keptView == CameraView.RailgunScope && !CameraModuleController.IsTransitioning &&
                CameraModuleController.CurrentModuleType == CameraModuleType.Orbit)
                _keptView = CameraView.ThirdPerson;
        }

        /// <summary>Sets the kept pitch at once and forgets it, so no tilt is left behind.</summary>
        private void FinishRestoreNow()
        {
            if (_keptView != CameraView.None && (_restoring || _view == CameraView.None))
            {
                SetPitch(_keptView, _keptPitch);
                // A railgun scope that has closed already handed its pitch to the third-person camera.
                var inventory = GameManager.LocalPlayerInventory;
                if (_keptView == CameraView.RailgunScope && (inventory == null || !inventory.IsInFirstPersonAim))
                    SetPitch(CameraView.ThirdPerson, _keptPitch);
            }

            if (_keptView == CameraView.FirstPerson) FirstPersonPitch = 0f;
            _restoring = false;
            _keptView = CameraView.None;
        }

        private void StopSteering()
        {
            if (_locked) Unlock(_view);
            VrCamera.IgnoreHeadRotation = false;
            if (_view == CameraView.None) return;
            if (Settings.DiagnosticsEnabled.Value)
                Plugin.Log.LogInfo($"Motion camera: turned the {(_view == CameraView.FirstPerson ? "first-person" : _view == CameraView.RailgunScope ? "railgun scope's" : "third-person")} " +
                                   $"camera {_appliedYaw:+0.0;-0.0;0}° while aiming an item; tilt eases back to {_keptPitch:0.0}°.");
            _view = CameraView.None;
            _appliedYaw = 0f;
            _appliedTilt = 0f;
            _wasUsing = false;
            _steering = false;
            if (_keptView != CameraView.None) _restoring = true;
        }

        /// <summary>
        /// A One Euro filter (Casiez, Roussel and Vogel, CHI 2012) for one angle in degrees: a low-pass filter whose
        /// cutoff rises with the angle's speed. Held nearly still, the cutoff is <see cref="MinCutoff"/>, which takes
        /// out hand tremor (around 8 to 12 Hz); moving, it rises by <see cref="Beta"/> per degree a second, so quick
        /// turns follow the hand closely. It passes a steady angle through unchanged, so the turn stays 1:1.
        /// </summary>
        private struct OneEuroFilter
        {
            /// <summary>Cutoff while the hand is still, in hertz.</summary>
            private const float MinCutoff = 1f;

            /// <summary>Extra cutoff per degree a second of movement, in hertz.</summary>
            private const float Beta = 0.04f;

            /// <summary>Cutoff for the speed estimate itself, in hertz.</summary>
            private const float SpeedCutoff = 1f;

            private bool _primed;
            private float _value;
            private float _speed;

            internal void Reset() => _primed = false;

            internal float Filter(float value, float dt)
            {
                if (!_primed || dt <= 0f)
                {
                    _primed = true;
                    _value = value;
                    _speed = 0f;
                    return value;
                }

                _speed += Alpha(SpeedCutoff, dt) * ((value - _value) / dt - _speed);
                _value += Alpha(MinCutoff + Beta * Mathf.Abs(_speed), dt) * (value - _value);
                return _value;
            }

            private static float Alpha(float cutoff, float dt)
            {
                var r = 2f * Mathf.PI * cutoff * dt;
                return r / (r + 1f);
            }
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
