# -*- coding: utf-8 -*-
"""核 21 档里：同一档内「第 1 条断言」与后续断言的表达，授予条件是不是完全相同。
完全相同 ⇒ 同层同分 ⇒ 按 `SelectExpression` 同分取先者，后续断言**排不到**（与送达探针读数互证）。
"""
import io
import json
import os

import yaml

MIRR = r"D:\AWAKE-Dev\AWAKE\docs\worldbook-migration\projection\authoring-out"
J = {}
for n in (1, 2, 3):
    J.update(json.load(io.open(os.path.join(MIRR, "_status_l2_towns_%d_20260919.json" % n), encoding="utf-8")))
targets = sorted(k for k in J if not k.startswith("_"))


def sig(grants):
    return json.dumps(sorted(json.dumps(g, sort_keys=True, ensure_ascii=False) for g in grants),
                      ensure_ascii=False)


same = 0
diff = []
for t in targets:
    d = yaml.safe_load(io.open(os.path.join(MIRR, t), encoding="utf-8"))
    asrts = d["assertions"]
    by = [{} for _ in asrts]           # 每条断言：layer -> 授予签名
    for i, a in enumerate(asrts):
        for e in a.get("expressions") or []:
            by[i][e["layer"]] = sig(e.get("grants") or [])
    base = by[0]
    ok = True
    for i in range(1, len(asrts)):
        for layer, s in by[i].items():
            if layer in base and base[layer] != s:
                ok = False
            if layer not in base:
                ok = False
    if ok:
        same += 1
    else:
        diff.append((t, [sorted(x) for x in by]))
print("21 档中：后续断言授予条件与首条**完全同款**（⇒ 同分排不到）: %d" % same)
print("        有区别的: %d" % len(diff))
for t, b in diff:
    print("   ", t, b)

# 层覆盖：每条断言各自有哪些层
print()
print("逐档层分布（断言序号: 层）:")
for t in targets:
    d = yaml.safe_load(io.open(os.path.join(MIRR, t), encoding="utf-8"))
    row = []
    for a in d["assertions"]:
        row.append(a["kind"][:4] + "[" + "/".join(e["layer"] for e in a.get("expressions") or []) + "]")
    print("  %-30s %s" % (t, "  ".join(row)))
