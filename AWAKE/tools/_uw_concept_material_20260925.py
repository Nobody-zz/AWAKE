# -*- coding: utf-8 -*-
"""概念档素材探针：为 23 个空子域各找权威素材（游戏 DB 只读）。

口径：
  - 素材优先 `localization_entries`（CNs，filePath 分类）；
  - 只登记"确实存在且够长"的条目，避免拿 3 个字的 UI 标签当素材；
  - 输出 JSON 供生成器消费，不写 authoring。

⚠️ 本脚本只读 DB，不写任何文件到 authoring。
"""
import io
import os
import json
import sqlite3

ROOT = r"D:\AWAKE-Dev\AWAKE"
DB = r"C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db"
OUT = os.path.join(ROOT, "tools", "_uw_concept_material_20260925.json")

con = sqlite3.connect("file:%s?mode=ro" % DB.replace("\\", "/"), uri=True)
cur = con.cursor()

# 通用：按关键词在 CNs 里找长句
def find(kws, minlen=8, limit=40, files=None):
    rows = []
    for kw in kws:
        q = ("SELECT stringId, text, filePath FROM localization_entries "
             "WHERE language='CNs' AND text LIKE ? AND length(text)>=?")
        args = ["%" + kw + "%", minlen]
        if files:
            q += " AND (" + " OR ".join(["filePath LIKE ?"] * len(files)) + ")"
            args += ["%" + f + "%" for f in files]
        q += " ORDER BY length(text) DESC LIMIT ?"
        for sid, txt, fp in cur.execute(q, args + [limit]).fetchall():
            rows.append({"kw": kw, "stringId": sid, "text": txt, "filePath": fp})
    return rows


# 每空子域 → 检索关键词（中文，取自游戏官方译名口径）
SPEC = {
    "culture/clothing":   ["斗篷", "长袍", "衣着", "穿", "服饰"],
    "culture/festivals":  ["节日", "庆典", "宴会", "祭祀", "祝"],
    "culture/language":   ["语", "方言", "口音", "文字", "书写"],
    "culture/marriage":   ["婚姻", "娶", "嫁", "联姻", "妻", "夫"],
    "economy/currency":   ["第纳尔", "钱币", "货币", "金币", "银币"],
    "economy/debt":       ["债", "欠", "赎金", "借贷", "抵押"],
    "economy/food":       ["面包", "食物", "粮食", "谷", "肉", "酒"],
    "economy/land_production": ["田", "耕地", "收成", "佃", "农"],
    "economy/taxation":   ["税", "征税", "贡", "赋", "徭役"],
    "economy/trade_routes": ["商路", "商队", "路线", "通商", "货"],
    "economy/workshops":  ["作坊", "工匠", "工坊", "锻造", "铁匠"],
    "geography/climate":  ["气候", "冬", "严寒", "酷热", "雨季"],
    "geography/directions": ["东方", "西方", "南方", "北方", "边境"],
    "geography/natural_boundaries": ["山脉", "大河", "海峡", "草原", "荒漠"],
    "geography/roads":    ["道路", "大道", "驿", "路", "渡口"],
    "geography/sea_routes": ["海", "航", "港口", "船", "沿岸"],
    "politics/kingdoms":  ["王国", "帝国", "汗国", "公国", "苏丹"],
    "politics/offices":   ["元老", "波耶", "总督", "官", "职", "那颜"],
    "politics/succession": ["继承", "继位", "王位", "宣称", "正统"],
    "war/fortifications": ["城墙", "要塞", "城堡", "攻城", "围城", "塔"],
    "war/logistics":      ["补给", "粮草", "辎重", "后勤", "行军"],
    "war/prisoners":      ["俘虏", "囚", "赎", "战俘", "监"],
    "war/tactics":        ["战术", "阵", "突袭", "伏击", "包抄", "骑兵"],
}

res = {}
for sub, kws in SPEC.items():
    rows = find(kws, minlen=10, limit=30)
    # 去重（同 stringId 只留最长的一条）
    ded = {}
    for r in rows:
        k = r["stringId"]
        if k not in ded or len(r["text"]) > len(ded[k]["text"]):
            ded[k] = r
    res[sub] = sorted(ded.values(), key=lambda r: -len(r["text"]))[:12]

io.open(OUT, "w", encoding="utf-8").write(
    json.dumps(res, ensure_ascii=False, indent=1))

print("子域素材盘点：")
tot = 0
for sub, rows in res.items():
    tot += len(rows)
    print("  %-32s %d 条  最长 %d 字  %s" % (
        sub, len(rows),
        max([len(r["text"]) for r in rows], default=0),
        (rows[0]["text"][:36] if rows else "—— 无 ——")))
print("合计 %d 条候选" % tot)
con.close()
