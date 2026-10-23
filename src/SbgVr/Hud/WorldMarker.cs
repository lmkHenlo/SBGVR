// SBG VR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Uuvr;

namespace SbgVr.Hud
{
    /// <summary>
    /// Moves one HUD marker (an icon, name tag or reticle) into the 3D world at its target while VR runs, so
    /// each eye sees it at the target's depth instead of on UUVR's floating screen 1 m away (where markers
    /// on nearby targets look doubled).
    ///
    /// The marker's UI objects are moved, unchanged, under a world-space canvas placed at the target, facing
    /// the camera and scaled so the marker keeps the size it had on the floating screen. The game keeps
    /// laying the marker out as usual; only where it's drawn changes. When the marker goes off screen, VR
    /// stops, or the setting is off, it goes back to the game's HUD.
    ///
    /// While away from the HUD the marker still follows it: the canvas lives in the HUD's scene (so it is
    /// unloaded with it), copies the HUD's visibility and fading, and the marker is destroyed if the HUD
    /// object it came from is.
    /// </summary>
    internal sealed class WorldMarker : MonoBehaviour
    {
        private const int CanvasSortingOrder = 30000;
        private const float FloatingScreenWidthPerScale = 1.8f;

        private static Material _onTopMaterial;

        private Transform _rect;
        private GameObject _anchor;
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private Transform _hudParent;
        private int _hudSiblingIndex;
        private bool _hadHudParent;
        private bool _inWorld;
        private bool _materialsSwapped;
        private int _graphicCount;
        private readonly List<KeyValuePair<Graphic, Material>> _originalMaterials = new List<KeyValuePair<Graphic, Material>>();
        private readonly List<KeyValuePair<TMP_Text, Material>> _originalFontMaterials = new List<KeyValuePair<TMP_Text, Material>>();

        internal static void Initialize()
        {
            _onTopMaterial = new Material(Canvas.GetDefaultCanvasMaterial())
            {
                name = "SBG VR marker (on top)",
            };
            _onTopMaterial.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
        }

        /// <summary>True when markers should be drawn in the world right now.</summary>
        internal static bool Enabled => VrState.IsRunning && Settings.WorldSpaceMarkers.Value && _onTopMaterial != null;

        /// <summary>
        /// Draws <paramref name="marker"/> at <paramref name="worldPosition"/> when <paramref name="inWorld"/> is
        /// true, or returns it to the HUD. Call after the game has positioned the marker for this frame.
        /// </summary>
        internal static void Place(Component marker, Vector3 worldPosition, bool inWorld)
        {
            if (marker == null) return;

            var worldMarker = marker.GetComponent<WorldMarker>();
            if (worldMarker == null)
            {
                if (!inWorld) return;
                worldMarker = marker.gameObject.AddComponent<WorldMarker>();
            }

            worldMarker.PlaceInternal(worldPosition, inWorld);
        }

        private void OnDestroy()
        {
            if (_anchor != null) Destroy(_anchor);
        }

        private void OnTransformParentChanged()
        {
            if (_rect == null) return;
            // The game took the marker away from us (back to its pool, usually): it's a HUD object again.
            if (!_inWorld || (_anchor != null && _rect.parent == _anchor.transform)) return;
            RestoreMaterials();
            _inWorld = false;
        }

        private void LateUpdate()
        {
            // The HUD object the marker came from is gone, which would have taken the marker with it.
            if (_rect != null && _inWorld && _hadHudParent && _hudParent == null && _anchor != null && _rect.parent == _anchor.transform)
                Destroy(gameObject);
        }

        private void PlaceInternal(Vector3 worldPosition, bool inWorld)
        {
            if (_rect == null) _rect = transform;
            var camera = Enabled ? Fades.FullViewFade.ViewCamera() : null;
            if (!inWorld || camera == null)
            {
                if (_inWorld) MoveToHud();
                return;
            }

            if (!_inWorld || _rect.parent != _anchor.transform) MoveToWorld();
            UpdateMaterials();
            MirrorHudVisibility();
            Position(camera, worldPosition);
        }

        private void MoveToWorld()
        {
            if (_anchor == null) CreateAnchor();

            // The game (or its object pool) may have moved the marker since last time: remember where it is now.
            if (_rect.parent != _anchor.transform)
            {
                _hudParent = _rect.parent;
                _hudSiblingIndex = _rect.GetSiblingIndex();
                _hadHudParent = _hudParent != null;
            }

            if (_hudParent != null && _anchor.scene != _hudParent.gameObject.scene)
            {
                if (_hudParent.gameObject.scene.name == "DontDestroyOnLoad") DontDestroyOnLoad(_anchor);
                else SceneManager.MoveGameObjectToScene(_anchor, _hudParent.gameObject.scene);
            }

            _rect.SetParent(_anchor.transform, false);
            _inWorld = true;
        }

        private void MoveToHud()
        {
            RestoreMaterials();
            _inWorld = false;
            if (_anchor == null || _rect.parent != _anchor.transform) return;

            // This frame the game already placed the marker for the HUD, in screen coordinates; keep that.
            var screenPosition = _rect.position;
            _rect.SetParent(_hudParent, false);
            if (_hudParent != null) _rect.SetSiblingIndex(Mathf.Min(_hudSiblingIndex, _hudParent.childCount - 1));
            _rect.position = screenPosition;
            _rect.localRotation = Quaternion.identity;
        }

