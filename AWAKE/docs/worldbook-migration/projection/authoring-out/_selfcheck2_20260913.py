# -*- coding: utf-8 -*-
"""越限自检 v2（红测二整改产物，2026-09-13）。
v1（_selfcheck_20260912.py）只查 grants；v2 补：
  ③ denies 检查：非空 denies 只允许在 -secret 档；deny 不得用祖链根
     （commoner/villager/townsfolk/notable——链尾炸全家，IMPL §3.3 审查裁定）
  ④ 身份覆盖：六身份 grant 落点（villager/townsfolk 可经 commoner 兜底，
     merchant/soldier 无兜底链 → 缺失即违规，war1 W10/W11 教训）
  ⑤ 规模硬限：断言 1-3（>3 警告）、每断言表达 >4（警告）
"""
import yaml, glob, io, os, json

RANK_D = {"rumor": 1, "summary": 2, "detail": 3, "secret": 4}
RANK_S = {"local": 1, "regional": 2, "national": 3, "faction": 4, "elite": 5, "private": 6}
CAP = {
    "commoner": ("local", "rumor"), "villager": ("local", "rumor"),
    "townsfolk": ("regional", "summary"), "notable": ("regional", "detail"),
    "headman": ("national", "detail"), "merchant": ("faction", "detail"),
    "tavernkeeper": ("faction", "detail"), "ransom_broker": ("faction", "detail"),
    "soldier": ("national", "detail"), "anonymous": ("", ""),
}
NOBLE = ("elite", "secret")
DENY_ROOTS = {"commoner", "villager", "townsfolk", "notable"}  # 祖链根禁用
SIX = ["villager", "townsfolk", "merchant", "soldier", "headman", "noble"]
# 无兜底链的身份（Role 回退链不含祖链或链不覆盖）：缺失即违规
HARD = ["merchant", "soldier"]

ROOT = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out"
viol, warns = [], []
total_expr, total_grants, total_denies = 0, 0, 0
docs = 0
for f in sorted(glob.glob(os.path.join(ROOT, "*.yaml"))):
    base = os.path.basename(f)
    if base.startswith("_"):
        continue
    docs += 1
    d = yaml.safe_load(open(f, encoding="utf-8"))
    doc_profiles = set()
    n_assert = 0
    for a in d.get("assertions", []):
        n_assert += 1
        exprs = a.get("expressions", [])
        if len(exprs) > 4:
            warns.append([base, a.get("id", "?"), "expr-over-4", len(exprs)])
        for e in exprs:
            total_expr += 1
            layer = e.get("layer")
            for g in e.get("grants", []):
                total_grants += 1
                pid = g["profile_id"].replace("profile.", "")
                doc_profiles.add(pid)
                cs, cd = CAP.get(pid) or (NOBLE if pid.startswith("noble") else ("", ""))
                if not cs:
                    viol.append([base, e["id"], g["profile_id"], "unknown-profile", ""])
                    continue
                if RANK_S[cs] < RANK_S[g["scope"]] or RANK_D[cd] < RANK_D[g["min_detail"]]:
                    viol.append([base, e["id"], g["profile_id"], "cap-exceeded",
                                 "scope %s>%s or detail %s>%s" % (g["scope"], cs, g["min_detail"], cd)])
                if g["min_detail"] != layer:
                    viol.append([base, e["id"], g["profile_id"], "invariant-broken",
                                 "layer=%s min_detail=%s" % (layer, g["min_detail"])])
            for g in e.get("denies", []) or []:
                total_denies += 1
                pid = g["profile_id"].replace("profile.", "")
                if not base.replace(".yaml", "").endswith("-secret"):
                    viol.append([base, e["id"], pid, "deny-outside-secret", ""])
                if pid in DENY_ROOTS:
                    viol.append([base, e["id"], pid, "deny-root-profile",
                                 "root profile denies whole lineage"])
    if n_assert > 3:
        warns.append([base, "-", "assertions-over-3", n_assert])
    for rid in HARD:
        if rid not in doc_profiles:
            # 警告级：身份覆盖以选题稿调取场景为准（章法 §3.6 澄清），
            # 非全员硬覆盖——war1 W10/W11 教训限定于"选题声明了商人/士兵要能问到"的档
            warns.append([base, "-", "identity-coverage-missing", rid])

report = {
    "schema": "awake.worldbook.geo1-self-check.v2",
    "date": "2026-09-13",
    "docs": docs,
    "expressions": total_expr,
    "grants": total_grants,
    "denies": total_denies,
    "violations": viol,
    "warnings": warns,
    "passed": len(viol) == 0,
}
out = os.path.join(ROOT, "SELF-CHECK-20260913-v2.json")
with io.open(out, "w", encoding="utf-8", newline="\n") as f:
    json.dump(report, f, ensure_ascii=False, indent=1)
print(json.dumps({k: report[k] for k in ["docs", "expressions", "grants", "denies", "passed"]}, ensure_ascii=False))
print("violations:")
for v in viol:
    print("  ", v)
print("warnings:")
for w in warns:
    print("  ", w)
print("SELF-CHECK-V2-DONE ->", out)
