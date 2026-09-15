# -*- coding: utf-8 -*-
"""PoC2 选品：按 weapon_class 分组抽武器 + 头盔/盾候选。"""
import io
import importlib.util

spec = importlib.util.spec_from_file_location(
    "scan", r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out/_poc2_scan_20260913.py")
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)

ITEMS = m.GAME + "/SandBoxCore/ModuleData/items/"

print("--- 武器 CraftedItem 按 weapon_class 抽样 ---")
by_class = {}
for r in m.scan(ITEMS + "weapons.xml", "CraftedItem"):
    if r["cn"] and r.get("wc"):
        by_class.setdefault(r["wc"], []).append(r)
for wc, rows in sorted(by_class.items()):
    rows.sort(key=lambda x: -int(x["swing"] or x["thrust"] or 0))
    r = rows[0]
    print(f"  [{wc}] n={len(rows)} | {r['id']} | {r['cn']} | sw={r['swing']} th={r['thrust']} spd={r['speed']} len={r['len']} | {r['culture']}")

print("--- 弓/投掷（Item 类，远程）---")
for r in m.scan(ITEMS + "weapons.xml", "Item"):
    if r["cn"] and r["wc"] in ("Bow", "Crossbow", "Javelin", "ThrowingAxe", "ThrowingKnife"):
        print(f"  {r['id']} | {r['cn']} | {r['wc']} | v={r['value']} | {r['culture']}")

print("--- 头盔候选（armor>=30）---")
heads = []
for r in m.scan(ITEMS + "head_armors.xml", "Item"):
    try:
        ar = int(r.get("armor") or 0)
    except (TypeError, ValueError):
        ar = 0
    if ar >= 30 and r["cn"]:
        heads.append(r)
heads.sort(key=lambda x: -int(x.get("armor") or 0))
for r in heads[:8]:
    print(f"  {r['id']} | {r['cn']} | armor={r['armor']} | {r['culture']}")

print("--- 盾候选（有 armor 字段优先，按价值）---")
sh = []
for r in m.scan(ITEMS + "shields.xml", "Item"):
    if r["cn"]:
        s = m.con.execute(
            "SELECT body_armor FROM (? )", (1,)) if False else None
        sh.append(r)
# 盾的防护字段在 Shield 组件
import xml.etree.ElementTree as ET
root = ET.parse(ITEMS + "shields.xml").getroot()
cands = []
for it in root.iter("Item"):
    iid = it.get("id") or ""
    if iid.startswith("mp_"):
        continue
    name = it.get("name") or ""
    if "{=" not in name:
        continue
    tok = name.split("{=")[1].split("}")[0]
    sdf = it.find(".//Shield")
    if sdf is None:
        continue
    cands.append((iid, m.cn(tok), sdf.get("body_armor"), it.get("value"),
                  (it.get("culture") or "").replace("Culture.", "")))
for c in sorted(cands, key=lambda x: -int(x[3] or 0))[:8]:
    print(f"  {c[0]} | {c[1]} | shield={c[2]} | v={c[3]} | {c[4]}")
