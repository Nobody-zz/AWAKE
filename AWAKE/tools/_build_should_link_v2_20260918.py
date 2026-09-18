# -*- coding: utf-8 -*-
"""v2：在 v1 判据**一字不改**的前提下，把「分桶」与「方向」固化成边表的一等字段。

v1 的问题：`bucket`（专名/高频专名/类别星形）与 `mutual`（互提/单向）都是**读的时候临场算**的
⇒ 下游拿到的只是一堆 from→to，不知道哪条能反向用、哪条是星形噪声。
v2 把它们写进边表，并给出 `usableAs`（这条边允许往哪个方向用）。

新增字段（其余与 v1 完全一致）：
  df         该 viaName 的文档频率（可核）
  bucket     proper    | 专名边（DF <= 12）
             hubproper | 高频专名边（DF > 12 但它是真地名/物产的标题主词）—— 应进图
             star      | 类别星形边（DF > 12 且它的**所有**归属条目都是概念词条）
  mutual     同一对存在反向边 ⇒ True（互提）
  direction  both（互提）| out（只有这一个方向）
  usableAs   允许的用法：["forward"] 或 ["forward","backward"]
             ★ 单向边的入边**禁止反向使用**（见规范 §五），所以这里不给 "backward"

判据与噪声控制与 v1 逐字相同，只增不清：只读、可复核。
"""
import json, io, re, os, collections, datetime

PKG = r"D:\AWAKE-Dev\AWAKE\ModuleData\Worldbook\packages\calradia\runtime.json"
OUTDIR = r"D:\AWAKE-Dev\AWAKE\docs\mappings\worldbook-should-link\20260918"
OUTJSON = os.path.join(OUTDIR, "should-link.v2.json")
REPORT = r"D:\AWAKE-Dev\AWAKE\tools\_should_link_report_v2_20260918.txt"

DF_MAX = 12
NAME_MIN, NAME_MAX = 2, 10
CJK = re.compile(r"[\u4e00-\u9fff]")
GAMECODE = re.compile(r"^[A-Za-z0-9_\-\.]+$")
CONCEPT_MARK = "settlement-types-"     # 概念词条的 id 特征（判 star 用，不硬编码名单）


def walk(o):
    if isinstance(o, dict):
        if isinstance(o.get("id"), str) and o["id"].startswith("awake:entry:"):
            yield o
        for v in o.values():
            yield from walk(v)
    elif isinstance(o, list):
        for v in o:
            yield from walk(v)


ents = list(walk(json.load(io.open(PKG, encoding="utf-8"))))


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


bodies = {e["id"]: body(e) for e in ents}
df = collections.Counter()
name_owner = {}
for e in ents:
    for n in names_of(e):
        name_owner.setdefault(n, set()).add(e["id"])
for n in list(name_owner):
    df[n] = sum(1 for txt in bodies.values() if n in txt)

title_words = set()
for e in ents:
    t = zh(e.get("title")).strip()
    if t:
        w = t.split("·")[0].strip()
        if NAME_MIN <= len(w) <= NAME_MAX and CJK.search(w):
            title_words.add(w)

kept = sorted(set(n for n in name_owner if df[n] <= DF_MAX) | set(n for n in name_owner if df[n] > DF_MAX and n in title_words))
dropped_only = sorted([n for n in name_owner if df[n] > DF_MAX and n not in title_words], key=lambda x: -df[x])
hubs = [n for n in name_owner if df[n] > DF_MAX and n in title_words]


def bucket_of(n):
    if df[n] <= DF_MAX:
        return "proper"
    if all(CONCEPT_MARK in o for o in name_owner[n]):
        return "star"                     # 目标全是概念词条 ⇒ 星形，信息量低
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
                "fromSubdomain": (e.get("extensions") or {}).get("subdomain"),
                "toSubdomain": None,
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

byid = {e["id"]: e for e in ents}
for ed in edges:
    ed["toSubdomain"] = (byid[ed["to"]].get("extensions") or {}).get("subdomain")

# ---- 方向：按**有向条目对**判互提（不看 viaName —— 两边用不同名字互提也算）----
directed = set((e["from"], e["to"]) for e in edges)
for ed in edges:
    m = (ed["to"], ed["from"]) in directed
    ed["mutual"] = m
    ed["direction"] = "both" if m else "out"
    ed["usableAs"] = ["forward", "backward"] if m else ["forward"]

pairs = set(frozenset((e["from"], e["to"])) for e in edges)
froms = set(e["from"] for e in edges)
tos = set(e["to"] for e in edges)

cnt = collections.Counter(e["bucket"] for e in edges)
mut_by_bucket = collections.Counter(e["bucket"] for e in edges if e["mutual"])

