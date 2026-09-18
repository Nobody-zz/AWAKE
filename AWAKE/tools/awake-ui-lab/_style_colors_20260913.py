# -*- coding: utf-8 -*-
"""取色 + 九宫格边距：把画风口径落成数字。"""
import os
import glob
from collections import Counter
from PIL import Image
import xml.etree.ElementTree as ET

GAME = r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
_p = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_p, "awake_ui.py")):
    _p = os.path.dirname(_p)
LAB = _p
SP = os.path.join(LAB, "out", "atlas", "sprites")
CS = os.path.join(LAB, "out", "style-study", "conversation_sprites")


def dom(path, n=6, min_alpha=200):
    im = Image.open(path).convert("RGBA")
    px = list(im.getdata())
    px = [p for p in px if p[3] >= min_alpha]
    if not px:
        return []
    c = Counter((p[0] // 8 * 8, p[1] // 8 * 8, p[2] // 8 * 8) for p in px)
    tot = sum(c.values())
    return [("#%02X%02X%02X" % k, round(v * 100.0 / tot, 1)) for k, v in c.most_common(n)]


print("=" * 70)
print("【A】原版组件主色（不透明像素，量化到 8）")
targets = [
    ("按钮·常规", "General__Button__main_button_regular.png"),
    ("按钮·常规hover", "General__Button__main_button_regular_hover.png"),
    ("按钮·完成", "General__Button__main_button_done.png"),
    ("按钮·取消", "General__Button__button_cancel.png"),
    ("关闭·印", "StdAssets__close_button.png"),
    ("关闭·印hover", "StdAssets__close_button_hover.png"),
    ("石板箭头", "StdAssets__arrow_large_pointing_left.png"),
    ("滑块", "General__Slider__slider_knob.png"),
    ("滚动条底座", "General__Scrollbar.Vertical1__scroller_bed.png"),
    ("页条", "StdAssets__page_button_center.png"),
    ("石板纹理", "stone_texture_overlay.png"),
    ("石纹连续", "stone_texture_continuous.png"),
    ("标题头", "TitleHeader.png"),
    ("弹出底板", "StdAssets__Popup__canvas.png"),
]
for label, f in targets:
    p = os.path.join(SP, f)
    if os.path.isfile(p):
        print("  %-14s %s" % (label, dom(p)))

print()
print("【B】对话系图标主色（看是不是纯白剪影 / 金)")
for f in sorted(os.listdir(CS)):
    nm = f[:-4]
    if any(k in nm for k in ("block_icon", "progress_icon", "persuasion_success",
                             "persuasion_fail", "persuasion_critical", "persuasion_pass",
                             "persuasion_option_blocked", "empty_circle", "check",
                             "click_to_cont", "hover_indicator", "progress_indicator",
                             "relation_bar", "dialog_option")):
        print("  %-32s %s" % (nm, dom(os.path.join(CS, f))))

print()
print("=" * 70)
print("【C】对话相关 Brush：颜色 / 九宫格边距 / 音效")
for m in ("Native", "SandBox"):
    for p in glob.glob(os.path.join(GAME, "Modules", m, "GUI", "Brushes", "*.xml")):
        try:
            root = ET.parse(p).getroot()
        except Exception:
            continue
        for br in root.iter("Brush"):
            nm = br.get("Name") or ""
            if not any(k in nm for k in ("Conversation", "Dialog", "dialog", "Popup.Close")):
                continue
            font = br.get("Font")
            layers = []
            for ly in br.iter("BrushLayer"):
                sprite = ly.get("Sprite")
                if sprite:
                    ext = {k: ly.get("Extend" + k) for k in ("Left", "Top", "Right", "Bottom")}
                    layers.append((ly.get("Name"), sprite, ext))
            styles = []
            for st in br.iter("Style"):
                fc = st.get("FontColor")
                if fc:
                    styles.append((st.get("Name"), fc, st.get("FontSize")))
            print("  [%s] %s  font=%s" % (m, nm, font))
            for ly in layers:
                print("      layer %s sprite=%s extend=%s" % ly)
            for st in styles:
                print("      style %s color=%s size=%s" % st)
