# -*- coding: utf-8 -*-
"""修2：14 档纯 B 核因——回查官方 DB 两处（章法 §1.1 坑1：两处都查才算查过）。
  ① bannerlord_settlements.descriptionText（英文叙述文，不进 localization）
  ② localization_entries（CNs 中文反查 + 任意语言精确/模糊）
输出每档命中摘要，供人工判定"官方没有(豁免)" vs "可查未查(违规)"。
"""
import sqlite3, yaml, glob, os, json, re

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
DIR = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out"
OUT = os.path.join(DIR, "_fix2_dbprobe_result_20260913.json")

PURE_B = ["charas-bay", "charas-origin-tales", "dawn-mtn", "dawn-stew", "dawn-taboo",
          "der-furs", "der-vill", "kach-land", "kach-own", "kach-tales",
          "lac-lake", "lac-tales", "royal-guard", "sturgia-military"]

con = sqlite3.connect(f'file:{DB}?mode=ro', uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

def q(sql, args=()):
    return [dict(r) for r in cur.execute(sql, args).fetchall()]

results = {}
for slug in PURE_B:
    d = yaml.safe_load(open(os.path.join(DIR, slug + ".yaml"), encoding="utf-8"))
    al = d.get("aliases") or {}
    en_terms = [t for t in (al.get("en") or []) if len(t) >= 3]
    zh_terms = [t for t in (al.get("zh-CN") or []) if len(t) >= 2]
    # 也取正文里第一个引文的 quote 前几个词作为补充英文线索（可能含人名地名）
    extra_en = []
    for s in d.get("sources") or []:
        m = re.findall(r"[A-Z][a-zA-Z]{3,}", s.get("quote") or "")
        extra_en += [w for w in m if w.lower() not in ("the", "and", "his", "her", "they")][:3]
    hits = {"settlement_desc": [], "loc_cns": [], "loc_en_exact": []}
    for t in set(en_terms + extra_en[:4]):
        rows = q("SELECT settlementId, substr(descriptionText,1,120) dt FROM bannerlord_settlements "
                 "WHERE descriptionText LIKE ? LIMIT 5", (f"%{t}%",))
        for r in rows:
            hits["settlement_desc"].append({"term": t, "settlement": r["settlementId"], "snip": r["dt"]})
        rows = q("SELECT DISTINCT stringId, substr(text,1,80) tx FROM localization_entries "
                 "WHERE text = ? LIMIT 3", (t,))
        for r in rows:
            hits["loc_en_exact"].append({"term": t, "stringId": r["stringId"], "text": r["tx"]})
    for t in zh_terms:
        rows = q("SELECT stringId, substr(text,1,80) tx, filePath FROM localization_entries "
                 "WHERE language='CNs' AND text LIKE ? AND (stringId LIKE '%.text.%' OR filePath LIKE '%world_lore%') "
                 "LIMIT 5", (f"%{t}%",))
        for r in rows:
            hits["loc_cns"].append({"term": t, "stringId": r["stringId"], "text": r["tx"], "fp": r["filePath"]})
    results[slug] = {"en_terms": en_terms, "zh_terms": zh_terms, "extra_en": extra_en[:4], "hits": hits}
    n = len(hits["settlement_desc"]) + len(hits["loc_cns"]) + len(hits["loc_en_exact"])
    print(f"[{slug}] en={en_terms} zh={zh_terms} -> 命中 {n}")

json.dump(results, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("\n明细 ->", OUT)

# 摘要打印命中详情
for slug, r in results.items():
    h = r["hits"]
    if h["settlement_desc"] or h["loc_cns"] or h["loc_en_exact"]:
        print(f"\n===== {slug} =====")
        for x in h["settlement_desc"][:4]:
            print(f"  [settle.desc] {x['term']} -> {x['settlement']}: {x['snip'][:70]}")
        for x in h["loc_en_exact"][:4]:
            print(f"  [loc.exact ] {x['term']} -> {x['stringId']}")
        for x in h["loc_cns"][:5]:
            print(f"  [loc.CNs   ] {x['term']} -> {x['stringId']}: {x['text'][:50]}")
