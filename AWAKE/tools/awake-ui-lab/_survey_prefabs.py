# -*- coding: utf-8 -*-
"""结构普查：把一个模组的全部 Gauntlet Prefab 量化成可对照的指标。

用来回答「别人的布局 UI 是什么口径」：
  · 面板外框尺寸 / 尺寸策略组合（尤其 CoverChildren × StretchToParent）
  · Color 字面量的位宽与约定（#RRGGBBAA 还是 #AARRGGBB）
  · 字号分布
  · Sprite / Brush 用了几种、官方多还是自有多
  · 是否用 DataSource / Id / ItemTemplate / MultilineEditableTextWidget 等

用法：python _survey_prefabs.py <Prefabs 目录> [AWAKE 的 Prefabs 目录]
"""
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter

WRAPPER = ("Children", "ItemTemplate")


def layout_children(el):
    kids = list(el)
    if len(kids) == 1 and kids[0].tag in WRAPPER:
        return list(kids[0])
    direct = [k for k in kids if k.tag not in WRAPPER]
    tmpl = [k for k in kids if k.tag == "ItemTemplate"]
    if tmpl:
        direct = direct + list(tmpl[0])
    return direct


def walk(root):
    stack = [root]
    out = []
    while stack:
        n = stack.pop()
        out.append(n)
        for c in layout_children(n):
            stack.append(c)
    return out


def survey(d):
    files = sorted(f for f in os.listdir(d) if f.lower().endswith(".xml"))
    tot = Counter()
    combo = Counter()
    colors = Counter()
    fonts = Counter()
    sprites = Counter()
    brushes = Counter()
    panels = {}
    max_hits = []

    for fn in files:
        try:
            root = ET.parse(os.path.join(d, fn)).getroot()
        except Exception as ex:
            max_hits.append((fn, "parse error " + str(ex)))
            continue
        nodes = walk(root)
        tot["文件"] += 1
        tot["控件"] += len(nodes)

        win = None
        for el in root.iter():
            if el.tag == "Widget":
                win = el
                break
        # 找最大的 Fixed 面板（典型的"面板本体"）
        best = 0
        for n in nodes:
            w = n.get("SuggestedWidth")
            h = n.get("SuggestedHeight")
            if n.get("WidthSizePolicy") == "Fixed" and n.get("HeightSizePolicy") == "Fixed" and w and h:
                try:
                    area = int(float(w)) * int(float(h))
                except ValueError:
                    continue
                if area > best:
                    best = area
                    panels[fn] = (w, h)

        for n in nodes:
            wp = n.get("WidthSizePolicy") or "(默认=StretchToParent)"
            hp = n.get("HeightSizePolicy") or "(默认=StretchToParent)"
            combo[(wp, hp)] += 1
            if hp == "CoverChildren":
                tot["CoverChildren 父"] += 1
            if hp == "StretchToParent":
                tot["Stretch 子(待判父)"] += 1
            c = n.get("Color")
            if c:
                colors[c] += 1
                tot["有色字面量"] += 1
                if re.fullmatch(r"#[0-9A-Fa-f]{8}", c):
                    tot["8位色"] += 1
                elif re.fullmatch(r"#[0-9A-Fa-f]{6}", c):
                    tot["6位色"] += 1
                else:
                    tot["非常规色(" + c + ")"] += 1
            fs = n.get("Brush.FontSize") or n.get("EditorFontSize")
            if fs:
                fonts[fs] += 1
            sp = n.get("Sprite")
            if sp:
                sprites[sp] += 1
            br = n.get("Brush")
            if br:
                brushes[br] += 1
            for key in ("DataSource", "Id", "ItemTemplate", "MultilineEditableTextWidget",
                        "ImageIdentifierWidget", "ExtendLeft", "Command.Click",
                        "DimensionSyncWidget", "Scale", "ClipContents", "AutoHideScrollBars"):
                if key == "ItemTemplate":
                    if n.tag == "ItemTemplate" or any(k.tag == "ItemTemplate" for k in list(n)):
                        tot["用了 ItemTemplate"] += 1
                elif key == "MultilineEditableTextWidget":
                    if n.tag == "MultilineEditableTextWidget":
                        tot["用了 多行输入"] += 1
                elif key == "ImageIdentifierWidget":
                    if n.tag == "ImageIdentifierWidget":
                        tot["用了 原生肖像控件"] += 1
                elif key == "DimensionSyncWidget":
                    if n.tag == "DimensionSyncWidget":
                        tot["用了 尺寸同步控件"] += 1
                elif n.get(key):
                    tot["用了 " + key] += 1

    print("=" * 72)
    print("目录:", d)
    print("=" * 72)
    print("文件数 %d · 控件合计 %d" % (tot["文件"], tot["控件"]))
    print()
    print("--- 关键计数 ---")
    for k in ("CoverChildren 父", "Stretch 子(待判父)", "用了 ItemTemplate",
              "用了 多行输入", "用了 原生肖像控件", "用了 尺寸同步控件",
              "用了 DataSource", "用了 Id", "用了 Command.Click", "用了 Scale",
              "用了 ClipContents", "用了 AutoHideScrollBars", "用了 ExtendLeft"):
        if tot[k]:
            print("  %-24s %d" % (k, tot[k]))
    print()
    print("--- Color 位宽 ---")
    print("  有色字面量 %d · 8 位 %d · 6 位 %d" % (tot["有色字面量"], tot["8位色"], tot["6位色"]))
    for k, v in tot.items():
        if "非常规色" in k:
            print("  %s ×%d" % (k, v))
    print("  最常见的 8 位色：")
    for c, n in colors.most_common(10):
        print("    %-12s ×%d" % (c, n))
    print()
    print("--- 字号分布 ---")
    for f, n in sorted(fonts.items(), key=lambda kv: -kv[1]):
        print("  %-6s ×%d" % (f, n))
    print()
    print("--- 尺寸策略组合 top 12 ---")
    for (wp, hp), n in combo.most_common(12):
        print("  %-26s × %-26s %d" % (wp, hp, n))
    print()
    print("--- Sprite top 12 ---")
    for s, n in sprites.most_common(12):
        print("  %-56s ×%d" % (s, n))
    print()
    print("--- Brush top 12 ---")
    for s, n in brushes.most_common(12):
        print("  %-56s ×%d" % (s, n))
    print()
    print("--- 各文件最大固定面板 ---")
    for fn, (w, h) in sorted(panels.items()):
        print("  %-34s %s×%s" % (fn, w, h))


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    for a in sys.argv[1:]:
        survey(a)
        print()
