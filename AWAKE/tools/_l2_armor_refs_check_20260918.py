# -*- coding: utf-8 -*-
"""护甲批 L2 文案的 refs 自查（只读）。

校验三件事：
 1) 每张卡 assertion 里的每个 ref，都必须出现在护甲快照 game-items-armor.txt 里；
 2) 任何 ref 不跨卡重复（一件物品只归一张卡，避免跨卡 quote_hash 撞车）；
 3) 每张卡至少 1 条 assertion、每条 assertion 至少 1 个 ref、至少 1 条 expr。
另打印：快照里多少件物品被至少一张卡引用（覆盖率），多少件无人引用。
"""
import json
import re
import sys
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SNAP = ROOT / "tools/worldbook-studio/workspace/full-geo1/authoring/sources/game-items-armor.txt"
L2 = ROOT / "docs/worldbook-migration/projection/authoring-out/_l2_armor_20260918.json"

fail = []
warn = []

# ---- 读快照：eid -> 行 ----
snap = {}
for ln in SNAP.read_text(encoding="utf-8").splitlines():
    ln = ln.strip()
    if not ln:
        continue
    m = re.match(r"^([a-z_]+)\.([A-Za-z0-9_]+)\s*=>\s*(.*)$", ln)
    if not m:
        fail.append(f"快照行解析失败: {ln[:80]}")
        continue
    snap[m.group(2)] = (m.group(1), m.group(3))

data = json.loads(L2.read_text(encoding="utf-8"))
cards = data["cards"]

ref_owner = {}
per_card = {}
n_assert = 0
n_expr = 0
n_ref = 0

for c in cards:
    slug = c["slug"]
    if slug in per_card:
        fail.append(f"slug 重复: {slug}")
    if not re.match(r"^[a-z0-9\-]+$", slug):
        fail.append(f"slug 字符不合规: {slug}")
    if len(c["al_zh"]) != len(set(c["al_zh"])):
        fail.append(f"{slug}: al_zh 自身有重复")
    if not c.get("asserts"):
        fail.append(f"{slug}: 没有 asserts")
    refs_here = []
    for ai, a in enumerate(c["asserts"]):
        n_assert += 1
        if not a.get("text"):
            fail.append(f"{slug}#{ai}: assert 无 text")
        if not a.get("refs"):
            fail.append(f"{slug}#{ai}: assert 无 refs")
        if not a.get("exprs"):
            fail.append(f"{slug}#{ai}: assert 无 exprs")
        for e in a.get("exprs", []):
            n_expr += 1
            if e.get("grants") == "detail" and e.get("culture"):
                warn.append(f"{slug}#{ai}: detail 带 culture={e['culture']}（确认是否有意）")
            if not e.get("text"):
                fail.append(f"{slug}#{ai}: expr[{e.get('tag')}] 无 text")
            if not e.get("layer"):
                fail.append(f"{slug}#{ai}: expr[{e.get('tag')}] 无 layer")
        for r in a["refs"]:
            n_ref += 1
            refs_here.append(r)
            if r not in snap:
                fail.append(f"{slug}#{ai}: ref 不在快照里 -> {r}")
            if r in ref_owner and ref_owner[r] != slug:
                fail.append(f"ref 跨卡重复: {r}  已属 {ref_owner[r]}，又被 {slug} 引用")
            ref_owner[r] = slug
    dup_in_card = [x for x in set(refs_here) if refs_here.count(x) > 1]
    if dup_in_card:
        fail.append(f"{slug}: 卡内 ref 重复 -> {dup_in_card}")
    per_card[slug] = len(set(refs_here))

print(f"卡数 {len(cards)}   assert {n_assert}   expr {n_expr}   ref {n_ref}（去重 {len(ref_owner)}）")
print(f"快照物品 {len(snap)}   被引用 {len(ref_owner)}   未引用 {len(snap) - len(ref_owner)}")
print()
print("每卡引用代表件数：")
for slug, n in per_card.items():
    print(f"  {slug:<18} {n}")
print()

# 未引用的物品按 Type 汇总（供判断是否需要补引用）
unused = [k for k in snap if k not in ref_owner]
by_type = defaultdict(int)
for k in unused:
    by_type[snap[k][0]] += 1
print(f"未引用 {len(unused)} 件，按类别：{dict(by_type)}")

if warn:
    print()
    print(f"WARN {len(warn)}:")
    for w in warn[:40]:
        print("  " + w)

print()
if fail:
    print(f"FAIL {len(fail)}:")
    for f in fail[:60]:
        print("  " + f)
    sys.exit(1)
print("VERDICT PASS")
