# -*- coding: utf-8 -*-
"""读《火焰余烬》全部变体：When（谁在说）+ Content（说了什么），2026-09-24 只读。"""
import io
import json
import os

RULE = r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AnimusForge\PlayerExports\卡拉迪亚编年史\knowledge\rules\rule_火焰余烬__火焰余烬.json"

d = json.load(io.open(RULE, encoding="utf-8-sig"))
vars_ = d.get("Variants") or d.get("variants") or []
print("文件：%s" % os.path.basename(RULE))
print("顶层键：%s" % list(d.keys()))
print("变体数：%d" % len(vars_))
print("=" * 92)

for i, v in enumerate(vars_):
    when = v.get("When") or {}
    print("── 变体 %d" % i)
    print("   When = %s" % json.dumps(when, ensure_ascii=False))
    print("   Priority = %s   Weight = %s" % (v.get("Priority"), v.get("Weight")))
    print("   Content:")
    print("     %s" % (v.get("Content") or ""))
    print()
