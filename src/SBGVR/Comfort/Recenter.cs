// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using Uuvr.VrCamera;

namespace SBGVR.Comfort
{
    /// <summary>
    /// VR Comfort.RecenterKey: turns the view so the direction you're facing becomes straight ahead, by
    /// cancelling the headset's current yaw under every VR camera. Pitch and roll are left alone, so the
    /// horizon stays level.
    /// </summary>
    internal sealed class Recenter : MonoBehaviour
    {
        private void OnEnable()
        {
            VrState.Changed += OnVrChanged;
        }

        private void OnDisable()
        {
            VrState.Changed -= OnVrChanged;
        }

        private static void OnVrChanged(bool running)
        {
            // A fresh VR session starts from the headset's own forward.
            if (!running) VrCameraOffset.RecenterRotation = Quaternion.identity;
        }

        private void Update()
        {
            if (!VrState.IsRunning) return;

            var key = Settings.RecenterKey.Value;
            if (key == Key.None) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            var control = keyboard[key];
            if (control == null || !control.wasPressedThisFrame) return;

#pragma warning disable CS0618 // The same head rotation UUVR's cameras follow.
            var head = InputTracking.GetLocalRotation(XRNode.CenterEye);
#pragma warning restore CS0618
            var yaw = head.eulerAngles.y;
            VrCameraOffset.RecenterRotation = Quaternion.Euler(0f, -yaw, 0f);
            Plugin.Log.LogInfo($"View recentred (turned {Mathf.DeltaAngle(0f, -yaw):0} degrees).");
        }
    }
}
