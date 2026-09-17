# -*- coding: utf-8 -*-
"""核实：AWAKE 那几个头盔词条名，到底是不是官方译名。重点是「锅盔」。

为什么查：Max 指出「锅盔」在中文里是陕西面食，中文语义模型读它必然被食物方向牵引
⇒ 若这个词本身是我们自己造的，那"测不出来"就不是模型的错，是**名字的错**。
"""
import sqlite3

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect('file:%s?mode=ro' % DB, uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

print("=== 1. bannerlord_items 表结构 ===")
for r in cur.execute("PRAGMA table_info(bannerlord_items)"):
    print("   ", r["name"], r["type"])

print()
print("=== 2. localization_entries 里 CNs 含「锅盔」===")
for r in cur.execute("SELECT DISTINCT stringId, text, filePath FROM localization_entries "
                     "WHERE language='CNs' AND text LIKE '%锅盔%' LIMIT 20"):
    print("   ", r["stringId"], "|", r["text"], "|", r["filePath"])

print()
print("=== 3. stringId 含 kettle 的所有语言 ===")
for r in cur.execute("SELECT DISTINCT language, stringId, text FROM localization_entries "
                     "WHERE stringId LIKE '%kettle%' ORDER BY stringId, language LIMIT 60"):
    print("   %-4s %-40s %s" % (r["language"], r["stringId"], r["text"]))

print()
print("=== 4. CNs 里以「盔」结尾的官方物品名（看命名体系）===")
for r in cur.execute("SELECT DISTINCT stringId, text FROM localization_entries "
                     "WHERE language='CNs' AND text LIKE '%盔' AND length(text)<=8 "
                     "ORDER BY text LIMIT 60"):
    print("   %-14s %s" % (r["text"], r["stringId"]))

print()
print("=== 5. bannerlord_items 里含 kettle 的行 ===")
cols = [c["name"] for c in cur.execute("PRAGMA table_info(bannerlord_items)")]
sel = ", ".join(cols[:12])
import re
for r in cur.execute("SELECT %s FROM bannerlord_items" % sel):
    blob = " ".join(str(r[c]) for c in cols[:12])
    if re.search(r"kettle", blob, re.I):
        print("   ", dict(zip(cols[:12], [r[c] for c in cols[:12]])))

print()
print("=== 6. AWAKE 用的另外几个名字是否在官方 CNs 里 ===")
for w in ["护鼻盔", "链甲围帽", "全覆面盔", "布制围帽", "锅盔"]:
    n = cur.execute("SELECT COUNT(*) FROM localization_entries WHERE language='CNs' AND text LIKE ?",
                    ("%" + w + "%",)).fetchone()[0]
    print("   %-8s 官方条目数 = %d" % (w, n))

con.close()
