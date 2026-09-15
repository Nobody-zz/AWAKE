# -*- coding: utf-8 -*-
"""
扫描骑砍原版（Native / SandBox / SandBoxCore / StoryMode / Multiplayer）全部 GUI Prefab，
统计原版面板尺寸与间距习惯，输出 Markdown 报告 + JSON 原始数据。

用途：给 AWAKE 自研 UI 提供"原版尺寸基准"，避免拍脑袋定数字。
只读游戏目录，不修改任何文件。

用法：python scan_native_prefab_metrics.py
"""
import os
import re
import io
import json
from collections import Counter

import xml.etree.ElementTree as ET

GAME = r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
MODULES = ["Native", "SandBox", "SandBoxCore", "StoryMode", "Multiplayer"]
OUT_DIR = r"D:\AWAKE-Dev\AWAKE\docs"
OUT_MD = os.path.join(OUT_DIR, "NATIVE-PREFAB-METRICS-20260913.md")
OUT_JSON = os.path.join(OUT_DIR, "native-prefab-metrics-20260913.json")

MARGIN_ATTRS = ["MarginLeft", "MarginRight", "MarginTop", "MarginBottom"]
SIZE_ATTRS = ["SuggestedWidth", "SuggestedHeight"]

INT_RE = re.compile(r"^-?\d+$")


def as_int(value):
    if value is None:
        return None
    value = value.strip()
    if INT_RE.match(value):
        return int(value)
    return None


def collect_prefabs():
    found = []
    for module in MODULES:
        root = os.path.join(GAME, "Modules", module, "GUI", "Prefabs")
        if not os.path.isdir(root):
            continue
        for dirpath, _dirnames, filenames in os.walk(root):
            for name in filenames:
                if name.lower().endswith(".xml"):
                    full = os.path.join(dirpath, name)
                    rel = os.path.relpath(full, root).replace("\\", "/")
                    found.append((module, rel, full))
    return sorted(found)


def is_control(el):
    return ("WidthSizePolicy" in el.attrib) or ("HeightSizePolicy" in el.attrib)


