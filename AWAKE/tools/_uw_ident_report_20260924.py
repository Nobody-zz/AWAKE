# -*- coding: utf-8 -*-
"""§六 核验读数汇总（2026-09-24，只读）—— 把 sim 的 out.json 排成一张表。"""
import io
import json
import os

REPO = r"D:\AWAKE-Dev\AWAKE"
OUT = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1\_uw_ident_out_20260924.json")

rows = json.load(io.open(OUT, encoding="utf-8"))

# 已知会产出 role 的（从 adapter 读出来的）—— 只有这些是"真实会遇到的人"
ADAPTER_EMITS = {"villager", "commoner", "townsfolk", "headman", "rural_notable",
                 "merchant", "wanderer", "gang_leader", "preacher", "soldier",
                 "noble", "arena_master", "notable", "tavernkeeper", "ransom_broker",
                 "merchant", "gangster", "bandit"}

print("=" * 78)
print("§六-2  role 名 → 运行时能不能认出身份（scope 空 = 落 anonymous）")
print("=" * 78)
print("%-20s %-10s %-10s %-8s %s" % ("role", "scope", "detail", "state", "判定"))
print("-" * 78)
unknown, known = [], []
for r in rows:
    name = (r.get("name") or "")
    if not name.startswith("role:"):
        continue
    role = name[5:]
    scope = r.get("scope") or ""
    detail = r.get("detail") or ""
    if scope:
        known.append((role, scope, detail, r.get("state")))
        verdict = "认出"
    else:
        unknown.append(role)
        verdict = "★认不出（落 anonymous）"
    print("%-20s %-10s %-10s %-8s %s" % (role, scope or "(空)", detail or "(空)", r.get("state"), verdict))

print()
print("─" * 78)
print("认得出的 role（%d 个）：" % len(known))
for role, scope, detail, st in known:
    tag = "  ← adapter 会产出" if role in ADAPTER_EMITS else ""
    print("   %-20s %-10s %s%s" % (role, scope, detail, tag))
print()
print("认不出的 role（%d 个）：" % len(unknown))
print("   " + " / ".join(unknown))

print()
print("=" * 78)
print("§六-3  is_clan_leader 能不能表达「属于某小派系」")
print("=" * 78)
for r in rows:
    if (r.get("name") or "").startswith("clanleader:"):
        print("   %-18s scope=%-8s detail=%-8s state=%s"
              % (r.get("name"), r.get("scope"), r.get("detail"), r.get("state")))
print()
print("   读法：is_clan_leader 只影响「贵族 → noble」这一档的判定（见 WorldbookIdentityEvaluator:42,189）；")
print("        与「属于哪个 clan」无关 —— 它只知道「是不是族长」，不知道「是哪一家的」。")
