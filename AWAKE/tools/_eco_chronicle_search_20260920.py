# -*- coding: utf-8 -*-
"""搜全部 337 个编年史 rule 的 Content，找经济题材。"""
import io, json, os, glob, re

RULES = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/AnimusForge/PlayerExports/卡拉迪亚编年史/knowledge/rules"
KW = ["第纳尔", "商队", "价钱", "买", "卖", "税", "货", "集市", "物价", "工坊", "银币", "钱"]
hits = {}
for f in glob.glob(os.path.join(RULES, "*.json")):
    try:
        d = json.load(io.open(f, encoding="utf-8-sig"))
    except Exception:
        continue
    vs = d.get("Variants") or []
    for i, v in enumerate(vs):
        c = (v.get("Content") or "")
        if any(k in c for k in KW):
            hits.setdefault(os.path.basename(f), []).append((i, c))
print("命中文件数:", len(hits))
for fn in sorted(hits):
    print("=" * 78)
    print(fn)
    for i, c in hits[fn]:
        print("  [V%d] %s" % (i, c[:400].replace("\n", " ")))