def main():
    prefabs = collect_prefabs()

    margin_h = Counter()   # MarginLeft / MarginRight
    margin_v = Counter()   # MarginTop / MarginBottom
    width_counter = Counter()
    height_counter = Counter()
    tag_counter = Counter()
    policy_counter = Counter()
    per_file_roots = {}
    parse_errors = []

    total_controls = 0

    for module, rel, full in prefabs:
        try:
            tree = ET.parse(full)
        except Exception as exc:  # noqa: BLE001
            parse_errors.append((module + "/" + rel, str(exc)))
            continue
        root = tree.getroot()

        controls_here = []

        def walk(el, depth):
            nonlocal total_controls
            if is_control(el):
                total_controls += 1
                tag = el.tag
                tag_counter[tag] += 1
                policy = (el.attrib.get("WidthSizePolicy", ""), el.attrib.get("HeightSizePolicy", ""))
                policy_counter[policy] += 1

                ml = as_int(el.attrib.get("MarginLeft"))
                mr = as_int(el.attrib.get("MarginRight"))
                mt = as_int(el.attrib.get("MarginTop"))
                mb = as_int(el.attrib.get("MarginBottom"))
                if ml is not None:
                    margin_h[ml] += 1
                if mr is not None:
                    margin_h[mr] += 1
                if mt is not None:
                    margin_v[mt] += 1
                if mb is not None:
                    margin_v[mb] += 1

                sw = as_int(el.attrib.get(SIZE_ATTRS[0]))
                sh = as_int(el.attrib.get(SIZE_ATTRS[1]))
                if sw is not None:
                    width_counter[sw] += 1
                if sh is not None:
                    height_counter[sh] += 1

                controls_here.append({
                    "depth": depth,
                    "tag": tag,
                    "w": sw,
                    "h": sh,
                    "wp": el.attrib.get("WidthSizePolicy", ""),
                    "hp": el.attrib.get("HeightSizePolicy", ""),
                    "ha": el.attrib.get("HorizontalAlignment", ""),
                    "va": el.attrib.get("VerticalAlignment", ""),
                })

            for child in el:
                walk(child, depth + 1)

        walk(root, 0)
        per_file_roots[module + "/" + rel] = controls_here

    # --- 顶层固定尺寸面板（depth 最浅的 Fixed x Fixed 且有宽高） ---
    top_panels = []
    for key, controls in per_file_roots.items():
        cands = [c for c in controls if c["w"] and c["h"] and c["wp"] == "Fixed" and c["hp"] == "Fixed"]
        if cands:
            cands.sort(key=lambda c: c["depth"])
            best = cands[0]
            top_panels.append((key, best["w"], best["h"], best["depth"]))
    top_panels.sort(key=lambda t: (-t[1]))

    # --- 台阶分析 ---
    def alignment(values):
        vals = [v for v in values if v != 0]
        if not vals:
            return {}
        return {
            "总取值数": len(vals),
            "5的倍数占比": round(sum(1 for v in vals if v % 5 == 0) / len(vals) * 100, 1),
            "10的倍数占比": round(sum(1 for v in vals if v % 10 == 0) / len(vals) * 100, 1),
            "4的倍数占比": round(sum(1 for v in vals if v % 4 == 0) / len(vals) * 100, 1),
            "2的倍数占比": round(sum(1 for v in vals if v % 2 == 0) / len(vals) * 100, 1),
            "最小值": min(vals),
            "最大值": max(vals),
        }

    all_margin = Counter()
    all_margin.update(margin_h)
    all_margin.update(margin_v)

    report = []
    w = report.append
    w("# 骑砍原版 Prefab 尺寸与间距统计（只读扫描）")
    w("")
    w("> 来源：`%s`" % GAME)
    w("> 模块：%s" % "、".join(MODULES))
    w("> 生成脚本：`AWAKE/docs/scan_native_prefab_metrics.py`（可重跑）")
    w("")
    w("## 1. 总览")
    w("")
    w("- Prefab 文件数：**%d**" % len(prefabs))
    w("- 布局控件总数（带 SizePolicy 的元素）：**%d**" % total_controls)
    if parse_errors:
        w("- 解析失败：%d 个" % len(parse_errors))
        for name, err in parse_errors:
            w("  - `%s`：%s" % (name, err))
    w("")
    w("## 2. 尺寸策略组合频次（Top 10）")
    w("")
    w("| WidthSizePolicy | HeightSizePolicy | 次数 |")
    w("|---|---|---|")
    for (wp, hp), cnt in policy_counter.most_common(10):
        w("| %s | %s | %d |" % (wp or "-", hp or "-", cnt))
    w("")
    w("## 3. 控件类型频次（Top 15）")
    w("")
    w("| 控件 | 次数 |")
    w("|---|---|")
    for tag, cnt in tag_counter.most_common(15):
        w("| `%s` | %d |" % (tag, cnt))
    w("")

    def dump_counter(title, counter, limit):
        w("### %s" % title)
        w("")
        w("| 值 | 次数 |")
        w("|---|---|")
        for value, cnt in counter.most_common(limit):
            w("| %d | %d |" % (value, cnt))
        w("")

    w("## 4. 间距使用习惯")
    w("")
    w("水平间距（MarginLeft / MarginRight 合并）与垂直间距（MarginTop / MarginBottom 合并）：")
    w("")
    dump_counter("4.1 水平间距 Top 25", margin_h, 25)
    dump_counter("4.2 垂直间距 Top 25", margin_v, 25)
    w("台阶分析（不含 0）：")
    w("")
    w("| 维度 | " + " | ".join(alignment(margin_h).keys()) + " |")
    w("|---|" + "---|" * len(alignment(margin_h)))
    ha = alignment(margin_h)
    va = alignment(margin_v)
    w("| 水平 | " + " | ".join(str(ha[k]) for k in ha) + " |")
    w("| 垂直 | " + " | ".join(str(va[k]) for k in va) + " |")
    w("")

    w("## 5. 建议尺寸使用习惯")
    w("")
    dump_counter("5.1 SuggestedWidth Top 25", width_counter, 25)
    dump_counter("5.2 SuggestedHeight Top 25", height_counter, 25)
    w("")
    w("## 6. 各 Prefab 的最外层固定尺寸面板")
    w("")
    w("共 **%d** 个 Prefab 的最外层是「固定尺寸」面板。先看尺寸分布：" % len(top_panels))
    w("")
    panel_w = Counter(pw for _k, pw, _ph, _d in top_panels)
    panel_h = Counter(ph for _k, _pw, ph, _d in top_panels)
    w("### 6.1 面板宽度分布 Top 20")
    w("")
    w("| 宽 | 次数 |")
    w("|---|---|")
    for value, cnt in panel_w.most_common(20):
        w("| %d | %d |" % (value, cnt))
    w("")
    w("### 6.2 面板高度分布 Top 20")
    w("")
    w("| 高 | 次数 |")
    w("|---|---|")
    for value, cnt in panel_h.most_common(20):
        w("| %d | %d |" % (value, cnt))
    w("")
    w("### 6.3 面板清单（按宽度降序）")
    w("")
    w("| 模块/文件 | 宽 | 高 | 深度 |")
    w("|---|---|---|---|")
    for key, pw, ph, depth in top_panels:
        w("| `%s` | %d | %d | %d |" % (key, pw, ph, depth))
    w("")
    w("## 7. 参考：标准控件贴图尺寸（StdAssets）")
    w("")
    w("> 贴图像素尺寸不等同 UI 显示尺寸；九宫格 sprite 会被拉伸。此处仅供了解标准控件天生比例。")
    w("")
    for line in [
        "| Sprite | 宽 | 高 |",
        "|---|---|---|",
        "| `StdAssets\\Popup\\canvas` | 512 | 645 |",
        "| `StdAssets\\Popup\\canvas_dark` | 699 | 666 |",
        "| `StdAssets\\triple_button_frame` | 713 | 126 |",
        "| `StdAssets\\triple_button_left` | 296 | 55 |",
        "| `StdAssets\\triple_button_mid` | 96 | 82 |",
        "| `StdAssets\\triple_button_right` | 293 | 55 |",
    ]:
        w(line)
    w("")

    os.makedirs(OUT_DIR, exist_ok=True)
    with io.open(OUT_MD, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(report))

    payload = {
        "game_root": GAME,
        "modules": MODULES,
        "prefab_files": len(prefabs),
        "controls": total_controls,
        "margin_horizontal": dict(margin_h),
        "margin_vertical": dict(margin_v),
        "suggested_width": dict(width_counter),
        "suggested_height": dict(height_counter),
        "size_policy": {"%s|%s" % k: v for k, v in policy_counter.items()},
        "top_panels": [{"file": k, "w": pw, "h": ph, "depth": d} for k, pw, ph, d in top_panels],
        "per_file": per_file_roots,
    }
    with io.open(OUT_JSON, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(json.dumps(payload, ensure_ascii=False, indent=2))

    print("prefab_files=%d controls=%d parse_errors=%d" % (len(prefabs), total_controls, len(parse_errors)))
    print("report=%s" % OUT_MD)
    print("json=%s" % OUT_JSON)
    print("--- margin_h top10 ---")
    for v, c in margin_h.most_common(10):
        print("  %5d : %d" % (v, c))
    print("--- margin_v top10 ---")
    for v, c in margin_v.most_common(10):
        print("  %5d : %d" % (v, c))
    print("--- width top10 ---")
    for v, c in width_counter.most_common(10):
        print("  %5d : %d" % (v, c))
    print("--- height top10 ---")
    for v, c in height_counter.most_common(10):
        print("  %5d : %d" % (v, c))
    print("--- top panels (first 15) ---")
    for key, pw, ph, depth in top_panels[:15]:
        print("  %-52s %5d x %5d  d=%d" % (key, pw, ph, depth))


if __name__ == "__main__":
    main()
