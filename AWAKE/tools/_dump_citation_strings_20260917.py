# -*- coding: utf-8 -*-
"""把计划引用的 stringId 逐条取出官方中文原文（写词条前先逐字看清，避免转抄出错）。"""
import io
import sqlite3
import sys

sys.stdout.reconfigure(encoding="utf-8")

DB = r"C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db"
con = sqlite3.connect(f"file:{DB}?mode=ro", uri=True)

IDS = {
    "村庄": ["3GsZXXOi", "2qZ14G9p", "7TbVhbT9", "8cY08v3s", "5BabRyaa", "1bxTLLAk",
             "4bkLDxIU", "25pNV9E3", "2Af5HRJU", "5HsJkbZz", "665JbYIC", "7zaMwF08",
             "VMcd4nma", "6snepBi5", "4o7R829M"],
    "城堡": ["1FPpHasQ", "2AhTA1ba", "T8w5VRAy", "PiLml6Nl", "QpQQJjD6", "3lxq5fvI",
             "8oaVYIlk", "RdbLbpgO", "O7lU6qaU", "7ZOp7cg5", "Ll1EJHXF", "W6XMWJ8R"],
    "城镇": ["AfiEQPky", "6Xl9F8Oa", "8PsaGhI8", "6kn630ka", "FQntPChs", "8bSHlWBL",
             "Bg83jhCR", "AVQUGwTg", "8qwvZ15E", "6q7UsTtn", "47hUs9yg"],
}

for label, ids in IDS.items():
    print("=" * 78)
    print("### %s" % label)
    for sid in ids:
        rows = list(con.execute(
            "SELECT DISTINCT text FROM localization_entries WHERE language='CNs' AND stringId=?", (sid,)))
        if not rows:
            print("   %-10s <<未命中>>" % sid)
            continue
        for r in rows:
            print("   %-10s %s" % (sid, r[0]))
