# SBG VR

Play Super Battle Golf in a PC VR headset. Client-side: only you need it.

> **Experimental.** SBG VR is Raicuparta's Universal Unity VR (UUVR), packaged and built for Super Battle Golf, with fixes and comfort settings for the game. 0.2.5 is a test build: expect rough edges.

## What it does

- Starts VR when the game starts. The game is shown in stereo in your headset and follows your head.
- Shows menus and the HUD on a floating screen in front of you, and draws markers (your ball, the hole, name tags, the interaction reticle) in the world at their target, so they line up at any distance.
- Draws the game's ambient occlusion correctly for each eye. Turn `AmbientOcclusion` off for more frame rate.
- Fades your whole view when the game fades the screen, holds the main menu's background scenes still, and turns off screen shake, explosion flashes, the CRT effect and the pause menu's background blur (which made the menu itself blurry in VR). Camera moves between views are quicker. All of this can be changed in the settings.
- Keeps your usual controls: mouse and keyboard or gamepad. Motion controllers aren't used.
- Only changes what you see. Nothing is sent to other players, so you can play VR in any lobby with flat-screen players.

## Install

Install `SBG_VR` with r2modman or another Thunderstore mod manager. BepInEx is installed with it.

Before you start the game:

1. Connect your headset and start SteamVR (for example through Steam Link).
2. In SteamVR's settings, press **Set SteamVR as OpenXR Runtime**. Depending on your SteamVR version, it's on the OpenXR page or the Developer page. If another runtime (such as Meta Quest Link) is the OpenXR runtime, the headset may stay black.

Then start the game from the mod manager.

## Usage

| Key | Action |
| --- | --- |
| F3 | Turn VR off and on while playing |
| F4 | Recentre the view: the direction you're facing becomes straight ahead |

Settings are in two files under `BepInEx\config`. In r2modman, open them with **Config editor**.

- `raicuparta.uuvr-modern.cfg`: UUVR's settings: the VR runtime (OpenXR or OpenVR), camera tracking, and the floating screen's size and position.
- `com.sbgvr.cfg`: SBG VR's settings. They only apply while VR is running:
  - **VR Comfort:** `ScreenShake` (0 = none, 1 = the game's), `ImpactFrames` and `FlashFrames` (off), `CameraTransitions` (`Game`, `Fast` or `Instant`), `MenuBackground` (`Moving` or `Still`), `FullViewFades` (on) and `RecenterKey` (F4).
  - **VR Graphics:** `AmbientOcclusion` (on).
  - **VR HUD:** `WorldSpaceMarkers` (on), `MarkersOnTop` (draw markers over scenery, on), `MarkerScale` (1), `PlayersCoverMarkers` (player characters cover the markers behind them, on) and `MarkerViewAngle` (how far from straight ahead icons stay in the world before becoming edge arrows, 70°).
  - **Diagnostics:** logging for bug reports.

If the headset shows nothing, try these UUVR settings, one at a time:

1. `General` → `Preferred VR APi` = `OpenVr`
2. `Camera` → `Camera Tracking Mode` = `RelativeMatrix` or `Absolute`

## Good to know

- **Files in the game folder.** Each time the game starts, the mod copies its VR runtime files into `Super Battle Golf_Data` (`Plugins`, `UnitySubsystems` and `StreamingAssets\SteamVR`). Uninstalling the mod doesn't remove them. They aren't used unless SBG VR is installed, but you can delete them: `Plugins\openvr_api.dll`, `Plugins\openxr_loader.dll`, `Plugins\UnityOpenXR.dll`, `Plugins\XRSDKOpenVR.dll`, the `UnitySubsystems` folder and `StreamingAssets\SteamVR`.
- **Field of view.** Your headset sets the field of view, so the game's FOV slider doesn't apply in VR yet.
- **MaxFOVSlider.** Turn it off while you use VR. SBG VR doesn't handle it yet.
- **Reporting problems.** Send `BepInEx\LogOutput.log` from a session where the problem happened. SBG VR writes the VR and camera details needed to track it down.

## Credits

- [UUVR](https://github.com/Raicuparta/uuvr) by Raicuparta, the VR mod this package is built on.
- [BepInEx](https://github.com/BepInEx/BepInEx), the mod loader.

## License

GPL-3.0-or-later. SBG VR is © 2026 lmkHenlo, and UUVR is © 2024 Raicuparta. The VR runtime libraries included keep their own licences; see `THIRD_PARTY_NOTICES.md`.
