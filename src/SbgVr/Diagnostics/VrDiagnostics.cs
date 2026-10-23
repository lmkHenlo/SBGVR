// SBG VR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Bootstrap;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UrpXrSystem = UnityEngine.Experimental.Rendering.XRSystem;

namespace SbgVr.Diagnostics
{
    /// <summary>
    /// Writes what UUVR and the game's renderer are doing to the BepInEx log: the VR runtime's state,
    /// each eye's view, every active camera and the renderer's features. Each kind of detail is
    /// logged once and then again only when it changes, so the log stays readable.
    /// </summary>
    internal sealed class VrDiagnostics : MonoBehaviour
    {
        private const float PollInterval = 2f;
        private const string Prefix = "[diag] ";

        private readonly List<XRDisplaySubsystem> _displays = new List<XRDisplaySubsystem>();
        private readonly HashSet<string> _reportedErrors = new HashSet<string>();

        private string _lastXr = "";
        private string _lastEyes = "";
        private string _lastCameras = "";
        private float _nextPoll;
        private bool _vrRunning;

        private int _frames;
        private float _frameSeconds;
        private float _slowestFrame;
        private float _nextPerformanceLog;

        private void Start()
        {
            Run("environment", LogEnvironment);
            Run("render pipeline", LogRenderPipeline);
            Run("plugins", LogPlugins);
            Run("UUVR settings", LogUuvrConfig);
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            Info($"Scene: '{next.name}'");
            // Always list the cameras of a newly loaded scene.
            _lastCameras = "";
            _nextPoll = 0f;
        }

        private void Update()
        {
            TrackFrameRate();

            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + PollInterval;

            Run("VR state", PollXr);
            Run("eye views", PollEyes);
            Run("cameras", PollCameras);
        }

        // ---- Once at startup ------------------------------------------------------------------------

        private static void LogEnvironment()
        {
            var refresh = Screen.currentResolution.refreshRateRatio.value;
            Info($"Game: {Application.productName} {Application.version}, Unity {Application.unityVersion}, " +
                 $"{SystemInfo.graphicsDeviceType} on {SystemInfo.graphicsDeviceName}, " +
                 $"screen {Screen.width}x{Screen.height} @ {F(refresh)} Hz ({Screen.fullScreenMode}), " +
                 $"quality level '{QualitySettings.names[QualitySettings.GetQualityLevel()]}'");
        }

        private static void LogRenderPipeline()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null)
            {
                Info("Render pipeline: built-in renderer");
                return;
            }

            Info($"Render pipeline: {pipeline.GetType().Name} '{pipeline.name}'");
            if (!(pipeline is UniversalRenderPipelineAsset urp)) return;

            Info($"  render scale {F(urp.renderScale)}, MSAA {urp.msaaSampleCount}x, HDR {urp.supportsHDR}");

            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var renderers = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", flags)
                ?.GetValue(urp) as ScriptableRendererData[];
            var defaultIndex = typeof(UniversalRenderPipelineAsset).GetField("m_DefaultRendererIndex", flags)
                ?.GetValue(urp) as int? ?? -1;
            if (renderers == null)
            {
                Info("  renderer list not readable");
                return;
            }

