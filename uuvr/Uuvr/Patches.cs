using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.XR;

namespace Uuvr;

[HarmonyPatch]
public static class Patches
{
    public static Vector3 hehe = Vector3.one * 0.5f;

    // SBGVR: the field of view each camera was last asked for, by camera instance ID, kept even while the
    // change itself is blocked, so a plugin can apply the game's field of view in VR its own way.
    private static readonly Dictionary<int, float> RequestedFieldOfView = new();
    private static readonly List<XRDisplaySubsystem> Displays = new();
    private static int _displayCheckFrame = -1;
    private static bool _displayRunning;

    /// <summary>
    /// The field of view the game last set on <paramref name="camera"/>, whether or not it was applied.
    /// </summary>
    public static bool TryGetRequestedFieldOfView(Camera camera, out float fieldOfView)
    {
        fieldOfView = 0f;
        return camera != null && RequestedFieldOfView.TryGetValue(camera.GetInstanceID(), out fieldOfView);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Camera), "set_fieldOfView")]
    // Unity already prevents this, but it also nags you constantly about it.
    // Some games try to change the FOV every frame, and all those logs can reduce performance.
    // SBGVR: only blocked while a VR display is active, so the game's FOV still works on a flat screen
    // (after F3, for one). The requested value is remembered either way.
    private static bool PreventChangingFov(Camera __instance, float value)
    {
        if (__instance != null) RequestedFieldOfView[__instance.GetInstanceID()] = value;
        return !IsDisplayRunning();
    }

    // SBGVR: whether a VR display is being rendered to, checked once a frame.
    private static bool IsDisplayRunning()
    {
        if (_displayCheckFrame == Time.frameCount) return _displayRunning;
        _displayCheckFrame = Time.frameCount;
        SubsystemManager.GetSubsystems(Displays);
        _displayRunning = false;
        foreach (var display in Displays)
        {
            if (display.running)
            {
                _displayRunning = true;
                break;
            }
        }

        return _displayRunning;
    }
}
