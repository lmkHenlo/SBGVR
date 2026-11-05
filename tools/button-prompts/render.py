"""Renders button-prompts.html to src/SBGVR/Assets/meta-button-prompts.png (1024 x 1024, 128 px cells).

The icons are drawn from ../controller-layout/meta-quest-touch-plus.svg, Meta's line art. Needs Python's
playwright with Chromium, and the Inter font files in ../controller-layout/fonts/ (see that folder's README.md).
Prints the cell order, which must match GlyphNames in src/SBGVR/Hud/MetaButtonPrompts.cs.
"""
import pathlib
from playwright.sync_api import sync_playwright

HERE = pathlib.Path(__file__).resolve().parent
ART = HERE.parent / "controller-layout" / "meta-quest-touch-plus.svg"
OUT = HERE.parent.parent / "src" / "SBGVR" / "Assets" / "meta-button-prompts.png"

with sync_playwright() as p:
    browser = p.chromium.launch()
    page = browser.new_page(viewport={"width": 1024, "height": 1024}, device_scale_factor=1)
    page.goto((HERE / "button-prompts.html").as_uri())
    page.wait_for_load_state("networkidle")
    page.evaluate("document.fonts.load('700 20px Inter')")
    page.evaluate("document.fonts.ready")
    names = page.evaluate("svg => render(svg)", ART.read_text(encoding="utf-8"))
    page.evaluate("document.fonts.ready")
    OUT.parent.mkdir(parents=True, exist_ok=True)
    page.locator("#atlas").screenshot(path=str(OUT), omit_background=True)
    browser.close()
print(f"Wrote {OUT}")
for i, name in enumerate(names):
    print(i, name)
