// SBG VR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System.Collections.Generic;
using UnityEngine;

namespace SbgVr.Hud
{
    /// <summary>
    /// VR HUD.PlayersCoverMarkers: player characters cover the markers behind them, while scenery still
    /// doesn't (VR HUD.MarkersOnTop).
    ///
    /// Markers ignore depth so scenery can't hide them, which means nothing can: they're drawn after the
    /// scene. So each frame, just before rendering, the markers and the characters in front of them are
    /// put in drawing order from far to near, like a painter, using render queues just below the markers'
    /// usual one: a marker is drawn, then any character nearer than it is drawn again over it
    /// (<see cref="PlayerOccluder"/>), then nearer markers, and so on. Only characters that are in front of a
    /// marker they overlap in view are drawn again; the rest of the frame is unchanged.
    ///
    /// A name tag or teammate icon isn't covered by the player it belongs to.
    /// </summary>
    internal static class MarkerOcclusion
    {
        /// <summary>Render queues used below the markers' usual one, one per marker or character in order.</summary>
        internal const int QueueSlots = 80;

        private struct MarkerInView
        {
            internal WorldMarker Marker;
            internal Rect Area;
            internal float Distance;
        }

        private struct Item
        {
            internal float Distance;
            internal WorldMarker Marker;
            internal PlayerOverlap.PlayerView Player;
        }

        private static readonly Dictionary<PlayerInfo, PlayerOccluder> Occluders = new Dictionary<PlayerInfo, PlayerOccluder>();
        private static readonly List<MarkerInView> Markers = new List<MarkerInView>();
        private static readonly List<Item> Items = new List<Item>();
        private static readonly HashSet<WorldMarker> CoveredMarkers = new HashSet<WorldMarker>();
        private static readonly HashSet<PlayerOverlap.PlayerView> CoveringPlayers = new HashSet<PlayerOverlap.PlayerView>();
        private static readonly HashSet<PlayerInfo> Drawn = new HashSet<PlayerInfo>();
        private static readonly Dictionary<WorldMarker, int> Queues = new Dictionary<WorldMarker, int>();
        private static readonly List<PlayerInfo> Gone = new List<PlayerInfo>();

        internal static void Initialize()
        {
            Application.onBeforeRender += OnBeforeRender;
        }

        private static void OnBeforeRender()
        {
            var top = WorldMarker.TopRenderQueue;
            var camera = WorldMarker.Enabled && Settings.MarkersOnTop.Value && Settings.PlayersCoverMarkers.Value
                ? Fades.FullViewFade.ViewCamera()
                : null;

            Markers.Clear();
            Items.Clear();
            CoveredMarkers.Clear();
            CoveringPlayers.Clear();
            Drawn.Clear();
            Queues.Clear();

            var cameraTransform = camera != null ? camera.transform : null;
            foreach (var marker in WorldMarker.InWorld)
            {
                if (marker == null) continue;
                if (camera == null || !marker.DrawnOnTop || !marker.TryGetArea(cameraTransform, out var area)) continue;

                Markers.Add(new MarkerInView
                {
                    Marker = marker,
                    Area = area,
                    Distance = Vector3.Distance(cameraTransform.position, marker.Position),
                });
            }

            if (Markers.Count > 0)
            {
                foreach (var player in PlayerOverlap.Update(camera))
                {
                    foreach (var marker in Markers)
                    {
                        if (player.Player == marker.Marker.Owner || player.Distance >= marker.Distance ||
                            !player.Overlaps(marker.Area))
                            continue;

                        if (CoveringPlayers.Add(player))
                            Items.Add(new Item { Distance = player.Distance, Player = player });
                        if (CoveredMarkers.Add(marker.Marker))
                            Items.Add(new Item { Distance = marker.Distance, Marker = marker.Marker });
                    }
                }
            }

            // Far to near; at equal distance the marker first, so the character covers it.
            Items.Sort((a, b) =>
            {
                var order = b.Distance.CompareTo(a.Distance);
                if (order != 0) return order;
                return (a.Marker != null ? 0 : 1).CompareTo(b.Marker != null ? 0 : 1);
            });

            for (var i = 0; i < Items.Count; i++)
            {
                var queue = top - QueueSlots + Mathf.Min(i, QueueSlots - 1);
                var item = Items[i];
                if (item.Marker != null)
                {
                    Queues[item.Marker] = queue;
                    continue;
                }

                var player = item.Player.Player;
                if (!Occluders.TryGetValue(player, out var occluder))
                {
                    occluder = new PlayerOccluder();
                    Occluders[player] = occluder;
                }

                occluder.Draw(item.Player, queue);
                Drawn.Add(player);
            }

            // Each marker's queue is set once a frame: changing it switches its graphics' materials, which
            // rebuilds its canvas.
            foreach (var marker in WorldMarker.InWorld)
            {
                if (marker != null) marker.SetRenderQueue(Queues.TryGetValue(marker, out var queue) ? queue : top);
            }

            foreach (var pair in Occluders)
            {
                if (pair.Key == null) Gone.Add(pair.Key);
                else if (!Drawn.Contains(pair.Key)) pair.Value.Hide();
            }

            foreach (var player in Gone)
            {
                Occluders[player].Dispose();
                Occluders.Remove(player);
            }

            Gone.Clear();
        }
    }
}
