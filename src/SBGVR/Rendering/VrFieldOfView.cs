// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace SBGVR.Rendering
{
    /// <summary>
    /// VR Graphics.FieldOfView and RailgunZoom: the game's field of view in VR.
    ///
    /// The headset sets each eye's projection, so the game's field of view (its slider, the railgun's zoom)
    /// had no effect. Before each frame is drawn, URP builds the eyes' views for each camera and then calls
    /// XRSystem's layout override; for the game camera, this divides each eye's projection by a factor, so
    /// everything the eye sees draws in or spreads out about its straight-ahead direction:
    ///
    ///   factor = tan(wanted / 2) / tan(60° / 2)
    ///
    /// At the game's default of 60° the factor is 1 and the headset's view is unchanged. A wider field of
    /// view shows more of the world, squeezed into the headset (the eyes' culling is widened to match), and a
    /// narrower one magnifies it. While magnified, the eyes are also drawn closer together by the factor, so
    /// the doubled picture stays easy to fuse.
    ///
    /// The floating screen (<see cref="Uuvr.VrUi.VrUiManager.ViewScale"/>) and world-space markers are scaled
    /// by the factor, so the HUD keeps its size. Ambient occlusion reads each eye's projection from the same
    /// views, so it follows.
    /// </summary>
    [DefaultExecutionOrder(31000)]
    internal sealed class VrFieldOfView : MonoBehaviour
    {
        /// <summary>The game's default field of view: the one that maps to the headset's own view.</summary>
        internal const float ReferenceFieldOfView = 60f;

        private const float MaximumFieldOfView = 179.5f;
        private const float MinimumFieldOfView = 1f;

        /// <summary>How much each eye's view is widened (above 1) or magnified (below 1) this frame.</summary>
        internal static float Factor { get; private set; } = 1f;

        /// <summary>How far apart the eyes are drawn, as a share of their real separation.</summary>
        internal static float EyeSeparation { get; private set; } = 1f;

        private static readonly AccessTools.FieldRef<CameraModuleController, Camera> ControllerCamera =
            AccessTools.FieldRefAccess<CameraModuleController, Camera>("camera");

        private static readonly AccessTools.FieldRef<CameraModuleController, CameraModule> CurrentModule =
            AccessTools.FieldRefAccess<CameraModuleController, CameraModule>("currentModule");

        private static readonly AccessTools.FieldRef<FirstPersonCameraModule, bool> IsFovOverridden =
            AccessTools.FieldRefAccess<FirstPersonCameraModule, bool>("isFovOverridden");

        // URP's XR view is internal, and so is the way to replace a pass's culling.
        private static FieldInfo _views;
        private static FieldInfo _projMatrix;
        private static FieldInfo _viewMatrix;
        private static FieldInfo _prevViewMatrix;
        private static MethodInfo _assignCullingParams;
        private static FieldInfo _layoutOverride;
        private static Action<XRLayout, Camera> _previousOverride;

        private static Camera _camera;
        private static readonly List<XRPass> Passes = new List<XRPass>();
        private static readonly List<Vector3> EyePositions = new List<Vector3>();
        private static readonly List<Matrix4x4> CullingMatrices = new List<Matrix4x4>();
        private static readonly Plane[] Planes = new Plane[6];
        private static readonly Plane[] Union = new Plane[6];
        private static Vector3 _centre;
        private static bool _loggedApplied;
        private static int _loggedFieldOfView = -1;
        private static FieldOfViewMode _loggedMode;

        /// <summary>
        /// Finds the parts of URP this needs and installs the layout override. False, after logging why, when
        /// any part is missing; the feature is then off and the headset's view is left alone.
        /// </summary>
        internal static bool Initialize()
        {
            var viewType = AccessTools.TypeByName("UnityEngine.Experimental.Rendering.XRView");
            _views = AccessTools.Field(typeof(XRPass), "m_Views");
            _projMatrix = viewType != null ? AccessTools.Field(viewType, "projMatrix") : null;
            _viewMatrix = viewType != null ? AccessTools.Field(viewType, "viewMatrix") : null;
            _prevViewMatrix = viewType != null ? AccessTools.Field(viewType, "prevViewMatrix") : null;
            _assignCullingParams = AccessTools.Method(typeof(XRPass), "AssignCullingParams",
                new[] { typeof(int), typeof(ScriptableCullingParameters) });
            // XRSystem.SetLayoutOverride is internal here; it only sets this field.
            var overrideField = AccessTools.Field(typeof(XRSystem), "s_LayoutOverride");
            _layoutOverride = overrideField;

            var missing = new List<string>();
            if (viewType == null) missing.Add("XRView");
            if (_views == null) missing.Add("XRPass.m_Views");
            if (_projMatrix == null) missing.Add("XRView.projMatrix");
            if (_viewMatrix == null) missing.Add("XRView.viewMatrix");
            if (_prevViewMatrix == null) missing.Add("XRView.prevViewMatrix");
            if (_assignCullingParams == null) missing.Add("XRPass.AssignCullingParams");
            if (overrideField == null) missing.Add("XRSystem.s_LayoutOverride");
            if (missing.Count > 0)
            {
                Plugin.Log.LogError(
                    $"[FAILED] Field of view: this version of URP has no {string.Join(", ", missing)}. The game's " +
                    "field of view doesn't apply in VR; the rest of SBGVR works as before.");
                return false;
            }

            // Keep any override someone else installed, and run it first.
            _previousOverride = overrideField.GetValue(null) as Action<XRLayout, Camera>;
            overrideField.SetValue(null, (Action<XRLayout, Camera>)OnLayout);
            return true;
        }

        private void LateUpdate()
        {
            _camera = GameCamera(out var controller);
            var factor = 1f;
            var separation = 1f;
            // Only while the game camera is the one in the headset, so the floating screen and markers
            // (scaled to match) are drawn by the camera whose view changed: not the main menu's, for one.
            if (VrState.IsRunning && _camera != null && Fades.FullViewFade.ViewCamera() == _camera)
            {
                var wanted = Mathf.Clamp(WantedFieldOfView(controller, _camera), MinimumFieldOfView, MaximumFieldOfView);
                factor = Mathf.Tan(wanted * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(ReferenceFieldOfView * 0.5f * Mathf.Deg2Rad);
                if (factor < 1f) separation = factor;
                LogChange(wanted, factor);
            }

            Factor = factor;
            EyeSeparation = separation;
            Uuvr.VrUi.VrUiManager.ViewScale = factor;
        }

        private void OnDisable()
        {
            Factor = 1f;
            EyeSeparation = 1f;
            Uuvr.VrUi.VrUiManager.ViewScale = 1f;
        }

        private static Camera GameCamera(out CameraModuleController controller)
        {
            controller = SingletonBehaviour<CameraModuleController>.HasInstance
                ? SingletonBehaviour<CameraModuleController>.Instance
                : null;
            return controller != null ? ControllerCamera(controller) : null;
        }

        /// <summary>The field of view the game would show on a flat screen, as far as the settings follow it.</summary>
        private static float WantedFieldOfView(CameraModuleController controller, Camera camera)
        {
            var hasRequested = Uuvr.Patches.TryGetRequestedFieldOfView(camera, out var requested);
            if (hasRequested && IsRailgunZoomed(controller))
                return Settings.RailgunZoom.Value ? requested : SliderFieldOfView();

            switch (Settings.FieldOfView.Value)
            {
                case FieldOfViewMode.Setting:
                    return SliderFieldOfView();
                case FieldOfViewMode.Game:
                    return hasRequested ? requested : SliderFieldOfView();
                default:
                    return ReferenceFieldOfView;
            }
        }

        /// <summary>
        /// The Field of view slider's value. MaxFOVSlider raises the game's own limit, so its range comes
        /// through here too.
        /// </summary>
        private static float SliderFieldOfView()
        {
            var all = GameSettings.All;
            return all != null && all.Graphics != null ? all.Graphics.FieldOfView : ReferenceFieldOfView;
        }

        /// <summary>The railgun's aim zooms by overriding the first-person camera's field of view.</summary>
        private static bool IsRailgunZoomed(CameraModuleController controller)
        {
            return controller != null && CurrentModule(controller) is FirstPersonCameraModule firstPerson &&
                   IsFovOverridden(firstPerson);
        }

        /// <summary>
        /// Called by URP for each camera once its XR views are built, before it's drawn.
        /// </summary>
        private static void OnLayout(XRLayout layout, Camera camera)
        {
            _previousOverride?.Invoke(layout, camera);

            var factor = Factor;
            var separation = EyeSeparation;
            if (camera == null || camera != _camera) return;
            if (Mathf.Approximately(factor, 1f) && Mathf.Approximately(separation, 1f)) return;

            try
            {
                ApplyTo(layout, camera, factor, separation);
            }
            catch (Exception exception)
            {
                // Don't break rendering; go back to the headset's own view.
                Plugin.Log.LogError($"Field of view: couldn't change the eyes' views, so it's off: {exception}");
                _layoutOverride.SetValue(null, _previousOverride);
            }
        }

        private static void ApplyTo(XRLayout layout, Camera camera, float factor, float separation)
        {
            Passes.Clear();
            EyePositions.Clear();
            foreach (var (passCamera, pass) in layout.GetActivePasses())
            {
                if (passCamera != camera || pass == null || !pass.enabled) continue;
                Passes.Add(pass);
                for (var i = 0; i < pass.viewCount; i++) EyePositions.Add(pass.GetViewMatrix(i).inverse.GetColumn(3));
            }

            if (Passes.Count == 0) return;
            if (!_loggedApplied)
            {
                _loggedApplied = true;
                Plugin.Log.LogInfo($"Field of view: changing the eyes' views of camera '{camera.name}' " +
                                   $"({Passes.Count} pass(es), {EyePositions.Count} eye(s)).");
            }

            var centre = Vector3.zero;
            foreach (var position in EyePositions) centre += position;
            centre /= EyePositions.Count;
            _centre = centre;

            CullingMatrices.Clear();
            var eye = 0;
            foreach (var pass in Passes)
            {
                var views = (IList)_views.GetValue(pass);
                for (var i = 0; i < views.Count; i++, eye++)
                {
                    var view = views[i];
                    var projection = (Matrix4x4)_projMatrix.GetValue(view);
                    projection.m00 /= factor;
                    projection.m11 /= factor;
                    _projMatrix.SetValue(view, projection);

                    var viewMatrix = (Matrix4x4)_viewMatrix.GetValue(view);
                    if (!Mathf.Approximately(separation, 1f))
                    {
                        // Move the eye towards the centre: V' = V * T(p - p').
                        var position = EyePositions[eye];
                        var moved = centre + (position - centre) * separation;
                        var shift = Matrix4x4.Translate(position - moved);
                        viewMatrix *= shift;
                        _viewMatrix.SetValue(view, viewMatrix);
                        _prevViewMatrix.SetValue(view, (Matrix4x4)_prevViewMatrix.GetValue(view) * shift);
                    }

                    views[i] = view;
                    CullingMatrices.Add(projection * viewMatrix);
                }
            }

            // A wider view needs wider culling, or what's newly in view isn't drawn. A magnified one is
            // inside the headset's own culling already.
            if (factor > 1f) WidenCulling();
        }

        /// <summary>
        /// Culls with planes that contain every eye's new frustum: for each side, the most outward of the
        /// eyes' planes (seen from between the eyes), moved out until every eye is inside it.
        /// </summary>
        private static void WidenCulling()
        {
            for (var eye = 0; eye < CullingMatrices.Count; eye++)
            {
                GeometryUtility.CalculateFrustumPlanes(CullingMatrices[eye], Planes);
                for (var side = 0; side < Planes.Length; side++)
                {
                    if (eye == 0 || Planes[side].GetDistanceToPoint(_centre) > Union[side].GetDistanceToPoint(_centre))
                        Union[side] = Planes[side];
                }
            }

            for (var side = 0; side < 4; side++)
            {
                foreach (var position in EyePositions)
                {
                    var distance = Union[side].GetDistanceToPoint(position);
                    if (distance < 0f) Union[side].distance -= distance;
                }
            }

            foreach (var pass in Passes)
            {
                var culling = pass.cullingParams;
                culling.cullingMatrix = CullingMatrices[0];
                culling.cullingPlaneCount = Union.Length;
                for (var side = 0; side < Union.Length; side++) culling.SetCullingPlane(side, Union[side]);
                _assignCullingParams.Invoke(pass, new object[] { pass.cullingPassId, culling });
            }
        }

        private static void LogChange(float wanted, float factor)
        {
            var rounded = Mathf.RoundToInt(wanted);
            var mode = Settings.FieldOfView.Value;
            if (rounded == _loggedFieldOfView && mode == _loggedMode) return;
            _loggedFieldOfView = rounded;
            _loggedMode = mode;
            if (!Settings.DiagnosticsEnabled.Value) return;
            Plugin.Log.LogInfo(
                $"[diag] Field of view: {mode}, {rounded}° on a flat screen, so each eye's view is " +
                (factor >= 1f ? $"{factor:0.##}x wider" : $"magnified {1f / factor:0.##}x, eyes {factor:0.##}x as far apart") + ".");
        }

        /// <summary>
        /// How far from straight ahead the view now reaches, for a direction that was
        /// <paramref name="degrees"/> from straight ahead in the headset's own view.
        /// </summary>
        internal static float ScaleAngle(float degrees)
        {
            var factor = Factor;
            if (Mathf.Approximately(factor, 1f)) return degrees;
            var limited = Mathf.Min(degrees, 89.9f);
            if (degrees >= 90f && factor > 1f) return degrees;
            return Mathf.Atan(factor * Mathf.Tan(limited * Mathf.Deg2Rad)) * Mathf.Rad2Deg;
        }
    }
}
