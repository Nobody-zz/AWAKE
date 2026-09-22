# -*- coding: utf-8 -*-
"""在游戏本体 CN 文本里找能讲「骑枪冲锋 / 冲锋杀伤 / 机动性」的句子，作为 A 级可引源。"""
import sqlite3

DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
con = sqlite3.connect("file:" + DB + "?mode=ro", uri=True)
cur = con.cursor()

WORDS = ["骑枪", "冲枪", "夹枪", "冲锋", "长枪", "重骑", "战马", "冲击"]

seen = set()
for w in WORDS:
    rows = cur.execute(
        "select stringId, text from localization_entries where language='CNs' and text like ?",
        ("%" + w + "%",)).fetchall()
    print("=" * 72)
    print("「%s」命中 %d 条" % (w, len(rows)))
    print("=" * 72)
    for sid, t in rows:
        if len(t) < 18:          # 太短的多是物品名，跳过
            continue
        if sid in seen:
            continue
        seen.add(sid)
        print("[%s] %s" % (sid, t[:220]))
    print()

con.close()
