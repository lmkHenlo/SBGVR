# SBG VR

Play Super Battle Golf in a PC VR headset. Client-side: only you need it.

> **Experimental.** 0.1.0 is the first test build. It is Raicuparta's Universal Unity VR (UUVR), packaged and built for Super Battle Golf, plus logging that records what works and what doesn't in the headset. Fixes made for Super Battle Golf will come in later versions.

## What it does

- Starts VR when the game starts. The game is shown in stereo in your headset and follows your head.
- Shows menus and the HUD on a floating screen in front of you.
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

Settings are in two files under `BepInEx\config`. In r2modman, open them with **Config editor**.

- `raicuparta.uuvr-modern.cfg`: UUVR's settings: the VR runtime (OpenXR or OpenVR), camera tracking, and the floating screen's size and position.
- `com.sbgvr.cfg`: SBG VR's settings. In 0.1.0 these only control the diagnostic logging.

If the headset shows nothing, try these UUVR settings, one at a time:

1. `General` → `Preferred VR APi` = `OpenVr`
2. `Camera` → `Camera Tracking Mode` = `RelativeMatrix` or `Absolute`

## Good to know

- **Files in the game folder.** Each time the game starts, the mod copies its VR runtime files into `Super Battle Golf_Data` (`Plugins`, `UnitySubsystems` and `StreamingAssets\SteamVR`). Uninstalling the mod doesn't remove them. They aren't used unless SBG VR is installed, but you can delete them: `Plugins\openvr_api.dll`, `Plugins\openxr_loader.dll`, `Plugins\UnityOpenXR.dll`, `Plugins\XRSDKOpenVR.dll`, the `UnitySubsystems` folder and `StreamingAssets\SteamVR`.
- **MaxFOVSlider.** Turn it off while you use VR. SBG VR doesn't handle it yet.
- **Reporting problems.** Send `BepInEx\LogOutput.log` from a session where the problem happened. SBG VR writes the VR and camera details needed to track it down.

## Credits

- [UUVR](https://github.com/Raicuparta/uuvr) by Raicuparta, the VR mod this package is built on.
- [BepInEx](https://github.com/BepInEx/BepInEx), the mod loader.

## License

GPL-3.0-or-later. SBG VR is © 2026 lmkHenlo, and UUVR is © 2024 Raicuparta. The VR runtime libraries included keep their own licences; see `THIRD_PARTY_NOTICES.md`.
