# -*- coding: utf-8 -*-
"""勘察：两处 authoring 目录的真实档数、前缀→子域映射、跨档引用。只读。"""
import os
import re
import io

WS = r"D:\AWAKE-Dev\AWAKE\tools\worldbook-studio\workspace\full-geo1\authoring"
AO = r"D:\AWAKE-Dev\AWAKE\docs\worldbook-migration\projection\authoring-out"

ID_RE = re.compile(r"^id:\s*(\S+)", re.M)
DOM_RE = re.compile(r"^domain:\s*(\S+)", re.M)
SUB_RE = re.compile(r"^subdomain:\s*(\S+)", re.M)
DOC_RE = re.compile(r"doc\.[a-z_]+\.", re.M)


def scan(d):
    files = sorted(f for f in os.listdir(d) if f.endswith(".yaml"))
    rows = []
    bad = []
    for f in files:
        p = os.path.join(d, f)
        txt = io.open(p, encoding="utf-8").read()
        m_id, m_dom, m_sub = ID_RE.search(txt), DOM_RE.search(txt), SUB_RE.search(txt)
        docrefs = [x for x in DOC_RE.findall(txt)]
        # 除 id 行以外的 doc. 引用
        other = []
        for line in txt.splitlines():
            if DOC_RE.search(line) and not line.startswith("id:"):
                other.append(line.strip())
        if m_id is None or m_dom is None or m_sub is None:
            bad.append((f, bool(m_id), bool(m_dom), bool(m_sub)))
            continue
        rows.append({
            "file": f,
            "id": m_id.group(1),
            "domain": m_dom.group(1),
            "subdomain": m_sub.group(1),
            "other_refs": other,
        })
    return rows, bad


for label, d in (("WS-authoring", WS), ("authoring-out", AO)):
    print("=" * 70)
    print(label, d)
    rows, bad = scan(d)
    print("  档数:", len(rows), " 缺字段:", len(bad))
    if bad:
        print("  缺字段明细:", bad[:10])
    # 前缀（doc id 第三段首个 '-' 前）
    pairs = {}
    for r in rows:
        seg = r["id"].split(".", 2)[2] if r["id"].count(".") >= 2 else "?"
        pre = seg.split("-", 1)[0]
        pairs.setdefault((pre, r["subdomain"]), []).append(r["file"])
    print("  前缀→子域 配对（%d 组）:" % len(pairs))
    for (pre, sub), fs in sorted(pairs.items(), key=lambda kv: -len(kv[1])):
        print("    %-14s -> %-20s %3d" % (pre, sub, len(fs)))
    refs = [r for r in rows if r["other_refs"]]
    print("  含跨档 doc. 引用的档数:", len(refs))
    for r in refs[:5]:
        print("    ", r["file"], r["other_refs"][:2])
