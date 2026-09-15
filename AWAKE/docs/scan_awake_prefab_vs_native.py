# -*- coding: utf-8 -*-
"""
对照诊断：AWAKE 自有 Prefab 的布局数字 vs 骑砍原版栅格体系。

回答的问题：
- AWAKE 用的间距/尺寸落在什么节奏上？跟原版（5/10 系列）是不是一个体系？
- 哪些裸值其实是"别的元素尺寸算出来的"（咬合链嫌疑）？
- 哪些结构在原版里本可复用标准件？

只读，不修改任何文件。
用法：python scan_awake_prefab_vs_native.py
"""
import os
import re
import io
from collections import Counter

import xml.etree.ElementTree as ET

AWAKE_PREFABS = r"D:\AWAKE-Dev\AWAKE\GUI\Prefabs"
OUT_DIR = r"D:\AWAKE-Dev\AWAKE\docs"
OUT_MD = os.path.join(OUT_DIR, "AWAKE-PREFAB-LAYOUT-VS-NATIVE-20260913.md")

INT_RE = re.compile(r"^-?\d+$")


def as_int(value):
    if value is None:
        return None
    value = value.strip()
    return int(value) if INT_RE.match(value) else None


def is_control(el):
    return ("WidthSizePolicy" in el.attrib) or ("HeightSizePolicy" in el.attrib)


def scan(path):
    tree = ET.parse(path)
    root = tree.getroot()
    controls = []

    def walk(el, depth, parent_index):
        if is_control(el):
            idx = len(controls)
            controls.append({
                "depth": depth,
                "parent": parent_index,
                "tag": el.tag,
                "attrs": dict(el.attrib),
            })
            parent_index = idx
        for child in el:
            walk(child, depth + 1, parent_index)

    walk(root, 0, -1)
    return controls


def bucket(value):
    if value is None:
        return "?"
    if value == 0:
        return "零"
    if value % 10 == 0:
        return "10的倍数"
    if value % 5 == 0:
        return "5的倍数"
    if value % 4 == 0:
        return "4的倍数"
    if value % 2 == 0:
        return "2的倍数"
    return "奇数/其它"


