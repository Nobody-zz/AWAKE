# -*- coding: utf-8 -*-
"""按货物名搜编年史变体，输出「货 → 候选变体」映射，供人工筛。"""
import io, json, os, glob, sys

RULES = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/AnimusForge/PlayerExports/卡拉迪亚编年史/knowledge/rules"
OUT = r"D:\AWAKE-Dev\AWAKE\tools\_eco_chronicle_cands_20260920.json"

GOODS = {
 "谷物": ["谷物", "粮食", "麦子", "产粮"],
 "肉": ["猪肉", "牛肉", "羊肉", "肉的", "屠宰"],
 "兽皮": ["兽皮", "生皮", "硝皮"],
 "硬木": ["硬木", "木料", "伐木", "木材"],
 "木炭": ["木炭", "烧炭"],
 "铁矿石": ["铁矿石", "铁矿", "矿石"],
 "板材": ["板材", "木板", "锯木"],
 "毛毡": ["毛毡", "毡"],
 "工具": ["工具", "铁器"],
 "鱼": ["鱼", "渔获", "捕鱼"],
 "亚麻": ["亚麻"],
 "黏土": ["黏土", "陶土"],
 "葡萄": ["葡萄"],
 "羊毛": ["羊毛"],
 "黄油": ["黄油"],
 "橄榄": ["橄榄"],
 "盐": ["盐"],
 "奶酪": ["奶酪"],
 "枣": ["椰枣", "枣"],
 "啤酒": ["啤酒"],
 "生丝": ["生丝", "养蚕", "丝绸"],
 "葡萄酒": ["葡萄酒"],
 "陶器": ["陶器", "烧陶"],
 "皮革": ["皮革", "鞣"],
 "亚麻布": ["亚麻布", "布匹"],
 "油": ["油"],
 "香料": ["香料"],
 "毛皮": ["毛皮", "貂皮", "皮毛"],
 "天鹅绒": ["天鹅绒"],
 "贵重品": ["贵重品", "珠宝", "宝石"],
 "银矿石": ["银矿"],
 "铁锭": ["钢", "铁锭", "精炼"],
}

files = sorted(glob.glob(os.path.join(RULES, "*.json")))
cache = {}
for f in files:
    try:
        cache[f] = json.load(io.open(f, encoding="utf-8-sig"))
    except Exception:
        pass
print("载入 rule 文件:", len(cache))

result = {}
for good, kws in GOODS.items():
    cands = []
    for f, d in cache.items():
        for i, v in enumerate(d.get("Variants") or []):
            c = (v.get("Content") or "")
            if any(k in c for k in kws):
                cands.append({"file": os.path.basename(f), "vi": i, "content": c})
    result[good] = cands
    print("%-8s 候选 %d" % (good, len(cands)))

json.dump(result, io.open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("\n已落盘:", OUT)
