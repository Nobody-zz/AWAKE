# -*- coding: utf-8 -*-
"""结构探针（一次性）：在原版官方 Prefab 里统计「父级高度策略 × 子级高度策略」。

问题：`HeightSizePolicy="CoverChildren"` 的父级里挂一个
      `HeightSizePolicy="StretchToParent"` 的子级 —— 官方自己用不用？

关键：XML 里有个 `<Children>` 包装层，它不是布局父级。
      真正的布局父级 = 最近的 **Widget 类**祖先。第一版探针栽在这里，
      把 `<Children>` 当父级 ⇒ 恒 0 命中（假阴性）。

用法：python _probe_native_stretch_20260915.py
"""
import os
import sys
import xml.etree.ElementTree as ET
from collections import Counter

MODULES = "D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules"
NATIVE = ("Native", "SandBox", "SandBoxCore", "StoryMode", "Multiplayer")

WRAPPER_TAGS = ("Children", "ItemTemplate")


def prefab_dirs():
    out = []
    for m in NATIVE:
        d = os.path.join(MODULES, m, "GUI", "Prefabs")
        if os.path.isdir(d):
            out.append(d)
    return out


def layout_children(el):
    """返回 el 的布局子级：有 <Children> 就取它下面，否则取直接子级。

    <ItemTemplate> 的清单同样返回（它的布局父级是拥有它的 ListPanel）。
    """
    kids = list(el)
    if len(kids) == 1 and kids[0].tag in WRAPPER_TAGS:
        return list(kids[0])
    direct = [k for k in kids if k.tag not in WRAPPER_TAGS]
    tmpl = [k for k in kids if k.tag == "ItemTemplate"]
    if tmpl:
        direct = direct + list(tmpl[0])
    return direct


def iter_pairs(root):
    """产出 (布局父级, 布局子级)。布局父级跳过 <Children>/<ItemTemplate> 包装层。"""
    stack = [root]
    while stack:
        p = stack.pop()
        for c in layout_children(p):
            yield p, c
            stack.append(c)


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    # 传参则扫描指定目录（用来扫 AWAKE 自己的 Prefab）
    if len(sys.argv) > 1:
        scan_dirs = [sys.argv[1]]
        label = sys.argv[1]
    else:
        scan_dirs = prefab_dirs()
        label = "原版官方模块"
    print("扫描范围 :", label)
    print()
    files = 0
    pair_counter = Counter()     # (父 hsp, 子 hsp) → 次数
    cc_parents = 0
    hits = []                    # CoverChildren 父 + Stretch 子
    ff_samples = []

    for d in scan_dirs:
        for dirpath, _dn, fns in os.walk(d):
            for fn in fns:
                if not fn.lower().endswith(".xml"):
                    continue
                p = os.path.join(dirpath, fn)
                try:
                    root = ET.parse(p).getroot()
                except Exception:
                    continue
                files += 1
                for par, ch in iter_pairs(root):
                    ph = par.get("HeightSizePolicy") or "(默认=StretchToParent)"
                    chh = ch.get("HeightSizePolicy") or "(默认=StretchToParent)"
                    pair_counter[(ph, chh)] += 1
                    if ph == "CoverChildren":
                        cc_parents += 1
                        if chh == "StretchToParent":
                            if len(hits) < 12:
                                hits.append("%s  <%s %s>  子<%s %s w=%s>"
                                            % (fn, par.tag, ph, ch.tag, chh,
                                               ch.get("WidthSizePolicy")))
                    if ph == "Fixed" and chh == "StretchToParent":
                        if len(ff_samples) < 6:
                            ff_samples.append("%s  <%s>  子<%s w=%s>"
                                              % (fn, par.tag, ch.tag,
                                                 ch.get("WidthSizePolicy")))

    print("扫描文件数 :", files)
    print("布局父级=CoverChildren 的次数 :", cc_parents)
    print("  其中 子级 h=StretchToParent :", len(hits), "（列全，最多打 12）")
    print()
    print("--- 全组合分布（父 hsp × 子 hsp）top 12 ---")
    for (ph, chh), n in pair_counter.most_common(12):
        print("  %-26s × %-26s %6d" % (ph, chh, n))
    print()
    print("--- 阳性对照：父=Fixed 且 子=StretchToParent ---")
    print("  次数:", sum(n for (ph, chh), n in pair_counter.items()
                      if ph == "Fixed" and chh == "StretchToParent"))
    for s in ff_samples:
        print("    ", s)
    print()
    print("--- 命中样例（应为主结论的证据）---")
    for s in hits:
        print("  ", s)


if __name__ == "__main__":
    main()
