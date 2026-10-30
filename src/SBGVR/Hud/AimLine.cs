// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using UnityEngine;
using UnityEngine.Rendering;

namespace SBGVR.Hud
{
    /// <summary>
    /// VR HUD.AimLine: while a weapon reticle is drawn in the world, a thin white line runs from the hand that
    /// holds the item to the reticle, so you can see where the shot goes from where it leaves. The railgun, thunderstorm
    /// and flash camera have no line.
    /// </summary>
    /// <remarks>
    /// The line is a world-space <see cref="LineRenderer"/> facing the viewer, so each eye draws it at its own
    /// depth, like the reticle. It's updated every frame after <see cref="WorldReticles"/> places the reticle,
    /// from the character's right-hand equipment holder to the middle of the crosshair as drawn, so it follows both as they
    /// move. Its width grows with distance from the camera, so it looks equally thin near and far instead of
    /// shrinking to a flickering sub-pixel line. Scenery between the two points covers it, as it does a shot.
    /// </remarks>
    [DefaultExecutionOrder(30001)]
    internal sealed class AimLine : MonoBehaviour
    {
        /// <summary>Line width per metre of distance from the camera: about 0.1 degrees.</summary>
        private const float WidthPerMetre = 0.0018f;

        private LineRenderer _line;

        private void Awake()
        {
            var lineObject = new GameObject("SBGVR Aim Line");
            lineObject.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(lineObject);

            _line = lineObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = 2;
            _line.alignment = LineAlignment.View;
            _line.numCapVertices = 2;
            _line.shadowCastingMode = ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.lightProbeUsage = LightProbeUsage.Off;
            _line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _line.allowOcclusionWhenDynamic = false;
            _line.startColor = Color.white;
            _line.endColor = Color.white;
            var material = new Material(Canvas.GetDefaultCanvasMaterial())
            {
                name = "SBGVR aim line",
                color = Color.white,
                renderQueue = (int)RenderQueue.Transparent,
            };
            // The UI shader's depth test comes from this property (a canvas sets it); test against the scene.
            material.SetInt("unity_GUIZTestMode", (int)CompareFunction.LessEqual);
            _line.sharedMaterial = material;
            _line.enabled = false;
        }

        private void OnDestroy()
        {
            if (_line != null) Destroy(_line.gameObject);
        }

        private void LateUpdate()
        {
            var start = Vector3.zero;
            var show = VrState.IsRunning && Settings.AimLine.Value && WorldReticles.AimShowing &&
                       TryGetStart(out start);
            var camera = show ? Fades.FullViewFade.ViewCamera() : null;
            if (camera == null)
            {
                _line.enabled = false;
                return;
            }

            var end = WorldReticles.AimPoint;
            var eye = camera.transform.position;
            _line.SetPosition(0, start);
            _line.SetPosition(1, end);
            // The railgun scope's zoom magnifies the world, and so the line; this cancels that out.
            var width = WidthPerMetre * View.RailgunScopeZoom.HudScale;
            _line.startWidth = width * Vector3.Distance(eye, start);
            _line.endWidth = width * Vector3.Distance(eye, end);
            _line.enabled = true;
        }

        /// <summary>Where the line leaves from: the hand holding the item.</summary>
        private static bool TryGetStart(out Vector3 start)
        {
            start = default;
            var player = GameManager.LocalPlayerInfo;
            if (player == null) return false;

            var hand = player.RightHandEquipmentSwitcher;
            start = hand != null ? hand.transform.position : player.transform.position + Vector3.up;
            return true;
        }
    }
}
