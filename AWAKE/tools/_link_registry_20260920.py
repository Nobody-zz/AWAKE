# -*- coding: utf-8 -*-
"""互引边表 v3（09-20）：把 v2 判据**一字不改**重跑到 558 档语料，并产出**编译输入件**。

为什么要重跑：v2（09-18）是对当时 482 档的现役包算的。此后加了军事 25 ＋ 经济 40 ＋ 暗面 11
＝ **76 档新内容，一条边都没有**。边表要接进召回，第一件事就是让它覆盖全量。

与 v2 的两处新增（判据不动，只加出口）：
  ① 除审计件 `should-link.v3.json`（带 evidence，给人看）外，另出一份**编译输入件**
     `docs/worldbook-studio-plan/link-registry.v1.json`（只带机器字段，给 Studio 编译器读）；
  ② ★ **不含 `bucket=star` 的边** —— 星形边是「类别词 → 概念词条」的辐条，不构成网状关系，
     进召回就是过匹配。它照旧留在审计件里，只是不进 registry。

用法（仓库根为 CWD）：
  python -u tools/_link_registry_20260920.py
"""
import collections
import datetime
import io
import json
import os
import re
import sys

REPO = r"D:\AWAKE-Dev\AWAKE"
# ★ 判据跑在**编译产物**上，而不是 authoring yaml 上：这样「名字池」＝运行时真正看到的
#   K1 关键词（标题＋别名＋实体锚点可读名），不会因为我自己在 Python 里重写一遍 K1 而变成平行实现。
PKG = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1\compiled\geo1-v21-dark\runtime.json")
LIVE = os.path.join(REPO, r"ModuleData\Worldbook\packages\calradia\runtime.json")
AUDIT_DIR = os.path.join(REPO, r"docs\mappings\worldbook-should-link\20260920")
AUDIT = os.path.join(AUDIT_DIR, "should-link.v3.json")
REGISTRY = os.path.join(REPO, r"docs\worldbook-studio-plan\link-registry.v1.json")
REPORT = os.path.join(REPO, r"tools\_link_registry_report_20260920.txt")

DF_MAX = 12
NAME_MIN, NAME_MAX = 2, 10
CJK = re.compile(r"[\u4e00-\u9fff]")
GAMECODE = re.compile(r"^[A-Za-z0-9_\-\.]+$")
CONCEPT_MARK = "settlement-types-"


def walk(o):
    if isinstance(o, dict):
        if isinstance(o.get("id"), str) and o["id"].startswith("awake:entry:"):
            yield o
        for v in o.values():
            yield from walk(v)
    elif isinstance(o, list):
        for v in o:
            yield from walk(v)


def zh(o):
    return (o or {}).get("zh-CN") or "" if isinstance(o, dict) else ""


def body(e):
    parts = [zh(e.get("summary"))]
    for ex in e.get("expressions") or []:
        parts.append(zh(ex.get("text")))
    return "\n".join(p for p in parts if p)


def names_of(e):
    out = set()
    t = zh(e.get("title")).strip()
    if t:
        out.add(t.split("·")[0].strip())
    for k in e.get("keywords") or []:
        k = (k or "").strip()
        if not k or GAMECODE.match(k) or not CJK.search(k):
            continue
        out.add(k.split("·")[0].strip())
    return {n for n in out if NAME_MIN <= len(n) <= NAME_MAX and CJK.search(n)}


ents = list(walk(json.load(io.open(PKG, encoding="utf-8"))))
live_ents = list(walk(json.load(io.open(LIVE, encoding="utf-8"))))
live_ids = {e["id"] for e in live_ents}
pkg_ids = {e["id"] for e in ents}
print("语料：编译包 %d 档（现役 %d 档，本代新增 %d）" % (len(ents), len(live_ids), len(pkg_ids - live_ids)), flush=True)

bodies = {e["id"]: body(e) for e in ents}
name_owner = {}
for e in ents:
    for n in names_of(e):
        name_owner.setdefault(n, set()).add(e["id"])
df = {n: sum(1 for txt in bodies.values() if n in txt) for n in name_owner}

title_words = set()
for e in ents:
    t = zh(e.get("title")).strip()
    if t:
        w = t.split("·")[0].strip()
        if NAME_MIN <= len(w) <= NAME_MAX and CJK.search(w):
            title_words.add(w)

