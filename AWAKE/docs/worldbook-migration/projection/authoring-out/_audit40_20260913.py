# -*- coding: utf-8 -*-
"""存量对账审查（章法 §七 执行，2026-09-13）。
对 authoring-out 全部 40 档逐档查可脚本化项，产出对账表数据：
  命名：en title 空缺 / aliases zh-en 齐全度
  素材：source_id 分级（game/localization=A 官方，chronicle=B 编年史，其他=D 待登记）
  引文：五元组 + quote 齐全
  交叉覆盖：quote_hash 跨档重复（档内去重，与名录生成器同口径）
  权限：合并 SELF-CHECK-20260913-v2.json 结果
  归类：人工裁定映射（基于红测一 + 语义复核）
输出：_audit40_result_20260913.json
"""
import os, json, glob
import yaml

DIR = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out"
V2 = os.path.join(DIR, "SELF-CHECK-20260913-v2.json")

# 档型归类人工裁定（红测一 + 语义复核；无锚点地理/传说=场景专题档）
CLASS_MAP = {
    "charas-town": "实体本档", "varcheg-town": "实体本档",
    "husn-fulq-town": "实体本档", "lycaron-town": "实体本档", "der-vill": "实体本档",
    "varcheg-swap": "事件档", "kach-own": "事件档", "charas-reign": "事件档",
    "charas-cortain-secret": "秘密档",
    "charas-bay": "侧档(挂锚点)", "lycaron-mines": "侧档(挂锚点)",
    "der-furs": "侧档(挂锚点)", "kach-land": "侧档(挂锚点)", "kach-tales": "侧档(挂锚点)",
}
def classify(slug, has_ent):
    if slug in CLASS_MAP:
        return CLASS_MAP[slug]
    if has_ent:
        return "待裁(有锚点)"
    return "场景/专题档(无锚点)"

def src_level(sid):
    if "game" in sid or "localization" in sid:
        return "A"
    if "chronicle" in sid or "animusforge" in sid:
        return "B"
    return "D?"

v2 = json.load(open(V2, encoding="utf-8"))
warn_map = {}
for w in v2["warnings"]:
    warn_map.setdefault(w[0], []).append(w[2] if len(w) > 2 else w[1])

rows = []
quote_map = {}  # qhash -> set(files)
docs = []
for f in sorted(glob.glob(os.path.join(DIR, "*.yaml"))):
    base = os.path.basename(f)
    if base.startswith("_"):
        continue
    d = yaml.safe_load(open(f, encoding="utf-8"))
    slug = base[:-5]
    title = d.get("title") or {}
    en_title = (title.get("en") or "").strip()
    al = d.get("aliases") or {}
    zh_al = al.get("zh-CN") or []
    en_al = al.get("en") or []
    ents = d.get("entity_ids") or []
    # 素材分级与引文
    levels, missing_quote = set(), []
    for s_root in ("sources",):
        for s in d.get(s_root) or []:
            levels.add(src_level(s.get("source_id", "")))
            for k in ("source_id", "source_version", "source_content_hash", "locator", "quote_hash", "quote"):
                if not s.get(k):
                    missing_quote.append(k)
            qh = s.get("quote_hash")
            if qh:
                quote_map.setdefault(qh, set()).add(slug)
    for a in d.get("assertions") or []:
        for s in a.get("sources") or []:
            levels.add(src_level(s.get("source_id", "")))
            for k in ("source_id", "source_version", "source_content_hash", "locator", "quote_hash", "quote"):
                if not s.get(k):
                    missing_quote.append(k)
            qh = s.get("quote_hash")
            if qh:
                quote_map.setdefault(qh, set()).add(slug)
        for e in a.get("expressions") or []:
            for s in e.get("sources") or []:
                levels.add(src_level(s.get("source_id", "")))
                for k in ("source_id", "source_version", "source_content_hash", "locator", "quote_hash", "quote"):
                    if not s.get(k):
                        missing_quote.append(k)
                qh = s.get("quote_hash")
                if qh:
                    quote_map.setdefault(qh, set()).add(slug)
    rows.append({
        "slug": slug, "id": d.get("id"), "status": d.get("status"),
        "domain": d.get("domain"), "subdomain": d.get("subdomain"),
        "class": classify(slug, bool(ents)),
        "en_title": en_title, "zh_alias_n": len(zh_al), "en_alias_n": len(en_al),
        "levels": sorted(levels), "missing_quote_fields": sorted(set(missing_quote)),
        "warn": warn_map.get(base, []),
    })

# 交叉覆盖
cross = {q: sorted(fs) for q, fs in quote_map.items() if len(fs) > 1}
for r in rows:
    r["cross_warn"] = [q[:8] for q, fs in cross.items() if r["slug"] in fs]

out = os.path.join(DIR, "_audit40_result_20260913.json")
json.dump({"docs": rows, "cross_cover": cross}, open(out, "w", encoding="utf-8"), ensure_ascii=False, indent=1)

# 摘要打印
print("=== 汇总 ===")
print("总档数:", len(rows))
print("\n-- en title 空缺 --")
for r in rows:
    if not r["en_title"]:
        print("  ", r["slug"])
print("-- aliases 缺失(zh 或 en 为 0) --")
for r in rows:
    if r["zh_alias_n"] == 0 or r["en_alias_n"] == 0:
        print(f"   {r['slug']} zh={r['zh_alias_n']} en={r['en_alias_n']}")
print("-- 引文字段缺失 --")
for r in rows:
    if r["missing_quote_fields"]:
        print("  ", r["slug"], r["missing_quote_fields"])
print("-- 素材等级分布 --")
lv = {}
for r in rows:
    lv[tuple(r["levels"])] = lv.get(tuple(r["levels"]), 0) + 1
print("  ", lv)
print("-- 交叉覆盖(quote_hash 跨档) --")
for q, fs in sorted(cross.items()):
    print(f"   {q[:8]}: {fs}")
print("-- 归类待裁(有锚点不在映射) --")
for r in rows:
    if r["class"].startswith("待裁"):
        print("  ", r["slug"], r["entity"] if "entity" in r else "")
print("-- v2 警告分布 --")
for r in rows:
    if r["warn"]:
        print(f"   {r['slug']}: {r['warn']}")
print("\n明细 ->", out)
