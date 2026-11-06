"""Renders controller-layout.html to src/SBGVR/Assets/controller-layout.png, and with ?mirrored to
controller-layout-mirrored.png (VR Controls.MirrorButtons on). Both 2048 px wide, with transparent corners.

Needs Python's playwright with Chromium, and the Inter font files in fonts/ (see README.md).
"""
import pathlib
from playwright.sync_api import sync_playwright

HERE = pathlib.Path(__file__).resolve().parent
ASSETS = HERE.parent.parent / "src" / "SBGVR" / "Assets"
WIDTH, HEIGHT, PIXELS = 1440, 1100, 2048
IMAGES = {"controller-layout.png": "", "controller-layout-mirrored.png": "?mirrored"}

with sync_playwright() as p:
    browser = p.chromium.launch()
    page = browser.new_page(viewport={"width": WIDTH, "height": HEIGHT}, device_scale_factor=PIXELS / WIDTH)
    ASSETS.mkdir(parents=True, exist_ok=True)
    for name, query in IMAGES.items():
        page.goto((HERE / "controller-layout.html").as_uri() + query)
        page.evaluate("document.fonts.ready")
        page.wait_for_load_state("networkidle")
        out = ASSETS / name
        page.locator("#layout").screenshot(path=str(out), omit_background=True)
        print(f"Wrote {out}")
    browser.close()
