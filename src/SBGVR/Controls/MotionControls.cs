// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;

namespace SBGVR.Controls
{
    /// <summary>
    /// VR Controls.MotionControls: while VR runs, reads both VR controllers through Unity's XR input and feeds
    /// them into a virtual gamepad. The game already plays fully on a gamepad, so every action, menu and rebind
    /// works without changing the game's own input code.
    /// </summary>
    /// <remarks>
    /// The controller state is sent just before each Input System update, so it reaches the game in the same
    /// frame. Nothing is sent until a controller is actually used, and nothing more while the controllers are
    /// left alone, so a real gamepad, keyboard and mouse keep working alongside.
    /// </remarks>
    internal sealed class MotionControls : MonoBehaviour
    {
        private const string GamepadName = "SBGVR Motion Controls";

        /// <summary>Stick and trigger values below this are treated as at rest, so drift doesn't send input.</summary>
        private const float StickDeadZone = 0.12f;
        private const float TriggerDeadZone = 0.05f;

        /// <summary>How far the right stick goes before it counts as a D-pad press while the left grip is held.</summary>
        private const float DPadThreshold = 0.6f;

        /// <summary>A left grip released within this time, without using its D-pad layer, is a tap: LB.</summary>
        private const float GripTapTime = 0.6f;

        /// <summary>How long a left grip tap holds LB down, so the game sees the press.</summary>
        private const float GripTapPressTime = 0.06f;

        private readonly Hand _left = new Hand(InputDeviceCharacteristics.Left, "Left");
        private readonly Hand _right = new Hand(InputDeviceCharacteristics.Right, "Right");

        private Gamepad _gamepad;
        private GamepadState _lastSent;
        private bool _hasSent;

        private bool _leftGripHeld;
        private float _leftGripPressedAt;
        private bool _leftGripLayerUsed;
        private float _leftShoulderPulseUntil = -1f;

        private void OnEnable()
        {
            InputSystem.onBeforeUpdate += BeforeInputUpdate;
            VrState.Changed += OnVrChanged;
        }

        private void OnDisable()
        {
            InputSystem.onBeforeUpdate -= BeforeInputUpdate;
            VrState.Changed -= OnVrChanged;
            RemoveGamepad();
        }

        private void OnVrChanged(bool running)
        {
            if (!running) RemoveGamepad();
        }

        private void BeforeInputUpdate()
        {
            var updateType = InputState.currentUpdateType;
            if (updateType == InputUpdateType.BeforeRender || updateType == InputUpdateType.Editor) return;

            if (!VrState.IsRunning)
            {
                RemoveGamepad();
                return;
            }

            _left.Refresh();
            _right.Refresh();

            var state = _left.IsValid || _right.IsValid ? ReadState() : default;
            if (_hasSent ? SameState(state, _lastSent) : IsAtRest(state)) return;

            if (!EnsureGamepad()) return;
            InputSystem.QueueStateEvent(_gamepad, state);
            _lastSent = state;
            _hasSent = true;
        }

