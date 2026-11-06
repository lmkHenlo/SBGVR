# Button prompt icons

`button-prompts.html` is the source of `src/SBGVR/Assets/meta-button-prompts.png`, the icons the game's
button prompts show while you play with Meta Quest Touch controllers (`Hud/MetaButtonPrompts.cs`). The
PNG is embedded in `SBGVR.dll`, so it ships with the mod; rebuild after changing it.

Every icon is SBGVR's own drawing, in white letters and outlines in the left (blue) or right (orange)
controller's colour, with a dark edge so it reads over bright scenes: thumbsticks as a double ring with L
or R (triangles for a direction, a notch for a press), the triggers (LT, RT) and the A, B, X, Y and Menu
buttons as round buttons with their side showing, and the grip buttons (LG, RG) as a tapered pill. Icons
of a kind share one scale, so their rings and letters match. None of Meta's artwork is used.

To render it again after editing the HTML:

1. Put Inter's font files in `../controller-layout/fonts/` (see that folder's README).
2. Run `python render.py` (needs Python's `playwright` with Chromium). It prints the cells in order;
   they must match `GlyphNames` in `Hud/MetaButtonPrompts.cs`.
