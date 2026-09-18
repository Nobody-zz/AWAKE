# -*- coding: utf-8 -*-
"""生成「聚落归属」关系表 —— 这是「条目 ↔ 条目」的第一张真图，也是"牵"要的料。

数据来源（两份都是一手、都在版本管理里）：
  A. persona-entity 登记表（ID 权威）：
     docs/mappings/persona-entity/generations/<build>/entity-registry.v1.json
     393 个聚落实体（村庄 273 / 城堡 67 / 城镇 53），村庄带 `bound_settlement_code`。
  B. 上线包 runtime.json：每个条目的 `extensions.entityRefs`（402/482 条各锚一个聚落）。

产出：每条**村庄条目** → 它归属的**城堡/城镇条目**。边不在契约里，所以先落成独立料档。
只读两份源，只写一份新料档，不动任何既有产物。
"""
import json, io, os, collections, datetime

BUILD = "b1-7590085e3662512d7c1c6646c529694bda9850146234827b2dafbd9f20c6a2b3"
REG = r"D:\AWAKE-Dev\AWAKE\docs\mappings\persona-entity\generations\%s\entity-registry.v1.json" % BUILD
PKG = r"D:\AWAKE-Dev\AWAKE\ModuleData\Worldbook\packages\calradia\runtime.json"
OUTDIR = r"D:\AWAKE-Dev\AWAKE\docs\mappings\worldbook-settlement-hierarchy\20260918"
OUTJSON = os.path.join(OUTDIR, "settlement-hierarchy.v1.json")
REPORT = r"D:\AWAKE-Dev\AWAKE\tools\_settlement_hierarchy_report_20260918.txt"

def walk(o):
    if isinstance(o, dict):
        if isinstance(o.get("id"), str) and o["id"].startswith("awake:entry:"):
            yield o
        for v in o.values():
            yield from walk(v)
    elif isinstance(o, list):
        for v in o:
            yield from walk(v)

reg = json.load(io.open(REG, encoding="utf-8"))
ents = list(walk(json.load(io.open(PKG, encoding="utf-8"))))

# 聚落 code（小写）→ 世界书条目 id
code2entry = {}
for e in ents:
    for a in ((e.get("extensions") or {}).get("entityRefs") or []):
        code = a.split(":")[-1]           # awake:settlement:castle_village_a6_2
        code2entry.setdefault(code, []).append(e["id"])

settlements = [x for x in reg["entities"] if x.get("kind") == "settlement"]
by_code = {}
for s in settlements:
    by_code[s["settlement_code"].lower()] = s

villages = [s for s in settlements if s.get("settlement_type") == "village"]
towns = [s for s in settlements if s.get("settlement_type") == "town"]
castles = [s for s in settlements if s.get("settlement_type") == "castle"]

edges = []
no_bound, bound_unknown, no_parent_entry, no_village_entry = [], [], [], []
for v in villages:
    vcode = v["settlement_code"].lower()
    v_entries = code2entry.get(vcode, [])
    if not v_entries:
        no_village_entry.append(vcode)
    bcode = (v.get("bound_settlement_code") or "").lower()
    if not bcode:
        no_bound.append(vcode)
        continue
    parent = by_code.get(bcode)
    if parent is None:
        bound_unknown.append((vcode, bcode))
        continue
    p_entries = code2entry.get(bcode, [])
    if not p_entries:
        no_parent_entry.append((vcode, bcode))
    for ve in v_entries:
        edges.append({
            "from": ve,
            "to": p_entries,
            "via": "bound_settlement",
            "villageCode": v["settlement_code"],
            "villageNameZh": v.get("display_name_zh"),
            "parentCode": parent["settlement_code"],
            "parentNameZh": parent.get("display_name_zh"),
            "parentType": parent.get("settlement_type"),
            "cultureCode": v.get("culture_code"),
            "parentEntryCount": len(p_entries),
        })

doc = {
    "schema_version": "awake.worldbook.settlement-hierarchy.v1",
    "generated_at_utc": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    "sources": {
        "entity_registry": {"path": REG.replace("\\", "/"), "catalog_build_id": BUILD,
                            "entities": len(reg["entities"])},
        "runtime_package": {"path": PKG.replace("\\", "/"), "entries": len(ents)},
    },
    "note": ("「条目 ↔ 条目」在契约里没有字段（`knowledge_entry` 只到 `related_domains` 领域级），"
             "所以这层关系先以独立料档存在。边的含义＝**村庄条目 ↔ 它归属的城堡/城镇条目**，"
             "依据是游戏数据里的 `bound_settlement_code`（登记表已核到 base_game/exact_base）。"
             "运行时暂无消费者 —— 这是「牵」那一步要吃的料，不是已经接通的东西。"),
    "counts": {
        "villages": len(villages), "towns": len(towns), "castles": len(castles),
        "edges": len(edges),
        "village_without_bound": len(no_bound),
        "village_without_entry": len(no_village_entry),
        "parent_without_entry": len(no_parent_entry),
    },
    "edges": edges,
}
os.makedirs(OUTDIR, exist_ok=True)
io.open(OUTJSON, "w", encoding="utf-8", newline="\n").write(
    json.dumps(doc, ensure_ascii=False, indent=1))

w = []
def p(s=""):
    w.append(s); print(s)

p("=== 聚落归属表 ===")
p("登记表 %d 实体（聚落 %d：村庄 %d / 城堡 %d / 城镇 %d）" % (len(reg["entities"]), len(settlements), len(villages), len(castles), len(towns)))
p("上线包 %d 条目，其中带 entityRefs 的 %d" % (len(ents), len([e for e in ents if (e.get("extensions") or {}).get("entityRefs")])))
p("")
p("== 生成结果 ==")
p("  边数（村庄条目 → 父条目）      %d" % len(edges))
p("  村庄无 bound_settlement_code  %d" % len(no_bound))
p("  村庄本身没有世界书条目        %d" % len(no_village_entry))
p("  父聚落没有世界书条目          %d" % len(no_parent_entry))
p("  bound 指向的 code 查不到      %d" % len(bound_unknown))
p("")
p("== 按父聚落类型 ==")
c = collections.Counter(e["parentType"] for e in edges)
for k, v in c.most_common():
    p("  %-10s %d 条边" % (k, v))
p("")
p("== 每条村庄条目平均连到几个父条目 ==")
c2 = collections.Counter(e["parentEntryCount"] for e in edges)
for k in sorted(c2):
    p("  连到 %d 个父条目 : %d 条" % (k, c2[k]))
p("")
p("== 样例（前 8 条边）==")
for e in edges[:8]:
    p("  %-42s → %s" % (e["villageNameZh"] + "(" + e["villageCode"] + ")",
                        ", ".join(x.split(":")[-1] for x in e["to"]) + " [" + (e["parentNameZh"] or "") + "]"))
p("")
p("== 同一父聚落下挂了多少条村庄（最大的前 10 组）==")
g = collections.Counter(e["parentCode"] for e in edges)
for k, v in g.most_common(10):
    nm = by_code[k.lower()].get("display_name_zh") if k.lower() in by_code else "?"
    p("  %-16s %-10s %d 个村庄条目" % (k, nm, v))

io.open(REPORT, "w", encoding="utf-8", newline="\n").write("\n".join(w))
print("")
print("料档写入:", OUTJSON)
print("报告写入:", REPORT)
