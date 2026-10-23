// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SBGVR.Hud
{
    /// <summary>
    /// Marks where one player character is visible, so markers behind it can leave those pixels alone.
    ///
    /// The character's meshes are drawn a second time, after the scene's solid objects and before the
    /// markers, with a material that draws no colour at all: it only sets one bit of the stencil buffer
    /// (<see cref="MarkerOcclusion"/> picks the bit) wherever the character itself is visible. It's depth
    /// tested against the character's own depth from the first time it was drawn, so scenery in front of
    /// the character keeps its pixels unmarked. Markers behind the character then skip pixels with its bit.
    ///
    /// Nothing about how the character looks changes: its colours, face, effects and shield are drawn once,
    /// as usual. Skinned meshes get a twin SkinnedMeshRenderer that shares the original's mesh and bones, so
    /// it poses identically; other meshes are drawn with Graphics.RenderMesh at the original's exact matrix.
    /// </summary>
    internal sealed class PlayerOccluder
    {
        /// <summary>Drawn at the start of the transparent objects: after every solid object, before markers.</summary>
        private const int MarkingRenderQueue = (int)RenderQueue.Transparent - 1;

        private static Transform _root;
        private static readonly Dictionary<int, Material> MarkingMaterials = new Dictionary<int, Material>();
        private static readonly List<Material> Materials = new List<Material>();

        private readonly Dictionary<Renderer, SkinnedMeshRenderer> _twins = new Dictionary<Renderer, SkinnedMeshRenderer>();
        private readonly List<Renderer> _gone = new List<Renderer>();

        /// <summary>Marks the character's visible pixels with <paramref name="stencilBit"/> this frame.</summary>
        internal void Draw(PlayerOverlap.PlayerView view, int stencilBit)
        {
            var material = MarkingMaterial(stencilBit);
            foreach (var renderer in view.Renderers)
            {
                var drawn = PlayerOverlap.IsDrawn(renderer);
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    var twin = Twin(skinned);
                    if (twin == null) continue;
                    twin.enabled = drawn;
                    if (drawn) Sync(skinned, twin, material);
                }
                else if (drawn && renderer is MeshRenderer meshRenderer)
                {
                    DrawMesh(meshRenderer, material);
                }
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

        /// <summary>Stops marking the character.</summary>
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

            _twins.Clear();
        }

        /// <summary>
        /// A material that writes <paramref name="stencilBit"/> where it passes the depth test and draws no
        /// colour. It's the UI shader: it exposes colour mask, depth test and stencil as properties, which the
        /// game's character shaders don't, and it draws any mesh.
        /// </summary>
        private static Material MarkingMaterial(int stencilBit)
        {
            if (MarkingMaterials.TryGetValue(stencilBit, out var material) && material != null) return material;

            material = new Material(Canvas.GetDefaultCanvasMaterial())
            {
                name = $"SBGVR player marking ({stencilBit})",
                renderQueue = MarkingRenderQueue,
            };
            material.SetInt("_ColorMask", 0);
            material.SetInt("unity_GUIZTestMode", (int)CompareFunction.LessEqual);
            material.SetInt("_Stencil", stencilBit);
            material.SetInt("_StencilComp", (int)CompareFunction.Always);
            material.SetInt("_StencilOp", (int)StencilOp.Replace);
            material.SetInt("_StencilWriteMask", stencilBit);
            material.SetInt("_StencilReadMask", 255);
            MarkingMaterials[stencilBit] = material;
            return material;
        }

        private SkinnedMeshRenderer Twin(SkinnedMeshRenderer original)
        {
            if (_twins.TryGetValue(original, out var twin) && twin != null) return twin;
            if (original.sharedMesh == null) return null;

            if (_root == null)
            {
                var root = new GameObject("SBGVR player marking") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(root);
                _root = root.transform;
            }

            var gameObject = new GameObject($"Marking ({original.name})") { hideFlags = HideFlags.HideAndDontSave };
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
            twin.receiveShadows = false;
            twin.lightProbeUsage = LightProbeUsage.Off;
            twin.reflectionProbeUsage = ReflectionProbeUsage.Off;
            twin.allowOcclusionWhenDynamic = false;
            twin.enabled = false;
            _twins[original] = twin;
            return twin;
        }

        /// <summary>Keeps a twin posed like its original, with the marking material on every part.</summary>
        private static void Sync(SkinnedMeshRenderer original, SkinnedMeshRenderer twin, Material material)
        {
            twin.gameObject.layer = original.gameObject.layer;

            if (twin.sharedMesh != original.sharedMesh)
            {
                twin.sharedMesh = original.sharedMesh;
                twin.bones = original.bones;
                twin.rootBone = original.rootBone;
                twin.localBounds = original.localBounds;
            }

            var mesh = original.sharedMesh;
            var parts = mesh != null ? mesh.subMeshCount : 0;
            var twinMaterials = twin.sharedMaterials;
            var changed = twinMaterials.Length != parts;
            for (var i = 0; !changed && i < parts; i++) changed = twinMaterials[i] != material;
            if (changed)
            {
                var materials = new Material[parts];
                for (var i = 0; i < parts; i++) materials[i] = material;
                twin.sharedMaterials = materials;
            }

            for (var i = 0; mesh != null && i < mesh.blendShapeCount; i++)
                twin.SetBlendShapeWeight(i, original.GetBlendShapeWeight(i));
        }

        private static void DrawMesh(MeshRenderer original, Material material)
        {
            var filter = original.GetComponent<MeshFilter>();
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null) return;

            // Only the parts the original draws (one per material).
            original.GetSharedMaterials(Materials);
            var parts = Mathf.Min(mesh.subMeshCount, Materials.Count);
            Materials.Clear();

            var parameters = new RenderParams(material)
            {
                layer = original.gameObject.layer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                lightProbeUsage = LightProbeUsage.Off,
                reflectionProbeUsage = ReflectionProbeUsage.Off,
                motionVectorMode = MotionVectorGenerationMode.ForceNoMotion,
                worldBounds = original.bounds,
            };
            var matrix = original.localToWorldMatrix;
            for (var i = 0; i < parts; i++) Graphics.RenderMesh(parameters, mesh, i, matrix);
        }
    }
}
