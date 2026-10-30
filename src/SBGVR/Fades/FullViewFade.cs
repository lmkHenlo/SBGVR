// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Uuvr;

namespace SBGVR.Fades
{
    /// <summary>
    /// VR Comfort.FullViewFades: the game fades the screen with 2D images, which UUVR can only show on its
    /// floating screen, so a fade darkened a rectangle in front of you and left the rest of the view lit.
    /// While VR runs, those images are hidden and their colour and strength go to two spheres around the
    /// camera instead:
    /// <list type="bullet">
    /// <item>"Over": ColorOverlay, the game's fade from black into the main menu (used when the game starts by
    /// joining a Steam lobby), which covers the menu too. Drawn after the floating screen: same render queue (5000, the last URP draws), one sorting
    /// order later.</item>
    /// <item>"Behind": the main menu's fade between background scenes, which sits behind the menu. Drawn before
    /// the floating screen, so the menu stays readable as it does on a flat screen.</item>
    /// </list>
    /// </summary>
    internal sealed class FullViewFade : MonoBehaviour
    {
        // URP draws transparent objects only in render queues 2501 to 5000 (RenderQueueRange.transparent), so
        // the "over" sphere can't use a queue above the floating screen's (5000) to come after it; its sorting
        // order does that (Uuvr.VrUi.UiSorting).
        private const int BehindUiQueue = 4999;
        private const int OverUiQueue = 5000;
        private const int OverUiOrdersAfterScreen = 1;

        private static readonly AccessTools.FieldRef<ColorOverlay, Image> OverlayImage =
            AccessTools.FieldRefAccess<ColorOverlay, Image>("overlay");

        private static readonly AccessTools.FieldRef<ColorOverlay, float> OverlayAlpha =
            AccessTools.FieldRefAccess<ColorOverlay, float>("alpha");

        private static readonly AccessTools.FieldRef<MainMenuBackgroundHandler, CanvasGroup> MenuOverlayGroup =
            AccessTools.FieldRefAccess<MainMenuBackgroundHandler, CanvasGroup>("overlayCanvasGroup");

        private static FullViewFade _instance;

        // The latest state the game asked for, kept while its own image is hidden.
        private static ColorOverlay _colorOverlay;
        private static MainMenuBackgroundHandler _menu;
        private static float _menuAlpha;
        private static bool _active;

        private FadeSphere _over;
        private FadeSphere _behind;

        /// <summary>True while the game's fade images are hidden and drawn by the spheres instead.</summary>
        internal static bool Active => _active;

        private void Awake()
        {
            _instance = this;
            _over = new FadeSphere("SBGVR fade (over UI)", OverUiQueue, OverUiOrdersAfterScreen, transform);
            _behind = new FadeSphere("SBGVR fade (behind UI)", BehindUiQueue, 0, transform);
        }

        private void OnEnable()
        {
            Application.onBeforeRender += OnBeforeRender;
            VrState.Changed += OnVrChanged;
            Settings.FullViewFades.SettingChanged += OnSettingChanged;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRender;
            VrState.Changed -= OnVrChanged;
            Settings.FullViewFades.SettingChanged -= OnSettingChanged;
            SetActive(false);
        }

        private void OnVrChanged(bool running)
        {
            SetActive(running && Settings.FullViewFades.Value);
        }

        private void OnSettingChanged(object sender, System.EventArgs e)
        {
            SetActive(VrState.IsRunning && Settings.FullViewFades.Value);
        }

        /// <summary>Hands the game's fades to the spheres, or gives them back.</summary>
        private static void SetActive(bool active)
        {
            if (_active == active) return;
            _active = active;

            if (_colorOverlay == null && SingletonBehaviour<ColorOverlay>.HasInstance)
                _colorOverlay = SingletonBehaviour<ColorOverlay>.Instance;
            if (_colorOverlay != null) ColorOverlayChanged(_colorOverlay);

            if (_menu != null)
            {
                var group = MenuOverlayGroup(_menu);
                if (group != null) group.alpha = active ? 0f : _menuAlpha;
            }
        }

        /// <summary>Called after the game sets ColorOverlay's strength or colour.</summary>
        internal static void ColorOverlayChanged(ColorOverlay overlay)
        {
            _colorOverlay = overlay;
            var image = OverlayImage(overlay);
            if (image == null) return;

            var color = image.color;
            color.a = _active ? 0f : OverlayAlpha(overlay);
            image.color = color;
        }

