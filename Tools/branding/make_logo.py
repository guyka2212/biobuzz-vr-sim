"""
VrFsim logo source: writes HTML pages (mark geometry + Chakra Petch wordmark) that headless
Chrome renders to the PNGs in Assets/VrFsim/Resources/Branding, Assets/VrFsim/Art/Branding and
Docs/images. The mark is a 200x200 SVG: a honeycomb cell with a VR headset across it.

Re-render:
  1. Put ChakraPetch-BoldItalic.woff2 and ChakraPetch-Medium.woff2 (Google Fonts, SIL OFL)
     next to this script.
  2. python make_logo.py
  3. chrome --headless=new --hide-scrollbars --default-background-color=00000000
       --force-device-scale-factor=2 --window-size=W,H --screenshot=NAME.png file:///.../NAME.html
     lockup 1200x600 @2, compact 900x300 @2, icon 440x440 @2.3273 (1024 px), primary 1200x600 @1.
  4. Crop the transparent ones to their alpha bounds (+ a few px).
"""
import os
R = os.path.dirname(os.path.abspath(__file__))
A, DARK, CREAM, DIM = "#FFB400", "#14161A", "#F4F1E8", "#B9B5A8"
FONTS = """
@font-face{font-family:'Chakra Petch';font-style:italic;font-weight:700;src:url(ChakraPetch-BoldItalic.woff2) format('woff2')}
@font-face{font-family:'Chakra Petch';font-style:normal;font-weight:500;src:url(ChakraPetch-Medium.woff2) format('woff2')}
*{margin:0;padding:0;box-sizing:border-box} html,body{background:transparent;overflow:hidden}
"""
def mark(size, hole=DARK):
    return f'''<svg width="{size}" height="{size}" viewBox="0 0 200 200">
<polygon points="100,10 178,55 178,145 100,190 22,145 22,55" fill="none" stroke="{A}" stroke-width="12" stroke-linejoin="round"/>
<rect x="22" y="94" width="156" height="12" fill="{A}"/>
<path d="M60 74 H140 a14 14 0 0 1 14 14 V112 a14 14 0 0 1 -14 14 H118 L108 114 H92 L82 126 H60 a14 14 0 0 1 -14 -14 V88 a14 14 0 0 1 14 -14 Z" fill="{A}"/>
<polygon points="74,88 83.5,93.5 83.5,104.5 74,110 64.5,104.5 64.5,93.5" fill="{hole}"/>
<polygon points="126,88 135.5,93.5 135.5,104.5 126,110 116.5,104.5 116.5,93.5" fill="{hole}"/>
</svg>'''
def lockup(bg):
    return f'''<div style="width:1200px;height:600px;background:{bg};display:flex;align-items:center;justify-content:center;gap:56px;font-family:'Chakra Petch',sans-serif">
{mark(300)}
<div style="display:flex;flex-direction:column;gap:14px">
<div style="font-size:148px;font-weight:700;font-style:italic;line-height:1;letter-spacing:-2px;color:{CREAM}"><span style="color:{A}">Vr</span>Fsim</div>
<div style="font-size:26px;font-weight:500;letter-spacing:9px;color:{DIM};padding-left:6px">FTC SIMULATOR IN VR</div>
</div></div>'''
pages = {
    # One-line version for small headers: mark + wordmark, no tagline.
    "compact": f'''<div style="width:900px;height:300px;display:flex;align-items:center;justify-content:center;gap:28px;font-family:'Chakra Petch',sans-serif">
{mark(150)}<div style="font-size:120px;font-weight:700;font-style:italic;line-height:1;letter-spacing:-2px;color:{CREAM}"><span style="color:{A}">Vr</span>Fsim</div></div>''',
    # Transparent lockup for in-game use (the game draws its own dark background).
    "lockup": lockup("transparent"),
    # The designed primary logo, dark background, for the README.
    "primary": lockup(DARK),
    # Mark alone on transparent: the lens holes must stay dark, so draw them dark.
    "mark": f'<div style="width:600px;height:600px;display:flex;align-items:center;justify-content:center">{mark(600)}</div>',
    # App icon: the designed dark rounded tile, full-bleed, transparent corners.
    "icon": f'<div style="width:440px;height:440px;border-radius:96px;background:{DARK};display:flex;align-items:center;justify-content:center">{mark(340)}</div>',
}
for name, body in pages.items():
    open(os.path.join(R, name + ".html"), "w", encoding="utf-8").write(
        f"<!DOCTYPE html><html><head><meta charset='utf-8'><style>{FONTS}</style></head><body>{body}</body></html>")
print("ok")
