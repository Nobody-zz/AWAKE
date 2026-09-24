# -*- coding: utf-8 -*-
"""把暗面 8 档的真正文读出来（expressions 在 assertions[] 里面，不是文档级）。

输出：每条断言（kind=事实/传言/解读）＋ 它下面每一层说法（谁能听到、听到哪一档）。
"""
import io
import os

import yaml

DIR = r"D:\AWAKE-Dev\AWAKE\docs\worldbook-migration\projection\authoring-out"
OUT = r"D:\AWAKE-Dev\AWAKE\tools\_uw_read_20260924.txt"

FS = ["underworld-alleys", "underworld-gang-leaders", "underworld-struggle",
      "underworld-gangs", "underworld-crime-rating", "underworld-blood-money",
      "underworld-bandits", "underworld-smuggling"]

KIND = {"fact": "事实", "rumor": "传言", "interpretation": "解读"}
LAYER = {"rumor": "只言片语", "summary": "大致", "detail": "细致", "full": "详尽"}

L = []


def p(s=""):
    L.append(s)


for f in FS:
    d = yaml.safe_load(io.open(os.path.join(DIR, f + ".yaml"), encoding="utf-8"))
    p("=" * 72)
    p("%s ｜ %s ｜ 域=%s/%s" % (f, d["title"]["zh-CN"], d["domain"], d.get("subdomain")))
    p("摘要：%s" % d["summary"]["zh-CN"])
    p("")
    for a in d.get("assertions", []):
        p("── 断言 %s  【%s】" % (a["id"], KIND.get(a.get("kind"), a.get("kind"))))
        p("   内容：%s" % a["text"]["zh-CN"])
        p("   引文 %d 条" % len(a.get("sources", [])))
        for e in a.get("expressions", []):
            p("")
            p("   ▸ %s  〔%s〕" % (e["id"], LAYER.get(e.get("layer"), e.get("layer"))))
            p("     说给谁：" + "、".join(
                "%s(%s,%s)" % (g["profile_id"].replace("profile.", ""), g.get("scope"),
                               LAYER.get(g.get("min_detail"), g.get("min_detail")))
                for g in e.get("grants", [])) or "     （无人）")
            if e.get("denies"):
                p("     排除：" + "、".join(x.get("profile_id", "?") for x in e["denies"]))
            p("     原话：" + e["text"]["zh-CN"])
        p("")

io.open(OUT, "w", encoding="utf-8", newline="\n").write("\n".join(L) + "\n")
print("\n".join(L))