        /// <summary>
        /// Called instead of MainMenuBackgroundHandler.SetOverlayCanvasAlpha. Returns false when the game's own
        /// call should be skipped.
        /// </summary>
        internal static bool MenuOverlayChanged(MainMenuBackgroundHandler menu, float alpha)
        {
            _menu = menu;
            _menuAlpha = alpha;
            if (!_active) return true;

            var group = MenuOverlayGroup(menu);
            if (group != null) group.alpha = 0f;
            return false;
        }

        private void OnBeforeRender()
        {
            var camera = ViewCamera();
            var overAlpha = 0f;
            var overColor = Color.black;
            var behindAlpha = 0f;
            var behindColor = Color.black;

            if (_active && camera != null)
            {
                if (_colorOverlay != null && _colorOverlay.gameObject.activeInHierarchy)
                {
                    overAlpha = OverlayAlpha(_colorOverlay);
                    var image = OverlayImage(_colorOverlay);
                    if (image != null) overColor = image.color;
                }

                if (_menu != null && _menu.isActiveAndEnabled)
                {
                    behindAlpha = _menuAlpha;
                    var group = MenuOverlayGroup(_menu);
                    var graphic = group != null ? group.GetComponentInChildren<Graphic>(true) : null;
                    if (graphic != null)
                    {
                        behindColor = graphic.color;
                        behindAlpha *= graphic.color.a;
                    }
                }
            }

            _over.Show(camera, overColor, overAlpha);
            _behind.Show(camera, behindColor, behindAlpha);
        }

        /// <summary>The camera the headset sees through: the one UUVR's floating screen follows.</summary>
        internal static Camera ViewCamera()
        {
            var vrCamera = Uuvr.VrCamera.VrCamera.HighestDepthVrCamera;
            if (vrCamera != null && vrCamera.isActiveAndEnabled && vrCamera.ParentCamera != null)
                return vrCamera.ParentCamera;
            return Camera.main;
        }

        /// <summary>A sphere around the camera, drawn without depth testing at a fixed render queue and sorting order.</summary>
        private sealed class FadeSphere
        {
            private readonly GameObject _object;
            private readonly Renderer _renderer;
            private readonly Material _material;

            internal FadeSphere(string name, int renderQueue, int ordersAfterScreen, Transform parent)
            {
                _object = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _object.name = name;
                _object.hideFlags = HideFlags.HideAndDontSave;
                Object.Destroy(_object.GetComponent<Collider>());
                _object.transform.SetParent(parent, false);

                _renderer = _object.GetComponent<Renderer>();
                _renderer.shadowCastingMode = ShadowCastingMode.Off;
                _renderer.receiveShadows = false;
                // Sorted with the floating screen, after every other transparent object, so effects with a
                // higher sorting order can't show through a fade. "Behind" shares the screen's sorting order and
                // its lower render queue puts it first; "over" is one sorting order after the screen.
                Uuvr.VrUi.UiSorting.DrawLast(_renderer, ordersAfterScreen);

                // The same UI shader UUVR draws its floating screen with, which is known to render in this game's
                // VR setup. Culling is off in it, so the sphere is visible from inside.
                _material = new Material(Canvas.GetDefaultCanvasMaterial())
                {
                    name = name,
                    renderQueue = renderQueue,
                };
                _material.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
                _renderer.sharedMaterial = _material;
                _object.SetActive(false);
            }

            internal void Show(Camera camera, Color color, float alpha)
            {
                if (camera == null || alpha <= 0.001f)
                {
                    if (_object.activeSelf) _object.SetActive(false);
                    return;
                }

                color.a = Mathf.Clamp01(alpha);
                _material.color = color;
                _object.layer = LayerHelper.GetVrUiLayer();

                // Inside the near clip plane the sphere would be cut away; well inside the far one.
                var radius = Mathf.Max(0.35f, camera.nearClipPlane * 2f);
                _object.transform.position = camera.transform.position;
                _object.transform.localScale = Vector3.one * (radius * 2f);
                if (!_object.activeSelf) _object.SetActive(true);
            }
        }
    }
}
