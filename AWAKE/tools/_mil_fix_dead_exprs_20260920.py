# -*- coding: utf-8 -*-
"""军事批 · 修 6 条「写而不用」的表达（改 L2 数据源，随后重跑生成器）。**幂等**。

依据（probe 实测，--cap 模式）：
  ① 5 档的 a0 summary 同时挂 townsfolk+merchant，而同档另有一条 merchant 的 rumor；
     商人问一次只送一条，summary 层号高 ⇒ merchant rumor 永远不达。
     修：summary 的受众去掉 merchant（留 townsfolk）⇒ 市民拿 summary、商人拿传闻，两条都可达。
  ② troops-golden-boar 的 vlandia-detail 挂在 townsfolk 上却要 detail 层；
     市民能力上限＝(regional, summary) ⇒ 该表达不可达。
     修：min_detail detail → summary（正文口吻本就是本地市民的话）。

⚠️ L2 json 里表达 id **无 `expr.` 前缀**（生成器才加）。

用法：python _mil_fix_dead_exprs_20260920.py
"""
import io
import json
import os

AO = r"D:\AWAKE-Dev\AWAKE\docs\worldbook-migration\projection\authoring-out"

DROP_MERCHANT_FROM_SUMMARY = ["troops-banner-knight", "troops-ghulam", "troops-golden-boar",
                              "war_history-kuyug", "military-aserai"]
LOWER_DETAIL = [("troops-golden-boar", "golden-boar-vlandia-detail", "detail", "summary")]

FILES = ["_mil_A1_20260920.json", "_mil_A2_20260920.json", "_mil_A3_20260920.json",
         "_mil_B2_20260920.json"]

changed = []
for fn in FILES:
    p = os.path.join(AO, fn)
    data = json.load(io.open(p, encoding="utf-8"))
    touched = False
    for doc in data["docs"]:
        slug = doc["slug"]
        if slug in DROP_MERCHANT_FROM_SUMMARY:
            hit = 0
            for a in doc["assertions"]:
                for e in a["expressions"]:
                    keep = []
                    for g in e["grants"]:
                        if g.get("profile") == "merchant" and g.get("detail") == "summary":
                            hit += 1
                            changed.append("%s: 删 %s 的 merchant/summary" % (slug, e["id"]))
                            continue
                        keep.append(g)
                    if len(keep) != len(e["grants"]):
                        assert keep, "%s/%s 删后 grants 为空" % (slug, e["id"])
                        e["grants"] = keep
                        touched = True
            assert hit <= 1, "%s 期望至多删 1 条 merchant/summary，实际 %d" % (slug, hit)
        for (dslug, eid, old, new) in LOWER_DETAIL:
            if slug != dslug:
                continue
            hit = 0
            for a in doc["assertions"]:
                for e in a["expressions"]:
                    if e["id"] != eid:
                        continue
                    for g in e["grants"]:
                        if g.get("profile") == "townsfolk" and g.get("detail") == old:
                            g["detail"] = new
                            hit += 1
                            changed.append("%s: %s 的 townsfolk min_detail %s->%s" % (slug, eid, old, new))
                    if hit:
                        touched = True
            assert hit <= 1, "%s/%s 期望至多改 1 条，实际 %d" % (dslug, eid, hit)
    if touched:
        io.open(p, "w", encoding="utf-8", newline="\n").write(
            json.dumps(data, ensure_ascii=False, indent=1))

print("本轮改动 %d 处：" % len(changed))
for c in changed:
    print("  -", c)
print("OK（幂等：若此前已改，本轮为 0 处属正常）")
