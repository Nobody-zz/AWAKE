#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AWAKE UI 美术 · M0 生产驱动。

产出三处：
  1. <仓库>/AWAKE/GUI/SpriteParts/<分类>/<名>.png   ← 交付物（官方管线输入）
  2. out/sprites/<名>.png                           ← 本机校验副本 + 联络单
  3. <UI Lab>/out/atlas/custom/<原版 sprite 名>.png  ← 同名覆盖，让预览立刻看到换肤

用法：
    python build_m0.py                 # 全量出图 + 写交付目录 + 写预览覆盖
    python build_m0.py --no-preview    # 不碰 UI Lab 的 custom/
    python build_m0.py --clear-preview # 只清掉本工具写进去的预览覆盖
    python build_m0.py --sheet         # 顺带出联络单图

设计决定（改前先读）：
  * **源图取面板原尺寸**（如 960×720 就出 960×720）。理由：AWAKE 面板是固定尺寸，
    9-slice 边距相同时各区域 1:1 映射 ⇒ 像素级精确、纹理不被拉伸。
  * **逐状态出图**（用户 2026-09-13 定）：不做 Brush 提亮派生。
  * 九宫格边距写在 manifest 里，由 UI 侧补进 Brushes XML（sprite 上写不了）。
"""
import argparse
import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import artkit as K          # noqa: E402
import parts as P           # noqa: E402
from artkit import (BTN_FIELD, BTN_FIELD_HOVER, BTN_FIELD_PRESS, BTN_OFF,
                    BTN_SEC_FIELD, BTN_SEC_HOVER, BTN_SEC_PRESS, BTN_TAB_FIELD,
                    BTN_TAB_HOVER, BTN_TAB_PRESS, GOLD)   # noqa: E402

REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
DELIVER = os.path.join(REPO, "GUI", "SpriteParts")
OUT = os.path.join(HERE, "out")
UI_LAB_CUSTOM = os.path.normpath(os.path.join(HERE, "..", "awake-ui-lab", "out", "atlas", "custom"))

# 分类 → 说明（写进 Config.xml 注释）
CATS = {
    "ui_awake_frame": "面板底 / 框 / 凹槽",
    "ui_awake_ornament": "栏头 / 分隔 / 角饰",
    "ui_awake_button": "按钮（逐状态）",
}

# ---------------------------------------------------------------- M0 清单
# (分类, sprite 名, (w,h), 九宫格 L/T/R/B, 顶替的原版 sprite 名(可空), 构造式)

def bed(w, h):
    return lambda: P.plaque(w, h, cut=20, rule_at=20, rule_w=2, bev=7)


def tab(state):
    field = {"default": BTN_TAB_FIELD, "hover": BTN_TAB_HOVER,
             "pressed": BTN_TAB_PRESS, "selected": BTN_FIELD}[state]
    accent = (GOLD, 2, 4) if state == "selected" else None
    def f():
        return P.btn_plate(105, 35, cut=BUTTON_CUT, field=field, accent=accent)
    return f


# ---------------------------------------------------------------------------
# 按钮族「形」定案（2026-09-14 12:5x）
#
# **完全同一轮廓，只改长度**（甲方定案）。三控件共用这一个端头长度。
#
# ## 为什么是 13
#
# 两条约束的交集（`out/study/shape/tip_len_round2.png` 是判据板：
# **带完整铁轨 + 木面**的 7/9/11/13/16 五档放大对照）：
#
#   ① 端头必须**长过木面的内缩量**，否则木面的端头被内缩**吃平** ——
#      外轮廓收成斜楔、木面却顶成一道竖边，读起来像"被削了一刀"。
#      本文件 `btn_plate` 的内缩量 = `bev+1` = **5px**（bev 默认 4）。
#      ⇒ TIP > 5，且要留余量 ⇒ **TIP ≳ 11**。
#      ⚠️ 踩过：我先按"端头占宽的视觉权重"定了 9px（余量只有 4），
#         那一轮我在对照梯上**只画了纯轮廓填充**、看不到内缩吃端头这件事。
#         **判据板必须用真实渲染（铁轨＋木面都在），不能只画轮廓。**
#   ② 端头占宽不宜超过 ~12%，否则从"收"变成"两端各一个件"（三段式）。
#      14.5%（16px）已实测偏散 ⇒ **TIP ≲ 13**。
#
#   ⇒ 取 **13px**（占 110 宽的 11.8%，余量 8px）。
#
# ⚠️ 改这个值必须同时回看 `bev`：`bev` 一变大，内缩量跟着变大，余量会被吃掉。
#    也要回看 `NINE` 的左右边距（端头区不能进九宫格的拉伸区）。
BUTTON_CUT = 13.0


def primary(state):
    """主按钮＝要"更重"：框比次按钮亮一档，读起来是主动作。

    ⚠️ 按定案，轮廓与次按钮/页签**完全相同**，只有尺寸与框色不同。
       所以这里**不能**再单独给个 `cut` —— 层级靠"框亮一档"和尺寸，不靠形状。
    """
    if state == "disabled":
        return lambda: P.btn_plate(110, 35, cut=BUTTON_CUT, field=BTN_OFF, disabled=True)
    field = {"default": BTN_FIELD, "hover": BTN_FIELD_HOVER,
             "pressed": BTN_FIELD_PRESS}[state]
    return lambda: P.btn_plate(110, 35, cut=BUTTON_CUT, field=field,
                               frame_c=(66, 58, 47), frame_lit=(108, 96, 80))


def secondary(state):
    """次按钮＝要"更轻"：框压暗一档。"""
    field = {"default": BTN_SEC_FIELD, "hover": BTN_SEC_HOVER,
             "pressed": BTN_SEC_PRESS}[state]
    return lambda: P.btn_plate(100, 35, cut=BUTTON_CUT, field=field,
                               frame_c=(48, 42, 35), frame_lit=(82, 73, 61))


M0 = [
    # ---- C1 面板底
    ("ui_awake_frame", "panel_main_1280", (1280, 760), (32, 32, 32, 32), [], bed(1280, 760)),
    ("ui_awake_frame", "panel_main_960", (960, 720), (32, 32, 32, 32),
     ["npc_dialogue_panel_9"], bed(960, 720)),
    ("ui_awake_frame", "panel_dialog_1100", (1100, 680), (32, 32, 32, 32),
     ["StdAssets__Popup__canvas"], bed(1100, 680)),
    ("ui_awake_frame", "panel_input_800", (800, 60), (16, 16, 16, 16),
     ["General__CharacterCreation__name_input_area"],
     lambda: P.recess(800, 60, cut=8, accent=(GOLD, 2, 5))),
    ("ui_awake_frame", "panel_status_720", (720, 55), (16, 16, 16, 16), [],
     lambda: P.recess(720, 55, cut=8, base=(28, 23, 20), accent=(GOLD, 2, 5))),

    # ---- C2 栏头 / 分隔
    ("ui_awake_ornament", "header_band", (1280, 60), (24, 0, 24, 0),
     ["General__CharacterCreation__character_creation_background_gradient"],
     lambda: P.header_band(1280, 60, cut=10)),
    ("ui_awake_ornament", "divider_gold", (800, 5), (0, 0, 0, 0), ["GradientDivider_9"],
     lambda: P.divider(800, 5)),

    # ---- C3 按钮（逐状态）
    ("ui_awake_button", "btn_close_40", (40, 40), (12, 12, 12, 12),
     ["StdAssets__close_button"], lambda: P.seal(40, "default")),
    ("ui_awake_button", "btn_close_40_hover", (40, 40), (12, 12, 12, 12),
     ["StdAssets__close_button_hover"], lambda: P.seal(40, "hover")),
    ("ui_awake_button", "btn_close_40_pressed", (40, 40), (12, 12, 12, 12),
     ["StdAssets__close_button_pressed"], lambda: P.seal(40, "pressed")),

    ("ui_awake_button", "btn_tab_105", (105, 35), (14, 14, 10, 10),
     ["dialog_option_canvas_9"], tab("default")),
    ("ui_awake_button", "btn_tab_105_hover", (105, 35), (14, 14, 10, 10), [], tab("hover")),
    ("ui_awake_button", "btn_tab_105_pressed", (105, 35), (14, 14, 10, 10), [], tab("pressed")),
    ("ui_awake_button", "btn_tab_105_selected", (105, 35), (14, 14, 10, 10), [], tab("selected")),

    ("ui_awake_button", "btn_primary_110", (110, 35), (16, 16, 16, 16),
     ["General__Button__main_button_done"], primary("default")),
    ("ui_awake_button", "btn_primary_110_hover", (110, 35), (16, 16, 16, 16),
     ["General__Button__main_button_done_hover"], primary("hover")),
    ("ui_awake_button", "btn_primary_110_pressed", (110, 35), (16, 16, 16, 16), [],
     primary("pressed")),
    ("ui_awake_button", "btn_primary_110_disabled", (110, 35), (16, 16, 16, 16), [],
     primary("disabled")),

    ("ui_awake_button", "btn_secondary_100", (100, 35), (16, 16, 16, 16), [],
     secondary("default")),
    ("ui_awake_button", "btn_secondary_100_hover", (100, 35), (16, 16, 16, 16), [],
     secondary("hover")),
    ("ui_awake_button", "btn_secondary_100_pressed", (100, 35), (16, 16, 16, 16), [],
     secondary("pressed")),
]


def config_xml():
    lines = ['<?xml version="1.0" encoding="utf-8"?>',
             '<SpriteCategories>',
             '  <!-- AWAKE UI 自有贴图。分类名必须以 ui_ 开头（官方硬要求）。',
             '       参考 TaleWorlds《Generating and Loading UI Sprite Sheets》。',
             '       <AlwaysLoad/> 必需：否则该分类不会加载。 -->']
    for c, desc in CATS.items():
        lines += ['  <SpriteCategory Name="%s">' % c,
                  '    <!-- %s -->' % desc,
                  '    <AlwaysLoad/>',
                  '    <SpriteSheetSize Width="2048" Height="2048"/>',
                  '  </SpriteCategory>']
    lines += ['</SpriteCategories>', '']
    return "\n".join(lines)


def _font(sz):
    try:
        return ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", sz)
    except Exception:  # noqa: BLE001
        return ImageFont.load_default()


def contact_sheet(items, path):
    """联络单：所有 sprite 摆在深底上看一眼。"""
    pad, lab = 18, 34
    cols = 3
    cw = 430
    rows = []
    for it in items:
        im = Image.open(it["path"]).convert("RGBA")
        sc = min(1.0, (cw - 2 * pad) / im.width)
        rows.append((it, im, sc))
    rh = [max(90, int(im.height * sc) + lab + pad) for _, im, sc in rows]
    # 逐行排版：先算每一行的行高
    line_h = []
    for i in range(0, len(rows), cols):
        line_h.append(max(rh[i:i + cols]))
    W = 30 + cols * cw
    H = 100 + sum(line_h) + 30
    sheet = Image.new("RGB", (W, H), (27, 21, 18))
    d = ImageDraw.Draw(sheet)
    d.text((30, 26), "AWAKE UI · M0 联络单（真尺寸，深底 1:1 或等比缩）", font=_font(26),
           fill=(240, 232, 220))
    d.text((30, 60), "看倒角 / 金规线 / 材质颗粒 / 三态差异。判图前读 HANDOVER-UI-PREVIEW.md §5 保真度边界。",
           font=_font(15), fill=(150, 140, 128))
    y = 100
    for li, i in enumerate(range(0, len(rows), cols)):
        x = 30
        for it, im, sc in rows[i:i + cols]:
            show = im.resize((max(1, int(im.width * sc)), max(1, int(im.height * sc))),
                             Image.LANCZOS)
            canvas = Image.new("RGB", show.size, (27, 21, 18))
            canvas.paste(show, (0, 0), show)
            d.text((x, y), it["name"], font=_font(17), fill=(236, 228, 216))
            d.text((x, y + 20), "%dx%d  9-slice %s" % (im.width, im.height, it["nine"]),
                   font=_font(13), fill=(160, 148, 132))
            sheet.paste(canvas, (x, y + lab))
            d.rectangle([x - 1, y + lab - 1, x + canvas.width, y + lab + canvas.height],
                        outline=(70, 60, 52))
            x += cw
        y += line_h[li]
    sheet.save(path)
    return path


def detail_sheet(items, path):
    """细节单：小件放大 2~4x，铺在碑面色上（≈真机观感）。"""
    BG = (27, 21, 18)
    COLW = 600
    pick = ["panel_input_800", "panel_status_720", "header_band", "divider_gold",
            "btn_close_40", "btn_close_40_hover", "btn_close_40_pressed",
            "btn_tab_105", "btn_tab_105_hover", "btn_tab_105_selected",
            "btn_primary_110", "btn_primary_110_hover", "btn_primary_110_disabled",
            "btn_secondary_100", "btn_secondary_100_hover"]
    by = {it["name"]: it for it in items}
    seq = [by[n] for n in pick if n in by]
    rows = []
    for it in seq:
        im = Image.open(it["path"]).convert("RGBA")
        sc = min(4.0, (COLW - 46) / im.width)
        show = im.resize((max(1, round(im.width * sc)), max(1, round(im.height * sc))),
                         Image.LANCZOS)
        canvas = Image.new("RGB", show.size, BG)
        canvas.paste(show, (0, 0), show)
        rows.append((it, show, canvas, sc))

    cols = 2
    line_h = []
    for i in range(0, len(rows), cols):
        line_h.append(max(r[2].height for r in rows[i:i + cols]) + 46)
    W = 30 + cols * COLW
    H = 96 + sum(line_h) + 24
    sheet = Image.new("RGB", (W, H), (27, 21, 18))
    d = ImageDraw.Draw(sheet)
    d.text((30, 26), "AWAKE UI · M0 细节单（小件放大，铺碑面色）", font=_font(26),
           fill=(240, 232, 220))
    d.text((30, 60), "看倒角 / 受光方向 / 逐状态差异。放大倍数标在每件标题后。",
           font=_font(15), fill=(150, 140, 128))
    y = 96
    for li, i in enumerate(range(0, len(rows), cols)):
        x = 30
        for it, show, canvas, sc in rows[i:i + cols]:
            d.text((x, y), "%s  ×%.1f" % (it["name"], sc), font=_font(16),
                   fill=(236, 228, 216))
            d.text((x, y + 20), "%dx%d" % (it["size"][0], it["size"][1]),
                   font=_font(13), fill=(160, 148, 132))
            sheet.paste(canvas, (x, y + 38))
            d.rectangle([x - 1, y + 37, x + canvas.width, y + 38 + canvas.height],
                        outline=(70, 60, 52))
            x += COLW
        y += line_h[li]
    sheet.save(path)
    return path


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--no-preview", action="store_true", help="不写 UI Lab 的 custom/")
    ap.add_argument("--clear-preview", action="store_true", help="只清掉本工具写的预览覆盖")
    ap.add_argument("--sheet", action="store_true", help="出联络单图")
    args = ap.parse_args()

    os.makedirs(OUT, exist_ok=True)
    items = []
    overrides = []

    if not args.clear_preview:
        for cat, name, size, nine, replaces, build in M0:
            img = build()
            assert img.size == size, "%s 尺寸不符：%s != %s" % (name, img.size, size)
            d = os.path.join(DELIVER, cat)
            os.makedirs(d, exist_ok=True)
            img.save(os.path.join(d, name + ".png"))
            o = os.path.join(OUT, "sprites")
            os.makedirs(o, exist_ok=True)
            img.save(os.path.join(o, name + ".png"))
            items.append({"cat": cat, "name": name, "size": size, "nine": nine,
                          "replaces": replaces, "path": os.path.join(o, name + ".png")})
            for rep in replaces:
                overrides.append((rep, img))
            print("  %-26s %4dx%-5d %s" % (name, size[0], size[1], cat))

        with open(os.path.join(DELIVER, "Config.xml"), "w", encoding="utf-8") as f:
            f.write(config_xml())

        if not args.no_preview:
            os.makedirs(UI_LAB_CUSTOM, exist_ok=True)
            for rep, img in overrides:
                img.save(os.path.join(UI_LAB_CUSTOM, rep + ".png"))
            print("\n预览覆盖写入: %s  (%d 个)" % (UI_LAB_CUSTOM, len(overrides)))

    if args.clear_preview:
        n = 0
        for _, _, _, _, replaces, _ in M0:
            for rep in replaces:
                p = os.path.join(UI_LAB_CUSTOM, rep + ".png")
                if os.path.isfile(p):
                    os.remove(p)
                    n += 1
        print("已清除预览覆盖 %d 个" % n)
        return

    man = {"schema": "awake-ui-art-m0.v1", "generated": "2026-09-13",
           "deliver_root": "AWAKE/GUI/SpriteParts",
           "items": items}
    with open(os.path.join(OUT, "m0-manifest.json"), "w", encoding="utf-8") as f:
        json.dump(man, f, ensure_ascii=False, indent=2)

    print("\n交付目录: %s" % DELIVER)
    print("清单    : %s" % os.path.join(OUT, "m0-manifest.json"))
    print("sprite  : %d 个" % len(items))

    if args.sheet or True:
        p = contact_sheet(items, os.path.join(OUT, "sheet_m0.png"))
        print("联络单  : %s" % p)
        p2 = detail_sheet(items, os.path.join(OUT, "sheet_m0_detail.png"))
        print("细节单  : %s" % p2)


if __name__ == "__main__":
    main()
