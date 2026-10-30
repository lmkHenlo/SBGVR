// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Uuvr;

namespace SBGVR.View
{
    /// <summary>
    /// The railgun's scope in VR: a screen that stays still in front of you and shows the scope's zoomed view,
    /// aimed by the railgun, as the flat-screen game shows it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game zooms the scope by narrowing the first-person camera's field of view, from
    /// <c>ItemSettings.RailgunZoomInitialFov</c> easing to <c>RailgunZoomFinalFov</c>. A headset ignores
    /// <c>Camera.fieldOfView</c>, so in VR the scope didn't zoom. Zooming the whole headset view instead fixes
    /// the picture to your face, so it moves and tilts with your head relative to the room.
    /// </para>
    /// <para>
    /// So while the scope is open (<see cref="IsScoped"/>) and the camera has finished moving into it, a second
    /// camera renders the scope's view into a texture: from the game camera's position, along the railgun's aim
    /// (<see cref="Controls.ControllerAim.TransformOf"/>, the ray the game shoots along), at the scope's own field
    /// of view, read from the game each frame so it follows the game's zoom-in. A screen shows it, fixed in the
    /// game camera's frame, the way UUVR's floating screen is: it stays still in front of you while the railgun
    /// turns the view on it. Your headset tracks as usual, so turning or tilting your head looks around the
    /// screen and never moves or tilts the picture on it.
    /// </para>
    /// <para>
    /// The screen covers the same angle up and down as your Field of View setting covers on a flat screen, so
    /// things on it look exactly as much bigger than in the world around it as the flat-screen scope makes them:
    /// tan(setting / 2) / tan(scope / 2). It's drawn over the scenery, behind the floating screen, markers and
    /// crosshairs (the scope's own rings among them, centred on the aim and so on the screen's middle), and under
    /// full-view fades. The second camera leaves out the VR HUD layer and renders only while the screen shows.
    /// </para>
    /// </remarks>
    internal sealed class RailgunScopeScreen : MonoBehaviour
    {
        /// <summary>How far in front of you the screen is, in metres: behind the floating screen.</summary>
        private const float Distance = 3f;

        /// <summary>The largest angle the screen covers up and down, in degrees, whatever the Field of View setting.</summary>
        private const float MaxScreenAngle = 90f;

        /// <summary>Rendered pixels per degree of the screen (about a headset's).</summary>
        private const float PixelsPerDegree = 24f;

        private const float FadeInSeconds = 0.12f;
        private const string ZTestProperty = "unity_GUIZTestMode";

        private Camera _camera;
        private RenderTexture _texture;
        private GameObject _screen;
        private Renderer _renderer;
        private Material _material;
        private float _alpha;
        private bool _showing;
        private bool _logged;

        /// <summary>Whether the local player's railgun scope is open in VR.</summary>
        internal static bool IsScoped()
        {
            if (!VrState.IsRunning) return false;
            var inventory = GameManager.LocalPlayerInventory;
            var player = GameManager.LocalPlayerInfo;
            return inventory != null && inventory.IsInFirstPersonAim && player != null &&
                   player.NetworkedEquippedItem == ItemType.Railgun;
        }

        private void OnEnable()
        {
            Application.onBeforeRender += BeforeRender;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= BeforeRender;
            Hide();
        }

        private void OnDestroy()
        {
            if (_texture != null) _texture.Release();
            if (_texture != null) Destroy(_texture);
            if (_material != null) Destroy(_material);
            if (_screen != null) Destroy(_screen);
            if (_camera != null) Destroy(_camera.gameObject);
        }

