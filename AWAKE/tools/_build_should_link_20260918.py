# -*- coding: utf-8 -*-
"""判断「现有词条里哪些应该联系起来」—— 证据＝**A 条的正文里点了 B 条的名字**。

为什么用这条判据：
  · 它是**人写的引用**，不是机器猜的相似度；每一条边都能指回原文片段，可当场核。
  · 它对"器物/兵种/山水"这类**拿不到游戏锚点**的条目同样有效（锚点那条路对它们堵死）。

噪声控制（不做就会炸成一片）：
  · 名字只取「标题主词（去掉 · 后缀）」＋「keywords 里的中文别名」，**滤掉游戏码**（英文/下划线）。
  · **按文档频率丢泛词**：一个名字若出现在 > DF_MAX 条条目的正文里，它没有指向性（如"帝国""库赛特"），丢掉。
  · 排除自指；同一条对里名字多次出现只记一条，但保留全部证据句。

输出：机器可读 JSON ＋ 可读报告。只读，不写任何既有产物。
"""
import json, io, re, os, collections, datetime

PKG = r"D:\AWAKE-Dev\AWAKE\ModuleData\Worldbook\packages\calradia\runtime.json"
OUTDIR = r"D:\AWAKE-Dev\AWAKE\docs\mappings\worldbook-should-link\20260918"
OUTJSON = os.path.join(OUTDIR, "should-link.v1.json")
REPORT = r"D:\AWAKE-Dev\AWAKE\tools\_should_link_report_20260918.txt"

DF_MAX = 12          # 名字出现在超过这么多条正文里 ⇒ 视为泛词，丢弃
NAME_MIN, NAME_MAX = 2, 10
CJK = re.compile(r"[\u4e00-\u9fff]")
GAMECODE = re.compile(r"^[A-Za-z0-9_\-\.]+$")

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
        out.add(t.split("·")[0].strip())     # 「黎明山脉·禁忌与传说」→ 黎明山脉
    for k in e.get("keywords") or []:
        k = (k or "").strip()
        if not k or GAMECODE.match(k):
            continue
        if not CJK.search(k):
            continue
        out.add(k.split("·")[0].strip())
    return {n for n in out if NAME_MIN <= len(n) <= NAME_MAX and CJK.search(n)}

# ---- 文档频率：一个名字出现在多少条条目的正文里 ----
bodies = {e["id"]: body(e) for e in ents}
df = collections.Counter()
name_owner = {}          # name -> 拥有它的条目 id 集合
for e in ents:
    for n in names_of(e):
        name_owner.setdefault(n, set()).add(e["id"])
allnames = list(name_owner)
for n in allnames:
    c = 0
    for eid, txt in bodies.items():
        if n in txt:
            c += 1
    df[n] = c

kept = [n for n in allnames if df[n] <= DF_MAX]
dropped = sorted([n for n in allnames if df[n] > DF_MAX], key=lambda x: -df[x])

# 泛词与「枢纽」要分开：名字若**是某条条目的标题主词**，它就是 hub，多条提到它正是应连的证据，不丢。
title_words = set()
for e in ents:
    t = zh(e.get("title")).strip()
    if t:
        w = t.split("·")[0].strip()
        if NAME_MIN <= len(w) <= NAME_MAX and CJK.search(w):
            title_words.add(w)
hubs = [n for n in dropped if n in title_words]
kept = sorted(set(kept) | set(hubs))
dropped_only = [n for n in dropped if n not in title_words]

# ---- 提及边 ----
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
            # 证据句
            ev = []
            for seg in re.split(r"[。；;\n]", txt):
                if n in seg:
                    ev.append(seg.strip())
                if len(ev) >= 2:
                    break
            edges.append({
                "from": e["id"], "to": owner, "viaName": n,
                "strength": "strong" if n in title_words else "weak",
                "fromSubdomain": (e.get("extensions") or {}).get("subdomain"),
                "toSubdomain": None,
                "evidence": ev,
            })

# 去重（同一条对、同一名字只留一条，合并证据）
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

pairs = set(frozenset((e["from"], e["to"])) for e in edges)
froms = set(e["from"] for e in edges)
tos = set(e["to"] for e in edges)

os.makedirs(OUTDIR, exist_ok=True)
doc = {
    "schema_version": "awake.worldbook.should-link.v1",
    "generated_at_utc": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    "method": ("判据＝A 条正文（summary＋expressions）里点名了 B 条的名字。名字取标题主词＋中文别名，"
               "按文档频率丢泛词（DF > %d）。只读、可复核。" % DF_MAX),
    "params": {"dfMax": DF_MAX, "nameMin": NAME_MIN, "nameMax": NAME_MAX},
    "counts": {
        "entries": len(ents),
        "namesTotal": len(allnames), "namesKept": len(kept),
        "namesDroppedAsGeneric": len(dropped_only), "hubsKept": len(hubs),
        "edges": len(edges), "distinctPairs": len(pairs),
        "edgesStrong": len([e for e in edges if e["strength"] == "strong"]),
        "edgesWeak": len([e for e in edges if e["strength"] == "weak"]),
        "pairsStrong": len(set(frozenset((e["from"], e["to"])) for e in edges if e["strength"] == "strong")),
        "entriesWithOutgoing": len(froms), "entriesWithIncoming": len(tos),
    },
    "edges": edges,
}
io.open(OUTJSON, "w", encoding="utf-8", newline="\n").write(json.dumps(doc, ensure_ascii=False, indent=1))