kept = sorted(set(n for n in name_owner if df[n] <= DF_MAX)
              | set(n for n in name_owner if df[n] > DF_MAX and n in title_words))
dropped_only = sorted([n for n in name_owner if df[n] > DF_MAX and n not in title_words], key=lambda x: -df[x])
hubs = [n for n in name_owner if df[n] > DF_MAX and n in title_words]


def bucket_of(n):
    if df[n] <= DF_MAX:
        return "proper"
    if all(CONCEPT_MARK in o for o in name_owner[n]):
        return "star"
    return "hubproper"


edges = []
for e in ents:
    txt = bodies[e["id"]]
    if not txt:
        continue
    for n in kept:
        if n not in txt:
            continue
        for owner in name_owner[n]:
            if owner == e["id"]:
                continue
            ev = []
            for seg in re.split(r"[。；;\n]", txt):
                if n in seg:
                    ev.append(seg.strip())
                if len(ev) >= 2:
                    break
            edges.append({
                "from": e["id"], "to": owner, "viaName": n,
                "strength": "strong" if n in title_words else "weak",
                "df": df[n], "bucket": bucket_of(n),
                "evidence": ev,
            })

merged = {}
for ed in edges:
    k = (ed["from"], ed["to"], ed["viaName"])
    if k not in merged:
        merged[k] = ed
    else:
        merged[k]["evidence"] = list(dict.fromkeys(merged[k]["evidence"] + ed["evidence"]))[:3]
edges = list(merged.values())

directed = set((e["from"], e["to"]) for e in edges)
for ed in edges:
    m = (ed["to"], ed["from"]) in directed
    ed["mutual"] = m
    ed["direction"] = "both" if m else "out"
    ed["usableAs"] = ["forward", "backward"] if m else ["forward"]

# ---- 自检（硬断言）----
bad = []
for ed in edges:
    if ed["from"] not in pkg_ids or ed["to"] not in pkg_ids:
        bad.append(("端点不在包内", ed["from"], ed["to"]))
    if ed["from"] == ed["to"]:
        bad.append(("自环", ed["from"]))
if bad:
    print("!! 自检未过：%d 条" % len(bad))
    for x in bad[:10]:
        print("   ", x)
    sys.exit(1)
assert len(set((e["from"], e["to"], e["viaName"]) for e in edges)) == len(edges), "同一 (from,to,viaName) 出现多次"

# ---- 出口①：审计件（全量，带 evidence）----
counts = collections.Counter(e["bucket"] for e in edges)
mut_by_bucket = collections.Counter(e["bucket"] for e in edges if e["mutual"])
audit_edges = []
for ed in edges:
    d = dict(ed)
    d["fromSubdomain"] = (dict((x["id"], x) for x in ents)[ed["from"]].get("extensions") or {}).get("subdomain")
    d["toSubdomain"] = (dict((x["id"], x) for x in ents)[ed["to"]].get("extensions") or {}).get("subdomain")
    audit_edges.append(d)
audit_edges.sort(key=lambda x: (x["from"], x["bucket"], x["to"], x["viaName"]))

os.makedirs(AUDIT_DIR, exist_ok=True)
io.open(AUDIT, "w", encoding="utf-8", newline="\n").write(json.dumps({
    "schema_version": "awake.worldbook.should-link.v3",
    "supersedes": "awake.worldbook.should-link.v2（同目录 20260918/should-link.v2.json；判据逐字相同，只换语料 482→558）",
    "generated_at_utc": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    "method": ("判据＝A 条正文（summary＋expressions）里点名了 B 条的名字。名字取标题主词＋中文别名/锚点名，"
               "按文档频率丢泛词（DF > %d），但**是某条自身标题主词的名字保留**。只读、可复核。" % DF_MAX),
    "params": {"dfMax": DF_MAX, "nameMin": NAME_MIN, "nameMax": NAME_MAX},
    "usage": {
        "forward": "A 提 B ⇒ 问 A 时 B 可算相关（所有边都允许）",
        "backward": "B 提 A ⇒ 问 B 时 A 可算相关（**只有 mutual=true 才允许**）",
        "note": "单向边的入边禁止反向使用；类别星形边（bucket=star）不进召回，只留审计。",
    },
    "counts": {
        "entries": len(ents), "liveEntries": len(live_ids), "newEntries": len(pkg_ids - live_ids),
        "namesTotal": len(name_owner), "namesKept": len(kept),
        "namesDroppedAsGeneric": len(dropped_only), "hubsKept": len(hubs),
        "edges": len(edges),
        "distinctPairs": len(set(frozenset((e["from"], e["to"])) for e in edges)),
        "edgesStrong": sum(1 for e in edges if e["strength"] == "strong"),
        "edgesWeak": sum(1 for e in edges if e["strength"] == "weak"),
        "entriesWithOutgoing": len(set(e["from"] for e in edges)),
        "entriesWithIncoming": len(set(e["to"] for e in edges)),
        "byBucket": {"proper": counts["proper"], "hubproper": counts["hubproper"], "star": counts["star"]},
        "mutualEdges": sum(1 for e in edges if e["mutual"]),
        "mutualByBucket": {"proper": mut_by_bucket["proper"], "hubproper": mut_by_bucket["hubproper"],
                           "star": mut_by_bucket["star"]},
    },
    "edges": audit_edges,
}, ensure_ascii=False, indent=1))

