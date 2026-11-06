# Controller layout image

`controller-layout.html` is the source of `src/SBGVR/Assets/controller-layout.png`, the VR controller
layout SBGVR shows below the floating screen in the main menu (`Hud/MenuControlsPanel.cs`). Opened with
`?mirrored`, it's the source of `controller-layout-mirrored.png`, the same layout with `MirrorButtons` on:
each label keeps its button's name and takes the other controller's actions (except the Menu and Meta
buttons), and the grip-button combinations move to the right grip button. Both PNGs are embedded in
`SBGVR.dll`, so they ship with the mod; rebuild after changing them.

To render it again after editing the HTML:

1. Put Inter's `inter-latin-400-normal.woff2`, `inter-latin-600-normal.woff2` and
   `inter-latin-700-normal.woff2` in `fonts/` here, for example from the `@fontsource/inter` npm package
   (`npm pack @fontsource/inter@5.0.16`, then copy them from `package/files/`).
2. Run `python render.py` (needs Python's `playwright` with Chromium). It writes both images.

`meta-quest-touch-plus.svg` (also the source of `../button-prompts/`) is Meta's Meta Quest Touch Plus line art (converted from the controller
images in Meta's developer art assets), used under Meta's
[Art Attribution License 1.0](https://developers.meta.com/vr/licenses/art-1.0/) only to show the
controllers' buttons: Copyright © Facebook Technologies, LLC and its affiliates. All rights reserved.
It isn't covered by SBGVR's licence, and that licence doesn't cover Meta's names, trademarks or logos.
SBGVR isn't affiliated with, sponsored by or endorsed by Meta. See `THIRD_PARTY_NOTICES.md`.
