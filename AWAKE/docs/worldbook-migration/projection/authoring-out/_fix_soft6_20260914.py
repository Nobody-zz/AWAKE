# -*- coding: utf-8 -*-
"""世界书软项 7 处整改：预演/落盘。

纪律：
  - 只改正文（`zh-CN:` 正文行），**绝不碰 `quote:` / `quote_hash:`**（官方原文＋hash 校验）。
  - 双写：authoring-out/ ＋ workspace/full-geo1/authoring/。
  - 生成器同步：若目标串也在 `_rollout_*_gen_*.py` 里，一并改（防重跑回退）。

用法：python _fix_soft6_20260914.py          # 预演
      python _fix_soft6_20260914.py --apply  # 落盘
"""
import os, sys, glob

ROOT = r"D:/AWAKE-Dev/AWAKE"
AO = os.path.join(ROOT, "docs/worldbook-migration/projection/authoring-out")
WS = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1/authoring")

# (档名, 旧串, 新串)
EDITS = [
    ("item-western_plated_helmet.yaml", "护板结构兼顾侧面劈砍", "护板兼顾侧面劈砍"),
    ("troop-mamluk.yaml", "受系统的骑术、弓箭、刀术与战术教育", "受严整的骑术、弓箭、刀术与战术训练"),
    ("village-atrion.yaml", "收丝认水系", "收丝认水的来路"),
    ("village-deir-hawa.yaml", "今靠秘而不宣的技术从矿石提取贵金属。", "今靠秘而不宣的手艺从矿石里提银。"),
    ("village-deir-hawa.yaml", "提炼技术是村中秘传", "提炼的手艺是村中秘传"),
    ("village-lavenia.yaml", "尝得出水系", "尝得出水的来路"),
    ("weapon-crossbow.yaml", "毛病不在技术，在制度", "毛病不在弩，在军制"),
]

def is_quote(line):
    s = line.strip()
    return s.startswith("quote:") or s.startswith("quote_hash:")

apply = "--apply" in sys.argv
print("### 预演/落盘（quote 行一律跳过）")
for d in (AO, WS):
    print(f"--- {d}")
    for fn, old, new in EDITS:
        p = os.path.join(d, fn)
        if not os.path.exists(p):
            print(f"  (缺) {fn}"); continue
        lines = open(p, encoding="utf-8").read().splitlines(keepends=True)
        body, quote = 0, 0
        out = []
        for line in lines:
            if old in line:
                if is_quote(line):
                    quote += 1
                    out.append(line)
                else:
                    body += line.count(old)
                    out.append(line.replace(old, new))
            else:
                out.append(line)
        tag = "正文x%d" % body + (f" ⚠️quote命中x{quote}(未改)" if quote else "")
        print(f"  {fn}: {tag}")
        if apply and body:
            open(p, "w", encoding="utf-8", newline="").write("".join(out))

# 生成器同步
print("### 生成器同步")
GENS = sorted(glob.glob(os.path.join(AO, "*_gen_*.py")))
for g in GENS:
    raw = open(g, encoding="utf-8").read()
    hit = [(o, n, raw.count(o)) for _, o, n in EDITS if o in raw]
    if hit:
        print(f"  {os.path.basename(g)}:")
        for o, n, c in hit:
            print(f"     x{c}  {o[:20]}… → {n[:20]}…")
        if apply:
            for o, n, _ in hit:
                raw = raw.replace(o, n)
            open(g, "w", encoding="utf-8", newline="").write(raw)
print("(未落盘)" if not apply else "(已落盘)")
