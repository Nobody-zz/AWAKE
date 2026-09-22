# -*- coding: utf-8 -*-
"""军事批（09-20）gate 可达性探针：**逐条 grant 驱动**，找「写而不用」的表达。

做法：对本批每档的每一条表达，取它的每个 grant → 造一个「本该由这条出场」的查询
（identity=profile，culture/kingdom/role 照 grant 填），跑真 probe；
比对**实际送出的表达**是否就是这条。
  · 送出 == 期望  → 可达（REACH）
  · 送出 != 期望  → 被压制（SHADOWED，记下是谁压的）
  · 一无所获      → 不达（MISS）

这一条比固定矩阵更能回答问题：**我写的表达里有没有永远送不到人耳的**。

用法：python _mil_gate_probe_20260920.py
"""
import io
import json
import os
import subprocess
import sys

import yaml

REPO = r"D:\AWAKE-Dev\AWAKE"
WS = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1")
PKG = os.path.join(WS, r"compiled\geo1-v19-military")
SIM = os.path.join(REPO, r"tools\worldbook-runtime-sim")

BATCH = ["troops-banner-knight", "troops-khans-guard", "troops-ghulam", "troops-vaegir-guard",
         "troops-druzhinnik-cavalry", "troops-golden-boar", "troops-legion-of-the-betrayed",
         "troops-cataphract", "military-legion-old", "military-legion-modern", "war_history-kuyug",
         "military-empire-system", "military-battania", "military-empire-north",
         "military-empire-south", "military-empire-west", "military-nord", "military-aserai",
         "weapons-crossbow-ironbound", "weapons-ballista", "weapons-ballista-fire",
         "weapons-mangonel", "weapons-mangonel-fire", "weapons-siege-tower", "weapons-siege-ram"]

# --pure：把 scope/detail 覆盖成 grant 自身的值，只测「谁知道」（身份×文化×王国×角色）这一维；
#         不带则尊重身份真实能力上限，额外暴露「grant 与身份能力不匹配」。
PURE = "--pure" in sys.argv
SPEC = os.path.join(WS, "_mil_gate_%s_spec_20260920.json" % ("pure" if PURE else "cap"))
OUT = os.path.join(WS, "_mil_gate_%s_result_20260920.json" % ("pure" if PURE else "cap"))


def bare(v):
    for p in ("entity.culture.", "awake:culture:", "entity.kingdom.", "awake:kingdom:",
              "entity.role.", "awake:role:"):
        v = v.replace(p, "")
    return v


docs, meta = {}, {}
queries = []
expect = {}   # qname -> (slug, expr_id, text, layer)
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
                culs = [bare(x) for x in (g.get("culture_ids") or [])] or [None]
                kgs = [bare(x) for x in (g.get("kingdom_ids") or [])] or [None]
                rls = [bare(x) for x in (g.get("role_ids") or [])] or [None]
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
                            if cu:
                                q["culture"] = cu
                            if kg:
                                q["kingdom"] = kg
                            if rl:
                                q["role"] = rl
                            if PURE:
                                # 只测「谁知道」：把 scope/detail 钉到 grant 自身，排除能力上限干扰
                                q["scope"] = g["scope"]
                                q["detail"] = g["min_detail"]
                                q["requested_detail"] = g["min_detail"]
                            queries.append(q)
                            expect[qn] = (slug, e["id"], e["text"]["zh-CN"], e["layer"], aud)

json.dump({"queries": queries}, io.open(SPEC, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("档 %d，逐 grant 造问 %d 条 -> 跑 probe" % (len(BATCH), len(queries)), flush=True)

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
    print("   %-26s %-40s %s  ->  实际: %s" % (x[0], x[1], x[2], x[3]))
print()
print("== 不达（一无所获）==")
for x in miss[:40]:
    print("   %-26s %-40s %s state=%s" % (x[0], x[1], x[2], x[3]))
json.dump({"reach": reach, "shadow": shadow, "miss": miss},
          io.open(os.path.join(WS, "_mil_gate_%s_summary_20260920.json" % ("pure" if PURE else "cap")),
                  "w", encoding="utf-8"), ensure_ascii=False, indent=1)
