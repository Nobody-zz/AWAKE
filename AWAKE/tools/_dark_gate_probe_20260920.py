# -*- coding: utf-8 -*-
"""暗面批（09-20）gate 可达性探针：**逐条 grant 驱动**，找「写而不用」的表达。

做法：对本批每档的每条表达，取它的每个 grant → 造一个「本该由这条出场」的查询
（identity=profile），跑真 probe；比对**实际送出的表达**是否就是这条。
  · 送出 == 期望  → 可达（REACH）
  · 送出 != 期望  → 被压制（SHADOWED）
  · 一无所获      → 不达（MISS）

--pure：把 scope/detail 钉到 grant 自身，只测「谁知道」这一维。

用法：python _dark_gate_probe_20260920.py [--pure]
"""
import io
import json
import os
import subprocess
import sys

import yaml

REPO = r"D:\AWAKE-Dev\AWAKE"
WS = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1")
PKG = os.path.join(WS, r"compiled\geo1-v21-dark")
SIM = os.path.join(REPO, r"tools\worldbook-runtime-sim")

import glob

BATCH = []
for f in ["_dark_A_20260920.json"]:
    d = json.load(io.open(os.path.join(REPO, "docs/worldbook-migration/projection/authoring-out", f), encoding="utf-8"))
    BATCH += [x["slug"] for x in d["docs"]]

PURE = "--pure" in sys.argv
SPEC = os.path.join(WS, "_dark_gate_%s_spec_20260920.json" % ("pure" if PURE else "cap"))
OUT = os.path.join(WS, "_dark_gate_%s_result_20260920.json" % ("pure" if PURE else "cap"))

docs, meta = {}, {}
queries, expect = [], {}
for slug in BATCH:
    d = yaml.safe_load(io.open(os.path.join(WS, "authoring", slug + ".yaml"), encoding="utf-8"))
    docs[slug] = d
    name = (d.get("title") or {}).get("zh-CN") or slug
    texts = {}
    for a in d["assertions"]:
        for e in a["expressions"]:
            texts[e["text"]["zh-CN"]] = e["id"]
    meta[slug] = {"name": name, "texts": texts}
    n = 0
    for a in d["assertions"]:
        for e in a["expressions"]:
            seen_aud = set()
            for g in e["grants"]:
                culs = [x for x in (g.get("culture_ids") or [])] or [None]
                kgs = [x for x in (g.get("kingdom_ids") or [])] or [None]
                rls = [x for x in (g.get("role_ids") or [])] or [None]
                prof = (g.get("profile_id") or "").replace("profile.", "")
                for cu in culs:
                    for kg in kgs:
                        for rl in rls:
                            aud = (prof, cu, kg, rl)
                            if aud in seen_aud:
                                continue
                            seen_aud.add(aud)
                            n += 1
                            qn = "%s#%d" % (slug, n)
                            q = {"name": qn, "identity": "profile." + prof, "text": name}
                            if PURE:
                                q["scope"] = g["scope"]
                                q["detail"] = g["min_detail"]
                                q["requested_detail"] = g["min_detail"]
                            queries.append(q)
                            expect[qn] = (slug, e["id"], e["text"]["zh-CN"], e["layer"], aud)

json.dump({"queries": queries}, io.open(SPEC, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("档 %d，逐 grant 造问 %d 条 -> 跑 probe（%s）" % (len(BATCH), len(queries), "PURE" if PURE else "CAP"),
      flush=True)

r = subprocess.run(["dotnet", "run", "-c", "Release", "--", "probe",
                    os.path.join(PKG, "manifest.json"), SPEC, OUT], cwd=SIM, capture_output=True)
if r.returncode != 0:
    print(r.stdout.decode("utf-8", "replace")[-2000:])
    print(r.stderr.decode("utf-8", "replace")[-2000:])
    raise SystemExit(1)

res = json.load(io.open(OUT, encoding="utf-8"))
reach, shadow, miss = [], [], []
for row in res:
    slug, eid, txt, layer, aud = expect[row["name"]]
    served_id = None
    for t, i in meta[slug]["texts"].items():
        if t and t in (row["text"] or ""):
            served_id = i
            break
    if served_id is None:
        miss.append((slug, eid, aud, row["state"], (row["text"] or "")[:40]))
    elif served_id == eid:
        reach.append((slug, eid, aud))
    else:
        shadow.append((slug, eid, aud, served_id))

print()
print("逐 grant 问 %d 条：可达 %d / 被压制 %d / 不达 %d" % (len(res), len(reach), len(shadow), len(miss)))
print()
print("== 被压制（期望它出场，实际被别人顶了）==")
for x in shadow[:40]:
    print("   %-28s %-44s %s  ->  实际: %s" % (x[0], x[1], x[2], x[3]))
print()
print("== 不达（一无所获）==")
for x in miss[:40]:
    print("   %-28s %-44s %s state=%s" % (x[0], x[1], x[2], x[3]))
json.dump({"reach": reach, "shadow": shadow, "miss": miss},
          io.open(os.path.join(WS, "_dark_gate_%s_summary_20260920.json" % ("pure" if PURE else "cap")),
                  "w", encoding="utf-8"), ensure_ascii=False, indent=1)
