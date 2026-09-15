# -*- coding: utf-8 -*-
"""越限自检（IMPL §一 步骤4）：对 authoring-out 全部 22 档逐表达断言
  ① grant ≤ 身份能力上限（noble/noble_high_steward 按 age≥45→secret 建模，其余静态）
  ② 不变式 grant.min_detail == 所在表达 layer
输出 self-check 报告。"""
import yaml, glob, io, os, json, sys

RANK_D = {"rumor":1,"summary":2,"detail":3,"secret":4}
RANK_S = {"local":1,"regional":2,"national":3,"faction":4,"elite":5,"private":6}
CAP = {
 "commoner":("local","rumor"),"villager":("local","rumor"),"townsfolk":("regional","summary"),
 "notable":("regional","detail"),"headman":("national","detail"),"merchant":("faction","detail"),
 "tavernkeeper":("faction","detail"),"ransom_broker":("faction","detail"),"soldier":("national","detail"),
 "anonymous":("",""),
}
# noble 系：scope=elite；detail 上限=secret（age>=45 或 management>=80 时可达，其余年龄更低）
NOBLE = ("elite","secret")

ROOT = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out"
viol, total_expr, total_grants = [], 0, 0
for f in sorted(glob.glob(os.path.join(ROOT, "*.yaml"))):
    d = yaml.safe_load(open(f, encoding="utf-8"))
    for a in d.get("assertions", []):
        for e in a.get("expressions", []):
            total_expr += 1
            for g in e.get("grants", []):
                total_grants += 1
                pid = g["profile_id"].replace("profile.", "")
                cs, cd = CAP.get(pid) or (NOBLE if pid.startswith("noble") else ("",""))
                if not cs:
                    viol.append([os.path.basename(f), e["id"], g["profile_id"], "unknown-profile", ""])
                    continue
                if RANK_S[cs] < RANK_S[g["scope"]] or RANK_D[cd] < RANK_D[g["min_detail"]]:
                    viol.append([os.path.basename(f), e["id"], g["profile_id"], "cap-exceeded",
                                 "scope %s>%s or detail %s>%s" % (g["scope"], cs, g["min_detail"], cd)])
                if g["min_detail"] != e["layer"]:
                    viol.append([os.path.basename(f), e["id"], g["profile_id"], "invariant-broken",
                                 "layer=%s min_detail=%s" % (e["layer"], g["min_detail"])])

report = {
    "schema": "awake.worldbook.geo1-self-check.v1",
    "date": "2026-09-12",
    "docs": len(glob.glob(os.path.join(ROOT, "*.yaml"))),
    "expressions": total_expr,
    "grants": total_grants,
    "noble_model": "elite/secret(age>=45 or management>=80), 依 WorldbookIdentityCapabilityRules.ResolveNoble L78",
    "violations": viol,
    "passed": len(viol) == 0,
}
out = os.path.join(ROOT, "SELF-CHECK-20260912.json")
with io.open(out, "w", encoding="utf-8", newline="\n") as f:
    json.dump(report, f, ensure_ascii=False, indent=1)
print(json.dumps({k: report[k] for k in ["docs","expressions","grants","passed"]}, ensure_ascii=False))
for v in viol: print(v)
print("SELF-CHECK-DONE ->", out)