        private void CreateAnchor()
        {
            _anchor = new GameObject($"SBG VR marker ({name})", typeof(RectTransform));
            _anchor.hideFlags = HideFlags.DontSave;
            _canvas = _anchor.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = CanvasSortingOrder;
            _canvasGroup = _anchor.AddComponent<CanvasGroup>();
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            ((RectTransform)_anchor.transform).sizeDelta = Vector2.zero;
        }

        /// <summary>
        /// Copies what the HUD around the marker does to it: hidden when the HUD object is inactive or its canvas
        /// is off, and faded by the HUD's canvas groups (the game hides and fades whole HUD sections that way).
        /// </summary>
        private void MirrorHudVisibility()
        {
            var visible = _hudParent != null && _hudParent.gameObject.activeInHierarchy;
            var alpha = 1f;
            if (visible)
            {
                for (var current = _hudParent; current != null; current = current.parent)
                {
                    var canvas = current.GetComponent<Canvas>();
                    if (canvas != null && !canvas.enabled)
                    {
                        visible = false;
                        break;
                    }

                    var group = current.GetComponent<CanvasGroup>();
                    if (group == null || !group.enabled) continue;
                    alpha *= group.alpha;
                    if (group.ignoreParentGroups) break;
                }
            }

            _canvas.enabled = visible;
            _canvasGroup.alpha = alpha;
        }

        private void Position(Camera camera, Vector3 worldPosition)
        {
            var cameraTransform = camera.transform;
            var toTarget = worldPosition - cameraTransform.position;
            var distance = toTarget.magnitude;
            if (distance < 0.0001f) return;

            var direction = toTarget / distance;
            // Keep it between the clip planes; past them only its depth would change, not where it appears.
            distance = Mathf.Clamp(distance, camera.nearClipPlane * 2f, camera.farClipPlane * 0.9f);

            _anchor.layer = LayerHelper.GetVrUiLayer();
            _anchor.transform.SetPositionAndRotation(
                cameraTransform.position + direction * distance,
                Quaternion.LookRotation(direction, cameraTransform.up));
            _anchor.transform.localScale = Vector3.one * (MetresPerHudUnitAtOneMetre() * distance);

            _rect.localPosition = Vector3.zero;
            _rect.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// The size of one HUD canvas unit, in metres, for a marker 1 m away, so it covers the same angle as it
        /// did on UUVR's floating screen (which is 1.8 m x scale wide at its configured distance and shows the
        /// game window's full width).
        /// </summary>
        private static float MetresPerHudUnitAtOneMetre()
        {
            var config = ModConfiguration.Instance;
            var screenWidthMetres = FloatingScreenWidthPerScale * config.VrUiScale.Value;
            var screenDistance = Mathf.Max(0.1f, Mathf.Abs(config.VrUiPosition.Value.z));
            var hudScale = GameplayScreenspaceUiCanvas.Canvas != null ? GameplayScreenspaceUiCanvas.Canvas.scaleFactor : 1f;
            return hudScale * screenWidthMetres / Mathf.Max(1, Screen.width) / screenDistance * Settings.MarkerScale.Value;
        }

        /// <summary>
        /// World-space UI is depth tested, so scenery would hide markers that the game draws over everything.
        /// With MarkersOnTop the marker's graphics use a copy of the UI material that ignores depth.
        /// </summary>
        private void UpdateMaterials()
        {
            if (!Settings.MarkersOnTop.Value)
            {
                RestoreMaterials();
                return;
            }

            var graphics = GetComponentsInChildren<Graphic>(true);
            if (_materialsSwapped && graphics.Length == _graphicCount) return;

            RestoreMaterials();
            _graphicCount = graphics.Length;
            foreach (var graphic in graphics)
            {
                if (graphic is TMP_Text text)
                {
                    var original = text.fontSharedMaterial;
                    if (original == null) continue;
                    _originalFontMaterials.Add(new KeyValuePair<TMP_Text, Material>(text, original));
                    var onTop = new Material(original);
                    onTop.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
                    text.fontSharedMaterial = onTop;
                    continue;
                }

                // Only plain UI graphics; ones with their own shader (masks, effects) keep it.
                var material = graphic.material;
                if (material != null && material.shader != _onTopMaterial.shader) continue;
                _originalMaterials.Add(new KeyValuePair<Graphic, Material>(graphic, material));
                graphic.material = _onTopMaterial;
            }

            _materialsSwapped = true;
        }

        private void RestoreMaterials()
        {
            foreach (var pair in _originalMaterials)
            {
                if (pair.Key != null) pair.Key.material = pair.Value;
            }

            foreach (var pair in _originalFontMaterials)
            {
                if (pair.Key == null) continue;
                var onTop = pair.Key.fontSharedMaterial;
                pair.Key.fontSharedMaterial = pair.Value;
                if (onTop != null && onTop != pair.Value) Destroy(onTop);
            }

            _originalMaterials.Clear();
            _originalFontMaterials.Clear();
            _materialsSwapped = false;
            _graphicCount = 0;
        }
    }
}
