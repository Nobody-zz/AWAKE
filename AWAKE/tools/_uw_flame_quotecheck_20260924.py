# -*- coding: utf-8 -*-
"""核验 B 类引文是否为源文件连续子串（2026-09-24，只读）—— 跑生成器之前先自查。"""
import io
import json
import os

RULES = r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AnimusForge\PlayerExports\卡拉迪亚编年史\knowledge\rules"
FN = "rule_火焰余烬__火焰余烬.json"

d = json.load(io.open(os.path.join(RULES, FN), encoding="utf-8-sig"))
vars_ = d["Variants"]

QUOTES = [
    (0, "北区卖陶罐的老瘸子就是他们的人"),
    (4, "死了皇帝不埋，等着他回来"),
    (2, "我们不是匪帮，我们是被遗忘者的工会"),
    (2, "我七岁跟着爹进会，到现在鬓角白了，天亮还是没来"),
    (5, "同业，但不是同行"),
    (1, "火焰余烬是帝国的癣疥之疾，不是心腹之患"),
    (3, "那层千年教派的自我定位，是他们在这个灰产行当里唯一的信用担保"),
]

print("=" * 88)
print("B 类引文连续子串核验：%s（共 %d 变体）" % (FN, len(vars_)))
print("=" * 88)
allok = True
for idx, q in QUOTES:
    content = vars_[idx].get("Content") or ""
    ok = q in content
    allok &= ok
    print("[%s] 变体 %d : %s" % ("OK " if ok else "FAIL", idx, q))
    if not ok:
        # 找最接近的位置
        # 取引文头 6 字定位
        head = q[:6]
        p = content.find(head)
        print("      ✗ 未命中。以首 6 字 '%s' 定位 → pos=%s" % (head, p))
        if p >= 0:
            print("      源文该处：%s" % content[p:p + len(q) + 20])
print()
print("全部命中？", "✅ 是" if allok else "❌ 否 —— 必须先修引文")
