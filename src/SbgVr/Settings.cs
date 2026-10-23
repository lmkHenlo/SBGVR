// SBG VR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using BepInEx.Configuration;
using UnityEngine.InputSystem;

namespace SbgVr
{
    internal enum CameraTransitionMode
    {
        Game,
        Fast,
        Instant,
    }

    internal enum MenuBackgroundMode
    {
        Moving,
        Still,
    }

    /// <summary>
    /// Every SBG VR setting. VR settings only take effect while VR is running; the game's own
    /// settings, and its save file, are never changed.
    /// </summary>
    internal static class Settings
    {
        internal static ConfigEntry<float> ScreenShake;
        internal static ConfigEntry<bool> ImpactFrames;
        internal static ConfigEntry<bool> FlashFrames;
        internal static ConfigEntry<CameraTransitionMode> CameraTransitions;
        internal static ConfigEntry<MenuBackgroundMode> MenuBackground;
        internal static ConfigEntry<bool> FullViewFades;
        internal static ConfigEntry<Key> RecenterKey;

        internal static ConfigEntry<bool> WorldSpaceMarkers;
        internal static ConfigEntry<bool> MarkersOnTop;
        internal static ConfigEntry<float> MarkerScale;
        internal static ConfigEntry<float> MarkerViewAngle;
        internal static ConfigEntry<bool> HideMarkersOverPlayers;

        internal static ConfigEntry<bool> AmbientOcclusion;

        internal static ConfigEntry<bool> DiagnosticsEnabled;
        internal static ConfigEntry<float> PerformanceLogInterval;

        internal static void Bind(ConfigFile config)
        {
            const string comfort = "VR Comfort";
            ScreenShake = config.Bind(comfort, "ScreenShake", 0f,
                new ConfigDescription(
                    "Camera shake in VR, as a share of the shake the game would use with your Screen shake setting. " +
                    "0 = no shake, 1 = the game's shake.",
                    new AcceptableValueRange<float>(0f, 1f)));
            ImpactFrames = config.Bind(comfort, "ImpactFrames", false,
                "Show the game's impact frames (the brief distorted, recoloured frame when an explosion goes off near " +
                "you) in VR. They fill your whole view and are centred for a flat screen.");
            FlashFrames = config.Bind(comfort, "FlashFrames", false,
                "Show the game's flash frames (the full-screen flash with a doubled picture) in VR.");
            CameraTransitions = config.Bind(comfort, "CameraTransitions", CameraTransitionMode.Fast,
                "How the camera moves when the game switches views, for example to the hole overview and back. " +
                "Game = the game's own sweep, Fast = the same sweep in about a third of the time, Instant = a cut.");
            MenuBackground = config.Bind(comfort, "MenuBackground", MenuBackgroundMode.Still,
                "The main menu's background scenes. Moving = the game's slow camera pans, Still = each scene holds " +
                "still. The game still fades between scenes either way.");
            FullViewFades = config.Bind(comfort, "FullViewFades", true,
                "Fade your whole view when the game fades the screen (scene changes, the main menu's background), " +
                "instead of only the floating screen.");
            RecenterKey = config.Bind(comfort, "RecenterKey", Key.F4,
                "Key that turns the view so the direction you're facing becomes straight ahead. None = no key.");

            const string hud = "VR HUD";
            WorldSpaceMarkers = config.Bind(hud, "WorldSpaceMarkers", true,
                "Draw markers (ball, hole and other icons, name tags, the target reticle) in the world at their " +
                "target, so each eye sees them at the right depth. Off = UUVR's floating screen, where markers on " +
                "nearby targets look doubled.");
            MarkersOnTop = config.Bind(hud, "MarkersOnTop", true,
                "Draw world-space markers over scenery, like the game does. Menus on the floating screen still " +
                "cover them. Off = scenery in front of a target hides its marker.");
            MarkerScale = config.Bind(hud, "MarkerScale", 1f,
                new ConfigDescription(
                    "Size of world-space markers. 1 = the size they have on UUVR's floating screen.",
                    new AcceptableValueRange<float>(0.25f, 4f)));
            MarkerViewAngle = config.Bind(hud, "MarkerViewAngle", 70f,
                new ConfigDescription(
                    "Icons for targets within this many degrees of straight ahead are drawn in the world; " +
                    "targets further round get the game's arrow at the edge of the floating screen.",
                    new AcceptableValueRange<float>(30f, 120f)));
            HideMarkersOverPlayers = config.Bind(hud, "HideMarkersOverPlayers", true,
                "Hide a world-space marker while it's drawn over a player character (yours or anyone else's) and " +
                "show it again once it isn't, so markers don't cover the players. A name tag or teammate icon " +
                "isn't hidden by the player it belongs to.");

            const string graphics = "VR Graphics";
            AmbientOcclusion = config.Bind(graphics, "AmbientOcclusion", true,
                "The game's ambient occlusion (soft shading where surfaces meet) in VR. It costs more than on a " +
                "flat screen: each eye is drawn separately, at the headset's resolution. Off = none in VR; the " +
                "game's own setting still applies on a flat screen.");

            const string diagnostics = "Diagnostics";
            DiagnosticsEnabled = config.Bind(diagnostics, "Enabled", true,
                "Write VR, camera and renderer details to the BepInEx log (LogOutput.log), so problems in the " +
                "headset can be tracked down from the log. Only changes are logged, not every frame.");
            PerformanceLogInterval = config.Bind(diagnostics, "PerformanceLogInterval", 30f,
                new ConfigDescription(
                    "Seconds between frame rate lines in the log while VR is running. 0 = never.",
                    new AcceptableValueRange<float>(0f, 600f)));
        }
    }
}
