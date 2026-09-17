# -*- coding: utf-8 -*-
"""单人头盔批 · 形制分类（只读 XML）。

在 _head_inventory 的基础上补三样前次漏掉的官方字段，并按**形制**（不是物品名）切类：
  weight（挂在 <Item> 上，不在 <Armor> 里）、difficulty/appearance、
  Armor 的 hair_cover_type / beard_cover_type / stealth_factor / has_gender_variations。

分类是**正交两轴**：
  ① 主形制 head_shape（单一归属，按优先级）
  ② 交叉特色 flags（可叠加）：layered(叠穿 over X) / gender_variant / stealth / covers_hair / covers_beard

产出 _head_inventory_20260916.json（覆盖）＋ _head_types_20260916.json（分类视图）
"""
import collections
import io
import json
import os
import re
import xml.etree.ElementTree as ET

GAME = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/SandBoxCore/ModuleData/items"
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "_head_inventory_20260916.json")
OUT2 = os.path.join(HERE, "_head_types_20260916.json")

# 主形制：按优先级匹配 entityId（先匹配者胜）
SHAPE = [
    ("crown",     r"crown|ceremonial|jeweled|laurel|diadem"),
    ("spiked",    r"spiked|facemask|face_mask"),
    ("closed",    r"closed|goggled|visored|full_helm|faceguard|barbute"),
    ("kettle",    r"kettle"),
    ("nasal",     r"nasal|spangenhelm|legionary"),
    ("cheek",     r"earmuff|cheek"),
    ("mail",      r"mail"),
    ("fur",       r"fur|roughhide|thinhide|hide|nordic_leather|leather_cap|shaggy"),
    ("steel_cap", r"desert_helmet|steel_cap|nomad_helmet|steppe|kettle_?$"),
    ("cloth",     r"coif|hood|scarf|wrap|hijab|headcloth|band|turban|headdress|cap$|hat$"),
]


def shape_of(eid):
    for k, pat in SHAPE:
        if re.search(pat, eid, re.I):
            return k
    return "other"


def main():
    import sqlite3
    con = sqlite3.connect("file:%s?mode=ro" % DB, uri=True)
    cur = con.execute("SELECT stringId, text FROM localization_entries WHERE language='CNs'")
    cn = {r[0]: r[1] for r in cur.fetchall()}

    heads = []
    for fn in sorted(f for f in os.listdir(GAME) if f.lower().endswith(".xml")):
        root = ET.parse(os.path.join(GAME, fn)).getroot()
        for it in root.iter("Item"):
            if (it.get("Type") or "") != "HeadArmor":
                continue
            eid = it.get("id") or ""
            if eid.startswith("mp_"):
                continue
            nm = it.get("name") or ""
            m = re.match(r"^\{=([^}]+)\}(.*)$", nm)
            tok, en = (m.group(1), m.group(2).strip()) if m else ("", nm.strip())
            arm = it.find(".//Armor")
            a = dict(arm.attrib) if arm is not None else {}
            flags = {c.tag: dict(c.attrib) for c in it if c.tag == "Flags"}
            ff = flags.get("Flags", {})
            heads.append({
                "entityId": eid, "xml": fn, "en": en, "zh": cn.get(tok), "token": tok,
                "culture": (it.get("culture") or "").replace("Culture.", ""),
                "head": a.get("head_armor"),
                "weight": it.get("weight"),
                "difficulty": it.get("difficulty"),
                "appearance": it.get("appearance"),
                "material": a.get("material_type"),
                "modifier": a.get("modifier_group"),
                "hair_cover": a.get("hair_cover_type"),
                "beard_cover": a.get("beard_cover_type"),
                "stealth": a.get("stealth_factor"),
                "gender_var": a.get("has_gender_variations"),
                "team_color": ff.get("UseTeamColor"),
                "flag_stealth": ff.get("Stealth"),
                "is_merch": it.get("is_merchandise"),
                "shape": shape_of(eid),
                "layered": bool(re.search(r"over_|_over", eid, re.I)),
                "tier": None,
            })

    print("single-player HeadArmor:", len(heads))

    # ---- 主形制分布 ----
    by = collections.defaultdict(list)
    for h in heads:
        by[h["shape"]].append(h)
    print("\n===== 主形制分布 =====")
    for k in sorted(by, key=lambda x: -len(by[x])):
        rows = sorted(by[k], key=lambda h: int(h["head"] or 0))
        hv = [int(r["head"]) for r in rows if r["head"]]
        cul = collections.Counter(r["culture"] or "-" for r in rows)
        mat = collections.Counter(r["material"] or "-" for r in rows)
        print("\n-- %-10s n=%-3d 护头 %s–%s  文化 %s  材质 %s" % (
            k, len(rows), hv[0] if hv else "-", hv[-1] if hv else "-",
            dict(cul.most_common(4)), dict(mat.most_common(4))))
        for r in rows:
            print("     %-42s | %-30s | %-12s | 头%-3s 重%-5s %-9s%s" % (
                r["entityId"], (r["en"] or "")[:30], (r["zh"] or "")[:12],
                r["head"], r["weight"], r["material"] or "-",
                "  [叠穿]" if r["layered"] else ""))

    # ---- 交叉特色 ----
    print("\n===== 交叉特色 =====")
    print("叠穿 (over X):", sum(1 for h in heads if h["layered"]))
    print("分性别变体 has_gender_variations:", sum(1 for h in heads if h["gender_var"]))
    print("遮蔽毛发 hair_cover_type:", dict(collections.Counter(h["hair_cover"] or "(空)" for h in heads)))
    print("遮蔽胡须 beard_cover_type:", dict(collections.Counter(h["beard_cover"] or "(空)" for h in heads)))
    print("潜行 stealth_factor:", dict(collections.Counter(h["stealth"] or "(空)" for h in heads)))
    print("团队色 UseTeamColor:", sum(1 for h in heads if h["team_color"]))
    print("非卖品 is_merchandise!=true:", sum(1 for h in heads if h["is_merch"] != "true"))
    print("modifier_group:", dict(collections.Counter(h["modifier"] or "(空)" for h in heads)))

    with io.open(OUT, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(heads, fh, ensure_ascii=False, indent=1)
    with io.open(OUT2, "w", encoding="utf-8", newline="\n") as fh:
        json.dump({k: [h["entityId"] for h in v] for k, v in by.items()}, fh,
                  ensure_ascii=False, indent=1)
    print("\nwritten:", OUT, len(heads))


if __name__ == "__main__":
    main()
