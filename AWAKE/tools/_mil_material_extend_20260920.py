# -*- coding: utf-8 -*-
"""军事批 · A 级素材扩充（2026-09-20）。

dump 是按「题材词精确等值」查的，兵种档的 fact 层需要游戏本体更多引文。
本脚本在 bannerlord.db 的 CN 文本里做 LIKE 模糊搜，按词输出命中条目（stringId + 全文）。
"""
import io
import os
import sqlite3

DB = r"file:C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db?mode=ro"
OUT = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/_mil_material_extend_20260920.txt"

WORDS = [
    # A 组
    "方旗骑士", "方旗", "可汗亲卫", "亲卫", "古拉姆", "达西", "帕提沙", "维基亚", "瓦兰吉",
    "亲卫骑兵", "瓦兰格", "瓦良格", "德鲁日纳", "黄金野猪", "野猪兵团", "被弃者", "摒弃者",
    "旧式军团", "常备军", "常备军团", "军团", "私兵", "执政官", "奥通加德", "阿契特", "库由格",
    # B 组
    "军事制度", "军事实力", "军力", "军事力量",
    # C 组
    "包铁弩", "弩砲", "弩炮", "投石车", "攻城塔", "攻城槌", "攻城锤", "冲车", "围城",
]

db = sqlite3.connect(DB, uri=True)
cur = db.cursor()

buf = []
for w in WORDS:
    rows = cur.execute(
        "SELECT stringId, text FROM localization_entries "
        "WHERE language='CNs' AND text LIKE ? ORDER BY length(text) DESC LIMIT 40",
        ("%" + w + "%",)).fetchall()
    buf.append("=" * 74)
    buf.append("【%s】命中 %d 条（已按长度降序，长文在前）" % (w, len(rows)))
    for sid, t in rows:
        t1 = t.replace("\n", " / ")
        buf.append("  [%s] %s" % (sid, t1[:600] + ("…" if len(t1) > 600 else "")))
    buf.append("")

io.open(OUT, "w", encoding="utf-8", newline="\n").write("\n".join(buf))
print("已写:", OUT)
print("词数:", len(WORDS), "总行:", len(buf))
