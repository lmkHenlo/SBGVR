// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Uuvr;
using Uuvr.VrCamera;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;

namespace SBGVR.Controls
{
    internal enum ControllerHand
    {
        Right,
        Left,
    }

    /// <summary>
    /// VR Controls.ControllerAiming: while you aim or use an item, weapons aim where your dominant hand's
    /// controller points instead of along the game camera.
    /// </summary>
    /// <remarks>
    /// The game aims every weapon from the game camera: its position and forward, read through
    /// <c>GameManager.Camera.transform</c>. <see cref="ControllerAimPatches"/> sends those reads in the aiming
    /// code through <see cref="TransformOf"/>, which hands back a stand-in transform placed at the controller's
    /// pointing ray instead. The controller's pose is tracked relative to your head, so the ray starts where
    /// your hand is in relation to the view you see: UUVR's head-tracked camera, with recentring and its
    /// camera offset included. Everything after that (raycasts, aim assist, the item's own limits, what other
    /// players see) is the game's own code, unchanged.
    /// </remarks>
    internal static class ControllerAim
    {
        private static readonly InputFeatureUsage<Vector3> PointerPosition = new InputFeatureUsage<Vector3>("PointerPosition");
        private static readonly InputFeatureUsage<Quaternion> PointerRotation = new InputFeatureUsage<Quaternion>("PointerRotation");
        private static readonly List<XRInputDevice> Found = new List<XRInputDevice>();

        private static XRInputDevice _device;
        private static ControllerHand _deviceHand;
        private static float _nextSearch;
        private static Transform _standIn;
        private static bool _loggedPose;

        /// <summary>Set while the game picks the rocket launcher's lock-on target (shared with golf swings).</summary>
        internal static bool PickingRocketLauncherTarget;

        /// <summary>
        /// The transform the aiming code should read for <paramref name="component"/>: the controller's stand-in
        /// when it's the game camera and controller aiming applies right now, otherwise its own transform.
        /// </summary>
        internal static Transform TransformOf(Component component)
        {
            if (component != null && ReferenceEquals(component, GameManager.Camera) && TryGetAimRay(out var origin, out var direction))
            {
                var standIn = StandIn();
                standIn.SetPositionAndRotation(origin, Quaternion.LookRotation(direction, Up(direction)));
                return standIn;
            }

            return component.transform;
        }

        /// <summary><see cref="TransformOf"/>, only while the game is picking the rocket launcher's target.</summary>
        internal static Transform TransformOfForRocketLauncher(Component component) =>
            PickingRocketLauncherTarget ? TransformOf(component) : component.transform;

        /// <summary>
        /// The ray weapons aim along, in world space, while controller aiming applies: VR is running, the
        /// controllers are what the player last used (<see cref="MotionControls.InUse"/>), the local player is
        /// aiming or using an item, and the dominant hand's controller is tracked.
        /// </summary>
        internal static bool TryGetAimRay(out Vector3 origin, out Vector3 direction)
        {
            origin = default;
            direction = default;

            if (!VrState.IsRunning || !Settings.MotionControls.Value || !Settings.ControllerAiming.Value) return false;

            // Only while the player is playing with the controllers, not a gamepad, keyboard or mouse.
            if (!MotionControls.InUse) return false;

            var inventory = GameManager.LocalPlayerInventory;
            if (inventory == null || !(inventory.IsAimingItem || inventory.IsUsingItemAtAll)) return false;

            var camera = GameManager.Camera;
            if (camera == null || !TryGetTrackingFrame(camera, out var framePosition, out var frameRotation)) return false;
            if (!TryGetControllerPose(out var handPosition, out var handRotation)) return false;

#pragma warning disable CS0618 // The same head tracking UUVR's cameras follow.
            var headPosition = InputTracking.GetLocalPosition(XRNode.CenterEye);
#pragma warning restore CS0618

            var pitch = Quaternion.Euler(Settings.ControllerAimPitch.Value, 0f, 0f);
            origin = framePosition + frameRotation * (handPosition - headPosition);
            direction = frameRotation * handRotation * pitch * Vector3.forward;
            return direction.sqrMagnitude > 0.5f;
        }

        /// <summary>
        /// Where UUVR's tracking space sits in the world: the point and rotation the headset's tracked pose is
        /// applied under, so a tracked pose relative to the head lands in the world as you see it.
        /// </summary>
        private static bool TryGetTrackingFrame(Camera camera, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = default;

            if (ModConfiguration.Instance.CameraTracking.Value == ModConfiguration.CameraTrackingMode.Absolute)
            {
                // The game camera itself turns with the headset.
#pragma warning disable CS0618
                var head = InputTracking.GetLocalRotation(XRNode.CenterEye);
#pragma warning restore CS0618
                position = camera.transform.position;
                rotation = camera.transform.rotation * Quaternion.Inverse(head);
                return true;
            }

            // Every other mode tracks the head under UUVR's VrCameraOffset, a child of the game camera.
            var offset = camera.GetComponentInChildren<VrCameraOffset>(true);
            if (offset == null) return false;
            position = offset.transform.position;
            rotation = offset.transform.rotation;
            return true;
        }

        /// <summary>
        /// The dominant hand controller's pointing pose in tracking space (the room, in metres), independent of
        /// the game camera. Also used by the motion camera, which measures how the hand moves while you aim.
        /// </summary>
        internal static bool TryGetControllerPose(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = default;

            // DominantHand can be changed while the game runs (with a config editor): look for the other hand then.
            var hand = Settings.DominantHand.Value;
            if (_device.isValid && hand != _deviceHand)
            {
                _device = default;
                _nextSearch = 0f;
            }

            if (!_device.isValid)
            {
                if (Time.unscaledTime < _nextSearch) return false;
                _nextSearch = Time.unscaledTime + 1f;

                var side = hand == ControllerHand.Left ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right;
                Found.Clear();
                InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller | side, Found);
                if (Found.Count == 0) return false;
                _device = Found[0];
                _deviceHand = hand;
                _loggedPose = false;
            }

            if (_device.TryGetFeatureValue(XRCommonUsages.isTracked, out var tracked) && !tracked) return false;

            // The pointing pose (OpenXR's aim pose) points where the controller does; the grip pose, which
            // every runtime reports, is the fallback.
            var usesPointer = _device.TryGetFeatureValue(PointerPosition, out position) &&
                              _device.TryGetFeatureValue(PointerRotation, out rotation);
            if (!usesPointer &&
                !(_device.TryGetFeatureValue(XRCommonUsages.devicePosition, out position) &&
                  _device.TryGetFeatureValue(XRCommonUsages.deviceRotation, out rotation)))
            {
                return false;
            }

            if (!_loggedPose)
            {
                _loggedPose = true;
                Plugin.Log.LogInfo($"Controller aiming: aiming with the {Settings.DominantHand.Value.ToString().ToLowerInvariant()} " +
                                   $"controller (\"{_device.name}\"), using its {(usesPointer ? "pointing" : "grip")} pose.");
            }

            return rotation != default;
        }

        private static Vector3 Up(Vector3 direction) =>
            Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.999f ? Vector3.forward : Vector3.up;

        private static Transform StandIn()
        {
            if (_standIn != null) return _standIn;

            var standIn = new GameObject("SBGVR Controller Aim");
            standIn.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(standIn);
            _standIn = standIn.transform;
            return _standIn;
        }
    }
}
