# -*- coding: utf-8 -*-
"""统一从 CNs 重取 40 档的官方名与装量短语。"""
import sqlite3, re, io, json

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect('file:%s?mode=ro' % DB.replace('\\', '/'), uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

PT = r"D:\AWAKE-Dev\AWAKE\tools\_eco_auto_20260920.json"
rows = json.load(io.open(PT, encoding="utf-8"))

def fetch_cn_pairs(tok):
    """返回该 stringId 的 CNs 全部变体文本"""
    cur.execute("SELECT text FROM localization_entries WHERE language='CNs' AND stringId=?", (tok,))
    return [r[0] for r in cur.fetchall()]

def parse(txt):
    name = re.sub(r'\{@Plural\}.*', '', txt, flags=re.S)
    name = re.sub(r'\{[^}]*\}', '', name).strip()
    m = re.search(r'\{@Plural\}(.*?)\{\\@\}', txt, flags=re.S)
    return name, (m.group(1).strip() if m else "")

for r in rows:
    tok = r.get("key")
    if not tok:
        continue
    best = None
    for t in fetch_cn_pairs(tok):
        if re.search(r'[\u4e00-\u9fff]', t):
            best = t
            if "{@Plural}" in t:
                break
    if best:
        nm, pl = parse(best)
        r["name"] = nm
        r["plural"] = pl
        r["raw"] = best

print("%-14s %-12s | %-18s | %s" % ("id", "官方名", "装量短语", "原文"))
for r in rows:
    print("%-14s %-12s | %-18s | %s" % (r["id"], r.get("name", ""), r.get("plural", ""), r.get("raw", "")[:44]))
io.open(PT, "w", encoding="utf-8").write(json.dumps(rows, ensure_ascii=False, indent=1))
print("\n已写盘")
con.close()
