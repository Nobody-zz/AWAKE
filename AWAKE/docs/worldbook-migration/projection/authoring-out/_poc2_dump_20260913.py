# -*- coding: utf-8 -*-
"""PoC2 选品数据 dump：10 件品的完整属性转录。"""
import xml.etree.ElementTree as ET
import importlib.util

spec = importlib.util.spec_from_file_location(
    "scan", r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out/_poc2_scan_20260913.py")
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)

ITEMS = m.GAME + "/SandBoxCore/ModuleData/items/"
PICKS = [
    ("weapons.xml", "steppe_war_bow"),
    ("weapons.xml", "crossbow_c"),
    ("shields.xml", "heavy_round_shield"),
    ("body_armors.xml", "battania_mercenary_armor"),
    ("body_armors.xml", "desert_lamellar"),
    ("head_armors.xml", "sturgian_helmet_closed"),
    ("head_armors.xml", "western_plated_helmet"),
    ("shoulder_armors.xml", "scale_shoulder_armor"),
]

for fn, wid in PICKS:
    root = ET.parse(ITEMS + fn).getroot()
    hit = None
    for it in root.iter("Item"):
        if it.get("id") == wid:
            hit = it
            break
    assert hit is not None, (fn, wid)
    name = hit.get("name") or ""
    tok = name.split("{=")[1].split("}")[0] if "{=" in name else ""
    en = name.split("}", 1)[1] if "}" in name else ""
    out = {
        "file": fn, "id": wid, "cn": m.cn(tok), "en": en, "token": tok,
        "culture": (hit.get("culture") or "").replace("Culture.", ""),
        "value": hit.get("value"), "weight": hit.get("weight"),
        "Type": hit.get("Type"),
    }
    w = hit.find(".//Weapon")
    if w is not None:
        for k in ("weapon_class", "swing_damage", "swing_damage_type", "thrust_damage",
                  "thrust_damage_type", "thrust_speed", "missile_speed", "accuracy",
                  "speed_rating", "weapon_length", "body_armor", "hit_points"):
            if w.get(k):
                out[k] = w.get(k)
    a = hit.find(".//Armor")
    if a is not None:
        for k in ("body_armor", "head_armor", "arm_armor", "leg_armor"):
            if a.get(k):
                out[k] = a.get(k)
    print(out)
    print()
