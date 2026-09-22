# -*- coding: utf-8 -*-
"""把同一份探针规格在 v17 基线与 v18 产物上跑出来的两份结果并排比：
  · 哪几条答案变了（期望 = 我改的 21 档里被问到的那几条）；
  · 哪几条一字不变（期望含对照档）。
"""
import io
import json
import os

WS = r"D:\AWAKE-Dev\AWAKE\tools\worldbook-studio\workspace\full-geo1"

v17 = json.load(io.open(os.path.join(WS, "_v18_probe_result_v17.json"), encoding="utf-8"))
v18 = json.load(io.open(os.path.join(WS, "_v18_probe_result_v18.json"), encoding="utf-8"))
b17 = {r["name"]: r for r in v17}
b18 = {r["name"]: r for r in v18}

print("%-26s %-9s %-9s %-6s %s" % ("问", "v17层态", "v18层态", "换词?", "v18 答话"))
print("-" * 120)
changed = []
for n in b18:
    a, b = b17[n], b18[n]
    same_txt = (a["text"] == b["text"])
    same_state = (a["state"] == b["state"] and a["detail"] == b["detail"])
    if a["text"] != b["text"]:
        changed.append(n)
    print("%-26s %-9s %-9s %-6s %s" % (
        n, a["state"] + "/" + str(a["detail"]), b["state"] + "/" + str(b["detail"]),
        "变" if not same_txt else "不变",
        (b["text"] or "(空)")[:66]))

print()
print("答案变了的问:", len(changed), changed)
print("层态(known/partial/blocked × 层名)变了的问:",
      [n for n in b18 if b17[n]["state"] != b18[n]["state"] or b17[n]["detail"] != b18[n]["detail"]])
print()
print("--- 明细：变了的问，前后对照 ---")
for n in changed:
    print("·", n, " identity=", b18[n]["identity"], " detail=", b18[n]["detail"])
    print("   前:", b17[n]["text"])
    print("   后:", b18[n]["text"])
