# -*- coding: utf-8 -*-
"""单人头盔批 · 盘点（只读 XML + DB）。

物品档的 A 级素材走**游戏 items XML**（组件数据完备），DB 只用于 join 官方中文名。
本脚本产出 256 件单人头盔的清单切片，并回答三个决定切批方案的问题：
  1) 文化分布（按 culture 前缀 / XML culture 字段）
  2) **中文名重名率**（重名 ⇒ 跨档同构风险）
  3) 护甲值/重量/命名变体（_a/_b/_brnz/_cpr 后缀）的实际面貌

产出 _head_inventory_20260916.json（并打印摘要）
"""
import collections
import io
import json
import os
import re
import sqlite3
import xml.etree.ElementTree as ET

GAME = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/SandBoxCore/ModuleData/items"
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_head_inventory_20260916.json")


def cn_table(con):
    cur = con.execute("SELECT stringId, text FROM localization_entries WHERE language='CNs'")
    return {r[0]: r[1] for r in cur.fetchall()}


def parse_name(name):
    """{=token}English Name  -> (token, english)"""
    if not name:
        return "", ""
    m = re.match(r"^\{=([^}]+)\}(.*)$", name)
    if not m:
        return "", name.strip()
    return m.group(1), m.group(2).strip()


def main():
    con = sqlite3.connect("file:%s?mode=ro" % DB, uri=True)
    cn = cn_table(con)
    print("CNs entries:", len(cn))

    # ---- 全 XML 普查：Type=HeadArmor ----
    heads = []
    type_count = collections.Counter()
    files = sorted(f for f in os.listdir(GAME) if f.lower().endswith(".xml"))
    for fn in files:
        try:
            root = ET.parse(os.path.join(GAME, fn)).getroot()
        except Exception as e:
            print("  !! parse fail", fn, e)
            continue
        for it in root.iter("Item"):
            t = it.get("Type") or ""
            type_count[t] += 1
            if t != "HeadArmor":
                continue
            wid = it.get("id")
            tok, en = parse_name(it.get("name"))
            zh = cn.get(tok)
            arm = it.find(".//Armor")
            a = dict(arm.attrib) if arm is not None else {}
            heads.append({
                "entityId": wid,
                "xml": fn,
                "en": en,
                "zh": zh,
                "token": tok,
                "culture": (it.get("culture") or "").replace("Culture.", ""),
                "head": a.get("head_armor"),
                "weight": a.get("weight"),
                "material": a.get("material_type"),
                "modifier": a.get("modifier_group"),
                "value": it.get("value"),
                "flags": sorted(k for k in a.keys()),
            })

    print("XML files scanned:", len(files))
    print("XML total <Item>:", sum(type_count.values()))
    print("Type=HeadArmor:", len(heads))

    # ---- 与 DB 的 256 对齐 ----
    cur = con.execute("SELECT entityId FROM bannerlord_items WHERE entityKind='Item' AND itemType='HeadArmor'")
    db_ids = set(r[0] for r in cur.fetchall())
    xml_ids = set(h["entityId"] for h in heads)
    print("DB HeadArmor:", len(db_ids))
    print("  in XML not DB:", len(xml_ids - db_ids), sorted(xml_ids - db_ids)[:10])
    print("  in DB not XML:", len(db_ids - xml_ids), sorted(db_ids - xml_ids)[:10])

    # ---- 单/多人切分 ----
    mp = [h for h in heads if h["entityId"].startswith("mp_")]
    sp = [h for h in heads if not h["entityId"].startswith("mp_")]
    print("mp_ (多人):", len(mp), " 单人:", len(sp))

    # ---- 文化分布 ----
    cul = collections.Counter(h["culture"] or "(none)" for h in sp)
    print("--- culture dist (single-player) ---")
    for k, v in cul.most_common():
        print("   %-12s %d" % (k, v))

    # ---- 中文名覆盖 & 重名 ----
    miss = [h for h in sp if not h["zh"]]
    print("missing CN:", len(miss), [h["entityId"] for h in miss][:12])
    nm = collections.Counter(h["zh"] for h in sp if h["zh"])
    dup = {k: v for k, v in nm.items() if v > 1}
    print("--- duplicate CN names: %d 个名字 / %d 件 ---" % (len(dup), sum(dup.values())))
    for k, v in sorted(dup.items(), key=lambda x: -x[1])[:25]:
        ids = [h["entityId"] for h in sp if h["zh"] == k]
        print("   %-14s x%d  %s" % (k, v, ", ".join(ids[:5])))

    # ---- 护甲值 / 重量 分布 ----
    hv = sorted(int(h["head"]) for h in sp if h["head"])
    if hv:
        print("head armor: min %d / p25 %d / median %d / p75 %d / max %d" % (
            hv[0], hv[len(hv) // 4], hv[len(hv) // 2], hv[len(hv) * 3 // 4], hv[-1]))
        print("head armor buckets:",
              dict(collections.Counter("%d-%d" % (v // 10 * 10, v // 10 * 10 + 9) for v in hv)))
    wv = sorted(float(h["weight"]) for h in sp if h["weight"])
    if wv:
        print("weight: min %.2f / median %.2f / max %.2f" % (wv[0], wv[len(wv) // 2], wv[-1]))
    print("Armor attr keys union:", sorted(set(k for h in sp for k in h["flags"])))
    print("material_type dist:", dict(collections.Counter(h["material"] or "(none)" for h in sp)))
    print("value set (xml value attr):", dict(collections.Counter(h["value"] or "(none)" for h in sp).most_common(8)))

    # ---- 命名变体后缀 ----
    suffix = collections.Counter()
    for h in sp:
        m = re.search(r"_(a|b|c|d|brnz|cpr|old|new|\d+)$", h["entityId"])
        suffix[m.group(1) if m else "(none)"] += 1
    print("naming suffix:", dict(suffix.most_common()))

    with io.open(OUT, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(sp, fh, ensure_ascii=False, indent=1)
    print("written:", OUT, len(sp))


if __name__ == "__main__":
    main()
