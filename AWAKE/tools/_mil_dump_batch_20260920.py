# -*- coding: utf-8 -*-
"""军事批 · 批量取数：24 个题材词的编年史变体全文 + 游戏本体含该词的 CN 全文。

用法：python _mil_dump_batch_20260920.py
产出：docs/worldbook-migration/_mil_batch_dump_20260920.txt（完整素材，供手写 L2）
      stdout 打摘要（每档变体数 / 各变体字数 / CN 命中数）
"""
import io
import json
import os
import sqlite3

RULES = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/AnimusForge/PlayerExports/卡拉迪亚编年史/knowledge/rules"
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
OUT = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/_mil_batch_dump_20260920.txt"

GROUPS = [
    ("兵种·军团", ["方旗骑士", "可汗亲卫", "古拉姆", "维基亚卫队", "斯特吉亚亲卫骑兵",
                   "黄金野猪兵团", "被弃者军团", "旧式军团", "现代军团", "库由格"]),
    ("军制·军力", ["帝国军事制度", "巴旦尼亚军事制度", "北帝国的军事实力", "南帝国的军事实力",
                   "西帝国的军事实力", "诺德军事力量", "阿塞莱军事力量"]),
    ("弓弩·攻城器械", ["包铁弩", "弩砲", "火焰弩砲", "投石车", "火焰投石车", "攻城塔", "攻城锤"]),
]

buf = []
summ = []
con = sqlite3.connect("file:" + DB + "?mode=ro", uri=True)
cur = con.cursor()

for gname, words in GROUPS:
    buf.append("\n" + "#" * 78 + "\n# %s\n" % gname + "#" * 78)
    for kw in words:
        fn = "rule_%s__%s.json" % (kw, kw)
        p = os.path.join(RULES, fn)
        buf.append("\n" + "=" * 74 + "\n【%s】%s\n" % (kw, "找到" if os.path.exists(p) else "★缺文件"))
        nvar, vlens = 0, []
        if os.path.exists(p):
            d = json.load(io.open(p, encoding="utf-8-sig"))
            buf.append("  Id: %s\n  Keywords: %s\n" % (d.get("Id"), json.dumps(d.get("Keywords"), ensure_ascii=False)))
            rst = d.get("RagShortTexts")
            if rst:
                buf.append("  RagShortTexts: %s\n" % json.dumps(rst, ensure_ascii=False)[:600])
            vs = d.get("Variants") or []
            nvar = len(vs)
            for i, v in enumerate(vs):
                c = v.get("Content") or ""
                vlens.append(len(c))
                buf.append("\n  --- V%d  Priority=%s  When=%s ---\n  %s\n" % (
                    i, v.get("Priority"), json.dumps(v.get("When"), ensure_ascii=False), c))
        rows = cur.execute(
            "select stringId, text from localization_entries where language='CNs' and text like ?",
            ("%" + kw + "%",)).fetchall()
        buf.append("\n  --- 游戏本体 CN 命中 %d 条 ---\n" % len(rows))
        for sid, t in rows:
            buf.append("  [%s] %s\n" % (sid, t))
        summ.append((gname, kw, nvar, vlens, len(rows)))

con.close()
txt = "".join(buf)
io.open(OUT, "w", encoding="utf-8", newline="\n").write(txt)

print("总字数 %d -> %s" % (len(txt), OUT))
print("=" * 78)
print("%-14s %-20s %5s %-22s %6s" % ("组", "词", "变体", "各变体字数", "CN命中"))
for g, kw, nv, vl, nc in summ:
    print("%-14s %-20s %5d %-22s %6d" % (g, kw, nv, str(vl)[:22], nc))
