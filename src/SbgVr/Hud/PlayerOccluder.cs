// SBG VR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SbgVr.Hud
{
    /// <summary>
    /// Draws one player character a second time, late in the frame, so it covers markers behind it.
    ///
    /// Markers are drawn over scenery, after the scene. To let a character in front of a marker cover it, the
    /// character's meshes are drawn again after that marker, with the same materials, at a later render
    /// queue. The copy is depth tested against the character's own depth from the first time it was drawn,
    /// so it only shows where the character itself is visible: scenery in front of the character still
    /// covers it, and nothing changes where no marker is behind it.
    ///
    /// Skinned meshes get a twin SkinnedMeshRenderer that shares the original's mesh and bones, so it poses
    /// identically; other meshes are drawn with Graphics.RenderMesh at the original's exact matrix. Both use
    /// copies of the original materials whose render queue <see cref="Draw"/> sets.
    /// </summary>
    internal sealed class PlayerOccluder
    {
        private static Transform _root;
        private static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
        private static readonly List<Material> Materials = new List<Material>();

        private readonly Dictionary<Renderer, SkinnedMeshRenderer> _twins = new Dictionary<Renderer, SkinnedMeshRenderer>();
        private readonly Dictionary<Material, Material> _copies = new Dictionary<Material, Material>();
        private readonly Dictionary<Material, Material> _seeThroughCopies = new Dictionary<Material, Material>();
        private readonly Dictionary<Material, int> _copiedFrame = new Dictionary<Material, int>();
        private readonly List<Renderer> _gone = new List<Renderer>();

        // See-through parts switched to later-drawn copies for this frame, with the materials to put back.
        private static readonly List<KeyValuePair<Renderer, Material[]>> Swapped = new List<KeyValuePair<Renderer, Material[]>>();
        private int _renderQueue = -1;

        /// <summary>Draws the character's visible meshes again at <paramref name="renderQueue"/> this frame.</summary>
        internal void Draw(PlayerOverlap.PlayerView view, int renderQueue)
        {
            if (renderQueue != _renderQueue)
            {
                _renderQueue = renderQueue;
                foreach (var copy in _copies.Values) copy.renderQueue = renderQueue;
            }

            foreach (var renderer in view.Renderers)
            {
                var drawn = PlayerOverlap.IsDrawn(renderer);
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    var twin = Twin(skinned);
                    if (twin == null) continue;
                    twin.enabled = drawn;
                    if (drawn) Sync(skinned, twin);
                }
                else if (drawn && renderer is MeshRenderer meshRenderer)
                {
                    DrawMesh(meshRenderer);
                }
            }

            // The character's see-through parts (a shield on its chest, say) are drawn after solid objects.
            // Redrawn, the character would cover them, so they're drawn after it too: their renderers draw
            // with copies of their materials one queue later, for this frame only.
            foreach (var renderer in view.SeeThrough)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    renderer.forceRenderingOff)
                    continue;

                var originals = renderer.sharedMaterials;
                var copies = new Material[originals.Length];
                for (var i = 0; i < originals.Length; i++) copies[i] = Copy(originals[i], renderQueue + 1);
                renderer.sharedMaterials = copies;
                Swapped.Add(new KeyValuePair<Renderer, Material[]>(renderer, originals));
            }

            // Twins of meshes the character no longer has.
            foreach (var pair in _twins)
            {
                if (pair.Key == null || !view.Renderers.Contains(pair.Key)) _gone.Add(pair.Key);
            }

            foreach (var renderer in _gone)
            {
                if (_twins.TryGetValue(renderer, out var twin) && twin != null) Object.Destroy(twin.gameObject);
                _twins.Remove(renderer);
            }

            _gone.Clear();
        }

        /// <summary>Puts back the see-through parts' own materials once the frame has been drawn.</summary>
        internal static void RestoreSeeThrough()
        {
            foreach (var pair in Swapped)
            {
                if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
            }

            Swapped.Clear();
        }

        /// <summary>Stops drawing the character again.</summary>
        internal void Hide()
        {
            foreach (var twin in _twins.Values)
            {
                if (twin != null && twin.enabled) twin.enabled = false;
            }
        }

        internal void Dispose()
        {
            foreach (var twin in _twins.Values)
            {
                if (twin != null) Object.Destroy(twin.gameObject);
            }

            foreach (var copy in _copies.Values)
            {
                if (copy != null) Object.Destroy(copy);
            }

            foreach (var copy in _seeThroughCopies.Values)
            {
                if (copy != null) Object.Destroy(copy);
            }

            _seeThroughCopies.Clear();

            _twins.Clear();
            _copies.Clear();
            _copiedFrame.Clear();
        }

        private SkinnedMeshRenderer Twin(SkinnedMeshRenderer original)
        {
            if (_twins.TryGetValue(original, out var twin) && twin != null) return twin;
            if (original.sharedMesh == null) return null;

            if (_root == null)
            {
                var root = new GameObject("SBG VR player occluders") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(root);
                _root = root.transform;
            }

            var gameObject = new GameObject($"Occluder ({original.name})") { hideFlags = HideFlags.HideAndDontSave };
            gameObject.transform.SetParent(_root, false);
            twin = gameObject.AddComponent<SkinnedMeshRenderer>();
            twin.sharedMesh = original.sharedMesh;
            twin.bones = original.bones;
            twin.rootBone = original.rootBone;
            twin.localBounds = original.localBounds;
            twin.quality = original.quality;
            twin.updateWhenOffscreen = original.updateWhenOffscreen;
            twin.skinnedMotionVectors = false;
            twin.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            twin.shadowCastingMode = ShadowCastingMode.Off;
            twin.allowOcclusionWhenDynamic = false;
            twin.enabled = false;
            _twins[original] = twin;
            return twin;
        }

        /// <summary>Keeps a twin looking like its original: layer, lighting, materials, blend shapes.</summary>
        private void Sync(SkinnedMeshRenderer original, SkinnedMeshRenderer twin)
        {
            twin.gameObject.layer = original.gameObject.layer;
            twin.renderingLayerMask = original.renderingLayerMask;
            twin.receiveShadows = original.receiveShadows;
            twin.lightProbeUsage = original.lightProbeUsage;
            twin.reflectionProbeUsage = original.reflectionProbeUsage;
            twin.probeAnchor = original.probeAnchor != null ? original.probeAnchor : original.rootBone;

            if (twin.sharedMesh != original.sharedMesh)
            {
                twin.sharedMesh = original.sharedMesh;
                twin.bones = original.bones;
                twin.rootBone = original.rootBone;
                twin.localBounds = original.localBounds;
            }

            original.GetSharedMaterials(Materials);
            var twinMaterials = twin.sharedMaterials;
            var changed = twinMaterials.Length != Materials.Count;
            for (var i = 0; !changed && i < Materials.Count; i++) changed = twinMaterials[i] != Copy(Materials[i]);
            if (changed)
            {
                var copies = new Material[Materials.Count];
                for (var i = 0; i < copies.Length; i++) copies[i] = Copy(Materials[i]);
                twin.sharedMaterials = copies;
            }

            // Per-player colours and the like are often set through property blocks rather than materials.
            if (original.HasPropertyBlock())
            {
                original.GetPropertyBlock(Block);
                twin.SetPropertyBlock(Block);
            }
            else
            {
                twin.SetPropertyBlock(null);
            }

            for (var i = 0; i < Materials.Count; i++)
            {
                original.GetPropertyBlock(Block, i);
                if (!Block.isEmpty) twin.SetPropertyBlock(Block, i);
            }

            var mesh = original.sharedMesh;
            for (var i = 0; mesh != null && i < mesh.blendShapeCount; i++)
                twin.SetBlendShapeWeight(i, original.GetBlendShapeWeight(i));

            Materials.Clear();
        }

        private void DrawMesh(MeshRenderer original)
        {
            var filter = original.GetComponent<MeshFilter>();
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null) return;

            original.GetSharedMaterials(Materials);
            var hasBlock = original.HasPropertyBlock();
            if (hasBlock) original.GetPropertyBlock(Block);

            var matrix = original.localToWorldMatrix;
            var submeshes = Mathf.Min(mesh.subMeshCount, Materials.Count);
            for (var i = 0; i < submeshes; i++)
            {
                var material = Copy(Materials[i]);
                if (material == null) continue;

                var parameters = new RenderParams(material)
                {
                    layer = original.gameObject.layer,
                    renderingLayerMask = original.renderingLayerMask,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = original.receiveShadows,
                    lightProbeUsage = original.lightProbeUsage,
                    reflectionProbeUsage = original.reflectionProbeUsage,
                    motionVectorMode = MotionVectorGenerationMode.ForceNoMotion,
                    worldBounds = original.bounds,
                    matProps = hasBlock ? Block : null,
                };
                Graphics.RenderMesh(parameters, mesh, i, matrix);
            }

            Materials.Clear();
        }

        /// <summary>
        /// The copy of an original material drawn at this character's render queue, brought up to date with
        /// the original once a frame (the game changes some, for hit flashes and the like).
        /// </summary>
        private Material Copy(Material original)
        {
            return CopyAt(original, _renderQueue, _copies);
        }

        private Material Copy(Material original, int renderQueue)
        {
            return CopyAt(original, renderQueue, _seeThroughCopies);
        }

        private Material CopyAt(Material original, int renderQueue, Dictionary<Material, Material> copies)
        {
            if (original == null) return null;

            if (!copies.TryGetValue(original, out var copy) || copy == null)
            {
                copy = new Material(original) { name = $"{original.name} (SBG VR occluder)" };
                copies[original] = copy;
            }
            else if (!_copiedFrame.TryGetValue(copy, out var frame) || frame != Time.frameCount)
            {
                if (copy.shader != original.shader) copy.shader = original.shader;
                copy.CopyPropertiesFromMaterial(original);
            }

            _copiedFrame[copy] = Time.frameCount;
            copy.renderQueue = renderQueue;
            return copy;
        }
    }
}
