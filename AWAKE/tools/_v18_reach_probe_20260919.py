# -*- coding: utf-8 -*-
"""读数：一档里我写的多条断言，到底哪一条的表达**真被送出去**了。

背景（读代码得的）：`WorldKnowledgeQueryService.SelectExpression` 对**每一档**只挑**一条**表达
（`score = ruleScore * 10 + 层号`，同分取先者）。所以「把一件事拆成多条断言」之后，
排不到的那几条在当前授予条件下永远不出场 —— 等于写而不用。

做法：21 档 × 3 种身份，用档的 zh-CN 标题当问句，跑真 `probe`，
把**收到的正文**逐一回贴到「哪一档、第几条断言、哪一层」；
最后按「送出的是第几条断言」汇总（第 1 条 = 索引 0）。
"""
import io
import json
import os
import subprocess

REPO = r"D:\AWAKE-Dev\AWAKE"
MIRR = os.path.join(REPO, r"docs\worldbook-migration\projection\authoring-out")
WS = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1")
PKG = os.path.join(WS, r"compiled\geo1-v18-status-trust")
SIM = os.path.join(REPO, r"tools\worldbook-runtime-sim")
SPEC = os.path.join(WS, "_reach_spec_20260919.json")
OUT = os.path.join(WS, "_reach_result_20260919.json")

import yaml

J = {}
for n in (1, 2, 3):
    J.update(json.load(io.open(os.path.join(MIRR, "_status_l2_towns_%d_20260919.json" % n), encoding="utf-8")))
targets = sorted(k for k in J if not k.startswith("_"))

IDENT = ["profile.villager", "profile.merchant", "profile.noble"]
docs = {}
queries = []
for t in targets:
    d = yaml.safe_load(io.open(os.path.join(MIRR, t), encoding="utf-8"))
    name = (d.get("title") or {}).get("zh-CN") or t
    idx = {}
    for i, a in enumerate(d["assertions"]):
        for e in a.get("expressions") or []:
            idx[e["text"]["zh-CN"]] = (i, a["kind"], e["layer"], e["id"])
    docs[name] = {"file": t, "texts": idx}
    for idn in IDENT:
        queries.append({"name": "%s|%s" % (name, idn), "identity": idn, "text": name})

json.dump({"queries": queries}, io.open(SPEC, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("档 %d，问 %d 条 -> 跑 probe" % (len(targets), len(queries)), flush=True)

r = subprocess.run(["dotnet", "run", "-c", "Release", "--", "probe",
                    os.path.join(PKG, "manifest.json"), SPEC, OUT],
                   cwd=SIM, capture_output=True)
if r.returncode != 0:
    print(r.stdout.decode("utf-8", "replace")[-1500:])
    print(r.stderr.decode("utf-8", "replace")[-1500:])
    raise SystemExit(1)

res = json.load(io.open(OUT, encoding="utf-8"))
hit_first = 0
hit_other = []
miss = []
per_assert = {}
for row in res:
    name, idn = row["name"].split("|")
    info = docs[name]
    t = (row["text"] or "").split("  ")[0].strip()
    found = None
    for txt, meta in info["texts"].items():
        if txt and txt in row["text"]:
            found = meta
            break
    if found is None:
        miss.append((name, idn, row["state"], (row["text"] or "")[:40]))
        continue
    per_assert[found[0]] = per_assert.get(found[0], 0) + 1
    if found[0] == 0:
        hit_first += 1
    else:
        hit_other.append((name, idn, found))

print()
print("问 %d 条；收到正文并能对上我写的表达：%d 条" % (len(res), hit_first + len(hit_other)))
print("  送出的是【第 1 条断言】: %d" % hit_first)
print("  送出的是【第 2 条及以后】: %d  %s" % (len(hit_other), hit_other[:5]))
print("  没对上（答复为空或来自别的档）: %d" % len(miss))
for m in miss[:8]:
    print("     ", m)
print()
print("按断言序号汇总（送出次数）：", dict(sorted(per_assert.items())))
