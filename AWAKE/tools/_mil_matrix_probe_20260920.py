# -*- coding: utf-8 -*-
"""军事批（09-20）矩阵探针：25 档 × 6 种身份，跑真 probe，看**送出的是哪一条断言的表达**。

背景（读代码得的）：`SelectExpression` 对每一档只挑**一条**表达（`score=ruleScore*10+层号`，同分取先）。
故「把一件事拆成多条断言」后，排不到的断言在当前授予条件下永不出场。

6 行矩阵（本批受众门实测：profiles 7 种 / cultures 8 种 / kingdoms 3 种 / roles 仅 lord）：
  R1 平民      villager
  R2 市民      townsfolk
  R3 商人      merchant
  R4 本乡士兵  soldier       + 本乡 culture + 本乡 kingdom
  R5 本乡贵族  noble         + 本乡 culture + 本乡 kingdom + role=lord
  R6 工师管家  noble_high_steward + 本乡 culture + 本乡 kingdom

「本乡 culture/kingdom」= 本档 grants 里出现最多者（无则留空）。
用法：python _mil_matrix_probe_20260920.py
"""
import glob
import io
import json
import os
import subprocess
from collections import Counter

import yaml

REPO = r"D:\AWAKE-Dev\AWAKE"
AO = os.path.join(REPO, r"docs\worldbook-migration\projection\authoring-out")
WS = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1")
PKG = os.path.join(WS, r"compiled\geo1-v19-military")
SIM = os.path.join(REPO, r"tools\worldbook-runtime-sim")
SPEC = os.path.join(WS, "_mil_matrix_spec_20260920.json")
OUT = os.path.join(WS, "_mil_matrix_result_20260920.json")

BATCH = ["troops-banner-knight", "troops-khans-guard", "troops-ghulam", "troops-vaegir-guard",
         "troops-druzhinnik-cavalry", "troops-golden-boar", "troops-legion-of-the-betrayed",
         "troops-cataphract", "military-legion-old", "military-legion-modern", "war_history-kuyug",
         "military-empire-system", "military-battania", "military-empire-north",
         "military-empire-south", "military-empire-west", "military-nord", "military-aserai",
         "weapons-crossbow-ironbound", "weapons-ballista", "weapons-ballista-fire",
         "weapons-mangonel", "weapons-mangonel-fire", "weapons-siege-tower", "weapons-siege-ram"]

# ---- 载入档（直接从落盘 yaml 读，25 档统一，含 09-19 的 cataphract） ----
docs = {}
for slug in BATCH:
    y = yaml.safe_load(io.open(os.path.join(WS, "authoring", slug + ".yaml"), encoding="utf-8"))
    docs[slug] = y

ROWS = [
    ("R1本乡平民", dict(identity="profile.villager", culture="@c", kingdom="@k")),
    ("R2市民", dict(identity="profile.townsfolk")),
    ("R3商人", dict(identity="profile.merchant")),
    ("R4本乡士兵", dict(identity="profile.soldier", culture="@c", kingdom="@k")),
    ("R5本乡贵族", dict(identity="profile.noble", culture="@c", kingdom="@k", role="lord")),
    ("R6工师管家", dict(identity="profile.noble_high_steward", culture="@c", kingdom="@k")),
    ("N7外地平民", dict(identity="profile.villager")),   # 阴性对照：无文化 ⇒ 期望「一无所知」
]

queries = []
meta = {}
for slug in BATCH:
    d = docs[slug]
    name = (d.get("title") or {}).get("zh-CN") or slug
    cs, ks = Counter(), Counter()
    expr_meta = {}
    for i, a in enumerate(d["assertions"]):
        for e in a["expressions"]:
            expr_meta[e["text"]["zh-CN"]] = (i, a["kind"], e["layer"], e["id"])
            for g in e["grants"]:
                for c in g.get("culture_ids") or []:
                    cs[str(c).replace("entity.culture.", "").replace("awake:culture:", "")] += 1
                for k in g.get("kingdom_ids") or []:
                    ks[str(k).replace("entity.kingdom.", "").replace("awake:kingdom:", "")] += 1
    hc = cs.most_common(1)[0][0] if cs else ""
    hk = ks.most_common(1)[0][0] if ks else ""
    meta[name] = {"slug": slug, "texts": expr_meta, "hc": hc, "hk": hk}
    for rn, spec in ROWS:
        q = {"name": "%s|%s" % (name, rn), "identity": spec["identity"], "text": name}
        for k, v in spec.items():
            if k == "identity":
                continue
            q[k] = hc if v == "@c" else (hk if v == "@k" else v)
        if spec.get("culture") == "@c" and hc:
            q["culture"] = hc
        if spec.get("kingdom") == "@k" and hk:
            q["kingdom"] = hk
        q = {k: v for k, v in q.items() if v != ""}
        queries.append(q)

json.dump({"queries": queries}, io.open(SPEC, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("档 %d，问 %d 条 -> 跑 probe（包 %s）" % (len(BATCH), len(queries), os.path.basename(PKG)), flush=True)

r = subprocess.run(["dotnet", "run", "-c", "Release", "--", "probe",
                    os.path.join(PKG, "manifest.json"), SPEC, OUT],
                   cwd=SIM, capture_output=True)
if r.returncode != 0:
    print(r.stdout.decode("utf-8", "replace")[-2000:])
    print(r.stderr.decode("utf-8", "replace")[-2000:])
    raise SystemExit(1)

res = json.load(io.open(OUT, encoding="utf-8"))
rows_out = []
per_assert = Counter()
row_stat = {}   # 行 -> Counter(结果)
miss = []
for row in res:
    name, rn = row["name"].split("|")
    info = meta[name]
    served = None
    for txt, m in info["texts"].items():
        if txt and txt in (row["text"] or ""):
            served = m
            break
    rec = {"slug": info["slug"], "row": rn, "state": row["state"], "mode": row.get("match_mode"),
           "hits": row.get("hits"), "served_assert": served[0] if served else None,
           "served_kind": served[1] if served else None, "served_layer": served[2] if served else None,
           "served_expr": served[3] if served else None, "n_expr": len(info["texts"])}
    rows_out.append(rec)
    st = row_stat.setdefault(rn, Counter())
    if served is None:
        st["未对上"] += 1
        miss.append((info["slug"], rn, row["state"], (row["text"] or "")[:46]))
        continue
    per_assert[served[0]] += 1
    st["a%d" % served[0]] += 1

json.dump(rows_out, io.open(os.path.join(WS, "_mil_matrix_rows_20260920.json"), "w", encoding="utf-8"),
          ensure_ascii=False, indent=1)

print()
print("问 %d 条；能对上本档表达：%d 条" % (len(res), len(res) - len(miss)))
print()
print("== 真矩阵（行 × 送出的是第几条断言）==")
order = [r[0] for r in ROWS]
print("%-12s %5s %5s %5s %6s" % ("行", "a0", "a1", "a2", "未对上"))
for rn in order:
    st = row_stat.get(rn, Counter())
    print("%-12s %5d %5d %5d %6d   %s" % (rn, st.get("a0", 0), st.get("a1", 0), st.get("a2", 0),
                                          st.get("未对上", 0), dict(st)))
print()
print("按断言序号汇总（全 175 问）：", dict(sorted(per_assert.items())))
print("未对上 %d 条：" % len(miss))
for m in miss[:20]:
    print("   %-26s %-10s %-10s %s" % m)
