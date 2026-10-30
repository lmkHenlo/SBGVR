// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using BepInEx.Configuration;
using UnityEngine.InputSystem;

namespace SBGVR
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
    /// Every SBGVR setting. VR settings only take effect while VR is running; the game's own
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
        internal static ConfigEntry<bool> PlayersCoverMarkers;
        internal static ConfigEntry<bool> BallAndHoleIconsOnTop;
        internal static ConfigEntry<bool> WorldSpaceReticles;
        internal static ConfigEntry<bool> AimLine;
        internal static ConfigEntry<bool> MenuControls;
        internal static ConfigEntry<Hud.MenuControlsSide> MenuControlsSide;

        internal static ConfigEntry<bool> AmbientOcclusion;

        internal static ConfigEntry<bool> MotionControls;
        internal static ConfigEntry<bool> ControllerAiming;
        internal static ConfigEntry<Controls.ControllerHand> DominantHand;
        internal static ConfigEntry<float> ControllerAimPitch;
        internal static ConfigEntry<bool> MirrorButtons;
        internal static ConfigEntry<bool> MotionCamera;
        internal static ConfigEntry<float> MotionCameraStrength;
        internal static ConfigEntry<float> MotionCameraMovement;
        internal static ConfigEntry<float> MotionCameraMaxYaw;
        internal static ConfigEntry<float> MotionCameraMaxPitch;

        internal static ConfigEntry<bool> FirstPerson;
        internal static ConfigEntry<Key> FirstPersonKey;
        internal static ConfigEntry<bool> FirstPersonControllerToggle;
        internal static ConfigEntry<View.TurnMode> FirstPersonTurn;
        internal static ConfigEntry<float> SnapTurnAngle;
        internal static ConfigEntry<float> FirstPersonEyeOffset;

        internal static ConfigEntry<bool> DiagnosticsEnabled;
        internal static ConfigEntry<float> PerformanceLogInterval;

        internal static void Bind(ConfigFile config)
        {
            const string comfort = "VR Comfort";
            ScreenShake = config.Bind(comfort, "ScreenShake", 0.25f,
                new ConfigDescription(
                    "Camera shake in VR, as a share of the shake the game would use with your Screen shake setting. " +
                    "0 = no shake, 1 = the game's shake.",
                    new AcceptableValueRange<float>(0f, 1f)));
            ImpactFrames = config.Bind(comfort, "ImpactFrames", false,
                "Show the game's impact frames (the brief distorted, recoloured frame when an explosion goes off near " +
                "you) in VR. They fill your whole view and are centred for a flat screen.");
            FlashFrames = config.Bind(comfort, "FlashFrames", true,
                "Show the game's flash frames in VR: the full-view flash with a doubled picture that blinds you " +
                "when another player's flash camera catches you. Off = you aren't blinded in VR.");
            CameraTransitions = config.Bind(comfort, "CameraTransitions", CameraTransitionMode.Game,
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
                "cover them. Off = scenery in front of a target hides its marker. Weapon reticles are always " +
                "drawn on top.");
            MarkerScale = config.Bind(hud, "MarkerScale", 1f,
                new ConfigDescription(
                    "Size of world-space markers. 1 = the size they have on UUVR's floating screen.",
                    new AcceptableValueRange<float>(0.25f, 4f)));
            MarkerViewAngle = config.Bind(hud, "MarkerViewAngle", 70f,
                new ConfigDescription(
                    "Icons for targets within this many degrees of straight ahead are drawn in the world; " +
                    "targets further round get the game's arrow at the edge of the floating screen.",
                    new AcceptableValueRange<float>(30f, 120f)));
            WorldSpaceReticles = config.Bind(hud, "WorldSpaceReticles", true,
                "Draw weapon reticles in the world at the point you're aiming at, so both eyes see them on the " +
                "target, always on top of scenery and characters. Off = on UUVR's floating screen, where they " +
                "look doubled against a distant target.");
            AimLine = config.Bind(hud, "AimLine", false,
                "With WorldSpaceReticles, draw a thin white line from your character's hand to the weapon reticle " +
                "while you aim, so you can see where the shot goes from where it leaves. Not for the railgun, " +
                "thunderstorm or flash camera.");
            MenuControls = config.Bind(hud, "MenuControls", true,
                "With MotionControls, show the VR controller layout on a panel next to the floating screen while " +
                "you're in the main menu (from the moment the game starts) and while the pause menu is open. It " +
                "never covers the menu: look down (or to the side, see MenuControlsSide) to read it.");
            MenuControlsSide = config.Bind(hud, "MenuControlsSide", Hud.MenuControlsSide.Below,
                "Where the menus' controller layout is: Below the floating screen, tilted towards you, or to its " +
                "Left or Right.");
            PlayersCoverMarkers = config.Bind(hud, "PlayersCoverMarkers", false,
                "With MarkersOnTop, a player character (yours or anyone else's) in front of a marker covers it, " +
                "while scenery still doesn't. A name tag or teammate icon isn't covered by the player it belongs " +
                "to, and weapon reticles aren't covered at all.");
            BallAndHoleIconsOnTop = config.Bind(hud, "BallAndHoleIconsOnTop", false,
                "Draw the golf ball and hole icons over everything, as the game does on a flat screen: scenery, " +
                "player characters (yours too) and effects never hide them, whatever MarkersOnTop and " +
                "PlayersCoverMarkers say. Menus on the floating screen still cover them. Other markers are unchanged.");

            const string graphics = "VR Graphics";
            AmbientOcclusion = config.Bind(graphics, "AmbientOcclusion", true,
                "The game's ambient occlusion (soft shading where surfaces meet) in VR. It costs more than on a " +
                "flat screen: each eye is drawn separately, at the headset's resolution. Off = none in VR; the " +
                "game's own setting still applies on a flat screen.");

            const string controls = "VR Controls";
            MotionControls = config.Bind(controls, "MotionControls", true,
                "Play with your VR controllers: while VR runs, their thumbsticks, trigger buttons and buttons work " +
                "like a gamepad, including your gamepad rebinds. Left thumbstick moves, right thumbstick turns the " +
                "camera, left trigger button aims, right trigger button swings, A jumps, B dives, X interacts, Y " +
                "changes the swing angle, the right grip button and a tap of the left grip button cycle items, and " +
                "the Menu button pauses. Hold the left grip button for more: right thumbstick = D-pad (emotes, push " +
                "to talk, votes), A = pause, B = scoreboard. " +
                "A real gamepad, keyboard and mouse still work. Restart the game after changing this.");
            ControllerAiming = config.Bind(controls, "ControllerAiming", true,
                "With MotionControls, weapons aim where your DominantHand controller points while you aim or use " +
                "them (dueling pistol, elephant gun, rocket launcher and its lock-on, railgun, freeze bomb, " +
                "thunderstorm), and your character turns to face that way when you use them. Golf swings still " +
                "follow the camera. Only while you're playing with the controllers: after you use a gamepad, " +
                "keyboard or mouse, weapons aim along the camera (or your head, in first person) until you use a " +
                "controller again. Off = weapons always aim along the camera (or your head, in first person).");
            DominantHand = config.Bind(controls, "DominantHand", Controls.ControllerHand.Right,
                "The controller weapons aim with: Right or Left. Buttons stay where they are; see MirrorButtons.");
            ControllerAimPitch = config.Bind(controls, "ControllerAimPitch", 0f,
                new ConfigDescription(
                    "Degrees to tilt the aim from where the controller points: positive tilts it down, negative " +
                    "up. For controllers that aim too high or low in your hand.",
                    new AcceptableValueRange<float>(-45f, 45f)));
            MirrorButtons = config.Bind(controls, "MirrorButtons", false,
                "With MotionControls, mirror the button layout left to right, for example for left-handed play " +
                "with DominantHand = Left: the right controller moves and aims items, the left one turns the " +
                "camera and swings or fires, X and Y act as A and B, and the right grip button opens the D-pad " +
                "layer. The Menu button stays on the left controller.");
            MotionCamera = config.Bind(controls, "MotionCamera", true,
                "With MotionControls, while you aim and charge a golf club or rocket club swing, small movements of " +
                "your DominantHand controller turn the camera, like the right thumbstick: turn it or move it " +
                "sideways to turn the camera left or right (and, with MotionCameraMaxPitch above 0, tilt it to look up " +
                "or down). Your character faces the " +
                "camera while aiming, so this fine-tunes where the swing goes. It starts from how you hold the " +
                "controller when you start aiming and stops when you swing; the thumbstick still works. Only while " +
                "you're playing with the controllers, not a gamepad, keyboard or mouse. Works in first person too " +
                "(left and right only).");
            MotionCameraStrength = config.Bind(controls, "MotionCameraStrength", 0.25f,
                new ConfigDescription(
                    "How much of the controller's turn and tilt goes into the camera: 0.25 = turning it 8 degrees " +
                    "turns the camera 2 degrees.",
                    new AcceptableValueRange<float>(0.05f, 1f)));
            MotionCameraMovement = config.Bind(controls, "MotionCameraMovement", 2f,
                new ConfigDescription(
                    "Degrees the camera turns for every 10 cm you move the controller sideways. 0 = only turning " +
                    "and tilting count.",
                    new AcceptableValueRange<float>(0f, 10f)));
            MotionCameraMaxYaw = config.Bind(controls, "MotionCameraMaxYaw", 30f,
                new ConfigDescription("The furthest the controller turns the camera left or right while aiming, in degrees.",
                    new AcceptableValueRange<float>(1f, 45f)));
            MotionCameraMaxPitch = config.Bind(controls, "MotionCameraMaxPitch", 0f,
                new ConfigDescription("The furthest the controller tilts the camera up or down while aiming, in degrees. " +
                    "0 = left and right only.",
                    new AcceptableValueRange<float>(0f, 30f)));

            const string firstPerson = "VR First Person";
            FirstPerson = config.Bind(firstPerson, "FirstPerson", false,
                "See through your character's eyes in VR instead of from the third-person camera behind them. " +
                "FirstPersonKey and the controller toggle switch this, and it's remembered. You look around and " +
                "up and down with your head, and walking goes where you look. The game switches back to the " +
                "third-person camera on its own while you're knocked out, respawning or spectating, and returns " +
                "to first person afterwards. VR only: a flat screen always uses the third-person camera.");
            FirstPersonKey = config.Bind(firstPerson, "FirstPersonKey", Key.F5,
                "Key that switches between first person and third person in VR. None = no key.");
            FirstPersonControllerToggle = config.Bind(firstPerson, "FirstPersonControllerToggle", true,
                "With MotionControls, hold the left grip button and press the right thumbstick to switch between " +
                "first person and third person (the right grip button and left thumbstick with MirrorButtons). " +
                "Pressing the thumbstick without the grip button still drops your item.");
            FirstPersonTurn = config.Bind(firstPerson, "FirstPersonTurn", View.TurnMode.Smooth,
                "How the right thumbstick turns you in first person. Snap = turns in steps of SnapTurnAngle, " +
                "Smooth = turns continuously, like the third-person camera. A mouse always turns smoothly.");
            SnapTurnAngle = config.Bind(firstPerson, "SnapTurnAngle", 30f,
                new ConfigDescription("Degrees each snap turn turns you, with FirstPersonTurn = Snap.",
                    new AcceptableValueRange<float>(15f, 90f)));
            FirstPersonEyeOffset = config.Bind(firstPerson, "FirstPersonEyeOffset", 0f,
                new ConfigDescription("Raises (positive) or lowers (negative) your eyes in first person, in metres.",
                    new AcceptableValueRange<float>(-0.5f, 0.5f)));

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
