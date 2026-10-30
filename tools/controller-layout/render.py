"""Renders controller-layout.html to src/SBGVR/Assets/controller-layout.png (2048 px wide, transparent corners).

Needs Python's playwright with Chromium, and the Inter font files in fonts/ (see README.md).
"""
import pathlib
from playwright.sync_api import sync_playwright

HERE = pathlib.Path(__file__).resolve().parent
OUT = HERE.parent.parent / "src" / "SBGVR" / "Assets" / "controller-layout.png"
WIDTH, HEIGHT, PIXELS = 1440, 1100, 2048

with sync_playwright() as p:
    browser = p.chromium.launch()
    page = browser.new_page(viewport={"width": WIDTH, "height": HEIGHT}, device_scale_factor=PIXELS / WIDTH)
    page.goto((HERE / "controller-layout.html").as_uri())
    page.evaluate("document.fonts.ready")
    page.wait_for_load_state("networkidle")
    OUT.parent.mkdir(parents=True, exist_ok=True)
    page.locator("#layout").screenshot(path=str(OUT), omit_background=True)
    browser.close()
print(f"Wrote {OUT}")
