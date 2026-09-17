# -*- coding: utf-8 -*-
"""护甲族 · 形制分类盘点（只读 XML，2026-09-18）。

用途：为「下一批＝护甲形制批」提供选题依据 —— 回答「躯干/腿/手/披风/盾各自能切成几类、每类多少件」。
方法论照抄头盔批 `_head_types_20260916.py`：按 entityId 正则切**主形制**（不是物品名），
再用正交 flags（叠穿 over X / 分性别变体 / 潜行 / 材质）标注。

⚠️ 只读。不写包、不改索引。不产出词条。
"""
import collections
import io
import json
import os
import re
import sys
import xml.etree.ElementTree as ET

sys.stdout.reconfigure(encoding="utf-8")

GAME = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/SandBoxCore/ModuleData/items"
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "_probe_armor_shapes_20260918.json")

# 各部位要看的 Type
PARTS = {
    "BodyArmor": "躯干",
    "LegArmor": "腿",
    "HandArmor": "手",
    "Cape": "披风",
    "Shield": "盾",
}

# 主形制：按优先级匹配 entityId（先匹配者胜）。正则按"工艺/结构词"写，不按具体物品名。
SHAPES = [
    ("plate",       r"plate|cuirass|breastplate|_plate"),
    ("lamellar",    r"lamellar|lamelar"),
    ("scale",       r"scale|scalemail"),
    ("mail",        r"mail|chainmail|_chain"),
    ("brigandine",  r"brigandine|brigantine|coat_of_plates"),
    ("gambeson",    r"gambeson|padded|quilted|aketon|_aket_"),
    ("leather",     r"leather|leathery|hide|tanned"),
    ("cloth",       r"cloth|tunic|robe|dress|gown|silk|linen|wool|kilt|caftan"),
    ("fur",         r"fur|shaggy|rough"),
    ("armor_other", r"armor|armour"),
]


# 盾单独一张表（盾的形制词与甲完全不重叠）
SHIELD_SHAPES = [
    ("tower",    r"tower|pavise|pavese|_great_"),
    ("kite",     r"kite|teardrop"),
    ("heater",   r"heater"),
    ("round",    r"round|circle|oval|buckler|targe"),
    ("rect",     r"rect|_square|tall|tall_"),
]


def shape_of(eid, typ=""):
    table = SHIELD_SHAPES if typ == "Shield" else SHAPES
    for key, pat in table:
        if re.search(pat, eid, re.I):
            return key
    return "other"


def sval(v):
    """护值容错：盾没有 body/leg/arm/head_armor，取到的是 '-'。"""
    try:
        return int(v)
    except (TypeError, ValueError):
        return 0


def main():
    import sqlite3
    con = sqlite3.connect("file:%s?mode=ro" % DB, uri=True)
    cur = con.execute("SELECT stringId, text FROM localization_entries WHERE language='CNs'")
    cn = {r[0]: r[1] for r in cur.fetchall()}

    items = []
    for fn in sorted(f for f in os.listdir(GAME) if f.lower().endswith(".xml")):
        root = ET.parse(os.path.join(GAME, fn)).getroot()
        for it in root.iter("Item"):
            typ = it.get("Type") or ""
            if typ not in PARTS:
                continue
            eid = it.get("id") or ""
            if eid.startswith("mp_"):
                continue
            nm = it.get("name") or ""
            m = re.match(r"^\{=([^}]+)\}(.*)$", nm)
            tok, en = (m.group(1), m.group(2).strip()) if m else ("", nm.strip())
            arm = it.find(".//Armor")
            a = dict(arm.attrib) if arm is not None else {}
            items.append({
                "type": typ, "part": PARTS[typ], "entityId": eid, "en": en,
                "zh": cn.get(tok), "culture": (it.get("culture") or "").replace("Culture.", ""),
                "armor": a.get("body_armor") or a.get("leg_armor") or a.get("arm_armor")
                         or a.get("head_armor") or "-",
                "weight": it.get("weight"),
                "material": a.get("material_type"),
                "gender_var": a.get("has_gender_variations"),
                "shape": shape_of(eid, typ),
                "layered": bool(re.search(r"over_|_over", eid, re.I)),
            })

    print("护甲族单人件数 = %d" % len(items))
    print()

    # ---- 部位 × 形制 矩阵 ----
    grid = collections.defaultdict(collections.Counter)
    for it in items:
        grid[it["part"]][it["shape"]] += 1

    for part in PARTS.values():
        row = grid.get(part)
        if not row:
            continue
        total = sum(row.values())
        print("=== %s（%d 件）能切成 %d 类 ===" % (part, total, len(row)))
        for shape, n in row.most_common():
            rows = sorted([x for x in items if x["part"] == part and x["shape"] == shape],
                          key=lambda x: -sval(x["armor"]))
            cul = collections.Counter(x["culture"] or "-" for x in rows)
            mat = collections.Counter(x["material"] or "-" for x in rows)
            print("   %-13s n=%-4d 护值 %s–%s | 文化 %s | 材质 %s" % (
                shape, n, rows[-1]["armor"], rows[0]["armor"],
                dict(cul.most_common(3)), dict(mat.most_common(3))))
            print("        例：%s" % "、".join((r["zh"] or r["en"] or r["entityId"])[:14] for r in rows[:5]))
        print()

    print("=== 交叉特色（全族） ===")
    print("叠穿 over X:", sum(1 for x in items if x["layered"]))
    print("分性别变体:", sum(1 for x in items if x["gender_var"]))
    print("材质分布:", dict(collections.Counter(x["material"] or "(空)" for x in items).most_common()))

    with io.open(OUT, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(items, fh, ensure_ascii=False, indent=1)
    print()
    print("written:", OUT, len(items))
    print("PROBE_DONE")


main()
