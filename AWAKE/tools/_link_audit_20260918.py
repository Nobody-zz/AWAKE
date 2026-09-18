# -*- coding: utf-8 -*-
"""复核「哪些词条应当联系起来」的全部读数 —— 口径分层，一个数一个口径，不混着说。只读。

口径定义（写进报告时原样带过去）：
  提及边全量   = should-link.v1.json 的全部边（含枢纽边）
  专名边       = viaName 的 DF <= DF_MAX 的边
  专名强边     = 专名边 且 viaName 是某条条目的标题主词（strength==strong）
  专名弱边     = 专名边 且 viaName 只是别名（strength==weak）
  枢纽边       = viaName 的 DF > DF_MAX（即 67 条正文提「城堡」这类），已标注建议不进引用图
  归属边       = settlement-hierarchy.v1.json(D 里的 村庄→父聚落 结构边)
"""
import json, io, collections

PKG = r"D:\AWAKE-Dev\AWAKE\ModuleData\Worldbook\packages\calradia\runtime.json"
SL = r"D:\AWAKE-Dev\AWAKE\docs\mappings\worldbook-should-link\20260918\should-link.v1.json"
HI = r"D:\AWAKE-Dev\AWAKE\docs\mappings\worldbook-settlement-hierarchy\20260918\settlement-hierarchy.v1.json"


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
ids = [e["id"] for e in ents]
titles = {}
for e in ents:
    t = (e.get("title") or {}).get("zh-CN") or ""
    titles[e["id"]] = t.split("·")[0].strip()

sl = json.load(io.open(SL, encoding="utf-8"))
hi = json.load(io.open(HI, encoding="utf-8"))
DF_MAX = sl["params"]["dfMax"]

# ---- 结构探查：归属边的字段形态 ----
print("=== 结构 ===")
print("  settlement-hierarchy keys: %s" % sorted(hi.keys()))
print("  第一条归属边: %s" % json.dumps(hi["edges"][0], ensure_ascii=False)[:400])
print("  归属边条数(容器): %d" % len(hi["edges"]))
print("  should-link keys: %s" % sorted(sl.keys()))
print("  should-link counts: %s" % json.dumps(sl["counts"], ensure_ascii=False))

# ---- 提及边分层 ----
edges = sl["edges"]
# viaName -> DF（从 params 推不出来，直接按 viaName 是否在 dropped 集合判断不可靠）
# 用 strength + 是否命中条目标题主词 交叉：枢纽边 = viaName 不是任何条目标题主词且 strength=weak? 否。
# 脚本里 strength == (viaName in title_words)，与 DF 无关；所以枢纽边只能靠 DF。
# 这里以「viaName 出现在 >DF_MAX 条条目正文里」重算 DF，保证与生成脚本同源。
def body(e):
    parts = [(e.get("summary") or {}).get("zh-CN") or ""]
    for ex in e.get("expressions") or []:
        parts.append((ex.get("text") or {}).get("zh-CN") or "")
    return "\n".join(p for p in parts if p)


bodies = {e["id"]: body(e) for e in ents}
dfs = {}
def df_of(n):
    if n not in dfs:
        dfs[n] = sum(1 for txt in bodies.values() if n in txt)
    return dfs[n]

genu = [e for e in edges if df_of(e["viaName"]) <= DF_MAX]
genu_strong = [e for e in genu if e["strength"] == "strong"]
genu_weak = [e for e in genu if e["strength"] == "weak"]
hub = [e for e in edges if df_of(e["viaName"]) > DF_MAX]

def pairs(es):
    return set(frozenset((e["from"], e["to"])) for e in es)

def touched(es):
    s = set()
    for e in es:
        s.add(e["from"]); s.add(e["to"])
    return s

def cls(eid):
    return "-".join(eid.split(":")[-1].split("-")[:1])

print()
print("=== 提及边分层 ===")
for label, es in (("提及边全量", edges), ("专名边(DF<=%d)" % DF_MAX, genu),
                  ("  专名强边", genu_strong), ("  专名弱边", genu_weak), ("枢纽边(DF>%d)" % DF_MAX, hub)):
    print("  %-22s 边 %5d   条目对 %5d" % (label, len(es), len(pairs(es))))

print()
print("=== 专名强边：按 出方类别→入方类别 全量分组 ===")
g = collections.Counter((cls(e["from"]), cls(e["to"])) for e in genu_strong)
tot = 0
for (a, b), c in g.most_common():
    print("  %-24s -> %-24s %3d" % (a, b, c))
    tot += c
print("  -- 组合数 %d，合计 %d（应等于专名强边数）" % (len(g), tot))

print()
print("=== 归属边 ===")
hf, ht = set(), set()
nlinks = 0
ptype = collections.Counter()
for e in hi["edges"]:
    f = e["from"]
    tos = e["to"] if isinstance(e["to"], list) else [e["to"]]
    hf.add(f); nlinks += len(tos)
    for t in tos:
        ht.add(t)
    ptype[e.get("parentType")] += len(tos)
print("  容器条数 %d，展开后父链接 %d" % (len(hi["edges"]), nlinks))
print("  涉及条目：出方 %d，入方 %d，合计 %d" % (len(hf), len(ht), len(hf | ht)))
print("  父类型分布：%s" % dict(ptype))

print()
print("=== 孤立条目（按口径）===")
mention_any = touched(edges)
mention_genu = touched(genu)
mention_genu_strong = touched(genu_strong)
struct_any = hf | ht
for label, s in (("宽口径：任何提及边", mention_any),
                 ("专名口径：专名边(含弱)", mention_genu),
                 ("严口径：仅专名强边", mention_genu_strong)):
    for lbl2, s2 in (("不加归属边", s), ("加归属边", s | struct_any)):
        iso = [i for i in ids if i not in s2]
        print("  %-22s %-12s 孤立 %3d" % (label, lbl2, len(iso)))

print()
print("=== 孤立清单（严口径：仅专名强边 + 归属边）===")
touched_strict = mention_genu_strong | struct_any
iso_strict = [i for i in ids if i not in touched_strict]
c = collections.Counter(cls(i) for i in iso_strict)
for k, v in c.most_common():
    print("  %-24s %d" % (k, v))
for i in iso_strict:
    print("    %s   《%s》" % (i.split(":")[-1], titles.get(i, "")))
