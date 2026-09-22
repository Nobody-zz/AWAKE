# -*- coding: utf-8 -*-
"""生成 40 档参数骨架：官方名 / 装量短语 / 标价 / type / 出处，落 json。"""
import sqlite3, re, io, json, os

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect('file:%s?mode=ro' % DB.replace('\\', '/'), uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

def cn(key):
    c2 = con.cursor()
    c2.execute("SELECT text FROM localization_entries WHERE language='CNs' AND stringId=? LIMIT 1", (key,))
    r = c2.fetchone()
    return r[0] if r else ""

def split_plural(tok):
    """官方名 + 装量短语"""
    if not tok: return "", ""
    name = re.sub(r'\{@Plural\}.*', '', tok, flags=re.S)
    name = re.sub(r'\{[^}]*\}', '', name).strip()
    m = re.search(r'\{@Plural\}(.*?)\{\\@\}', tok, flags=re.S)
    return name, (m.group(1).strip() if m else "")

# ---- 本批 40 档 ----
# (id, value, type, 出处, 备注)
SPEC = [
 # A1: itemType='Goods'（XML）
 ("fish", 12, "Goods", "horses_and_others.xml"),
 ("flax", 15, "Goods", "horses_and_others.xml"),
 ("clay", 18, "Goods", "horses_and_others.xml"),
 ("grape", 20, "Goods", "horses_and_others.xml"),
 ("wool", 22, "Goods", "horses_and_others.xml"),
 ("butter", 25, "Goods", "horses_and_others.xml"),
 ("olives", 30, "Goods", "horses_and_others.xml"),
 ("cheese", 40, "Goods", "horses_and_others.xml"),
 ("date_fruit", 50, "Goods", "horses_and_others.xml"),
 ("beer", 50, "Goods", "horses_and_others.xml"),
 ("cotton", 80, "Goods", "horses_and_others.xml"),
 ("wine", 90, "Goods", "horses_and_others.xml"),
 ("pottery", 210, "Goods", "horses_and_others.xml"),
 ("leather", 230, "Goods", "horses_and_others.xml"),
 ("linen", 245, "Goods", "horses_and_others.xml"),
 ("oil", 290, "Goods", "horses_and_others.xml"),
 ("jewelry", 675, "Goods", "horses_and_others.xml"),
 ("stolen_goods", None, "Goods", "horses_and_others.xml"),
 # A2: DefaultItems.cs（C#）
 ("grain", 10, "Goods", "DefaultItems.cs"),
 ("meat", 30, "Goods", "DefaultItems.cs"),
 ("hides", 50, "Goods", "DefaultItems.cs"),
 ("hardwood", 25, "Goods", "DefaultItems.cs"),
 ("charcoal", 50, "Goods", "DefaultItems.cs"),
 ("iron", 50, "Goods", "DefaultItems.cs"),
 ("planks", 180, "Goods", "DefaultItems.cs"),
 ("felt", 230, "Goods", "DefaultItems.cs"),
 ("tools", 250, "Goods", "DefaultItems.cs"),
 ("ironIngot1", 20, "Goods", "DefaultItems.cs"),
 ("ironIngot2", 30, "Goods", "DefaultItems.cs"),
 ("ironIngot3", 60, "Goods", "DefaultItems.cs"),
 ("ironIngot4", 100, "Goods", "DefaultItems.cs"),
 ("ironIngot5", 160, "Goods", "DefaultItems.cs"),
 ("ironIngot6", 260, "Goods", "DefaultItems.cs"),
 # B: Animal / Horse
 ("cat", 20, "Animal", "horses_and_others.xml"),
 ("dog", 20, "Animal", "horses_and_others.xml"),
 ("goose", 50, "Animal", "horses_and_others.xml"),
 ("chicken", 50, "Animal", "horses_and_others.xml"),
 ("hog", 60, "Animal", "horses_and_others.xml"),
 ("sumpter_horse", 110, "Horse", "horses_and_others.xml"),
 ("old_horse", 130, "Horse", "horses_and_others.xml"),
]

rows = []
for eid, val, typ, src in SPEC:
    cur.execute("SELECT name, value FROM bannerlord_items WHERE entityId=?", (eid,))
    r = cur.fetchone()
    tok = r["name"] if r else None
    nm, plur = split_plural(tok)
    key = None
    if tok:
        m = re.search(r'\{=([^}]+)\}', tok)
        key = m.group(1) if m else None
    real_val = r["value"] if r and r["value"] is not None else val
    rows.append({"id": eid, "type": typ, "value": real_val, "src": src,
                 "key": key, "name": nm, "plural": plur})
    print("%-14s %-8s %-6s %-6s %s | %s" % (eid, typ, real_val, key, nm, plur))

io.open(r"D:\AWAKE-Dev\AWAKE\tools\_eco_auto_20260920.json", "w", encoding="utf-8").write(
    json.dumps(rows, ensure_ascii=False, indent=1))
print("\n共 %d 件，已落盘 tools/_eco_auto_20260920.json" % len(rows))
con.close()
