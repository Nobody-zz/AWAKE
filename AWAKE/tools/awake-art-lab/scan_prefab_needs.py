# -*- coding: utf-8 -*-
"""
扫描本机模组自己的生产 Prefab，抽出「实际需要的控件与资产」清单。
口径来源：AWAKE/GUI/Prefabs/*.xml（真机入口，非原版、非参考模组）。
输出：每个 Prefab 引用的 Awake.* 资源 + 控件类型/尺寸/状态。
"""
import os
import re
import xml.etree.ElementTree as ET
from collections import OrderedDict

PREFAB_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "GUI", "Prefabs")
PREFAB_DIR = os.path.abspath(PREFAB_DIR)

# 控件类型 -> 我们关心的尺寸属性
SIZE_ATTRS = ("SuggestedWidth", "SuggestedHeight")

assets = OrderedDict()   # name -> {kind: set(prefabs), sizes: set, states: set}
widgets = []             # (prefab, tag, width, height, brush, extra)


def scan(path):
    name = os.path.basename(path)
    try:
        tree = ET.parse(path)
    except ET.ParseError as e:
        print("  [parse error] %s: %s" % (name, e))
        return
    root = tree.getroot()

    for el in root.iter():
        tag = el.tag
        attrs = el.attrib

        # 1) 收集 Awake.* 资源引用
        for key, val in attrs.items():
            if val.startswith("Awake."):
                slot = assets.setdefault(val, {"kind": set(), "prefabs": set(),
                                               "sizes": set(), "states": set()})
                slot["kind"].add(key)
                slot["prefabs"].add(name)

        # 2) 控件与尺寸
        if tag in ("ButtonWidget", "Widget", "TextWidget", "RichTextWidget",
                   "EditableTextWidget", "ListPanel", "ScrollablePanel",
                   "Standard.VerticalScrollbar"):
            brush = attrs.get("Brush", "")
            w = attrs.get("SuggestedWidth", "")
            h = attrs.get("SuggestedHeight", "")
            if tag == "ButtonWidget" or brush.startswith("Awake."):
                states = []
                if "IsSelected" in attrs:
                    states.append("selected")
                if "IsEnabled" in attrs:
                    states.append("enabled-gated")
                widgets.append((name, tag, w, h, brush, ",".join(states)))
                if brush.startswith("Awake.") and (w or h):
                    assets[brush]["sizes"].add("%sx%s" % (w or "?", h or "?"))
                    for s in states:
                        assets[brush]["states"].add(s)


def main():
    print("Prefab 目录: %s" % PREFAB_DIR)
    files = sorted(f for f in os.listdir(PREFAB_DIR) if f.endswith(".xml"))
    print("生产 Prefab 数: %d -> %s\n" % (len(files), ", ".join(files)))

    for f in files:
        scan(os.path.join(PREFAB_DIR, f))

    print("=" * 78)
    print("一、模组实际需要的自制资源（Awake.*）")
    print("=" * 78)
    print("%-28s %-10s %-16s %s" % ("资源名", "用作", "出现尺寸", "状态/出处"))
    print("-" * 78)
    for name, info in sorted(assets.items()):
        print("%-28s %-10s %-16s %s | %s" % (
            name,
            "/".join(sorted(info["kind"])),
            "/".join(sorted(info["sizes"])) or "-",
            ",".join(sorted(info["states"])) or "-",
            ",".join(sorted(info["prefabs"])),
        ))

    print()
    print("=" * 78)
    print("二、按钮控件明细（多态需求从这里来）")
    print("=" * 78)
    print("%-24s %-8s %-12s %-24s %s" % ("Prefab", "宽", "高", "Brush", "状态"))
    print("-" * 78)
    for row in widgets:
        if row[1] == "ButtonWidget":
            print("%-24s %-8s %-12s %-24s %s" % (row[0], row[2], row[3], row[4], row[5]))

    print()
    print("=" * 78)
    print("三、非原版自定义 Brush 前缀统计")
    print("=" * 78)
    prefixes = {}
    for name in assets:
        p = ".".join(name.split(".")[:2])
        prefixes[p] = prefixes.get(p, 0) + 1
    for p, c in sorted(prefixes.items(), key=lambda x: -x[1]):
        print("  %-24s %d" % (p, c))


if __name__ == "__main__":
    main()
