# Button prompt icons

`button-prompts.html` is the source of `src/SBGVR/Assets/meta-button-prompts.png`, the icons the game's
button prompts show while you play with Meta Quest Touch controllers (`Hud/MetaButtonPrompts.cs`). The
PNG is embedded in `SBGVR.dll`, so it ships with the mod; rebuild after changing it.

Every icon is drawn from Meta's Meta Quest Touch Plus line art,
`../controller-layout/meta-quest-touch-plus.svg`: Meta's button caps with their letters (turned
upright), and Meta's drawings of the thumbsticks and of each controller with its trigger or grip button
filled in. SBGVR adds only the light backing, the left (blue) and right (orange) colours, the L and R
letters and the direction arrows. The artwork is used under Meta's
[Art Attribution License 1.0](https://developers.meta.com/vr/licenses/art-1.0/) only to show the
controllers' buttons: Copyright © Facebook Technologies, LLC and its affiliates. All rights reserved.
It isn't covered by SBGVR's licence, and that licence doesn't cover Meta's names, trademarks or logos.
SBGVR isn't affiliated with, sponsored by or endorsed by Meta. See `THIRD_PARTY_NOTICES.md`.

To render it again after editing the HTML:

1. Put Inter's font files in `../controller-layout/fonts/` (see that folder's README).
2. Run `python render.py` (needs Python's `playwright` with Chromium). It prints the cells in order;
   they must match `GlyphNames` in `Hud/MetaButtonPrompts.cs`.

Which drawing is the trigger and which the grip button was checked against Meta's 3D models of the
controllers (`left_b_trigger_front` and `left_b_trigger_grip`): the trigger is the piece on the
controller's inner side, the grip button the one below its face.
