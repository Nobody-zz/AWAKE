# -*- coding: utf-8 -*-
"""把 448 档的 subdomain / domain / id 字段与档名，按 2026-09-14 分类统一方案改写。
用法：python _apply_reclass_20260914.py            # dry-run
      python _apply_reclass_20260914.py --apply    # 落盘
两个目录同改：WS authoring、docs/projection/authoring-out。
按字节读写以保留原行尾。"""
import io
import json
import os
import re
import sys

ROOT = r"D:\AWAKE-Dev\AWAKE"
DIRS = [
    os.path.join(ROOT, "tools", "worldbook-studio", "workspace", "full-geo1", "authoring"),
    os.path.join(ROOT, "docs", "worldbook-migration", "projection", "authoring-out"),
]
TAX = os.path.join(ROOT, "docs", "worldbook-studio-plan", "knowledge-taxonomy.v1.json")

APPLY = "--apply" in sys.argv

# (旧前缀, 旧子域或 None=任意) -> (新前缀, 新子域, 新域或 None)
RULES = {
    ("village", None): ("village", "village", None),
    ("castle", None): ("castle", "castle", None),
    ("town", None): ("town", "town", None),
    ("lake", None): ("waters", "waters", None),
    ("river", None): ("waters", "waters", None),
    ("bay", None): ("waters", "waters", None),
    ("sea", None): ("waters", "waters", None),
    ("mount", None): ("mountain", "mountain", None),
    ("mountains", None): ("mountain", "mountain", None),
    ("plateau", None): ("plateau", "plateau", None),
    ("peninsula", None): ("peninsula", "peninsula", None),
    ("desert", None): ("desert", "desert", None),
    ("mine", None): ("resources", "resources", "geography"),
    ("furs", None): ("goods", "goods", None),
    ("item", "items"): ("items", "items", None),
    ("item", "goods"): ("goods", "goods", None),
    ("tale", None): ("tale", "tale", None),
    ("territory", None): ("territories", "territories", None),
    ("throne", None): ("throne", "throne", None),
    ("clan", None): ("clans", "clans", None),
    ("military", None): ("military", "military", None),
    ("troop", None): ("troops", "troops", None),
    ("weapon", None): ("weapons", "weapons", None),
}

RE_ID = re.compile(r"(?m)^id: [^\r\n]*")
RE_DOM = re.compile(r"(?m)^domain: [^\r\n]*")
RE_SUB = re.compile(r"(?m)^subdomain: [^\r\n]*")

tax = json.loads(io.open(TAX, encoding="utf-8").read())
VALID = {s["id"] for d in tax["domains"] for s in d["subdomains"]}
DOMAINS = {d["id"] for d in tax["domains"]}


def plan(d):
    files = sorted(f for f in os.listdir(d) if f.endswith(".yaml"))
    rows = []
    for f in files:
        txt = io.open(os.path.join(d, f), "rb").read().decode("utf-8")
        m_id = RE_ID.search(txt)
        m_dom = RE_DOM.search(txt)
        m_sub = RE_SUB.search(txt)
        if not (m_id and m_dom and m_sub):
            rows.append({"file": f, "skip": "缺字段"})
            continue
        oid = m_id.group(0)[4:].strip()
        odom = m_dom.group(0)[8:].strip()
        osub = m_sub.group(0)[11:].strip()
        stem = f[:-5]
        opre = stem.split("-", 1)[0]
        rest = stem.split("-", 1)[1] if "-" in stem else ""
        key = (opre, osub) if (opre, osub) in RULES else (opre, None)
        if key not in RULES:
            rows.append({"file": f, "skip": "无规则 %s/%s" % (opre, osub)})
            continue
        npre, nsub, ndom = RULES[key]
        ndom = ndom or odom
        nstem = npre + "-" + rest if rest else npre
        nfile = nstem + ".yaml"
        nid = "doc.%s.%s" % (ndom, nstem)
        nt = RE_ID.sub("id: " + nid, txt)
        nt = RE_DOM.sub("domain: " + ndom, nt)
        nt = RE_SUB.sub("subdomain: " + nsub, nt)
        rows.append({"file": f, "nfile": nfile, "oid": oid, "nid": nid,
                     "odom": odom, "ndom": ndom, "osub": osub, "nsub": nsub, "text": nt})
    return rows


for d in DIRS:
    print("=" * 72)
    print("目录:", d)
    rows = plan(d)
    skipped = [r for r in rows if "skip" in r]
    live = [r for r in rows if "skip" not in r]
    # 撞车检查
    from collections import defaultdict
    tgt = defaultdict(list)
    for r in live:
        tgt[r["nfile"]].append(r["file"])
    clash = {k: v for k, v in tgt.items() if len(v) > 1}
    renames = [r for r in live if r["file"] != r["nfile"]]
    print("  总档 %d ｜ 改名 %d ｜ 仅改字段 %d ｜ 跳过 %d"
          % (len(rows), len(renames), len(live) - len(renames), len(skipped)))
    if skipped:
        for r in skipped[:10]:
            print("    SKIP", r["file"], r["skip"])
    if clash:
        print("  !! 撞车 %d 组:" % len(clash))
        for k, v in clash.items():
            print("    ", k, "<-", v)
    else:
        print("  撞车检查: 无")
    badsub = [r for r in live if r["nsub"] not in VALID]
    baddom = [r for r in live if r["ndom"] not in DOMAINS]
    print("  新子域全部合法:", not badsub, "｜新域全部合法:", not baddom)
    print("  改名样例:")
    for r in renames[:10]:
        print("    %-40s -> %s" % (r["file"], r["nfile"]))
    if APPLY:
        for r in live:
            src = os.path.join(d, r["file"])
            dst = os.path.join(d, r["nfile"])
            io.open(dst, "wb").write(r["text"].encode("utf-8"))
            if r["nfile"] != r["file"]:
                os.remove(src)
        print("  已落盘")
