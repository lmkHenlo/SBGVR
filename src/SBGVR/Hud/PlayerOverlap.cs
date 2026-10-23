// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SBGVR.Hud
{
    /// <summary>
    /// Where the player characters are in view, for <see cref="MarkerOcclusion"/>.
    ///
    /// Areas are in the camera's tangent space: a point's sideways and upward offsets divided by its distance
    /// ahead. A marker and a character overlap in view exactly when their tangent-space areas do, whichever
    /// way the head is turned. A character's area is the set of its meshes' bounding boxes; a single box
    /// around the whole character would also cover the space between its arms and club.
    /// </summary>
    internal static class PlayerOverlap
    {
        private const float RendererRefreshSeconds = 1f;
        private const float MinimumDepth = 0.01f;

        internal sealed class PlayerView
        {
            internal PlayerInfo Player;

            /// <summary>
            /// The character's solid meshes: body, clothes, cosmetics and what it holds. Not effects or
            /// see-through parts.
            /// </summary>
            internal readonly List<Renderer> Renderers = new List<Renderer>();

            /// <summary>The visible meshes' areas in view, this frame.</summary>
            internal readonly List<Rect> Areas = new List<Rect>();

            /// <summary>Distance from the camera to the middle of the character, this frame.</summary>
            internal float Distance;

            internal bool Overlaps(Rect area)
            {
                foreach (var rect in Areas)
                {
                    if (rect.Overlaps(area)) return true;
                }

                return false;
            }
        }

        private static readonly Dictionary<PlayerInfo, PlayerView> Views = new Dictionary<PlayerInfo, PlayerView>();
        private static readonly List<PlayerView> Visible = new List<PlayerView>();
        private static readonly List<PlayerInfo> Gone = new List<PlayerInfo>();
        private static readonly List<Renderer> Found = new List<Renderer>();
        private static readonly Vector3[] Corners = new Vector3[8];
        private static float _nextRefresh;

        /// <summary>
        /// Every player character with a visible mesh in front of <paramref name="camera"/>, with its areas and
        /// distance for this frame.
        /// </summary>
        internal static List<PlayerView> Update(Camera camera)
        {
            Visible.Clear();

            var refresh = Time.unscaledTime >= _nextRefresh;
            if (refresh) _nextRefresh = Time.unscaledTime + RendererRefreshSeconds;

            foreach (var player in Views.Keys)
            {
                if (player == null) Gone.Add(player);
            }

            foreach (var player in Gone) Views.Remove(player);
            Gone.Clear();

            Consider(camera, GameManager.LocalPlayerInfo, refresh);
            var remotePlayers = GameManager.RemotePlayers;
            if (remotePlayers != null)
            {
                foreach (var player in remotePlayers) Consider(camera, player, refresh);
            }

            return Visible;
        }

        private static void Consider(Camera camera, PlayerInfo player, bool refresh)
        {
            if (player == null) return;

            if (!Views.TryGetValue(player, out var view))
            {
                view = new PlayerView { Player = player };
                Views[player] = view;
                refresh = true;
            }

            if (refresh) FindRenderers(view);

            view.Areas.Clear();
            var cameraTransform = camera.transform;
            var bounds = default(Bounds);
            var hasBounds = false;
            foreach (var renderer in view.Renderers)
            {
                if (!IsDrawn(renderer)) continue;

                var rendererBounds = renderer.bounds;
                if (!TryProject(cameraTransform, rendererBounds, out var rect)) continue;

                view.Areas.Add(rect);
                if (hasBounds) bounds.Encapsulate(rendererBounds);
                else bounds = rendererBounds;
                hasBounds = true;
            }

            if (!hasBounds) return;
            view.Distance = Vector3.Distance(cameraTransform.position, bounds.center);
            Visible.Add(view);
        }

        internal static bool IsDrawn(Renderer renderer)
        {
            return renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy &&
                   !renderer.forceRenderingOff && renderer.shadowCastingMode != ShadowCastingMode.ShadowsOnly;
        }

        private static void FindRenderers(PlayerView view)
        {
            view.Renderers.Clear();
            // Inactive ones too: cosmetics can be switched on later, and are checked when drawn.
            view.Player.GetComponentsInChildren(true, Found);
            foreach (var renderer in Found)
            {
                // See-through parts (shields, glows, effects) don't hide what's behind them.
                var material = renderer.sharedMaterial;
                if (material != null && material.renderQueue > (int)RenderQueue.GeometryLast) continue;

                if (renderer is SkinnedMeshRenderer || renderer is MeshRenderer) view.Renderers.Add(renderer);
            }

            Found.Clear();
        }

        /// <summary>
        /// Adds a world-space point to a tangent-space area. Returns false for a point at or behind the camera,
        /// which has no place on screen.
        /// </summary>
        internal static bool Encapsulate(Transform camera, Vector3 worldPoint, ref Rect rect, ref bool empty)
        {
            var local = camera.InverseTransformPoint(worldPoint);
            if (local.z <= MinimumDepth) return false;

            var point = new Vector2(local.x / local.z, local.y / local.z);
            if (empty)
            {
                rect = new Rect(point, Vector2.zero);
                empty = false;
            }
            else
            {
                rect.xMin = Mathf.Min(rect.xMin, point.x);
                rect.yMin = Mathf.Min(rect.yMin, point.y);
                rect.xMax = Mathf.Max(rect.xMax, point.x);
                rect.yMax = Mathf.Max(rect.yMax, point.y);
            }

            return true;
        }

        private static bool TryProject(Transform camera, Bounds bounds, out Rect rect)
        {
            var min = bounds.min;
            var max = bounds.max;
            Corners[0] = new Vector3(min.x, min.y, min.z);
            Corners[1] = new Vector3(max.x, min.y, min.z);
            Corners[2] = new Vector3(min.x, max.y, min.z);
            Corners[3] = new Vector3(max.x, max.y, min.z);
            Corners[4] = new Vector3(min.x, min.y, max.z);
            Corners[5] = new Vector3(max.x, min.y, max.z);
            Corners[6] = new Vector3(min.x, max.y, max.z);
            Corners[7] = new Vector3(max.x, max.y, max.z);

            rect = default;
            var empty = true;
            foreach (var corner in Corners)
            {
                // A box reaching behind the camera can't be placed on screen; leave it out rather than guess.
                if (!Encapsulate(camera, corner, ref rect, ref empty)) return false;
            }

            return !empty;
        }
    }
}
