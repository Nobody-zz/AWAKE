# -*- coding: utf-8 -*-
"""PoC2 探针：扫描战役武器/甲 XML，join 官方中文名，出候选清单。"""
import xml.etree.ElementTree as ET
import sqlite3
import io
import sys

GAME = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules"
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"

con = sqlite3.connect(f"file:{DB}?mode=ro", uri=True)
con.row_factory = sqlite3.Row


def cn(token):
    if not token:
        return None
    row = con.execute(
        "SELECT text FROM localization_entries WHERE stringId=? AND language='CNs' LIMIT 1",
        (token,)).fetchone()
    return row["text"] if row else None


def scan(path, tag):
    root = ET.parse(path).getroot()
    out = []
    for it in root.iter(tag):
        iid = it.get("id") or ""
        if iid.startswith("mp_") or "tournament" in path:
            continue
        name = it.get("name") or ""
        token = name.split("{=")[1].split("}")[0] if "{=" in name else None
        rec = {
            "id": iid,
            "token": token,
            "cn": cn(token),
            "culture": (it.get("culture") or "").replace("Culture.", ""),
            "value": it.get("value"),
            "type": it.get("Type") or it.get("type") or "",
        }
        # 武器关键属性
        w = it.find(".//Weapon")
        if w is not None:
            rec["wc"] = w.get("weapon_class")
            rec["swing"] = w.get("swing_damage")
            rec["thrust"] = w.get("thrust_damage") or w.get("thrust_speed")
            rec["speed"] = w.get("speed_rating")
            rec["len"] = w.get("weapon_length")
        # 甲关键属性
        a = it.find(".//Armor") or it.find(".//ItemComponent/Armor")
        if a is not None:
            rec["armor"] = a.get("body_armor") or a.get("head_armor")
            rec["weight"] = it.get("weight")
        rec["template"] = it.get("crafting_template")
        out.append(rec)
    return out


if __name__ == "__main__":
    items_dir = GAME + "/SandBoxCore/ModuleData/items/"
    for fn, tag in (("weapons.xml", "Item"), ("weapons.xml", "CraftedItem"),
                    ("body_armors.xml", "Item"), ("head_armors.xml", "Item"),
                    ("shields.xml", "Item")):
        rows = scan(items_dir + fn, tag)
        named = [r for r in rows if r["cn"]]
        print(f"== {fn}:{tag} total={len(rows)} withCN={len(named)}")
    # 候选抽样：各武器类高价值 + 各甲类高 armor
    print("\n--- 武器候选（CraftedItem, 有名, value>=400）---")
    for r in scan(items_dir + "weapons.xml", "CraftedItem"):
        try:
            v = int(r["value"] or 0)
        except (TypeError, ValueError):
            v = 0
        if v >= 400 and r["cn"]:
            print(f"  {r['id']} | {r['cn']} | {r['wc']} | v={r['value']} | sw={r['swing']}/th={r['thrust']} spd={r['speed']} len={r['len']} | {r['culture']}")
    print("\n--- 甲候选（body, armor>=20）---")
    for r in scan(items_dir + "body_armors.xml", "Item"):
        try:
            ar = int(r.get("armor") or 0)
        except (TypeError, ValueError):
            ar = 0
        if ar >= 20 and r["cn"]:
            print(f"  {r['id']} | {r['cn']} | armor={r['armor']} | v={r['value']} | {r['culture']}")
    print("\n--- 盾候选（高价值）---")
    for r in scan(items_dir + "shields.xml", "Item"):
        try:
            v = int(r["value"] or 0)
        except (TypeError, ValueError):
            v = 0
        if v >= 200 and r["cn"]:
            print(f"  {r['id']} | {r['cn']} | v={r['value']} | {r['culture']}")
