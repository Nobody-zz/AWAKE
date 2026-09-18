# -*- coding: utf-8 -*-
"""城堡铺开批取数：67 座城堡 + 下辖村（CN/EN/官方描述首句）+ culture/owner/prosperity/scene。
写 _castle_inventory_20260914.json；`python _castle_fetch_20260914.py [culture|all]` 打印明细。
"""
import sqlite3, json, io, os, re, sys, collections

DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_castle_inventory_20260914.json")

con = sqlite3.connect(f"file:{DB}?mode=ro", uri=True)
con.row_factory = sqlite3.Row

def loc(sid, lang="CNs"):
    if not sid:
        return None
    r = con.execute("SELECT text FROM localization_entries WHERE stringId=? AND language=? LIMIT 1", (sid, lang)).fetchone()
    return r["text"] if r else None

def parse_nm(nm):
    nm = nm or ""
    tok = nm.split("{=")[1].split("}")[0] if "{=" in nm else ""
    en = nm.split("}", 1)[1].strip() if "}" in nm else ""
    return tok, en

def first_sent(desc_text):
    s = re.sub(r"\s+", "", desc_text or "")
    return (s.split("。", 1)[0] + "。") if s else None

rows = con.execute("SELECT settlementId,name,culture,owner,prosperityOrHearth,sceneName,positionX,positionY,componentId,buildingCount,locationCount "
                   "FROM bannerlord_settlements WHERE settlementType='castle' ORDER BY settlementId").fetchall()
out = {}
for r in rows:
    sid = r["settlementId"]
    tok, en = parse_nm(r["name"])
    cn = loc(tok)
    vs = con.execute("SELECT settlementId,name,descriptionText FROM bannerlord_settlements "
                     "WHERE settlementType='village' AND boundSettlement=?", ("Settlement." + sid,)).fetchall()
    villages = []
    for v in vs:
        vt, ven = parse_nm(v["name"])
        vcn = loc(vt)
        dtok, _ = parse_nm(v["descriptionText"]) if False else (None, None)
        dtok = v["descriptionText"].split("{=")[1].split("}")[0] if v["descriptionText"] and "{=" in v["descriptionText"] else ""
        dcn = loc(dtok)
        villages.append({"sid": v["settlementId"], "cn": vcn, "en": ven, "desc": re.sub(r"\s+", "", dcn or ""),
                         "first": first_sent(dcn)})
    out[sid] = {
        "cn": cn, "en": en, "name_token": tok,
        "culture": (r["culture"] or "").replace("Culture.", ""),
        "owner": r["owner"] or "", "prosperity": r["prosperityOrHearth"],
        "scene": r["sceneName"] or "", "x": r["positionX"], "y": r["positionY"],
        "component": r["componentId"] or "", "buildings": r["buildingCount"], "locations": r["locationCount"],
        "villages": villages,
    }
json.dump(out, io.open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("城堡数:", len(out), "-> ", OUT)
print("文化分布:", dict(collections.Counter(v["culture"] for v in out.values())))

want = sys.argv[1] if len(sys.argv) > 1 else None
if want:
    for sid, c in sorted(out.items()):
        if want != "all" and c["culture"] != want:
            continue
        vv = "、".join(f"{v['cn']}({v['en']})" for v in c["villages"])
        print("=" * 100)
        print(f"{sid}  {c['cn']}  [{c['en']}]  文化={c['culture']}  领主={c['owner']}  繁荣={c['prosperity']}")
        print(f"  下辖: {vv}")
        for v in c["villages"]:
            print(f"    - {v['cn']} | {v['desc']}")
