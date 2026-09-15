# -*- coding: utf-8 -*-
"""拉 53 城镇：官方中文名 + 中文描述文全文，落 JSON 供 L2 人工文案用。"""
import sqlite3, re, json, io

DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
OUT = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out/_rollout_towns_20260913.json"
con = sqlite3.connect(f"file:{DB}?mode=ro", uri=True)
con.row_factory = sqlite3.Row


def loc(string_id):
    row = con.execute(
        "SELECT text FROM localization_entries WHERE stringId=? AND language='CNs' LIMIT 1",
        (string_id,)).fetchone()
    return row["text"] if row else None


rows = con.execute(
    "SELECT settlementId,name,descriptionText,culture,owner FROM bannerlord_settlements "
    "WHERE settlementType='town' ORDER BY settlementId").fetchall()
out = []
for r in rows:
    sid = r["settlementId"]
    nm = r["name"] or ""
    tok = nm.split("{=")[1].split("}")[0] if "{=" in nm else ""
    name_en = nm.split("}", 1)[1].strip() if "}" in nm else ""
    cn = loc(tok)
    # 描述文 token
    desc = r["descriptionText"] or ""
    dtok = desc.split("{=")[1].split("}")[0] if "{=" in desc else ""
    desc_cn = loc(dtok)
    out.append({
        "sid": sid, "token": tok, "name_en": name_en, "name_cn": cn,
        "culture": (r["culture"] or "").replace("Culture.", ""),
        "owner": (r["owner"] or "").replace("Faction.", ""),
        "desc_token": dtok, "desc_cn": desc_cn,
    })
io.open(OUT, "w", encoding="utf-8").write(
    json.dumps(out, ensure_ascii=False, indent=1))
n_desc = sum(1 for x in out if x["desc_cn"])
print(f"towns={len(out)} name_cn={sum(1 for x in out if x['name_cn'])} desc_cn={n_desc}")
for x in out[:4]:
    print(x["sid"], x["name_cn"], "|", (x["desc_cn"] or "")[:60])
