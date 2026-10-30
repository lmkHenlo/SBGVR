// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace SBGVR.View
{
    /// <summary>
    /// The railgun scope's zoom in VR, by the same magnification as on a flat screen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game zooms the scope by narrowing the first-person camera's field of view: it starts at
    /// <c>ItemSettings.RailgunZoomInitialFov</c> and eases to <c>RailgunZoomFinalFov</c>, from your Field of View
    /// setting. A headset ignores <c>Camera.fieldOfView</c> (each eye's view comes from the headset), so in VR
    /// the scope didn't zoom at all. Here each eye's projection is narrowed instead, in
    /// <c>XRSystem.BuildView</c>, by <see cref="Factor"/>: tan(setting / 2) / tan(scope / 2), the ratio the flat
    /// screen magnifies by. The scope's field of view is read from the game every frame, so the zoom follows
    /// the game's own ease-in, and it ends the moment the scope closes. The headset still shows the image
    /// across its full view, which is what magnifies it.
    /// </para>
    /// <para>
    /// Everything drawn for VR in the world would be magnified with it, so while zoomed:
    /// <list type="bullet">
    /// <item>World-space markers, crosshairs and the aim line are drawn <see cref="HudScale"/> times their size
    /// (<see cref="Hud.WorldMarker"/>, <see cref="Hud.AimLine"/>), so they look the same size as before.</item>
    /// <item>UUVR's floating screen, which follows your head on your view's axis, is moved <see cref="Factor"/>
    /// times further away at the same size. Magnified, that looks exactly as before to both eyes (same place,
    /// same size, same depth), rather than looking several times closer. It's drawn over the scenery that may
    /// then be in front of it, through a copy of its material that skips the depth test.</item>
    /// </list>
    /// </para>
    /// </remarks>
    internal sealed class RailgunScopeZoom : MonoBehaviour
    {
        private const float MaxFactor = 20f;
        private const string ZTestProperty = "unity_GUIZTestMode";

        private static int _factorFrame = -1;
        private static float _factor = 1f;
        private static bool _wasZoomed;

        private Transform _screen;
        private Renderer _screenRenderer;
        private Material _screenMaterial;
        private Material _onTopMaterial;
        private Vector3 _screenPosition;
        private bool _screenMoved;

        /// <summary>How much the railgun scope magnifies this frame: 1 when it isn't up or VR isn't running.</summary>
        internal static float Factor
        {
            get
            {
                if (_factorFrame == Time.frameCount) return _factor;
                _factorFrame = Time.frameCount;
                _factor = ComputeFactor();
                return _factor;
            }
        }

        /// <summary>What to scale world-space HUD by so the zoom doesn't magnify it: 1 / <see cref="Factor"/>.</summary>
        internal static float HudScale => 1f / Factor;

        private static float ComputeFactor()
        {
            var zoomed = TryGetScopeFov(out var setting, out var scope);
            var factor = zoomed
                ? Mathf.Clamp(Mathf.Tan(setting * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(scope * 0.5f * Mathf.Deg2Rad), 1f, MaxFactor)
                : 1f;
            if (zoomed != _wasZoomed && zoomed) LogZoom(setting);
            _wasZoomed = zoomed;
            return factor;
        }

        /// <summary>Your Field of View setting and the scope's current field of view, while the scope is up in VR.</summary>
        private static bool TryGetScopeFov(out float setting, out float scope)
        {
            setting = scope = 0f;
            if (!VrState.IsRunning) return false;

            var inventory = GameManager.LocalPlayerInventory;
            if (inventory == null || !inventory.IsInFirstPersonAim) return false;
            if (!CameraModuleController.TryGetFirstPersonModule(out var module) || module == null) return false;

            var all = GameSettings.All;
            if (all?.Graphics == null) return false;
            setting = all.Graphics.FieldOfView;
            scope = module.FieldOfView;
            return setting > 1f && setting < 179f && scope > 0.1f && scope < 179f;
        }

        private static void LogZoom(float setting)
        {
            if (!Settings.DiagnosticsEnabled.Value) return;
            var items = GameManager.ItemSettings;
            if (items == null) return;

            float Magnification(float fov) => Mathf.Tan(setting * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            Plugin.Log.LogInfo($"[diag] Railgun scope: field of view {setting:0.#} (setting), {items.RailgunZoomInitialFov:0.#} " +
                               $"to {items.RailgunZoomFinalFov:0.#} over {items.RailgunZoomFovChangeDuration:0.##} s (scope): " +
                               $"magnifies {Magnification(items.RailgunZoomInitialFov):0.00}x to {Magnification(items.RailgunZoomFinalFov):0.00}x.");
        }

        private void LateUpdate()
        {
            var factor = Factor;
            if (factor > 1f && FindScreen())
            {
                if (!_screenMoved)
                {
                    _screenPosition = _screen.localPosition;
                    _screenMoved = true;
                }

                _screen.localPosition = new Vector3(_screenPosition.x, _screenPosition.y, _screenPosition.z * factor);
                DrawScreenOnTop(true);
            }
            else
            {
                RestoreScreen();
            }
        }

        private void OnDisable()
        {
            RestoreScreen();
        }

        private void RestoreScreen()
        {
            if (!_screenMoved) return;
            _screenMoved = false;
            if (_screen != null) _screen.localPosition = _screenPosition;
            DrawScreenOnTop(false);
        }

        /// <summary>UUVR's floating screen: the quad under its VrUiManager's container, which follows your head.</summary>
        private bool FindScreen()
        {
            if (_screen != null) return true;
            var manager = FindAnyObjectByType<Uuvr.VrUi.VrUiManager>();
            _screen = manager != null ? manager.transform.Find("VrUiContainer/VrUiQuad") : null;
            _screenRenderer = _screen != null ? _screen.GetComponent<Renderer>() : null;
            if (_screen != null && _screenRenderer != null) return true;
            _screen = null;
            return false;
        }

        private void DrawScreenOnTop(bool onTop)
        {
            if (_screenRenderer == null) return;
            if (onTop)
            {
                var current = _screenRenderer.sharedMaterial;
                if (current == null || current == _onTopMaterial) return;
                // UUVR may have changed its material (shader, queue) since last time.
                if (_onTopMaterial != null) Destroy(_onTopMaterial);
                _screenMaterial = current;
                _onTopMaterial = new Material(current) { name = current.name + " (SBGVR scope zoom)" };
                _onTopMaterial.SetInt(ZTestProperty, (int)CompareFunction.Always);
                _screenRenderer.sharedMaterial = _onTopMaterial;
            }
            else if (_screenMaterial != null && _screenRenderer.sharedMaterial == _onTopMaterial)
            {
                _screenRenderer.sharedMaterial = _screenMaterial;
            }
        }

        private void OnDestroy()
        {
            if (_onTopMaterial != null) Destroy(_onTopMaterial);
        }
    }

    /// <summary>
    /// Narrows each eye's projection by <see cref="RailgunScopeZoom.Factor"/> while the railgun scope is up.
    /// <c>XRSystem.BuildView</c> builds each eye's view from the headset's projection; XRView is internal, so
    /// the projection is passed through <see cref="Zoom"/> as it's read rather than changed afterwards.
    /// </summary>
    [HarmonyPatch]
    internal static class RailgunScopeZoomPatches
    {
        private static readonly FieldInfo Projection =
            AccessTools.Field(typeof(XRDisplaySubsystem.XRRenderParameter), nameof(XRDisplaySubsystem.XRRenderParameter.projection));

        private static MethodBase TargetMethod() => AccessTools.Method(typeof(XRSystem), "BuildView");

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var found = 0;
            foreach (var instruction in instructions)
            {
                yield return instruction;
                if (!instruction.LoadsField(Projection)) continue;
                found++;
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RailgunScopeZoomPatches), nameof(Zoom)));
            }

            if (found != 1) throw new InvalidOperationException($"XRSystem.BuildView reads the eye's projection {found} times, expected once");
        }

        /// <summary>
        /// Scaling both focal lengths narrows the view about the eye's own axis; the off-centre terms (m02, m12)
        /// stay, so the eye's centre and its view's asymmetry are unchanged.
        /// </summary>
        private static Matrix4x4 Zoom(Matrix4x4 projection)
        {
            var factor = RailgunScopeZoom.Factor;
            if (factor <= 1f) return projection;
            projection.m00 *= factor;
            projection.m11 *= factor;
            return projection;
        }
    }
}
