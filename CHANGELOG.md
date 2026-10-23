# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and version numbers use Thunderstore's `Major.Minor.Patch` format.

No version has been published to Thunderstore yet. Every version is a test build, built from the commit that set its version number. Versions 0.1.0 to 0.2.14 were named SBG VR, with the Thunderstore package `SBG_VR`. Dates are in US Central time (UTC−05:00).

## [Unreleased]

### Added

- This changelog.

### Changed

- The project is renamed from SBG VR to SBGVR, without a space:
  - The Thunderstore package is `SBGVR` (it was `SBG_VR`), and the zip is `lmkHenlo-SBGVR-<version>.zip`.
  - The plugin is `SBGVR.dll` (it was `SbgVr.dll`), and the build file is `SBGVR.proj` (it was `SBG_VR.proj`).
  - The plugin ID `com.sbgvr` and the settings file `com.sbgvr.cfg` are unchanged, so your settings carry over.
  - Remove the old `SBG_VR` mod before installing this one, so the game doesn't end up with two copies.
- The package description is shorter and matches the README's.
- The README follows the [Standard Readme](https://github.com/RichardLitt/standard-readme) specification. It now has install and usage examples, how to update and uninstall, and how to contribute.

## [0.2.14] - 2026-10-23

### Fixed

- Weapon reticles and the orange lock-on reticle could be partly hidden. Player characters in front of them covered them, including your own character in third person. With `MarkersOnTop` off, they also sank into the surface they sit on. They're now always drawn on top of scenery, characters and effects. Menus and fades still cover them.

## [0.2.13] - 2026-10-23

### Added

- The orange lock-on reticle (homing shots from a full-power swing, and the rocket launcher) is drawn in the world on its target, instead of doubled on the floating screen. So are the airhorn's target indicators, the Jumbo Burger giant-form timer and pop-up text. When the game stops updating one of them, it goes back to the HUD and is hidden there, rather than staying where it was in the world.

## [0.2.12] - 2026-10-23

### Added

- Weapon reticles are drawn in the world at the point you're aiming at, instead of doubled on the floating screen. This covers the dueling pistol, elephant gun, rocket launcher, freeze bomb, flash camera, thunderstorm and railgun reticles, and the "can't use here" one. The reticle sits where the aim meets something: 50 m away when it meets nothing, and always between 2 m and 150 m. It glides between distances and keeps its floating-screen size.
- `VR HUD` → `WorldSpaceReticles` (on by default) turns this off.

## [0.2.11] - 2026-10-23

### Fixed

- Player characters and their faces flickered, and parts of them disappeared, while markers moved behind them. Characters are drawn once again, as usual. They now cover the markers behind them through a stencil mask instead of being drawn a second time.

## [0.2.10] - 2026-10-23

### Added

- Diagnostics log the game's RenderObjects renderer feature: when it draws, what it draws and its depth settings.

### Fixed

- Item effects showed through menus on the floating screen and through full-view fades. The floating screen (a change to UUVR) and the fades are now drawn after every other see-through object.

## [0.2.9] - 2026-10-23

### Added

- Known issue in both READMEs: how to stop the SteamVR menu opening when the game starts or a match begins, by turning off Steam's Desktop Game Theatre for the game.

### Fixed

- See-through parts of player characters, such as the shield on the chest, flickered while markers moved behind the character.

## [0.2.8] - 2026-10-23

### Fixed

- Distance labels on markers still drew over the player characters in front of them.

## [0.2.7] - 2026-10-23

### Fixed

- Player characters didn't cover the ball and tee markers, or marker text, behind them.
- A ball's icon and name tag weren't covered by the ball's own player. Only a player's own name tag and teammate icons are exempt now.

## [0.2.6] - 2026-10-23

### Added

- `VR HUD` → `PlayersCoverMarkers` (on by default): a player character in front of a marker covers the part of the marker behind it, while scenery still doesn't hide markers. A name tag or teammate icon isn't covered by the player it belongs to.

### Removed

- `VR HUD` → `HideMarkersOverPlayers`, replaced by `PlayersCoverMarkers`. Markers stay visible instead of fading out.

## [0.2.5] - 2026-10-23

### Added

- `VR HUD` → `HideMarkersOverPlayers` (on by default): a marker fades out while it's drawn over a player character, and back in afterwards. Markers for targets behind a player no longer cover that player. A name tag or teammate icon isn't hidden by the player it belongs to.

## [0.2.4] - 2026-10-23

### Added

- `VR Graphics` → `AmbientOcclusion` (on by default). Turn it off for a higher frame rate in VR.

### Changed

- Ambient occlusion is drawn in VR again, correctly for each eye: at the eye's size, with its off-centre view. If the fix can't be applied (after a game update, say), ambient occlusion stays off in VR, as in 0.2.3.

## [0.2.3] - 2026-10-22

### Fixed

- Black areas covered nearby scenery and your character, moving with you and the headset. The game's ambient occlusion was sized for the game window rather than each eye, so it's now off in VR.

## [0.2.2] - 2026-10-22

### Fixed

- Markers, including interaction prompts such as Set Up Match, showed through menus on the floating screen, such as Match Setup.

## [0.2.1] - 2026-10-22

### Fixed

- The pause menu, and the settings opened from it, were blurred and unreadable in VR. The game's menu blur is now off while VR runs.

## [0.2.0] - 2026-10-22

### Added

- `VR Comfort` settings, which only apply while VR runs:
  - `ScreenShake`: camera shake as a share of the game's own setting. 0 by default: no shake.
  - `ImpactFrames` and `FlashFrames`: the game's full-screen flashes, off by default. The CRT effect is always off in VR.
  - `CameraTransitions`: camera moves between views play as in the game (`Game`), in about a third of the time (`Fast`, the default) or as a cut (`Instant`).
  - `MenuBackground`: `Still` (the default) holds the main menu's background scenes still; `Moving` keeps the game's pans.
  - `FullViewFades` (on by default): the game's fades darken your whole view, not just the floating screen.
  - `RecenterKey` (F4 by default): turns the view so the direction you're facing becomes straight ahead.
- World-space markers: icons (your ball, the hole), name tags and the interaction reticle are drawn in the world at their target, at the right depth for each eye, instead of doubled on the floating screen. Their settings are in `VR HUD`: `WorldSpaceMarkers`, `MarkersOnTop`, `MarkerScale` and `MarkerViewAngle`.
- Each SBG VR feature is applied on its own. If one fails to load (after a game update, say), it's turned off and logged, and the rest keep working.

### Fixed

- The main menu disappeared from the headset when its background scene changed. UUVR kept following a camera that had stopped rendering.
- UUVR rebuilt itself while the game was quitting.

## [0.1.0] - 2026-10-22

### Added

- First test build. It packages Raicuparta's Universal Unity VR (UUVR) v0.4.0 as a BepInEx 5 Thunderstore mod for Super Battle Golf, built against the game's own assemblies. It starts VR with the game (F3 turns VR off and on), follows the headset, and shows the game's 2D interface on a floating screen.
- Changes to UUVR for this game:
  - Its Canvas redirect UI mode works with the game's UI library.
  - Its patcher removes leftover copies of `ucrtbased.dll` and `OVRPlugin.dll` from the game folder.
  - The package includes only the 64-bit VR libraries the game loads.
- The `com.sbgvr` plugin, which logs VR, camera, renderer and frame rate details for bug reports. The `Diagnostics` → `Enabled` and `PerformanceLogInterval` settings control it.
- `build.ps1` and `SBG_VR.proj`, which build everything and make the Thunderstore zip.
- `THIRD_PARTY_NOTICES.md`, with the licences of the bundled VR libraries.
