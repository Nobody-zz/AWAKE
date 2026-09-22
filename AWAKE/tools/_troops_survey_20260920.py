# -*- coding: utf-8 -*-
"""盘点现有「兵种类」词条：war 域全览 ＋ troops 子域逐档 dump（描述层面）。"""
import io
import os

import yaml

AO = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out"

docs = []
for f in sorted(os.listdir(AO)):
    if not f.endswith(".yaml"):
        continue
    try:
        d = yaml.safe_load(io.open(os.path.join(AO, f), encoding="utf-8"))
    except Exception:
        continue
    if not isinstance(d, dict) or not str(d.get("id", "")).startswith("doc.war."):
        continue
    exprs = [e for a in (d.get("assertions") or []) for e in (a.get("expressions") or [])]
    grants = [g for e in exprs for g in (e.get("grants") or [])]
    docs.append({
        "f": f, "id": d["id"], "sub": d.get("subdomain", ""),
        "zh": (d.get("title") or {}).get("zh-CN", ""),
        "en": (d.get("title") or {}).get("en", ""),
        "rev": d.get("revision"), "na": len(d.get("assertions") or []),
        "ne": len(exprs), "ng": len(grants),
        "cats": sorted({x["layer"] for x in exprs}),
        "sum": (d.get("summary") or {}).get("zh-CN", ""),
    })

print("=" * 78)
print("war 域全部 %d 档" % len(docs))
print("=" * 78)
print("%-34s %-16s %-3s %-3s %-3s %-3s %s" % ("doc_id", "subdomain", "rev", "断", "表", "授", "标题"))
for d in sorted(docs, key=lambda x: (x["sub"], x["id"])):
    print("%-34s %-16s %-3s %-3s %-3s %-3s %s" % (d["id"][:34], d["sub"], d["rev"], d["na"], d["ne"], d["ng"], d["zh"]))

troops = [d for d in docs if d["sub"] in ("troops", "military")]
print()
print("=" * 78)
print("兵种相关档（troops ＋ military）：%d 档 —— 描述逐条" % len(troops))
print("=" * 78)
for d in troops:
    D = yaml.safe_load(io.open(os.path.join(AO, d["f"]), encoding="utf-8"))
    print()
    print("#" * 78)
    print("## %s ｜ %s / %s ｜ rev %s ｜ %s" % (d["id"], d["zh"], d["en"], d["rev"], d["f"]))
    print("#" * 78)
    print("aliases 中:", "、".join((D.get("aliases") or {}).get("zh-CN") or []))
    print("summary  :", d["sum"])
    for a in D.get("assertions") or []:
        t = (a.get("text") or {}).get("zh-CN", "")
        print()
        print("  [断言 %s] kind=%s  (%d 字)" % (a["id"], a.get("kind"), len(t)))
        print("    " + t)
        for e in a.get("expressions") or []:
            et = (e.get("text") or {}).get("zh-CN", "")
            gs = ["%s@%s/%s%s" % (g.get("profile_id", "").replace("profile.", ""), g.get("scope"), g.get("min_detail"),
                                  ("+" + ",".join(x.split(".")[-1] for x in g["culture_ids"])) if g.get("culture_ids") else "")
                  for g in (e.get("grants") or [])]
            print("      · [%s] %s" % (e.get("layer"), " | ".join(gs)))
            print("        " + et)
