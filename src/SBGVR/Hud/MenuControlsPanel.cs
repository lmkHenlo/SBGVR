// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using Uuvr;

namespace SBGVR.Hud
{
    internal enum MenuControlsSide
    {
        Below,
        Left,
        Right,
    }

    /// <summary>
    /// VR HUD.MenuControls: shows the VR controller layout on its own panel beside UUVR's floating screen while
    /// you're in the main menu, so the controls are there as soon as the game starts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game's menus are left exactly as they are: the panel is a separate quad, not part of any canvas. It's
    /// placed like the floating screen (attached to the camera the headset looks through, at the screen's
    /// distance) but tilted down below it (or turned to one side), with its near edge a few degrees past the
    /// screen's edge, and it faces you. The angles come from UUVR's own screen settings (<c>VR UI Position</c>, <c>VR UI Scale</c>)
    /// and the game window's aspect ratio, so it stays clear of the screen whatever those are.
    /// </para>
    /// <para>
    /// It's drawn after everything else, the full-view fades included (render queue 5002, sorted with the floating
    /// screen), so the main menu's background cross-fades and scene fades leave it at full opacity, and scenery
    /// never cuts into it. It never overlaps the floating screen, so drawing after the screen changes nothing
    /// there; the fades still cover the rest of the view and the screen exactly as before.
    /// </para>
    /// <para>
    /// The image (<c>Assets/controller-layout.png</c>, embedded in the DLL) is rendered from
    /// <c>tools/controller-layout</c>. Its controller artwork is Meta's (see THIRD_PARTY_NOTICES.md).
    /// </para>
    /// </remarks>
    internal sealed class MenuControlsPanel : MonoBehaviour
    {
        private const string ImageResource = "SBGVR.controller-layout.png";

        /// <summary>
        /// After the full-view fade behind the screen (4999), the floating screen (5000) and the fade over the
        /// screen (5001), so no fade dims it.
        /// </summary>
        private const int RenderQueue = 5002;

        /// <summary>Panel width as a share of its distance: about 51° wide, so its smallest text stays readable.</summary>
        private const float WidthPerMetre = 0.95f;

        /// <summary>Gap between the floating screen's edge and the panel's near edge, in degrees.</summary>
        private const float GapDegrees = 4f;

        /// <summary>UUVR's floating screen is 1.8 m wide at VR UI Scale 1.</summary>
        private const float ScreenWidthPerScale = 1.8f;

        private GameObject _panel;
        private Material _material;
        private Texture2D _texture;
        private float _aspect = 1100f / 1440f;
        private bool _failed;
        private bool _logged;

        private void OnEnable()
        {
            Application.onBeforeRender += Place;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= Place;
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_panel != null) Destroy(_panel);
            if (_material != null) Destroy(_material);
            if (_texture != null) Destroy(_texture);
        }

        private void LateUpdate()
        {
            Place();
        }

        /// <summary>
        /// Shows the panel for as long as the main menu exists, while VR runs with motion controls: through its
        /// fade-in and fade-out too, so it stays fully visible until the menu is gone.
        /// </summary>
        private static bool ShouldShow()
        {
            return VrState.IsRunning && Settings.MenuControls.Value && Settings.MotionControls.Value &&
                   SingletonBehaviour<MainMenu>.HasInstance;
        }

