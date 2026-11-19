// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SBGVR.Hud
{
    /// <summary>
    /// Keeps the game's scrolling menu lists clipped in VR. While VR runs, the screen the game's screen-space menus
    /// are drawn on (which UUVR copies onto the floating screen) has no stencil buffer, so Unity's
    /// <see cref="Mask"/>, which clips with the stencil, clips nothing: the Match Setup course lists ran over the
    /// rest of the menu. While VR runs, each Mask that only clips (its own image hidden, as on a list's viewport)
    /// on a screen-space menu is turned off and a <see cref="RectMask2D"/>, which clips to its rectangle without
    /// the stencil, takes its place. When VR stops, the Masks are put back.
    /// </summary>
    /// <remarks>
    /// A RectMask2D clips to the mask's rectangle, so a viewport with rounded corners clips square. Masks that show
    /// their image (shaped icons and the like) are left alone: a rectangle isn't their shape. World-space canvases
    /// are drawn by the VR cameras, which have a stencil buffer, so their Masks work and are left alone too.
    /// </remarks>
    internal sealed class VrUiMasks : MonoBehaviour
    {
        /// <summary>Each Mask turned off, with the RectMask2D added in its place (null if it already had one).</summary>
        private static readonly Dictionary<Mask, RectMask2D> Swapped = new Dictionary<Mask, RectMask2D>();

        private static VrUiMasks _instance;
        private static bool _changing;

        private void OnEnable()
        {
            _instance = this;
            VrState.Changed += OnVrChanged;
            if (VrState.IsRunning) SwapAll();
        }

        private void OnDisable()
        {
            VrState.Changed -= OnVrChanged;
            RestoreAll();
            if (_instance == this) _instance = null;
        }

        private static void OnVrChanged(bool running)
        {
            if (running) SwapAll();
            else RestoreAll();
        }

        /// <summary>Called when a Mask turns on (<see cref="VrUiMaskPatches"/>), and for every Mask when VR starts.</summary>
        internal static void TrySwap(Mask mask)
        {
            if (_instance == null || _changing || !VrState.IsRunning) return;
            if (mask == null || !mask.isActiveAndEnabled || mask.showMaskGraphic || Swapped.ContainsKey(mask)) return;
            if (!OnScreenSpaceMenu(mask)) return;

            var existing = mask.GetComponent<RectMask2D>();
            _changing = true;
            try
            {
                mask.enabled = false;
                Swapped[mask] = existing != null ? null : mask.gameObject.AddComponent<RectMask2D>();
            }
            finally
            {
                _changing = false;
            }
        }

        /// <summary>
        /// Called when a Mask turns off. If the game turns off a Mask that was swapped, the RectMask2D added for it goes
        /// too, so nothing clips that the game didn't mean to.
        /// </summary>
        internal static void OnMaskDisabled(Mask mask)
        {
            if (_changing || mask == null || !Swapped.TryGetValue(mask, out var added)) return;
            Swapped.Remove(mask);
            if (added != null) Destroy(added);
        }

        private static bool OnScreenSpaceMenu(Mask mask)
        {
            var canvas = mask.GetComponentInParent<Canvas>();
            if (canvas == null) return false;
            var root = canvas.rootCanvas;
            return root.renderMode == RenderMode.ScreenSpaceOverlay ||
                   (root.renderMode == RenderMode.ScreenSpaceCamera && root.worldCamera == null);
        }

        private static void SwapAll()
        {
            foreach (var mask in FindObjectsByType<Mask>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                TrySwap(mask);
            if (Settings.DiagnosticsEnabled.Value)
                Plugin.Log.LogInfo($"[diag] Menu masks: {Swapped.Count} clip-only mask(s) on screen-space menus clip by rectangle while VR runs.");
        }

        private static void RestoreAll()
        {
            _changing = true;
            try
            {
                foreach (var pair in Swapped)
                {
                    if (pair.Value != null) Destroy(pair.Value);
                    if (pair.Key != null) pair.Key.enabled = true;
                }
            }
            finally
            {
                _changing = false;
                Swapped.Clear();
            }
        }
    }

    /// <summary>Hands each Mask that turns on or off to <see cref="VrUiMasks"/>.</summary>
    [HarmonyPatch]
    internal static class VrUiMaskPatches
    {
        [HarmonyPatch(typeof(Mask), "OnEnable")]
        [HarmonyPostfix]
        private static void MaskEnabled(Mask __instance) => VrUiMasks.TrySwap(__instance);

        [HarmonyPatch(typeof(Mask), "OnDisable")]
        [HarmonyPostfix]
        private static void MaskDisabled(Mask __instance) => VrUiMasks.OnMaskDisabled(__instance);
    }
}
