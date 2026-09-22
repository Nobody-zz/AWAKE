# -*- coding: utf-8 -*-
"""dump 编年史里经济题材的变体内容。"""
import io, json, os, glob

RULES = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/AnimusForge/PlayerExports/卡拉迪亚编年史/knowledge/rules"
KEYS = ["第纳尔", "商队", "农奴", "俘虏", "单位"]
for k in KEYS:
    for f in glob.glob(os.path.join(RULES, "rule_*%s*.json" % k)):
        d = json.load(io.open(f, encoding="utf-8-sig"))
        vs = d.get("Variants") or []
        print("=" * 78)
        print("文件:", os.path.basename(f), " 变体数:", len(vs))
        for i, v in enumerate(vs):
            c = (v.get("Content") or "").strip()
            print("  [V%d] %s" % (i, c[:600]))
        print()
