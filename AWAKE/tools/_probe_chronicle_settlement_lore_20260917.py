# -*- coding: utf-8 -*-
"""从编年史 rules 里挑「聚落形态」相关的一般性说法，逐条打出原文（含身份条件），
供三个概念词条取引文用。只读。
"""
import glob
import io
import json
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = r"C:\Users\26811\Downloads\卡拉迪亚编年史\卡拉迪亚编年史\knowledge\rules"

WANT = ["rule_农奴", "rule_自由民", "rule_帝国政治制度", "rule_瓦兰迪亚政治制度",
        "rule_库赛特政治制度", "rule_巴旦尼亚政治制度"]

for name in WANT:
    p = os.path.join(ROOT, name + "__" + name.replace("rule_", "") + ".json")
    if not os.path.exists(p):
        print("缺文件：%s" % p)
        continue
    d = json.load(io.open(p, encoding="utf-8-sig"))
    print("=" * 78)
    print("### %s   kind=%s scope=%s  keywords=%s" % (name, d.get("Kind"), d.get("Scope"), d.get("Keywords")))
    for i, v in enumerate(d.get("Variants") or []):
        w = v.get("When") or {}
        cond = {k: val for k, val in w.items() if val not in (None, [], "")}
        c = (v.get("Content") or "").strip()
        print("  [%d] 条件=%s" % (i, json.dumps(cond, ensure_ascii=False) if cond else "（无条件）"))
        print("      %s" % c[:420])
