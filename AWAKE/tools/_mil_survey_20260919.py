# -*- coding: utf-8 -*-
"""军事批开工前探测：① 编年史库 337 条全清单（挑军事类）② 游戏本体有没有兵种/武器描述文。
只读，不改任何东西。
"""
import io
import json
import os
import re
import sqlite3

RULES = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/AnimusForge/PlayerExports/卡拉迪亚编年史/knowledge/rules"
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"

print("=" * 70)
print("① 编年史库 337 条全清单")
print("=" * 70)
files = sorted(os.listdir(RULES))
names = [f for f in files if f.startswith("rule_")]
print("rule 文件数:", len(names))

# 军事实体名单（兵种/军团/器械/马/骆驼）
MIL_WORDS = ["卫队", "侍卫", "骑士", "骑兵", "军团", "兵团", "亲卫", "兵团", "兵营",
             "弩", "投石", "攻城", "马穆鲁克", "古拉姆", "库由格", "具装", "方旗",
             "军事", "军事实力", "军制", "马", "骆驼", "格吕", "拉格", "瓦良格",
             "黄金野猪", "被弃者", "旧式", "现代", "怯薛", "可汗", "达尔罕", "瓦连格"]
hit = []
for n in names:
    core = n[len("rule_"):].split("__")[0]
    if any(w in core for w in MIL_WORDS):
        hit.append(core)
print()
print("含军事关键词的条目（%d）：" % len(hit))
for i, h in enumerate(sorted(set(hit))):
    print("   %-2d %s" % (i + 1, h))

print()
print("=" * 70)
print("② 游戏本体：兵种/武器描述文可得性")
print("=" * 70)
con = sqlite3.connect("file:" + DB + "?mode=ro", uri=True)
cur = con.cursor()

# 表清单
tabs = [r[0] for r in cur.execute("select name from sqlite_master where type='table'").fetchall()]
print("表数:", len(tabs))
print("   ", ", ".join(tabs))

# bannerlord_troops 表结构
print()
print("--- bannerlord_troops 表结构 ---")
cols = [r[1] for r in cur.execute("PRAGMA table_info(bannerlord_troops)").fetchall()]
print("   " + ", ".join(cols))

# CNs 全量装进内存
CN = {}
for sid, t in cur.execute("select stringId, text from localization_entries where language='CNs'"):
    CN[sid] = t
print()
print("CNs 词条数:", len(CN))

# 搜若干兵种名，看是否有"描述性"文本（长句）
PROBE_CN = ["方旗骑士", "可汗卫士", "库由格", "皇家侍卫", "古拉姆", "马穆鲁克",
            "军团兵", "瓦连格", "具装骑兵", "弩砲", "卡拉德"]
KEYPROBE = re.compile(r"^(Character|Encyclopedia|Settlements|Items|Concepts|Kingdom|Troop|Culture|Clan)\.")

for w in PROBE_CN:
    hits = [(sid, t) for sid, t in CN.items() if w in (t or "")]
    print()
    print("   「%s」命中 %d 条" % (w, len(hits)))
    for sid, t in hits[:6]:
        print("      [%s] %s" % (sid, t[:90]))

# 键名模式统计
print()
print("--- stringId 前缀分布（CNs）---")
from collections import Counter
pref = Counter()
for sid in CN:
    pref[sid.split(".")[0] if "." in sid else "(裸token)"] += 1
for k, v in pref.most_common(20):
    print("   %-28s %d" % (k, v))

con.close()
