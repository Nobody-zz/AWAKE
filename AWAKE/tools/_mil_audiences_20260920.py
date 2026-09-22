# -*- coding: utf-8 -*-
"""把本批 24 档的 grant 受众摊开：每档第几条断言/哪一层/给了哪些 profile×culture×role×kingdom。"""
import glob
import io
import json
import os

AO = r"D:\AWAKE-Dev\AWAKE\docs\worldbook-migration\projection\authoring-out"
docs = []
for f in sorted(glob.glob(os.path.join(AO, "_mil_A*_20260920.json")) +
                glob.glob(os.path.join(AO, "_mil_B*_20260920.json")) +
                glob.glob(os.path.join(AO, "_mil_C*_20260920.json"))):
    docs.extend(json.load(io.open(f, encoding="utf-8"))["docs"])
print("档数:", len(docs))

cultures, roles, kingdoms, profiles = set(), set(), set(), set()
for d in docs:
    print("\n### %s  subdomain=%s" % (d["slug"], d["subdomain"]))
    for i, a in enumerate(d["assertions"]):
        for e in a["expressions"]:
            aud = []
            for g in e["grants"]:
                aud.append("%s/%s/%s%s%s" % (
                    g["profile"], g["detail"], g.get("culture") or "-",
                    ("/r:" + g["role"]) if g.get("role") else "",
                    ("/k:" + g["kingdom"]) if g.get("kingdom") else ""))
                if g.get("culture"):
                    cultures.add(g["culture"])
                if g.get("role"):
                    roles.add(g["role"])
                if g.get("kingdom"):
                    kingdoms.add(g["kingdom"])
                profiles.add(g["profile"])
            print("   a%d[%s] %s | %s | %s" % (i, a["kind"], e["id"], e["layer"], "; ".join(aud)))

print("\n\n== 汇总 ==")
print("cultures:", sorted(cultures))
print("roles   :", sorted(roles))
print("kingdoms:", sorted(kingdoms))
print("profiles:", sorted(profiles))
