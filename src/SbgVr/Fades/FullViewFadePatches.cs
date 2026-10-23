// SBG VR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using HarmonyLib;

namespace SbgVr.Fades
{
    /// <summary>Routes the game's two screen fades to <see cref="FullViewFade"/> while VR runs.</summary>
    [HarmonyPatch]
    internal static class FullViewFadePatches
    {
        [HarmonyPatch(typeof(ColorOverlay), "SetAlpha")]
        [HarmonyPostfix]
        private static void ColorOverlaySetAlpha(ColorOverlay __instance)
        {
            FullViewFade.ColorOverlayChanged(__instance);
        }

        [HarmonyPatch(typeof(ColorOverlay), "SetColorInternal")]
        [HarmonyPostfix]
        private static void ColorOverlaySetColor(ColorOverlay __instance)
        {
            FullViewFade.ColorOverlayChanged(__instance);
        }

        [HarmonyPatch(typeof(MainMenuBackgroundHandler), "SetOverlayCanvasAlpha")]
        [HarmonyPrefix]
        private static bool MenuSetOverlayCanvasAlpha(MainMenuBackgroundHandler __instance, float alpha)
        {
            return FullViewFade.MenuOverlayChanged(__instance, alpha);
        }
    }
}
