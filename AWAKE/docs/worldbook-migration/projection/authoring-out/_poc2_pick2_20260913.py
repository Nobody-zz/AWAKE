# -*- coding: utf-8 -*-
"""补查肩/腿/臂甲候选。"""
import xml.etree.ElementTree as ET
import importlib.util

spec = importlib.util.spec_from_file_location(
    "scan", r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out/_poc2_scan_20260913.py")
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)

ITEMS = m.GAME + "/SandBoxCore/ModuleData/items/"
for fn in ("shoulder_armors.xml", "leg_armors.xml", "arm_armors.xml"):
    print(f"--- {fn} (armor>=15) ---")
    rows = []
    for r in m.scan(ITEMS + fn, "Item"):
        try:
            ar = int(r.get("armor") or 0)
        except (TypeError, ValueError):
            ar = 0
        if ar >= 15 and r["cn"]:
            rows.append(r)
    rows.sort(key=lambda x: -int(x.get("armor") or 0))
    for r in rows[:6]:
        print(f"  {r['id']} | {r['cn']} | armor={r['armor']} | {r['culture']}")