            for (var i = 0; i < renderers.Length; i++)
            {
                var data = renderers[i];
                if (data == null) continue;

                Info($"  renderer {i}{(i == defaultIndex ? " (default)" : "")}: {data.GetType().Name} '{data.name}'");
                foreach (var feature in data.rendererFeatures)
                {
                    if (feature == null) continue;
                    Info($"    feature {feature.GetType().FullName} '{feature.name}' active={feature.isActive}");
                }
            }
        }

        private static void LogPlugins()
        {
            var plugins = Chainloader.PluginInfos.Values
                .Select(info => $"{info.Metadata.Name} {info.Metadata.Version} ({info.Metadata.GUID})")
                .OrderBy(text => text, StringComparer.OrdinalIgnoreCase);
            Info($"Plugins: {string.Join(", ", plugins)}");
        }

        private static void LogUuvrConfig()
        {
            if (!Chainloader.PluginInfos.TryGetValue(Plugin.UuvrGuid, out var uuvr) || uuvr.Instance == null)
            {
                Plugin.Log.LogWarning(Prefix + "UUVR is not loaded, so VR can't start.");
                return;
            }

            var config = uuvr.Instance.Config;
            var settings = config
                .Select(entry => $"{entry.Key.Section}.{entry.Key.Key}={entry.Value.BoxedValue}")
                .OrderBy(text => text, StringComparer.OrdinalIgnoreCase);
            Info($"UUVR settings ({config.ConfigFilePath}): {string.Join("; ", settings)}");
        }

        // ---- Polled every few seconds; logged on change ------------------------------------------

        private void PollXr()
        {
            SubsystemManager.GetSubsystems(_displays);
            _vrRunning = _displays.Any(display => display.running);

            var text = new StringBuilder();
            text.Append("displays=").Append(_displays.Count);
            foreach (var display in _displays)
            {
                text.Append(" ['").Append(display.subsystemDescriptor?.id).Append("' running=").Append(display.running).Append(']');
            }

            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            text.Append(", XR enabled=").Append(XRSettings.enabled)
                .Append(", device active=").Append(XRSettings.isDeviceActive)
                .Append(", device='").Append(XRSettings.loadedDeviceName).Append('\'')
                .Append(", headset='").Append(head.isValid ? head.name : "none").Append('\'')
                .Append(", eye texture=").Append(XRSettings.eyeTextureWidth).Append('x').Append(XRSettings.eyeTextureHeight)
                .Append(", stereo mode=").Append(XRSettings.stereoRenderingMode)
                .Append(", mirror to screen=").Append(XRSettings.showDeviceView)
                .Append(", URP XR active=").Append(UrpXrSystem.displayActive)
                .Append(", refresh=").Append(F(RefreshRate())).Append(" Hz");

            LogIfChanged(ref _lastXr, "VR: " + text);
        }

        private void PollEyes()
        {
            if (!_vrRunning) return;

            var display = _displays.FirstOrDefault(candidate => candidate.running);
            var camera = Camera.main != null ? Camera.main : Camera.allCameras.FirstOrDefault();
            if (display == null || camera == null) return;

            var text = new StringBuilder();
            var passCount = display.GetRenderPassCount();
            text.Append(passCount).Append(" render pass(es)");
            for (var passIndex = 0; passIndex < passCount; passIndex++)
            {
                display.GetRenderPass(passIndex, out var pass);
                text.Append("; pass ").Append(passIndex)
                    .Append(" target ").Append(pass.renderTargetDesc.width).Append('x').Append(pass.renderTargetDesc.height);

                for (var viewIndex = 0; viewIndex < pass.GetRenderParameterCount(); viewIndex++)
                {
                    pass.GetRenderParameter(camera, viewIndex, out var view);
                    var frustum = view.projection.decomposeProjection;
                    var left = Degrees(-frustum.left, frustum.zNear);
                    var right = Degrees(frustum.right, frustum.zNear);
                    var up = Degrees(frustum.top, frustum.zNear);
                    var down = Degrees(-frustum.bottom, frustum.zNear);
                    text.Append(", view ").Append(viewIndex)
                        .Append(": horizontal ").Append(F(left + right)).Append(" (left ").Append(F(left)).Append(", right ").Append(F(right)).Append(')')
                        .Append(" vertical ").Append(F(up + down)).Append(" (up ").Append(F(up)).Append(", down ").Append(F(down)).Append(')');
                }
            }

            LogIfChanged(ref _lastEyes, "Eye views (degrees): " + text);
        }

        private void PollCameras()
        {
            var cameras = Camera.allCameras.OrderBy(camera => camera.depth).ToArray();

            var structure = new StringBuilder();
            var lines = new List<string>();
            foreach (var camera in cameras)
            {
                var urp = camera.GetComponent<UniversalAdditionalCameraData>();
                var target = camera.targetTexture != null
                    ? $"texture {camera.targetTexture.width}x{camera.targetTexture.height}"
                    : "screen";
                var role = camera.GetComponent<CameraModuleController>() != null ? " gameplay" : "";
                var uuvr = camera.GetComponent("VrCamera") != null ? " uuvr" : "";

                var line = $"'{camera.name}'{role}{uuvr} depth={F(camera.depth)} eye={camera.stereoTargetEye} target={target} " +
                           $"clear={camera.clearFlags} mask={camera.cullingMask:X8}" +
                           (urp != null
                               ? $" URP={urp.renderType} xr={urp.allowXRRendering} stack={urp.cameraStack?.Count ?? 0} post={urp.renderPostProcessing}"
                               : " URP=none");
                structure.Append(line).Append('|');
                lines.Add(line + $" fov={F(camera.fieldOfView)}");
            }

            var signature = structure.ToString();
            if (signature == _lastCameras) return;
            _lastCameras = signature;

            Info($"Cameras ({cameras.Length}):");
            foreach (var line in lines) Info("  " + line);
        }

        private void TrackFrameRate()
        {
            var interval = Settings.PerformanceLogInterval.Value;
            if (!_vrRunning || interval <= 0f)
            {
                _frames = 0;
                _frameSeconds = 0f;
                _slowestFrame = 0f;
                _nextPerformanceLog = 0f;
                return;
            }

            var frameTime = Time.unscaledDeltaTime;
            _frames++;
            _frameSeconds += frameTime;
            if (frameTime > _slowestFrame) _slowestFrame = frameTime;

            if (_nextPerformanceLog == 0f) _nextPerformanceLog = Time.unscaledTime + interval;
            if (Time.unscaledTime < _nextPerformanceLog || _frameSeconds <= 0f) return;

            Info($"Frame rate: {F(_frames / _frameSeconds)} fps average, slowest frame {F(_slowestFrame * 1000f)} ms, " +
                 $"headset {F(RefreshRate())} Hz");
            _frames = 0;
            _frameSeconds = 0f;
            _slowestFrame = 0f;
            _nextPerformanceLog = Time.unscaledTime + interval;
        }

        // ---- Helpers ----------------------------------------------------------------------------------

        private void Run(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                // Report each distinct failure once instead of every poll.
                var key = what + exception.GetType().FullName + exception.Message;
                if (_reportedErrors.Add(key))
                    Plugin.Log.LogWarning($"{Prefix}Couldn't read {what}: {exception}");
            }
        }

        private float RefreshRate()
        {
            foreach (var display in _displays)
            {
                if (display.running && display.TryGetDisplayRefreshRate(out var rate)) return rate;
            }

            return 0f;
        }

        private static void LogIfChanged(ref string last, string text)
        {
            if (text == last) return;
            last = text;
            Info(text);
        }

        private static void Info(string text)
        {
            Plugin.Log.LogInfo(Prefix + text);
        }

        private static float Degrees(float extent, float near)
        {
            return near > 0f ? Mathf.Atan(extent / near) * Mathf.Rad2Deg : 0f;
        }

        private static string F(float value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private static string F(double value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }
    }
}
