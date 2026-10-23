// SBG VR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SbgVr.Hud
{
    /// <summary>
    /// Where player characters appear in the view, for VR HUD.HideMarkersOverPlayers.
    ///
    /// Positions are in the camera's tangent space: a point's sideways and upward offsets divided by its
    /// distance ahead. A marker and a character overlap in view exactly when their tangent-space rectangles
    /// do, whichever way the head is turned. Each character is the set of its mesh renderers' bounding
    /// boxes, projected once per frame; a single box around the whole character would also cover the space
    /// around its arms and club.
    /// </summary>
    internal static class PlayerOverlap
    {
        private const float RendererRefreshSeconds = 1f;
        private const float MinimumDepth = 0.01f;

        private struct Area
        {
            internal PlayerInfo Player;
            internal Rect Rect;
        }

        private static readonly List<Area> Areas = new List<Area>();
        private static readonly List<PlayerInfo> Players = new List<PlayerInfo>();
        private static readonly Dictionary<PlayerInfo, List<Renderer>> RenderersByPlayer = new Dictionary<PlayerInfo, List<Renderer>>();
        private static readonly List<Renderer> Found = new List<Renderer>();
        private static readonly Vector3[] Corners = new Vector3[8];

        private static int _frame = -1;
        private static Camera _camera;
        private static float _nextRefresh;

        /// <summary>
        /// True when <paramref name="area"/> (tangent space of <paramref name="camera"/>) overlaps any player
        /// character other than <paramref name="owner"/>.
        /// </summary>
        internal static bool Covers(Camera camera, Rect area, PlayerInfo owner)
        {
            Update(camera);
            foreach (var playerArea in Areas)
            {
                if (playerArea.Player != owner && playerArea.Rect.Overlaps(area)) return true;
            }

            return false;
        }

        /// <summary>
        /// Adds a world-space point to a tangent-space rectangle. Returns false for a point at or behind the
        /// camera, which has no place on screen.
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

        private static void Update(Camera camera)
        {
            if (_frame == Time.frameCount && _camera == camera) return;
            _frame = Time.frameCount;
            _camera = camera;
            Areas.Clear();

            Players.Clear();
            if (GameManager.LocalPlayerInfo != null) Players.Add(GameManager.LocalPlayerInfo);
            var remotePlayers = GameManager.RemotePlayers;
            if (remotePlayers != null)
            {
                foreach (var player in remotePlayers)
                {
                    if (player != null) Players.Add(player);
                }
            }

            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + RendererRefreshSeconds;
                RenderersByPlayer.Clear();
            }

            var cameraTransform = camera.transform;
            foreach (var player in Players)
            {
                foreach (var renderer in RenderersOf(player))
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                        renderer.forceRenderingOff || renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly)
                        continue;

                    if (TryProject(cameraTransform, renderer.bounds, out var rect))
                        Areas.Add(new Area { Player = player, Rect = rect });
                }
            }
        }

        /// <summary>The character's meshes: its body, clothes and cosmetics, and what it holds. Not effects.</summary>
        private static List<Renderer> RenderersOf(PlayerInfo player)
        {
            if (RenderersByPlayer.TryGetValue(player, out var renderers)) return renderers;

            renderers = new List<Renderer>();
            player.GetComponentsInChildren(false, Found);
            foreach (var renderer in Found)
            {
                if (renderer is SkinnedMeshRenderer || renderer is MeshRenderer) renderers.Add(renderer);
            }

            Found.Clear();
            RenderersByPlayer[player] = renderers;
            return renderers;
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
                // A box reaching behind the camera can't be placed on screen; skip it rather than guess.
                if (!Encapsulate(camera, corner, ref rect, ref empty)) return false;
            }

            return !empty;
        }
    }
}
