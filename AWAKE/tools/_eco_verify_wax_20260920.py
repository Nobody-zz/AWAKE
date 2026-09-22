# -*- coding: utf-8 -*-
"""核 蜡 / 海象牙 / 鲸油 的完整出处（哪个模块的本地化文件）+ 物品表状态。"""
import sqlite3, re, io, os, glob

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect('file:%s?mode=ro' % DB.replace('\\', '/'), uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

TARGETS = ["9pwIt4aa", "FhOkxlXJ", "0cQ3jk43", "QGaIYQpg", "IK2IPJi1"]
print("== 完整 filePath ==")
for k in TARGETS:
    cur.execute("SELECT stringId, text, filePath FROM localization_entries WHERE language='CNs' AND stringId=?", (k,))
    for r in cur.fetchall():
        print("  [%s] %-8s <%s>" % (r["stringId"], r["text"][:30], r["filePath"]))
print()

print("== xml_documents_fts 里这些文件属于哪个模块 ==")
for fn in ["std_common_strings", "std_module_strings", "std_TaleWorlds_CampaignSystem", "std_spitems"]:
    cur.execute("SELECT DISTINCT c0, c1 FROM xml_documents_fts_content WHERE c0 LIKE ?", ("%" + fn + "%",))
    rs = cur.fetchall()
    for r in rs:
        print("  %-24s -> %s | module=%s" % (fn, r[0], r[1]))
print()

print("== 蜡 / wax / honey 在物品表 ==")
cur.execute("SELECT entityId, itemType, name, value FROM bannerlord_items WHERE entityId LIKE '%wax%' OR entityId LIKE '%honey%' OR name LIKE '%wax%'")
rows = cur.fetchall()
print("  命中:", len(rows))
for r in rows:
    print("   ", r["entityId"], r["itemType"], r["value"])
print()

print("== 游戏真实安装目录 Modules 下列表 ==")
G = r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules"
if os.path.isdir(G):
    for d in sorted(os.listdir(G)):
        print("   ", d)
else:
    print("   目录不存在:", G)