        private void Update()
        {
            if (!_showing) return;
            _alpha = Mathf.MoveTowards(_alpha, 1f, Time.unscaledDeltaTime / FadeInSeconds);
            if (_material != null) _material.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, _alpha));
        }

        /// <summary>
        /// Just before the frame renders, after the game has placed its camera and before UUVR adds your head's
        /// turn to it for the eyes: aims the scope camera and places the screen.
        /// </summary>
        private void BeforeRender()
        {
            var gameCamera = GameManager.Camera;
            if (!IsScoped() || gameCamera == null || CameraModuleController.IsTransitioning ||
                !TryGetFieldsOfView(out var setting, out var scope))
            {
                Hide();
                return;
            }

            var screenAngle = Mathf.Min(setting, MaxScreenAngle);
            var aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            if (!EnsureObjects(gameCamera, screenAngle, aspect)) return;

            // The scope's view: from the game camera, along the railgun's aim, at the scope's field of view.
            var frame = gameCamera.transform;
            var aim = Controls.ControllerAim.TransformOf(gameCamera);
            _camera.transform.SetPositionAndRotation(frame.position, aim.rotation);
            _camera.fieldOfView = scope;
            _camera.aspect = aspect;
            _camera.nearClipPlane = gameCamera.nearClipPlane;
            _camera.farClipPlane = gameCamera.farClipPlane;
            _camera.cullingMask = gameCamera.cullingMask & ~(1 << LayerHelper.GetVrUiLayer());
            _camera.depth = gameCamera.depth - 1f;
            _camera.enabled = true;

            // The screen: still in front of you in the game camera's frame, as the floating screen is.
            var height = 2f * Distance * Mathf.Tan(screenAngle * 0.5f * Mathf.Deg2Rad);
            _screen.transform.SetPositionAndRotation(frame.position + frame.rotation * (Vector3.forward * Distance), frame.rotation);
            _screen.transform.localScale = new Vector3(height * aspect, height, 1f);
            _screen.layer = LayerHelper.GetVrUiLayer();
            if (!_showing)
            {
                _showing = true;
                _alpha = 0f;
                _material.color = new Color(1f, 1f, 1f, 0f);
                LogScope(setting, screenAngle);
            }

            _screen.SetActive(true);
        }

        private void Hide()
        {
            _showing = false;
            if (_camera != null) _camera.enabled = false;
            if (_screen != null) _screen.SetActive(false);
        }

        /// <summary>Your Field of View setting and the scope's current field of view.</summary>
        private static bool TryGetFieldsOfView(out float setting, out float scope)
        {
            setting = scope = 0f;
            if (!CameraModuleController.TryGetFirstPersonModule(out var module) || module == null) return false;
            var all = GameSettings.All;
            if (all?.Graphics == null) return false;
            setting = all.Graphics.FieldOfView;
            scope = module.FieldOfView;
            return setting > 1f && setting < 179f && scope > 0.1f && scope < 179f;
        }

        private bool EnsureObjects(Camera gameCamera, float screenAngle, float aspect)
        {
            var height = Mathf.Clamp(Mathf.RoundToInt(screenAngle * PixelsPerDegree), 480, 2048);
            var width = Mathf.Clamp(Mathf.RoundToInt(height * aspect), 480, 4096);
            if (_texture == null || _texture.width != width || _texture.height != height)
            {
                if (_texture != null)
                {
                    _texture.Release();
                    Destroy(_texture);
                }

                _texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "SBGVR railgun scope" };
                _texture.Create();
                if (_camera != null) _camera.targetTexture = _texture;
                if (_material != null) _material.mainTexture = _texture;
            }

            if (_camera == null)
            {
                var cameraObject = new GameObject("SBGVR railgun scope camera") { hideFlags = HideFlags.HideAndDontSave };
                DontDestroyOnLoad(cameraObject);
                _camera = cameraObject.AddComponent<Camera>();
                _camera.enabled = false;
                // Not a VR camera: UUVR leaves cameras with a target texture alone, and this one is on its list too.
                Uuvr.VrCamera.VrCamera.IgnoredCameras.Add(_camera);
                _camera.CopyFrom(gameCamera);
                _camera.stereoTargetEye = StereoTargetEyeMask.None;
                _camera.targetTexture = _texture;

                var data = _camera.GetUniversalAdditionalCameraData();
                var gameData = gameCamera.GetComponent<UniversalAdditionalCameraData>();
                data.allowXRRendering = false;
                data.renderType = CameraRenderType.Base;
                if (gameData != null)
                {
                    data.renderPostProcessing = gameData.renderPostProcessing;
                    data.antialiasing = gameData.antialiasing;
                    data.antialiasingQuality = gameData.antialiasingQuality;
                    data.requiresColorOption = gameData.requiresColorOption;
                    data.requiresDepthOption = gameData.requiresDepthOption;
                }
            }

            if (_screen == null)
            {
                _screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _screen.name = "SBGVR railgun scope screen";
                _screen.hideFlags = HideFlags.HideAndDontSave;
                DontDestroyOnLoad(_screen);
                Destroy(_screen.GetComponent<Collider>());
                _material = new Material(Canvas.GetDefaultCanvasMaterial())
                {
                    name = "SBGVR railgun scope screen",
                    mainTexture = _texture,
                    color = Color.white,
                    renderQueue = 5000,
                };
                // Over the scenery that may be between you and the screen.
                _material.SetInt(ZTestProperty, (int)CompareFunction.Always);
                _renderer = _screen.GetComponent<Renderer>();
                _renderer.sharedMaterial = _material;
                _renderer.shadowCastingMode = ShadowCastingMode.Off;
                _renderer.receiveShadows = false;
                // After the scene, before the markers (one order below the floating screen) and the floating screen.
                Uuvr.VrUi.UiSorting.DrawLast(_renderer);
                _renderer.sortingOrder = Uuvr.VrUi.UiSorting.ScreenOrder - 2;
                _screen.SetActive(false);
            }

            return _camera != null && _texture != null && _material != null;
        }

        private void LogScope(float setting, float screenAngle)
        {
            if (_logged && !Settings.DiagnosticsEnabled.Value) return;
            _logged = true;
            var items = GameManager.ItemSettings;
            if (items == null) return;

            float Magnification(float fov) => Mathf.Tan(screenAngle * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            Plugin.Log.LogInfo($"Railgun scope: shown on a screen {Distance:0.#} m ahead covering {screenAngle:0.#}° up and down " +
                               $"(Field of View setting {setting:0.#}), {_texture.width}x{_texture.height}; scope field of view " +
                               $"{items.RailgunZoomInitialFov:0.#} to {items.RailgunZoomFinalFov:0.#}, magnifying " +
                               $"{Magnification(items.RailgunZoomInitialFov):0.00}x to {Magnification(items.RailgunZoomFinalFov):0.00}x.");
        }
    }
}