        private GamepadState ReadState()
        {
            var state = new GamepadState
            {
                leftStick = Stick(_left.Axis(XRCommonUsages.primary2DAxis)),
                leftTrigger = Trigger(_left.Value(XRCommonUsages.trigger)),
                rightTrigger = Trigger(_right.Value(XRCommonUsages.trigger)),
            };
            var rightStick = Stick(_right.Axis(XRCommonUsages.primary2DAxis));

            var a = _right.Button(XRCommonUsages.primaryButton);
            var b = _right.Button(XRCommonUsages.secondaryButton);
            var x = _left.Button(XRCommonUsages.primaryButton);
            var y = _left.Button(XRCommonUsages.secondaryButton);
            var leftPadDirection = Vector2.zero;

            // Controllers without face buttons (HTC Vive, Windows Mixed Reality): click the touchpad's edges
            // instead. Right touchpad = A, B, X, Y; left touchpad = D-pad.
            if (!_right.HasFaceButtons && _right.PadClick(out var rightPad))
            {
                switch (Direction(rightPad))
                {
                    case 0: y = true; break;
                    case 1: b = true; break;
                    case 2: a = true; break;
                    case 3: x = true; break;
                }
            }
            if (!_left.HasFaceButtons && _left.PadClick(out var leftPad))
                leftPadDirection = leftPad;

            // Left grip: a tap is LB; held, it turns the right stick into a D-pad and A and B into pause and
            // scoreboard.
            var leftGrip = _left.Button(XRCommonUsages.gripButton);
            var now = Time.unscaledTime;
            if (leftGrip && !_leftGripHeld)
            {
                _leftGripPressedAt = now;
                _leftGripLayerUsed = false;
            }
            else if (!leftGrip && _leftGripHeld && !_leftGripLayerUsed && now - _leftGripPressedAt <= GripTapTime)
            {
                _leftShoulderPulseUntil = now + GripTapPressTime;
            }
            _leftGripHeld = leftGrip;

            if (leftGrip)
            {
                var dpad = rightStick.magnitude >= DPadThreshold ? Direction(rightStick) : -1;
                SetDPad(ref state, dpad);
                if (a) state = state.WithButton(GamepadButton.Start, true);
                if (b) state = state.WithButton(GamepadButton.Select, true);
                if (dpad >= 0 || a || b) _leftGripLayerUsed = true;
            }
            else
            {
                state.rightStick = rightStick;
                if (a) state = state.WithButton(GamepadButton.South, true);
                if (b) state = state.WithButton(GamepadButton.East, true);
                if (leftPadDirection != Vector2.zero) SetDPad(ref state, Direction(leftPadDirection));
            }

            if (x) state = state.WithButton(GamepadButton.West, true);
            if (y) state = state.WithButton(GamepadButton.North, true);
            if (_right.Button(XRCommonUsages.gripButton)) state = state.WithButton(GamepadButton.RightShoulder, true);
            if (now < _leftShoulderPulseUntil) state = state.WithButton(GamepadButton.LeftShoulder, true);
            if (_left.Button(XRCommonUsages.menuButton)) state = state.WithButton(GamepadButton.Start, true);

            // Stick clicks, on controllers whose primary axis is a thumbstick (a Vive's is its touchpad).
            if (_left.HasFaceButtons && _left.Button(XRCommonUsages.primary2DAxisClick))
                state = state.WithButton(GamepadButton.LeftStick, true);
            if (_right.HasFaceButtons && _right.Button(XRCommonUsages.primary2DAxisClick))
                state = state.WithButton(GamepadButton.RightStick, true);

            return state;
        }

        private bool EnsureGamepad()
        {
            if (_gamepad != null) return true;

            _gamepad = InputSystem.AddDevice<Gamepad>(GamepadName);
            if (_gamepad == null)
            {
                Plugin.Log.LogError("Motion controls: couldn't add the virtual gamepad.");
                enabled = false;
                return false;
            }

            Plugin.Log.LogInfo(_gamepad.enabled
                ? "Motion controls: VR controller input now goes to a virtual gamepad."
                : "Motion controls: the virtual gamepad was added but the game's input settings disabled it, " +
                  "so the controllers won't do anything.");
            return true;
        }

        private void RemoveGamepad()
        {
            _hasSent = false;
            _leftGripHeld = false;
            _leftShoulderPulseUntil = -1f;
            if (_gamepad == null) return;

            if (_gamepad.added) InputSystem.RemoveDevice(_gamepad);
            _gamepad = null;
            Plugin.Log.LogInfo("Motion controls: virtual gamepad removed.");
        }

        private static Vector2 Stick(Vector2 value)
        {
            var magnitude = value.magnitude;
            if (magnitude < StickDeadZone) return Vector2.zero;
            var scaled = Mathf.Min(1f, (magnitude - StickDeadZone) / (1f - StickDeadZone));
            return value / magnitude * scaled;
        }

        private static float Trigger(float value) => value < TriggerDeadZone ? 0f : Mathf.Min(1f, value);

        /// <summary>0 = up, 1 = right, 2 = down, 3 = left: whichever axis the vector leans on most.</summary>
        private static int Direction(Vector2 value)
        {
            if (Mathf.Abs(value.y) >= Mathf.Abs(value.x)) return value.y >= 0f ? 0 : 2;
            return value.x >= 0f ? 1 : 3;
        }

