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
    ///
    /// <see cref="MarkerOcclusion"/> tells each marker which player characters are in front of it; the
    /// marker skips the pixels those characters cover (marked in the stencil buffer).
    /// </summary>
    internal sealed class WorldMarker : MonoBehaviour
    {
        // Drawn just before UUVR's floating screen (its VR UI Render Queue, 5000 by default), so menus on the
        // floating screen cover markers the way the game's menus cover its HUD.
        private const int QueuesBeforeFloatingScreen = 10;
        private const float FloatingScreenWidthPerScale = 1.8f;

        /// <summary>Markers currently drawn in the world.</summary>
        internal static readonly List<WorldMarker> InWorld = new List<WorldMarker>();

        private static Material _onTopMaterial;

        private const string ZTestProperty = "unity_GUIZTestMode";

        // On-top materials shared by all markers: plain UI graphics by render queue, text by its font material
        // and render queue.
        private static readonly Dictionary<int, Material> UiMaterials = new Dictionary<int, Material>();
        private static readonly Dictionary<(Material, int), Material> FontMaterials = new Dictionary<(Material, int), Material>();

        // Copies of the materials markers are actually drawn with, by the drawn material, render queue and
        // covering stencil mask, and back to the material each copy was made from.
        private static readonly Dictionary<(Material, int, int), Material> DrawnCopies = new Dictionary<(Material, int, int), Material>();
        private static readonly Dictionary<Material, Material> DrawnCopySources = new Dictionary<Material, Material>();

        private int _renderQueue = -1;
        private int _coveringMask;
        private bool _isReticle;
        private int _lastPlacedFrame;
        private Canvas _hudCanvas;

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
        private readonly List<Graphic> _visibleGraphics = new List<Graphic>();
        private readonly Vector3[] _corners = new Vector3[4];

        /// <summary>The player the marker belongs to (a name tag's or teammate icon's), or null.</summary>
        internal PlayerInfo Owner { get; private set; }

        /// <summary>Where the marker is drawn this frame.</summary>
        internal Vector3 Position => _anchor != null ? _anchor.transform.position : transform.position;

        /// <summary>True when the marker is drawn over scenery and can be seen this frame.</summary>
        internal bool DrawnOnTop => _inWorld && _materialsSwapped && _canvas != null && _canvas.enabled &&
                                    _canvasGroup.alpha > 0f;

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
        /// <paramref name="owner"/> is the player the marker belongs to (a name tag's or teammate icon's), whose
        /// own character doesn't hide it. A <paramref name="reticle"/> keeps its size on the floating screen
        /// whatever VR HUD.MarkerScale is, and is held in a rectangle the size of its HUD canvas, so parts laid
        /// out against the screen's edges keep their places.
        /// </summary>
        internal static void Place(Component marker, Vector3 worldPosition, bool inWorld, PlayerInfo owner = null,
            bool reticle = false)
        {
            if (marker == null) return;

            var worldMarker = marker.GetComponent<WorldMarker>();
            if (worldMarker == null)
            {
                if (!inWorld) return;
                worldMarker = marker.gameObject.AddComponent<WorldMarker>();
            }

            worldMarker.Owner = owner;
            worldMarker._isReticle = reticle;
            worldMarker.PlaceInternal(worldPosition, inWorld);
        }

        private void OnDestroy()
        {
            InWorld.Remove(this);
            if (_anchor != null) Destroy(_anchor);
        }

        private void OnTransformParentChanged()
        {
            if (_rect == null) return;
            // The game took the marker away from us (back to its pool, usually): it's a HUD object again.
            if (!_inWorld || (_anchor != null && _rect.parent == _anchor.transform)) return;
            RestoreMaterials();
            _inWorld = false;
            InWorld.Remove(this);
        }

        private void LateUpdate()
        {
            // The HUD object the marker came from is gone, which would have taken the marker with it.
            if (_rect != null && _inWorld && _hadHudParent && _hudParent == null && _anchor != null && _rect.parent == _anchor.transform)
            {
                Destroy(gameObject);
                return;
            }

            // The game stopped laying the marker out (its owner went back to a pool, say): back to the HUD,
            // where it's hidden with its owner, rather than left showing in the world.
            if (_inWorld && Time.frameCount - _lastPlacedFrame > 1) MoveToHud();
        }

        private void PlaceInternal(Vector3 worldPosition, bool inWorld)
        {
            if (_rect == null) _rect = transform;
            _lastPlacedFrame = Time.frameCount;
            var camera = Enabled ? Fades.FullViewFade.ViewCamera() : null;
            if (!inWorld || camera == null)
            {
                if (_inWorld) MoveToHud();
                return;
            }

            if (!_inWorld || _rect.parent != _anchor.transform) MoveToWorld();
            UpdateMaterials();
            MirrorHudVisibility();
            PlaceAnchor(camera, worldPosition);
        }

        /// <summary>
        /// The stencil bits of the player characters in front of the marker this frame (0 for none); the
        /// marker isn't drawn where any of them is set. Takes effect in <see cref="EnforceDrawnMaterials"/>.
        /// </summary>
        internal void SetCoveringMask(int mask)
        {
            _coveringMask = mask;
        }

        /// <summary>
        /// Makes every part of the marker draw at its render queue, ignoring depth and skipping the pixels of
        /// the characters in front of it, by setting the material each canvas renderer actually draws with.
        /// Called once a frame after the UI has been rebuilt and before it's drawn.
        ///
        /// Setting the graphics' own materials isn't enough. What's drawn is the material after UI masks and
        /// mask plugins (SoftMask) have swapped in copies of their own, which they make once and keep; and the
        /// game can set a label's font material again at any time.
        /// </summary>
        internal void EnforceDrawnMaterials()
        {
            if (!_inWorld || !_materialsSwapped || _renderQueue < 0) return;

            GetComponentsInChildren(false, _visibleGraphics);
            foreach (var graphic in _visibleGraphics)
            {
                var canvasRenderer = graphic.canvasRenderer;
                if (canvasRenderer == null) continue;

                for (var i = 0; i < canvasRenderer.materialCount; i++)
                {
                    var drawn = canvasRenderer.GetMaterial(i);
                    if (drawn == null) continue;

                    var wanted = DrawnCopy(drawn, _renderQueue, _coveringMask);
                    if (wanted != drawn) canvasRenderer.SetMaterial(wanted, i);
                }
            }

            _visibleGraphics.Clear();
        }

        /// <summary>
        /// A copy of the material <paramref name="drawn"/> was made from that ignores depth, draws at
        /// <paramref name="renderQueue"/> and, with a non-zero <paramref name="coveringMask"/>, only where none
        /// of those stencil bits is set. Masks (UI masks also use the stencil buffer) are left as they are when
        /// nothing covers the marker.
        /// </summary>
        private static Material DrawnCopy(Material drawn, int renderQueue, int coveringMask)
        {
            var source = DrawnCopySources.TryGetValue(drawn, out var original) && original != null ? original : drawn;
            if (DrawnCopies.TryGetValue((source, renderQueue, coveringMask), out var copy) && copy != null) return copy;

            copy = new Material(source) { name = $"{source.name} (SBG VR marker {renderQueue}/{coveringMask})" };
            if (copy.HasProperty(ZTestProperty)) copy.SetInt(ZTestProperty, (int)CompareFunction.Always);
            if (coveringMask != 0 && copy.HasProperty("_StencilComp"))
            {
                copy.SetInt("_Stencil", 0);
                copy.SetInt("_StencilComp", (int)CompareFunction.Equal);
                copy.SetInt("_StencilReadMask", coveringMask);
                copy.SetInt("_StencilOp", (int)StencilOp.Keep);
                copy.SetInt("_StencilWriteMask", 0);
            }

            copy.renderQueue = renderQueue;
            DrawnCopies[(source, renderQueue, coveringMask)] = copy;
            DrawnCopySources[copy] = source;
            return copy;
        }

        private void ApplyRenderQueue()
        {
            var uiMaterial = UiMaterial(_renderQueue);
            foreach (var pair in _originalMaterials)
            {
                if (pair.Key != null) pair.Key.material = uiMaterial;
            }

            foreach (var pair in _originalFontMaterials)
            {
                if (pair.Key != null) pair.Key.fontSharedMaterial = FontMaterial(pair.Value, _renderQueue);
            }
        }

        private static Material UiMaterial(int renderQueue)
        {
            if (UiMaterials.TryGetValue(renderQueue, out var material) && material != null) return material;

            material = new Material(_onTopMaterial) { name = $"SBG VR marker ({renderQueue})", renderQueue = renderQueue };
            UiMaterials[renderQueue] = material;
            return material;
        }

        private static Material FontMaterial(Material original, int renderQueue)
        {
            if (FontMaterials.TryGetValue((original, renderQueue), out var material) && material != null) return material;

            material = new Material(original) { name = $"{original.name} (SBG VR marker {renderQueue})" };
            material.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            material.renderQueue = renderQueue;
            FontMaterials[(original, renderQueue)] = material;
            return material;
        }

        /// <summary>
        /// The world corners of what the graphic draws: its rectangle, or for text the text itself, since a
        /// label's rectangle is usually much wider than its words.
        /// </summary>
        private bool GetDrawnCorners(Graphic graphic)
        {
            var rectTransform = graphic.rectTransform;
            if (!(graphic is TMP_Text text))
            {
                rectTransform.GetWorldCorners(_corners);
                return true;
            }

            var bounds = text.textBounds;
            if (bounds.size.x <= 0f || bounds.size.y <= 0f) return false;
            var min = bounds.min;
            var max = bounds.max;
            _corners[0] = rectTransform.TransformPoint(new Vector3(min.x, min.y, 0f));
            _corners[1] = rectTransform.TransformPoint(new Vector3(min.x, max.y, 0f));
            _corners[2] = rectTransform.TransformPoint(new Vector3(max.x, max.y, 0f));
            _corners[3] = rectTransform.TransformPoint(new Vector3(max.x, min.y, 0f));
            return true;
        }

        /// <summary>
        /// The area the marker covers in view, in the camera's tangent space (see <see cref="PlayerOverlap"/>):
        /// the union of its visible graphics. False when it covers nothing or reaches behind the camera.
        /// </summary>
        internal bool TryGetArea(Transform cameraTransform, out Rect area)
        {
            area = default;
            var empty = true;
            GetComponentsInChildren(false, _visibleGraphics);
            foreach (var graphic in _visibleGraphics)
            {
                if (!graphic.enabled || graphic.color.a <= 0f) continue;
                if (!GetDrawnCorners(graphic)) continue;
                foreach (var corner in _corners)
                {
                    if (!PlayerOverlap.Encapsulate(cameraTransform, corner, ref area, ref empty))
                    {
                        _visibleGraphics.Clear();
                        return false;
                    }
                }
            }

            _visibleGraphics.Clear();
            return !empty;
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
                var canvas = _hudParent != null ? _hudParent.GetComponentInParent<Canvas>(true) : null;
                _hudCanvas = canvas != null ? canvas.rootCanvas : null;
            }

            if (_hudParent != null && _anchor.scene != _hudParent.gameObject.scene)
            {
                if (_hudParent.gameObject.scene.name == "DontDestroyOnLoad") DontDestroyOnLoad(_anchor);
                else SceneManager.MoveGameObjectToScene(_anchor, _hudParent.gameObject.scene);
            }

            _rect.SetParent(_anchor.transform, false);
            _inWorld = true;
            if (!InWorld.Contains(this)) InWorld.Add(this);
        }

        private void MoveToHud()
        {
            RestoreMaterials();
            _inWorld = false;
            InWorld.Remove(this);
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
            // Left at sorting order 0, like the floating screen: Unity sorts transparent objects by sorting
            // order before render queue, so a higher order would draw markers over the floating screen.
            _canvas.sortingOrder = 0;
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

        private void PlaceAnchor(Camera camera, Vector3 worldPosition)
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
            ((RectTransform)_anchor.transform).sizeDelta = _isReticle && _hudCanvas != null
                ? ((RectTransform)_hudCanvas.transform).rect.size
                : Vector2.zero;

            _rect.localPosition = Vector3.zero;
            _rect.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// The size of one HUD canvas unit, in metres, for a marker 1 m away, so it covers the same angle as it
        /// did on UUVR's floating screen (which is 1.8 m x scale wide at its configured distance and shows the
        /// game window's full width).
        /// </summary>
        private float MetresPerHudUnitAtOneMetre()
        {
            var config = ModConfiguration.Instance;
            var screenWidthMetres = FloatingScreenWidthPerScale * config.VrUiScale.Value;
            var screenDistance = Mathf.Max(0.1f, Mathf.Abs(config.VrUiPosition.Value.z));
            // The scale of the canvas the marker came from: one canvas unit is that many screen pixels.
            var canvas = _hudCanvas != null ? _hudCanvas : GameplayScreenspaceUiCanvas.Canvas;
            var hudScale = canvas != null ? canvas.scaleFactor : 1f;
            var scale = _isReticle ? 1f : Settings.MarkerScale.Value;
            return hudScale * screenWidthMetres / Mathf.Max(1, Screen.width) / screenDistance * scale;
        }

        /// <summary>
        /// World-space UI is depth tested, so scenery would hide markers that the game draws over everything.
        /// With MarkersOnTop the marker's graphics use copies of their materials that ignore depth, drawn after
        /// the scene but before the floating screen.
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
                    if (original != null) _originalFontMaterials.Add(new KeyValuePair<TMP_Text, Material>(text, original));
                    continue;
                }

                // Only plain UI graphics; ones with their own shader (effects) keep it.
                var material = graphic.material;
                if (material != null && material.shader != _onTopMaterial.shader) continue;
                _originalMaterials.Add(new KeyValuePair<Graphic, Material>(graphic, material));
            }

            _materialsSwapped = true;
            _renderQueue = TopRenderQueue;
            ApplyRenderQueue();
        }

        /// <summary>
        /// Where markers drawn over scenery go in the drawing order: after the scene and its effects, just
        /// before UUVR's floating screen.
        /// </summary>
        internal static int TopRenderQueue
        {
            get
            {
                var floatingScreenQueue = ModConfiguration.Instance.VrUiRenderQueue.Value;
                return Mathf.Max((int)RenderQueue.Transparent + 1, floatingScreenQueue - QueuesBeforeFloatingScreen);
            }
        }

        private void RestoreMaterials()
        {
            foreach (var pair in _originalMaterials)
            {
                if (pair.Key != null) pair.Key.material = pair.Value;
            }

            foreach (var pair in _originalFontMaterials)
            {
                if (pair.Key != null) pair.Key.fontSharedMaterial = pair.Value;
            }

            _originalMaterials.Clear();
            _originalFontMaterials.Clear();
            _materialsSwapped = false;
            _graphicCount = 0;
            _renderQueue = -1;
        }
    }
}
