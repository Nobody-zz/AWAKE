# -*- coding: utf-8 -*-
"""只读探针 2：看 feed_sweep 里 arms / literal / union 的内部结构。不写任何文件。"""
import json, os, io, sys

sys.stdout.reconfigure(encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__))

with io.open(os.path.join(HERE, "_feed_sweep_20260917.json"), "r", encoding="utf-8") as f:
    d = json.load(f)

print("== plans_token_len ==")
print(json.dumps(d["plans_token_len"], ensure_ascii=False)[:600])

print("\n== arms keys ==", list(d["arms"].keys()))
for mk in d["arms"]:
    arm = d["arms"][mk]
    print(" model:", mk, "type:", type(arm).__name__,
          "keys:", (list(arm.keys())[:20] if isinstance(arm, dict) else len(arm)))
    if isinstance(arm, dict):
        firstk = list(arm.keys())[0]
        v = arm[firstk]
        print("   first key:", firstk, "type:", type(v).__name__)
        print("   sample:", json.dumps(v, ensure_ascii=False)[:500])
        break

print("\n== literal sample ==")
lk = list(d["literal"].keys())[0]
print(" key:", lk)
print(" val:", json.dumps(d["literal"][lk], ensure_ascii=False)[:600])

print("\n== union (full) ==")
print(json.dumps(d["union"], ensure_ascii=False)[:1500])