def main():
    files = sorted(f for f in os.listdir(AWAKE_PREFABS) if f.lower().endswith(".xml"))

    lines = []
    w = lines.append
    w("# AWAKE 自有 Prefab 布局数字 vs 原版栅格 对照诊断")
    w("")
    w("> 对照基准：`docs/NATIVE-PREFAB-METRICS-20260913.md`（原版 430 个 Prefab / 11026 控件）")
    w("> 原版结论：间距以 **5 的倍数**为主（5/10/15/20/25/30…），5 的倍数占 55%。")
    w("> 生成脚本：`AWAKE/docs/scan_awake_prefab_vs_native.py`（可重跑，只读）")
    w("")

    all_margin = Counter()
    all_size = Counter()
    summary_rows = []

    detail_blocks = []

    for fname in files:
        path = os.path.join(AWAKE_PREFABS, fname)
        try:
            controls = scan(path)
        except Exception as exc:  # noqa: BLE001
            w("### %s" % fname)
            w("")
            w("解析失败：%s" % exc)
            w("")
            continue

        margin_values = []
        size_values = []
        for c in controls:
            a = c["attrs"]
            for key in ("MarginLeft", "MarginRight", "MarginTop", "MarginBottom"):
                v = as_int(a.get(key))
                if v is not None:
                    margin_values.append((key, v))
                    all_margin[v] += 1
            for key in ("SuggestedWidth", "SuggestedHeight"):
                v = as_int(a.get(key))
                if v is not None:
                    size_values.append((key, v))
                    all_size[v] += 1

        # 顶层固定尺寸面板
        fixed = [c for c in controls
                 if as_int(c["attrs"].get("SuggestedWidth")) is not None
                 and as_int(c["attrs"].get("SuggestedHeight")) is not None
                 and c["attrs"].get("WidthSizePolicy") == "Fixed"
                 and c["attrs"].get("HeightSizePolicy") == "Fixed"]
        top = min(fixed, key=lambda c: c["depth"]) if fixed else None

        # 同层兄弟中，出现多个不同 MarginRight/MarginLeft 的嫌疑（手工排列）
        by_parent = {}
        for c in controls:
            by_parent.setdefault(c["parent"], []).append(c)

        suspicious = []
        for parent, kids in by_parent.items():
            rights = [as_int(k["attrs"].get("MarginRight")) for k in kids]
            rights = [v for v in rights if v not in (None, 0)]
            lefts = [as_int(k["attrs"].get("MarginLeft")) for k in kids]
            lefts = [v for v in lefts if v not in (None, 0)]
            if len(set(rights)) >= 2:
                suspicious.append("同层 MarginRight 取值 %s" % sorted(set(rights)))
            if len(set(lefts)) >= 2:
                suspicious.append("同层 MarginLeft 取值 %s" % sorted(set(lefts)))

        mvals = sorted({v for _k, v in margin_values})
        svals = sorted({v for _k, v in size_values})

        top_desc = "%d × %d" % (as_int(top["attrs"]["SuggestedWidth"]), as_int(top["attrs"]["SuggestedHeight"])) if top else "无顶层固定尺寸"

        summary_rows.append((fname, len(controls), top_desc, len(mvals), len(svals)))

        blk = []
        blk.append("### `%s`" % fname)
        blk.append("")
        blk.append("- 控件数：**%d**，顶层固定尺寸：**%s**" % (len(controls), top_desc))
        blk.append("- 用到的 Margin 值（去重）：`%s`" % ", ".join(str(v) for v in mvals))
        blk.append("- 用到的 Suggested 尺寸（去重）：`%s`" % ", ".join(str(v) for v in svals))
        if suspicious:
            blk.append("- 同层手工排列嫌疑：")
            for s in sorted(set(suspicious)):
                blk.append("  - %s" % s)
        blk.append("")
        detail_blocks.append("\n".join(blk))

    w("## 1. 总览")
    w("")
    w("| 文件 | 控件数 | 顶层固定尺寸 | Margin 去重数 | 尺寸去重数 |")
    w("|---|---|---|---|---|")
    for row in summary_rows:
        w("| `%s` | %d | %s | %d | %d |" % row)
    w("")

    w("## 2. AWAKE 的间距落在什么节奏上")
    w("")
    w("| 间距值 | 出现次数 | 归属 |")
    w("|---|---|---|")
    for v, c in all_margin.most_common(40):
        w("| %d | %d | %s |" % (v, c, bucket(v)))
    w("")

    total = sum(all_margin.values())
    cnt10 = sum(c for v, c in all_margin.items() if v != 0 and v % 10 == 0)
    cnt5 = sum(c for v, c in all_margin.items() if v != 0 and v % 5 == 0)
    cnt4 = sum(c for v, c in all_margin.items() if v != 0 and v % 4 == 0)
    nonzero = sum(c for v, c in all_margin.items() if v != 0)
    w("对照（不含 0）：")
    w("")
    w("| 体系 | AWAKE | 原版 |")
    w("|---|---|---|")
    w("| 10 的倍数占比 | %.1f%% | 36.6%% / 35.1%%（水平/垂直） |" % (cnt10 / nonzero * 100 if nonzero else 0))
    w("| 5 的倍数占比 | %.1f%% | 55.2%% / 54.3%%（水平/垂直） |" % (cnt5 / nonzero * 100 if nonzero else 0))
    w("| 4 的倍数占比 | %.1f%% | 31.3%% / 31.1%%（水平/垂直） |" % (cnt4 / nonzero * 100 if nonzero else 0))
    w("")
    w("> 原版 5 的倍数占优；若 AWAKE 的 4 的倍数显著高于 5 的倍数，说明两套界面用的是**不同栅格**。")
    w("")

    w("## 3. AWAKE 的 Suggested 尺寸频次")
    w("")
    w("| 尺寸值 | 出现次数 | 归属 |")
    w("|---|---|---|")
    for v, c in all_size.most_common(40):
        w("| %d | %d | %s |" % (v, c, bucket(v)))
    w("")

    w("## 4. 逐文件明细")
    w("")
    for blk in detail_blocks:
        w(blk)
        w("")

    with io.open(OUT_MD, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(lines))

    print("files=%d" % len(files))
    print("report=%s" % OUT_MD)
    print("--- AWAKE margin 值 ---")
    for v, c in all_margin.most_common(40):
        print("  %5d : %-4d  %s" % (v, c, bucket(v)))
    print("--- AWAKE size 值 ---")
    for v, c in all_size.most_common(40):
        print("  %5d : %-4d  %s" % (v, c, bucket(v)))


if __name__ == "__main__":
    main()
