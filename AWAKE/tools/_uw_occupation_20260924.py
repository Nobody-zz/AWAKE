# -*- coding: utf-8 -*-
"""查游戏 occupation 枚举的真实取值写法（2026-09-24，只读）"""
import sqlite3

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect(f'file:{DB}?mode=ro', uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

print("=" * 70)
print("bannerlord_troops.occupation 的全部取值（游戏真实产出的写法）")
print("=" * 70)
cur.execute("SELECT occupation, COUNT(*) c FROM bannerlord_troops "
            "WHERE occupation IS NOT NULL AND occupation<>'' "
            "GROUP BY occupation ORDER BY c DESC")
rows = cur.fetchall()
print("共 %d 种不同的 occupation：" % len(rows))
for r in rows:
    print("   %-24s %5d" % (r["occupation"], r["c"]))

print()
print("=" * 70)
print("判定：游戏给的是蛇形(trooper) 还是驼峰(Trooper)？")
print("=" * 70)
for r in rows:
    o = r["occupation"]
    if "_" in o:
        print("   ★ 含下划线：%s" % o)
    elif o != o.lower() and o != o.upper():
        print("   ● 驼峰写法：%s" % o)
