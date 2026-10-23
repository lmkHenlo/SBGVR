// SBG VR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SbgVr.Hud
{
    /// <summary>
    /// VR HUD.PlayersCoverMarkers: player characters cover the markers behind them, while scenery still
    /// doesn't (VR HUD.MarkersOnTop).
    ///
    /// Markers ignore depth so scenery can't hide them, which means nothing can. So each frame, after the UI
    /// is rebuilt and before anything is drawn:
    /// <list type="number">
    /// <item>Each character that's nearer than a marker it overlaps in view gets a bit of the stencil buffer,
    /// and <see cref="PlayerOccluder"/> sets that bit wherever the character is visible.</item>
    /// <item>Each marker skips pixels with the bits of the characters in front of it, so those characters
    /// show through as if the marker were behind them.</item>
    /// </list>
    /// The characters themselves are drawn once, as usual; nothing about how they look changes.
    ///
    /// A name tag or teammate icon isn't covered by the player it belongs to.
    /// </summary>
    internal static class MarkerOcclusion
    {
        /// <summary>One stencil bit per covering character; more than this many share bits.</summary>
        private const int StencilBits = 8;

        private struct MarkerInView
        {
            internal WorldMarker Marker;
            internal Rect Area;
            internal float Distance;
        }

        private static readonly Dictionary<PlayerInfo, PlayerOccluder> Occluders = new Dictionary<PlayerInfo, PlayerOccluder>();
        private static readonly List<MarkerInView> Markers = new List<MarkerInView>();
        private static readonly List<PlayerOverlap.PlayerView> Covering = new List<PlayerOverlap.PlayerView>();
        private static readonly Dictionary<WorldMarker, int> Masks = new Dictionary<WorldMarker, int>();
        private static readonly Dictionary<PlayerOverlap.PlayerView, int> Bits = new Dictionary<PlayerOverlap.PlayerView, int>();
        private static readonly HashSet<PlayerInfo> Drawn = new HashSet<PlayerInfo>();
        private static readonly List<PlayerInfo> Gone = new List<PlayerInfo>();

        internal static void Initialize()
        {
            // Runs once a frame after the UI has rebuilt its graphics (CanvasUpdateRegistry handles the same
            // event; it's created first here, so it runs first) and before anything is drawn.
            _ = CanvasUpdateRegistry.instance;
            Canvas.willRenderCanvases += OnWillRenderCanvases;
        }

        private static void OnWillRenderCanvases()
        {
            var camera = WorldMarker.Enabled && Settings.MarkersOnTop.Value && Settings.PlayersCoverMarkers.Value
                ? Fades.FullViewFade.ViewCamera()
                : null;

            Markers.Clear();
            Covering.Clear();
            Masks.Clear();
            Bits.Clear();
            Drawn.Clear();

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
                // Characters in front of a marker they overlap, nearest first: if there are more than there
                // are stencil bits, the nearest get their own.
                foreach (var player in PlayerOverlap.Update(camera))
                {
                    foreach (var marker in Markers)
                    {
                        if (IsCovering(player, marker))
                        {
                            Covering.Add(player);
                            break;
                        }
                    }
                }

                Covering.Sort((a, b) => a.Distance.CompareTo(b.Distance));
                for (var i = 0; i < Covering.Count; i++)
                {
                    var player = Covering[i];
                    var bit = 1 << Mathf.Min(i, StencilBits - 1);
                    Bits[player] = bit;

                    if (!Occluders.TryGetValue(player.Player, out var occluder))
                    {
                        occluder = new PlayerOccluder();
                        Occluders[player.Player] = occluder;
                    }

                    occluder.Draw(player, bit);
                    Drawn.Add(player.Player);
                }

                foreach (var marker in Markers)
                {
                    var mask = 0;
                    foreach (var player in Covering)
                    {
                        if (IsCovering(player, marker)) mask |= Bits[player];
                    }

                    if (mask != 0) Masks[marker.Marker] = mask;
                }
            }

            // Every marker, covered or not: which characters cover it this frame, then the materials it's
            // drawn with.
            foreach (var marker in WorldMarker.InWorld)
            {
                if (marker == null) continue;
                marker.SetCoveringMask(Masks.TryGetValue(marker, out var mask) ? mask : 0);
                marker.EnforceDrawnMaterials();
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

        private static bool IsCovering(PlayerOverlap.PlayerView player, MarkerInView marker)
        {
            return player.Player != marker.Marker.Owner && player.Distance < marker.Distance &&
                   player.Overlaps(marker.Area);
        }
    }
}
