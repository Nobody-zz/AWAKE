# -*- coding: utf-8 -*-
"""只读：核 FEED §一 那组语料数字在当前包上的对应值（不改任何文件）。"""
import json, os, io, sys
from collections import Counter

sys.stdout.reconfigure(encoding="utf-8")
ROOT = r"D:\AWAKE-Dev\AWAKE"
PKG = os.path.join(ROOT, "ModuleData", "Worldbook", "packages")

for base, dirs, files in os.walk(PKG):
    for fn in files:
        if fn.endswith(".json"):
            p = os.path.join(base, fn)
            print("pkg file:", os.path.relpath(p, ROOT), os.path.getsize(p))

cand = os.path.join(PKG, "calradia", "runtime.json")
if not os.path.exists(cand):
    print("!! not found:", cand)
    sys.exit(1)
with io.open(cand, "r", encoding="utf-8") as f:
    d = json.load(f)
print("\nruntime.json top type:", type(d).__name__)
if isinstance(d, dict):
    print("top keys:", list(d.keys())[:20])
entries = d.get("entries") if isinstance(d, dict) else d
print("entries count:", len(entries))

te = sum(1 for e in entries if isinstance(e, dict) and isinstance(e.get("title"), dict) and "en" in e["title"])
tz = sum(1 for e in entries if isinstance(e, dict) and isinstance(e.get("title"), dict) and "zh-CN" in e["title"])
sm = sum(1 for e in entries if isinstance(e, dict) and isinstance(e.get("summary"), dict) and "zh-CN" in e["summary"].__str__() or True)
# 更稳：直接看字段形态
def lang_keys(v):
    return sorted(v.keys()) if isinstance(v, dict) else None

n_summary_zh = 0
n_summary_en = 0
n_expr = 0
n_expr_zh = 0
n_expr_en = 0
for e in entries:
    if not isinstance(e, dict):
        continue
    s = e.get("summary")
    if isinstance(s, dict):
        if "zh-CN" in s:
            n_summary_zh += 1
        if "en" in s:
            n_summary_en += 1
    ex = e.get("expressions")
    if isinstance(ex, list):
        for it in ex:
            if isinstance(it, dict):
                n_expr += 1
                t = it.get("text")
                if isinstance(t, dict):
                    if "zh-CN" in t:
                        n_expr_zh += 1
                    if "en" in t:
                        n_expr_en += 1

print("title.en:", te, " title.zh-CN:", tz)
print("summary.zh-CN:", n_summary_zh, " summary.en:", n_summary_en)
print("expression.text 总:", n_expr, " zh-CN:", n_expr_zh, " en:", n_expr_en)
print("domain 分布:", Counter(e.get("domain") for e in entries if isinstance(e, dict)))