# ---- 出口②：编译输入件（只机器字段，剔 star）----
reg_edges = [{"from": e["from"], "to": e["to"], "viaName": e["viaName"],
              "strength": e["strength"], "bucket": e["bucket"], "usableAs": list(e["usableAs"])}
             for e in edges if e["bucket"] != "star"]
reg_edges.sort(key=lambda x: (x["from"], x["to"], x["viaName"]))
io.open(REGISTRY, "w", encoding="utf-8", newline="\n").write(json.dumps({
    "registry_version": "awake.worldbook.link-registry.v1",
    "generated_at_utc": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    "judge": "should-link.v3（判据见 docs/worldbook-migration/EDGE-TO-RECALL-FEASIBILITY-20260920.md）",
    "source_entries": len(ents),
    "note": "只收 bucket != star 的边（星形边是类别辐条，进召回即过匹配）。evidence 不含，留在审计件里。",
    "edges": reg_edges,
}, ensure_ascii=False, indent=1))

# ---- 报告 ----
w = []


def p(s=""):
    w.append(s)
    print(s)


p("=== 互引边表 v3（语料 558 档）===")
p("条目 %d（现役 %d ＋ 新增 %d）；名字池 %d（留 %d；丢纯泛词 %d；保留枢纽名 %d）"
  % (len(ents), len(live_ids), len(pkg_ids - live_ids), len(name_owner), len(kept), len(dropped_only), len(hubs)))
p("")
p("== 按桶 ==")
for b, label in (("proper", "专名边（DF<=%d）" % DF_MAX), ("hubproper", "高频专名边（DF>%d 的真地名）" % DF_MAX),
                 ("star", "类别星形边（→概念词条）")):
    p("  %-30s 边 %4d   其中互提 %3d" % (label, counts[b], mut_by_bucket[b]))
p("  进 registry 合计 %d 条（= proper ＋ hubproper）" % len(reg_edges))
p("")
newids = pkg_ids - live_ids
cover = set()
for e in edges:
    cover.add(e["from"])
    cover.add(e["to"])
p("== ★ 覆盖（甲方关心的那条）==")
p("  有边参与的档      %d / %d" % (len(cover), len(ents)))
p("  无边参与的档      %d" % (len(pkg_ids - cover)))
p("  ★ 本代新增 76 档中有边参与的 %d 档" % len(cover & newids))
p("  ★ 本代新增 76 档中仍无边的 %d 档" % len(newids - cover))
p("")
v2 = os.path.join(REPO, r"docs\mappings\worldbook-should-link\20260918\should-link.v2.json")
if os.path.exists(v2):
    old = json.load(io.open(v2, encoding="utf-8"))
    p("== 与 v2（482 档语料）对比 ==")
    p("  v2 边 %d → v3 边 %d（%+d）" % (old["counts"]["edges"], len(edges), len(edges) - old["counts"]["edges"]))
    oldcover = set()
    for e in old["edges"]:
        oldcover.add(e["from"])
        oldcover.add(e["to"])
    p("  v2 覆盖档 %d → v3 覆盖档 %d" % (len(oldcover), len(cover)))
    gained = cover - oldcover
    p("  ★ 新获得边的档 %d 档（其中本代新增 %d 档）" % (len(gained), len(gained & newids)))
p("")
p("审计件: %s" % AUDIT)
p("编译输入件: %s" % REGISTRY)
io.open(REPORT, "w", encoding="utf-8", newline="\n").write("\n".join(w))
print("报告:", REPORT)
