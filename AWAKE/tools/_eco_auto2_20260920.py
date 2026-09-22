# -*- coding: utf-8 -*-
"""补齐 40 档的 key / 官方名 / 装量短语，覆盖写 _eco_auto_20260920.json。"""
import sqlite3, re, io, json

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect('file:%s?mode=ro' % DB.replace('\\', '/'), uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

def split_plural_tok(tok):
    if not tok: return "", ""
    name = re.sub(r'\{@Plural\}.*', '', tok, flags=re.S)
    name = re.sub(r'\{[^}]*\}', '', name).strip()
    m = re.search(r'\{@Plural\}(.*?)\{\\@\}', tok, flags=re.S)
    return name, (m.group(1).strip() if m else "")

# C# 组人工表：id -> token
CSHARP = {
 "grain": "Itv3fgJm", "meat": "LmwhFv5p", "hides": "4kvKQuXM", "hardwood": "ExjMoUiT",
 "charcoal": "iQadPYNe", "iron": "Kw6BkhIf", "planks": "5ac8Boz1", "felt": "hNwjpCVP",
 "tools": "n3cjEB0X", "ironIngot1": "gOpodlt1", "ironIngot2": "7HvtT8bm",
 "ironIngot3": "XHmmbnbB", "ironIngot4": "UfuLKuaI", "ironIngot5": "azjMBa86",
 "ironIngot6": "vLVAfcta",
}

rows = json.load(io.open(r"D:\AWAKE-Dev\AWAKE\tools\_eco_auto_20260920.json", encoding="utf-8"))
for r in rows:
    eid = r["id"]
    if eid in CSHARP:
        r["key"] = CSHARP[eid]
    if r["key"] and (not r["name"] or not r["plural"]):
        tok = r["key"]
        # 该 token 的完整原文在哪？直接在 localization_entries 找带 {@Plural} 的那条
        cur.execute("""SELECT text FROM localization_entries WHERE language='CNs' AND stringId=?
                       ORDER BY CASE WHEN text LIKE '%{@Plural}%' THEN 0 ELSE 1 END LIMIT 1""", (tok,))
        rr = cur.fetchone()
        nm, pl = split_plural_tok(rr[0] if rr else "")
        r["name"] = nm
        r["plural"] = pl

print("%-14s %-8s %-6s %-12s %s" % ("id", "type", "value", "name", "装量"))
for r in rows:
    print("%-14s %-8s %-6s %-12s %s" % (r["id"], r["type"], r["value"], r["name"], r["plural"]))

io.open(r"D:\AWAKE-Dev\AWAKE\tools\_eco_auto_20260920.json", "w", encoding="utf-8").write(
    json.dumps(rows, ensure_ascii=False, indent=1))
print("\n已覆盖写盘，共 %d 件" % len(rows))
con.close()
