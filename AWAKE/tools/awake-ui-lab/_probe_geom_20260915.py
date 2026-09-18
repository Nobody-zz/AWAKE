# -*- coding: utf-8 -*-
"""临时探针：把 NpcDialogue 的几何报告里最极端的控件挑出来。
用完即删，不进提交。
"""
import io
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PY = sys.executable


def main():
    out = subprocess.run(
        [PY, "awake_ui.py", "check", "--prefab", "NpcDialogue.xml",
         "--no-shot", "--no-flow", "--no-native", "--json"],
        cwd=HERE, capture_output=True, text=True, encoding="utf-8",
    )
    raw = out.stdout
    # 报告可能混着日志行，从第一个 { 开始截
    idx = raw.find("{")
    if idx < 0:
        print("no json in stdout")
        print(raw[:2000])
        print("STDERR:", out.stderr[:2000])
        return
    d = json.loads(raw[idx:])

    print("top keys:", sorted(d.keys()))
    rep = d.get("report") or {}
    print("report keys:", sorted(rep.keys()))
    prefabs = rep.get("prefabs") or []
    if not prefabs:
        print(json.dumps(d, ensure_ascii=False)[:3000])
        return
    p = prefabs[0]

    print("--- 单面板字段 ---")
    for k in ("name", "widget_count", "panel", "issue_count", "grid_violations"):
        if k in p:
            print(" ", k, "=", p[k])

    dg = p.get("digest") or {}
    print("digest keys:", sorted(dg.keys()))
    lay = dg.get("layout") or []
    print("digest.layout 条数:", len(lay))

    # 形如 tag@depth:x,y,w,h
    parsed = []
    for s in lay:
        try:
            head, nums = s.rsplit(":", 1)
            tag, dep = head.rsplit("@", 1)
            x, y, w, h = (float(v) for v in nums.split(","))
        except Exception:
            continue
        parsed.append({"tag": tag, "d": int(dep), "x": x, "y": y, "w": w, "h": h})

    print("--- 最高 10 个 ---")
    for e in sorted(parsed, key=lambda e: -e["h"])[:10]:
        print("  %-26s d%-2d %7.0fx%-7.0f @(%7.0f,%8.0f)"
              % (e["tag"], e["d"], e["w"], e["h"], e["x"], e["y"]))
    print("--- 最宽 5 个 ---")
    for e in sorted(parsed, key=lambda e: -e["w"])[:5]:
        print("  %-26s d%-2d %7.0fx%-7.0f @(%7.0f,%8.0f)"
              % (e["tag"], e["d"], e["w"], e["h"], e["x"], e["y"]))
    if parsed:
        print("右边界 x+w =", round(max(e["x"] + e["w"] for e in parsed)))
        print("下边界 y+h =", round(max(e["y"] + e["h"] for e in parsed)))
        print("最小 x =", round(min(e["x"] for e in parsed)),
              " 最小 y =", round(min(e["y"] for e in parsed)))

    print("--- 窄条控件（w<=3，应是金条）---")
    for e in sorted([x for x in parsed if x["w"] <= 3], key=lambda e: e["h"]):
        print("  %-26s d%-2d %7.0fx%-7.0f @(%7.0f,%8.0f)"
              % (e["tag"], e["d"], e["w"], e["h"], e["x"], e["y"]))

    print("--- 问题明细 ---")
    for i in (p.get("issues") or []):
        print("  ", i)
    return

    g = p.get("geometry") or []
    g2 = sorted(g, key=lambda e: -e["h"])
    print("--- 最高 8 个 ---")
    for e in g2[:8]:
        print("  %-26s d%-2s %7.0fx%-7.0f @(%7.0f,%8.0f)"
              % (e["tag"], e["depth"], e["w"], e["h"], e["x"], e["y"]))
    print("--- 最宽 4 个 ---")
    for e in sorted(g, key=lambda e: -e["w"])[:4]:
        print("  %-26s d%-2s %7.0fx%-7.0f @(%7.0f,%8.0f)"
              % (e["tag"], e["depth"], e["w"], e["h"], e["x"], e["y"]))
    gx = max(e["x"] + e["w"] for e in g)
    gy = max(e["y"] + e["h"] for e in g)
    print("右边界 x+w =", round(gx), " 下边界 y+h =", round(gy))
    print("最小 y =", round(min(e["y"] for e in g)), " 最小 x =", round(min(e["x"] for e in g)))

    print("--- 问题明细 ---")
    for i in (p.get("issues") or []):
        print("  ", i)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
