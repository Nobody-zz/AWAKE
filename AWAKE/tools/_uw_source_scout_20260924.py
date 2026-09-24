# -*- coding: utf-8 -*-
"""暗面 7 档「能补传言吗」的来源侦察 —— 翻编年史 rules 全库 + 游戏本地化。

甲案要的是「有原句支撑才补」。所以先回答：**这 7 档各自手上有什么料。**
料分两类：
  A 类（游戏本地化 bannerlord.db）：多为操作提示、地名、定型台词 —— 事实的凭据。
  B 类（编年史 rules/*.json，337 条）：**人的话** —— 说法、成见、转述。传言的凭据。

只读、只报，不改任何档。
"""
import io
import json
import os
import re
import sqlite3

RULES = (r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/"
         r"AnimusForge/PlayerExports/卡拉迪亚编年史/knowledge/rules")
DB = (r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/"
      r"dist/games/bannerlord/bannerlord.db")
OUT = r"D:\AWAKE-Dev\AWAKE\tools\_uw_source_scout_20260924.txt"

# 暗面 7 档（《强盗》《农奴》已有三层，不在此列）
WANT = [
    ("underworld-alleys", "巷子", ["巷", "后街", "滨水", "帮派头目", "守护者", "渣滓"]),
    ("underworld-gang-leaders", "巷子头目", ["帮派头目", "守护者", "头目", "后街"]),
    ("underworld-struggle", "巷子争夺", ["巷", "攻打", "夺", "帮派"]),
    ("underworld-gangs", "帮派", ["帮派", "帮", "匪帮", "湖鼠帮", "后街", "黑帮"]),
    ("underworld-crime-rating", "犯罪等级", ["犯罪", "通缉", "悬赏", "罪", "法外", "逃犯"]),
    ("underworld-blood-money", "赎罪金", ["赎罪", "血钱", "罚金", "赔", "人命", "偿"]),
    ("underworld-smuggling", "走私货", ["走私", "私货", "赃", "偷运", "违禁"]),
]

# 「人的话」标记：成见 / 转述 / 立场
VOICE = re.compile(r"我们|他们|人家|有人|据说|听说|可恶|可笑|嘲笑|羡慕|恨|看不起|"
                   r"叫他们|管他们|嗤之|骂|说他们|人称|外号|名号")

L = []


def p(s=""):
    L.append(s)


# ---------- 载入编年史全库 ----------
corpus = []
for fn in sorted(os.listdir(RULES)):
    if not fn.endswith(".json"):
        continue
    try:
        d = json.load(io.open(os.path.join(RULES, fn), encoding="utf-8-sig"))
    except Exception:
        continue
    vs = d.get("Variants") or []
    for i, v in enumerate(vs):
        c = (v.get("Content") or "").strip()
        if c:
            corpus.append((fn, i, c))

p("===== 暗面 7 档 · 来源侦察（甲案：有料才补）=====")
p("编年史料库：%d 条变体（%d 个文件）" % (len(corpus), len(set(x[0] for x in corpus))))
p("")

for slug, title, keys in WANT:
    hits_B, hits_A = [], []
    for fn, i, c in corpus:
        # 档名关键词 ＋ 料名关键词
        if any(k in fn for k in keys) or any((k in c[:120]) for k in keys):
            hits_B.append((fn, i, c))
    with sqlite3.connect("file:" + DB + "?mode=ro", uri=True) as con:
        for k in keys:
            rows = con.execute(
                "SELECT stringId,text FROM localization_entries WHERE language='CNs' "
                "AND text LIKE ? LIMIT 6", ("%" + k + "%",)).fetchall()
            for sid, txt in rows:
                hits_A.append((sid, k, txt))

    p("=" * 70)
    p("%s ｜ %s" % (slug, title))
    p("  B 类（编年史·人的话）：%d 条" % len(hits_B))
    for fn, i, c in hits_B[:8]:
        mark = "★有立场" if VOICE.search(c[:200]) else "   "
        p("   %s [%s#%d] %s" % (mark, fn, i, c[:110].replace("\n", " ")))
    p("  A 类（游戏本地化）：%d 条" % len(hits_A))
    for sid, k, txt in hits_A[:5]:
        p("      「%s」 %s" % (k, txt[:90]))
    p("")

p("=" * 70)
p("说明：★有立场 = 句子里说话人在场（我们/他们/有人/成见词），可作 rumor 的底。")
p("      A 类多为操作提示与地名，**不能**单独支撑 rumor。")

io.open(OUT, "w", encoding="utf-8", newline="\n").write("\n".join(L) + "\n")
print("\n".join(L))
