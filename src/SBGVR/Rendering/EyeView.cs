// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SBGVR.Rendering
{
    /// <summary>
    /// The eye URP is drawing right now, for code that asks the Camera for its size or view and would
    /// otherwise get the flat game window's.
    ///
    /// With multi-pass stereo (what Super Battle Golf runs), URP draws each camera once per eye, and records
    /// and executes that eye's render graph before starting the next. <see cref="Capture"/> runs as the
    /// passes are added for an eye, so everything recorded and executed after it, until the next camera or
    /// eye, belongs to that eye. The helpers return the eye's values for the captured camera, and the
    /// Camera's own values for any other camera or when VR isn't drawing, so flat-screen rendering is
    /// unchanged.
    /// </summary>
    internal static class EyeView
    {
        private static Camera _camera;
        private static int _eye;
        private static int _width;
        private static int _height;
        private static Matrix4x4 _projection;
        private static Matrix4x4 _view;
        private static int _loggedWidth;
        private static int _loggedHeight;

        /// <summary>Records the eye being drawn, or forgets it when this camera isn't drawn in stereo.</summary>
        internal static void Capture(ref RenderingData renderingData)
        {
            ref var cameraData = ref renderingData.cameraData;
            var xr = cameraData.xr;
            // Single-pass stereo draws both eyes at once, which the occlusion code handles itself.
            if (xr == null || !xr.enabled || xr.singlePassEnabled)
            {
                _camera = null;
                return;
            }

            var descriptor = cameraData.cameraTargetDescriptor;
            _camera = cameraData.camera;
            _eye = xr.multipassId;
            _width = descriptor.width;
            _height = descriptor.height;
            _projection = cameraData.GetProjectionMatrix(0);
            _view = cameraData.GetViewMatrix(0);

            if (_width == _loggedWidth && _height == _loggedHeight) return;
            _loggedWidth = _width;
            _loggedHeight = _height;
            Plugin.Log.LogInfo($"Ambient occlusion: drawn for each eye at {_width}x{_height}.");
        }

        private static bool IsEye(Camera camera)
        {
            return _camera != null && ReferenceEquals(camera, _camera);
        }

        internal static int ScaledPixelWidth(Camera camera)
        {
            return IsEye(camera) ? _width : camera.scaledPixelWidth;
        }

        internal static int ScaledPixelHeight(Camera camera)
        {
            return IsEye(camera) ? _height : camera.scaledPixelHeight;
        }

        internal static int PixelHeight(Camera camera)
        {
            return IsEye(camera) ? _height : camera.pixelHeight;
        }

        /// <summary>
        /// A vertical field of view with the eye's pixels per unit of view angle. The eye's view is lopsided
        /// (more below than above, more outward than inward), which a single angle can't describe, but sizes
        /// derived from it come out right.
        /// </summary>
        internal static float FieldOfView(Camera camera)
        {
            return IsEye(camera) ? 2f * Mathf.Atan(1f / _projection.m11) * Mathf.Rad2Deg : camera.fieldOfView;
        }

        internal static Matrix4x4 ProjectionMatrix(Camera camera)
        {
            return IsEye(camera) ? _projection : camera.projectionMatrix;
        }

        internal static Matrix4x4 NonJitteredProjectionMatrix(Camera camera)
        {
            return IsEye(camera) ? _projection : camera.nonJitteredProjectionMatrix;
        }

        internal static Matrix4x4 WorldToCameraMatrix(Camera camera)
        {
            return IsEye(camera) ? _view : camera.worldToCameraMatrix;
        }

        /// <summary>
        /// The key a camera's history (last frame's matrices and occlusion) is stored under. Each eye gets its
        /// own, since both eyes are drawn by the same camera and would otherwise overwrite each other's.
        /// </summary>
        internal static int HistoryKey(object camera)
        {
            if (camera is Camera unityCamera && IsEye(unityCamera))
                return unchecked(unityCamera.GetHashCode() * 397) ^ (_eye + 1);
            return camera.GetHashCode();
        }

        /// <summary>
        /// GTAO's factors for turning a screen position and depth into a view-space position: (x, y) multiply
        /// the 0..1 screen position, (z, w) are added. GTAO derives them from a centred view; the eye's view
        /// is off-centre, so they come from its projection instead: the view spans tan(left) + tan(right)
        /// across and starts at -tan(left), and likewise bottom to top.
        /// </summary>
        internal static Vector4 DepthToViewParams(Vector4 original, Camera camera)
        {
            if (!IsEye(camera)) return original;

            var p = _projection;
            return new Vector4(
                2f / p.m00,
                2f / p.m11,
                -(1f - p.m02) / p.m00,
                -(1f - p.m12) / p.m11);
        }
    }
}
