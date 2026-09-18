#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""把「AWAKE 面板控件 → Brush → 底层 sprite」这条链解出来。

用途：得出「我的资产要顶掉哪个原版 sprite 名」，才能在 UI Lab 的 custom/ 里做
同名覆盖预览（见 HANDOVER-UI-PREVIEW.md §4）。

只读，不改任何文件。用法：python _map_brushes.py
"""
import glob
import os
import re
import xml.etree.ElementTree as ET

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                     "..", ".."))
PREFABS = os.path.join(ROOT, "GUI", "Prefabs")
GAME = r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
MODULES = ("Native", "SandBox", "SandBoxCore", "StoryMode", "Multiplayer")


def brush_defs():
    """brush 名 -> [sprite 名, ...]"""
    out = {}
    for m in MODULES:
        for p in glob.glob(os.path.join(GAME, "Modules", m, "GUI", "Brushes", "**", "*.xml"),
                           recursive=True):
            try:
                root = ET.parse(p).getroot()
            except Exception:  # noqa: BLE001
                continue
            for b in root.iter("Brush"):
                nm = b.get("Name")
                if not nm:
                    continue
                sprites = []
                for lay in b.iter("BrushLayer"):
                    sp = lay.get("Sprite")
                    if sp:
                        sprites.append(sp)
                if sprites:
                    out.setdefault(nm, [])
                    for sp in sprites:
                        if sp not in out[nm]:
                            out[nm].append(sp)
    return out


def control_brushes(path):
    """控件标签 -> [brush 名]，按出现顺序，保留上下文。"""
    txt = open(path, encoding="utf-8").read()
    out = []
    for mo in re.finditer(r"<([A-Za-z][A-Za-z0-9_]*)\b[^>]*", txt):
        tag = mo.group(1)
        chunk = txt[mo.start():mo.start() + 600]
        bs = re.findall(r'Brush="([^"]+)"', chunk[:400])
        if bs:
            out.append((tag, bs[0]))
    return out


if __name__ == "__main__":
    bd = brush_defs()
    print("brush 定义数:", len(bd))
    for f in sorted(glob.glob(os.path.join(PREFABS, "*.xml"))):
        print()
        print("=" * 76)
        print(os.path.basename(f))
        seen = set()
        for tag, br in control_brushes(f):
            key = (tag, br)
            if key in seen:
                continue
            seen.add(key)
            sp = bd.get(br)
            print("  %-22s %-42s -> %s" % (tag, br, sp if sp else "(未找到定义)"))