os.makedirs(OUTDIR, exist_ok=True)
doc = {
    "schema_version": "awake.worldbook.should-link.v2",
    "supersedes": "awake.worldbook.should-link.v1（同目录 should-link.v1.json；判据相同，v2 增方向与分桶字段）",
    "generated_at_utc": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    "method": ("判据＝A 条正文（summary＋expressions）里点名了 B 条的名字。名字取标题主词＋中文别名，"
               "按文档频率丢泛词（DF > %d），但**是某条自身标题主词的名字保留**。只读、可复核。" % DF_MAX),
    "params": {"dfMax": DF_MAX, "nameMin": NAME_MIN, "nameMax": NAME_MAX},
    "usage": {
        "forward": "A 提 B ⇒ 问 A 时 B 可算相关（所有边都允许）",
        "backward": "B 提 A ⇒ 问 B 时 A 可算相关（**只有 mutual=true 才允许**）",
        "note": "单向边的入边禁止反向使用；类别星形边（bucket=star）不构成网状关系，建议不进加权图。",
    },
    "counts": {
        "entries": len(ents),
        "namesTotal": len(name_owner), "namesKept": len(kept),
        "namesDroppedAsGeneric": len(dropped_only), "hubsKept": len(hubs),
        "edges": len(edges), "distinctPairs": len(pairs),
        "edgesStrong": len([e for e in edges if e["strength"] == "strong"]),
        "edgesWeak": len([e for e in edges if e["strength"] == "weak"]),
        "entriesWithOutgoing": len(froms), "entriesWithIncoming": len(tos),
        "byBucket": {"proper": cnt["proper"], "hubproper": cnt["hubproper"], "star": cnt["star"]},
        "mutualEdges": len([e for e in edges if e["mutual"]]),
        "mutualByBucket": {"proper": mut_by_bucket["proper"], "hubproper": mut_by_bucket["hubproper"],
                           "star": mut_by_bucket["star"]},
        "mutualPairs": len(set(frozenset((e["from"], e["to"])) for e in edges if e["mutual"])),
    },
    "edges": edges,
}
io.open(OUTJSON, "w", encoding="utf-8", newline="\n").write(json.dumps(doc, ensure_ascii=False, indent=1))

w = []


def p(s=""):
    w.append(s)
    print(s)


p("=== v2：分桶与方向已固化进边表 ===")
p("条目 %d；名字池 %d（留 %d；丢纯泛词 %d；保留枢纽名 %d）" % (len(ents), len(name_owner), len(kept), len(dropped_only), len(hubs)))
p("")
p("== 按桶 ==")
for b, label, usable in (("proper", "专名边（DF<=%d）" % DF_MAX, "应进引用图"),
                         ("hubproper", "高频专名边（DF>%d 的真地名）" % DF_MAX, "应进引用图"),
                         ("star", "类别星形边（→概念词条）", "单列，不进加权图")):
    p("  %-30s 边 %4d   其中互提 %3d   %s" % (label, cnt[b], mut_by_bucket[b], usable))
p("  应进引用图合计 %d 条 / 互提 %d 条" % (cnt["proper"] + cnt["hubproper"], mut_by_bucket["proper"] + mut_by_bucket["hubproper"]))
p("")
p("== 方向 ==")
p("  互提（mutual）边     %d   （占应进图 %.0f%%）" % (doc["counts"]["mutualEdges"],
                                                   100.0 * doc["counts"]["mutualEdges"] / max(1, cnt["proper"] + cnt["hubproper"])))
p("  单向（out）边        %d" % (len(edges) - doc["counts"]["mutualEdges"]))
p("  互提的无向对         %d" % doc["counts"]["mutualPairs"])
p("")
p("== 互提对长在哪些类别组合（只算应进图的边）==")
g = collections.Counter()
for e in edges:
    if e["mutual"] and e["bucket"] != "star":
        ka, kb = sorted(["-".join(e["from"].split(":")[-1].split("-")[:1]),
                         "-".join(e["to"].split(":")[-1].split("-")[:1])])
        g[(ka, kb)] += 1
for k, c in g.most_common(12):
    p("  %-24s ↔ %-24s %3d" % (k[0], k[1], c))
p("")
p("== 反向不可用的中心节点（入边多、出边少 ⇒ 其入边一律只能 forward）==")
outd = collections.Counter(e["from"] for e in edges if e["bucket"] != "star")
ind = collections.Counter(e["to"] for e in edges if e["bucket"] != "star")
byid_t = {e["id"]: zh(e.get("title")) for e in ents}
for i, c in ind.most_common(6):
    hasback = [e for e in edges if e["to"] == i and e["from"] in [x["to"] for x in edges if x["from"] == i]]
    p("  %-40s 入 %3d  出 %2d  互提 %d  《%s》" % (i.split(":")[-1], c, outd.get(i, 0), len(hasback), byid_t[i]))
p("")
p("JSON: %s" % OUTJSON)
io.open(REPORT, "w", encoding="utf-8", newline="\n").write("\n".join(w))
print("报告:", REPORT)
