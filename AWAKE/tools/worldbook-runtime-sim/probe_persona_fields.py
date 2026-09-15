#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""列出 repo 侧 persona definition 的字段集合与取值，判断 status 由谁写入。"""
import json
import os
from collections import Counter

DEF_DIR = r"D:\AWAKE-Dev\AWAKE\ModuleData\Worldbook\persona_definitions\definitions"

keys = Counter()
sample = None
for name in sorted(os.listdir(DEF_DIR)):
    if not name.lower().endswith(".json"):
        continue
    if name == "hero_default.json":
        continue
    with open(os.path.join(DEF_DIR, name), "r", encoding="utf-8") as handle:
        obj = json.load(handle)
    keys.update(obj.keys())
    if sample is None:
        sample = (name, obj)

print("字段出现次数（76 张角色卡）：")
for key, count in keys.most_common():
    print("  %-26s %d" % (key, count))
print()
print("样板：", sample[0])
for key, value in sample[1].items():
    text = json.dumps(value, ensure_ascii=False)
    if len(text) > 110:
        text = text[:110] + " …"
    print("  %-26s %s" % (key, text))