        private void Place()
        {
            if (_failed) return;

            var camera = ShouldShow() ? Fades.FullViewFade.ViewCamera() : null;
            if (camera == null)
            {
                if (_panel != null && _panel.activeSelf) _panel.SetActive(false);
                return;
            }

            if (_panel == null && !Create()) return;

            var config = ModConfiguration.Instance;
            var screen = config.VrUiPosition.Value;
            var distance = Mathf.Max(0.3f, Mathf.Abs(screen.z));
            var screenHalfWidth = ScreenWidthPerScale * config.VrUiScale.Value / 2f;
            // UUVR's screen shows the game window, so it has the window's shape.
            var screenHalfHeight = screenHalfWidth * Screen.height / Mathf.Max(1, Screen.width);
            var width = WidthPerMetre * distance;
            var side = Settings.MenuControlsSide.Value;

            Quaternion localRotation;
            Vector3 offset;
            float angle;
            if (side == MenuControlsSide.Below)
            {
                // Tilted down below the screen's bottom edge, facing you.
                var edgeDegrees = Mathf.Atan2(screen.y - screenHalfHeight, distance) * Mathf.Rad2Deg;
                var halfDegrees = Mathf.Atan2(width * _aspect / 2f, distance) * Mathf.Rad2Deg;
                angle = -edgeDegrees + GapDegrees + halfDegrees;
                localRotation = Quaternion.Euler(angle, 0f, 0f);
                offset = new Vector3(screen.x, 0f, 0f);
            }
            else
            {
                // Turned to one side of the screen's left or right edge, facing you.
                var left = side == MenuControlsSide.Left;
                var edgeX = left ? screen.x - screenHalfWidth : screen.x + screenHalfWidth;
                var edgeDegrees = Mathf.Atan2(edgeX, distance) * Mathf.Rad2Deg;
                var halfDegrees = Mathf.Atan2(width / 2f, distance) * Mathf.Rad2Deg;
                angle = left ? edgeDegrees - GapDegrees - halfDegrees : edgeDegrees + GapDegrees + halfDegrees;
                localRotation = Quaternion.Euler(0f, angle, 0f);
                offset = new Vector3(0f, screen.y, 0f);
            }

            var localPosition = localRotation * new Vector3(0f, 0f, distance) + offset;

            var cameraTransform = camera.transform;
            var panel = _panel.transform;
            panel.SetPositionAndRotation(cameraTransform.TransformPoint(localPosition), cameraTransform.rotation * localRotation);
            panel.localScale = new Vector3(width, width * _aspect, 1f);
            _panel.layer = LayerHelper.GetVrUiLayer();
            if (!_panel.activeSelf) _panel.SetActive(true);

            if (_logged) return;
            _logged = true;
            Plugin.Log.LogInfo($"Menu controls: controller layout shown {(side == MenuControlsSide.Below ? "below" : side == MenuControlsSide.Left ? "left of" : "right of")} " +
                               $"the floating screen, {Mathf.Abs(angle):0}° from straight ahead, {width:0.00} m wide at {distance:0.00} m.");
        }

        private bool Create()
        {
            try
            {
                _texture = LoadImage();
                _aspect = (float)_texture.height / _texture.width;

                _panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _panel.name = "SBGVR Menu Controls";
                _panel.hideFlags = HideFlags.HideAndDontSave;
                DontDestroyOnLoad(_panel);
                var collider = _panel.GetComponent<Collider>();
                if (collider != null) Destroy(collider);

                var renderer = _panel.GetComponent<Renderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                Uuvr.VrUi.UiSorting.DrawLast(renderer);

                // The UI shader UUVR draws its floating screen with: unlit, transparent, culling off.
                _material = new Material(Canvas.GetDefaultCanvasMaterial())
                {
                    name = "SBGVR menu controls",
                    mainTexture = _texture,
                    renderQueue = RenderQueue,
                };
                _material.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
                renderer.sharedMaterial = _material;
                _panel.SetActive(false);
                return true;
            }
            catch (Exception exception)
            {
                _failed = true;
                Plugin.Log.LogError($"Menu controls: couldn't show the controller layout: {exception.GetBaseException().Message}");
                return false;
            }
        }

        private static Texture2D LoadImage()
        {
            byte[] bytes;
            using (var stream = typeof(MenuControlsPanel).Assembly.GetManifestResourceStream(ImageResource))
            {
                if (stream == null) throw new FileNotFoundException("The controller layout image is missing from SBGVR.dll.");
                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    bytes = memory.ToArray();
                }
            }

            // sRGB colour, with mipmaps so the text stays smooth at a distance.
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, false)
            {
                name = "SBGVR controller layout",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8,
            };
            if (!texture.LoadImage(bytes, true)) throw new InvalidDataException("The controller layout image couldn't be read.");
            return texture;
        }
    }
}
