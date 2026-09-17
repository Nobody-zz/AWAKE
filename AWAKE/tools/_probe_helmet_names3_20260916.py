# -*- coding: utf-8 -*-
"""精确核：AWAKE 那批头盔词条名，官方 CNs 里有没有**独立**条目（不带任何修饰）。"""
import sqlite3

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect('file:%s?mode=ro' % DB, uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

WORDS = ["锅盔", "圆顶锅盔", "护鼻盔", "链甲围帽", "全覆面盔", "布制围帽与头巾",
         "侧护覆板盔", "布制围帽"]

print("%-12s %-8s %s" % ("词", "独立名", "含它的官方最长/最短样例"))
for w in WORDS:
    exact = cur.execute("SELECT DISTINCT stringId FROM localization_entries "
                        "WHERE language='CNs' AND text=?", (w,)).fetchall()
    like = cur.execute("SELECT DISTINCT text FROM localization_entries "
                       "WHERE language='CNs' AND text LIKE ?", ("%" + w + "%",)).fetchall()
    forms = sorted(t["text"] for t in like)
    sample = ("最短: %s ／ 最长: %s" % (min(forms, key=len), max(forms, key=len))) if forms else "—"
    print("%-12s %-8s %s" % (w, ("有(%d)" % len(exact)) if exact else "**无**", sample))
    print("            共 %d 种形态" % len(forms))

con.close()