        private static void SetDPad(ref GamepadState state, int direction)
        {
            switch (direction)
            {
                case 0: state = state.WithButton(GamepadButton.DpadUp, true); break;
                case 1: state = state.WithButton(GamepadButton.DpadRight, true); break;
                case 2: state = state.WithButton(GamepadButton.DpadDown, true); break;
                case 3: state = state.WithButton(GamepadButton.DpadLeft, true); break;
            }
        }

        private static bool IsAtRest(GamepadState state) => SameState(state, default);

        private static bool SameState(GamepadState a, GamepadState b) =>
            a.buttons == b.buttons && a.leftStick == b.leftStick && a.rightStick == b.rightStick &&
            a.leftTrigger == b.leftTrigger && a.rightTrigger == b.rightTrigger;

        /// <summary>One VR controller, found through Unity's XR input, from OpenXR or OpenVR alike.</summary>
        private sealed class Hand
        {
            private const float SearchInterval = 1f;
            private static readonly List<XRInputDevice> Found = new List<XRInputDevice>();
            private static readonly List<InputFeatureUsage> Usages = new List<InputFeatureUsage>();

            private readonly InputDeviceCharacteristics _side;
            private readonly string _name;
            private XRInputDevice _device;
            private bool _wasValid;
            private float _nextSearch;
            private InputFeatureUsage<bool> _padClick;
            private InputFeatureUsage<Vector2> _padAxis;

            internal Hand(InputDeviceCharacteristics side, string name)
            {
                _side = side;
                _name = name;
            }

            internal bool IsValid => _device.isValid;

            /// <summary>Whether the controller has A/B or X/Y buttons (not a Vive wand or WMR controller).</summary>
            internal bool HasFaceButtons { get; private set; }

            internal void Refresh()
            {
                if (_device.isValid) return;

                if (_wasValid)
                {
                    _wasValid = false;
                    Plugin.Log.LogInfo($"Motion controls: {_name.ToLowerInvariant()} controller disconnected.");
                }

                if (Time.unscaledTime < _nextSearch) return;
                _nextSearch = Time.unscaledTime + SearchInterval;

                Found.Clear();
                InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller | _side, Found);
                if (Found.Count == 0) return;

                _device = Found[0];
                _wasValid = true;
                OnConnected();
            }

            private void OnConnected()
            {
                Usages.Clear();
                _device.TryGetFeatureUsages(Usages);
                var names = Usages.Select(u => u.name).ToList();

                HasFaceButtons = names.Contains(XRCommonUsages.primaryButton.name) &&
                                 names.Contains(XRCommonUsages.secondaryButton.name);

                // A WMR controller's touchpad is its secondary axis; a Vive wand's is its primary one.
                var hasSecondaryPad = names.Contains(XRCommonUsages.secondary2DAxisClick.name);
                _padClick = hasSecondaryPad ? XRCommonUsages.secondary2DAxisClick : XRCommonUsages.primary2DAxisClick;
                _padAxis = hasSecondaryPad ? XRCommonUsages.secondary2DAxis : XRCommonUsages.primary2DAxis;

                Plugin.Log.LogInfo(
                    $"Motion controls: {_name.ToLowerInvariant()} controller connected: \"{_device.name}\" by " +
                    $"\"{_device.manufacturer}\" ({_device.characteristics}). " +
                    (HasFaceButtons ? "" : "No face buttons, so touchpad clicks stand in for them. ") +
                    $"Inputs: {string.Join(", ", names)}.");
            }

            internal bool Button(InputFeatureUsage<bool> usage) =>
                _device.isValid && _device.TryGetFeatureValue(usage, out var value) && value;

            internal float Value(InputFeatureUsage<float> usage) =>
                _device.isValid && _device.TryGetFeatureValue(usage, out var value) ? value : 0f;

            internal Vector2 Axis(InputFeatureUsage<Vector2> usage) =>
                _device.isValid && _device.TryGetFeatureValue(usage, out var value) ? value : Vector2.zero;

            /// <summary>Whether the touchpad is clicked, and where on it.</summary>
            internal bool PadClick(out Vector2 position)
            {
                position = Axis(_padAxis);
                return Button(_padClick) && position != Vector2.zero;
            }
        }
    }
}