w = []
def p(s=""):
    w.append(s); print(s)

p("=== 判据：正文点名的 ↔ 被点名的 ===")
p("条目 %d 条；名字池 %d 个（留 %d；按 DF>%d 丢掉纯泛词 %d 个，但**是条目标题主词的枢纽名保留 %d 个**）"
  % (len(ents), len(allnames), len(kept), DF_MAX, len(dropped_only), len(hubs)))
p("")
p("== 结果 ==")
p("  提及边（去重后）      %d   （强边 %d ／ 弱边 %d）"
  % (len(edges), len([e for e in edges if e["strength"] == "strong"]),
     len([e for e in edges if e["strength"] == "weak"])))
p("  不同的条目对          %d   （强边贡献 %d）"
  % (len(pairs), len(set(frozenset((e["from"], e["to"])) for e in edges if e["strength"] == "strong"))))
p("  有出边的条目          %d" % len(froms))
p("  有入边的条目          %d" % len(tos))
p("")
p("== 保留的枢纽名（是条目标题主词、DF 又超过阈值的，前 20）==")
for n in sorted(hubs, key=lambda x: -df[x])[:20]:
    p("  %-16s 出现在 %3d 条正文里" % (n, df[n]))
p("")
p("== 丢掉的纯泛词（不是任何条目标题，前 20）==")
for n in dropped_only[:20]:
    p("  %-14s 出现在 %3d 条正文里" % (n, df[n]))
p("")
p("== 用作边的名字里，复用最多的（前 20）==")
uniq = collections.Counter(e["viaName"] for e in edges)
pairsbyname = collections.Counter()
for e in edges:
    pairsbyname[e["viaName"]] += 1
for n, c in pairsbyname.most_common(20):
    p("  %-16s %2d 条边   (DF=%d)" % (n, c, df[n]))
p("")
p("== 出边最多的条目（前 12）==")
outc = collections.Counter(e["from"] for e in edges)
for eid, c in outc.most_common(12):
    p("  %-48s %d 条出边" % (eid.split(":")[-1], c))
p("")
p("== 例子（前 20 条边，带原文）==")
for e in edges[:20]:
    p("  %s" % e["from"].split(":")[-1])
    p("     → %s   （因为正文提到「%s」）" % (e["to"].split(":")[-1], e["viaName"]))
    for s in e["evidence"][:1]:
        p("       原文：%s" % s[:90])
p("")
p("== 按「出方类别 → 入方类别」分组（只算专名边，DF<=%d）==" % DF_MAX)
g = collections.Counter((e["from"].split(":")[-1].split("-")[0], e["to"].split(":")[-1].split("-")[0])
                        for e in edges if df[e["viaName"]] <= DF_MAX)
for (a, b), c in g.most_common(16):
    p("  %-24s → %-24s %d" % (a, b, c))
p("")
p("== 枢纽／上位边（viaName 的 DF>%d，即 67 条正文提「城堡」这类）—— **单列，建议不进引用图** ==" % DF_MAX)
hubedges = [e for e in edges if df[e["viaName"]] > DF_MAX]
hg = collections.Counter(e["viaName"] for e in hubedges)
p("  合计 %d 条" % len(hubedges))
for n, c in hg.most_common(14):
    tg = sorted(set(x["to"].split(":")[-1] for x in hubedges if x["viaName"] == n))
    p("  %-16s %3d 条边  → 指向 %s" % (n, c, ", ".join(tg)[:70]))
p("")
p("== 每个类别组合给一个例子（专名边）==")
seen_kind = set()
for e in edges:
    if df[e["viaName"]] > DF_MAX:
        continue
    kind = (e["from"].split(":")[-1].split("-")[0], e["to"].split(":")[-1].split("-")[0])
    if kind in seen_kind:
        continue
    seen_kind.add(kind)
    p("  [%s → %s] %s" % (kind[0], kind[1], e["from"].split(":")[-1]))
    p("      → %s（因为提到「%s」）" % (e["to"].split(":")[-1], e["viaName"]))
    if e["evidence"]:
        p("        原文：%s" % e["evidence"][0][:80])
    if len(seen_kind) >= 12:
        break

io.open(REPORT, "w", encoding="utf-8", newline="\n").write("\n".join(w))
print("\nJSON:", OUTJSON)
print("报告:", REPORT)
