# -*- coding: utf-8 -*-
"""① 在 Python 里**逐字复刻**运行时的索引卫生三规则（WorldbookKeywordIndex.IsExcluded），
  ② 对三个泛词穷举"关键词腿能捞到谁"，
  ③ 拿它跟真探针报的 literal_keyword_hits 对表 —— 对上了，说明这份复刻跟真件同源。

规则出处：src/WorldKnowledgeLoader.cs:217-266
    MaxKeywordDocumentFrequency = 40
    IsExcluded: 覆盖>40 / 起头 doc. / 纯 ASCII 且含下划线
"""
import io
import json
import sys
import collections

sys.stdout.reconfigure(encoding="utf-8")

PKG = "ModuleData/Worldbook/packages/calradia/runtime.json"
SIDE = "tools/_entry_fit_side_20260917.json"
OUT = "tools/_entry_fit_off_20260917.json"
PFX = "awake:entry:"
MAXDF = 40


def kw_of(e):
    k = e.get("keywords")
    return (k.get("zh-CN") or []) if isinstance(k, dict) else (k or [])


def looks_like_internal_identifier(s):
    has_us = False
    for ch in s:
        if ch == "_":
            has_us = True
            continue
        o = ord(ch)
        if o > 0x7E or o < 0x20:
            return False
    return has_us


pkg = json.load(io.open(PKG, encoding="utf-8"))
entries = pkg["entries"]
short = lambda i: i.replace(PFX, "")

# ---- 复刻建索引 ----
df = collections.Counter()
per = {}
for e in entries:
    kw = {w.strip() for w in kw_of(e) if w and w.strip()}
    per[short(e["id"])] = kw
    for w in kw:
        df[w] += 1

index = collections.defaultdict(list)  # keyword -> [entry]
for eid, kw in per.items():
    for w in kw:
        if df[w] > MAXDF:
            continue
        if w.lower().startswith("doc."):
            continue
        if looks_like_internal_identifier(w):
            continue
        index[w].append(eid)

print("复刻出的索引：%d 个关键词 / 覆盖 %d 条条目" % (len(index), len(entries)))
print("（真件自报：入库 1006 个词 —— 见 _verify_index_hygiene_20260917.py ④）")

# ---- 三个泛词穷举 ----
raw = json.load(io.open(OUT, encoding="utf-8"))
res = raw["queries"] if isinstance(raw, dict) and "queries" in raw else raw
probe = {}
side = json.load(io.open(SIDE, encoding="utf-8"))["side"]
for name, s in side.items():
    if s["kind"] == "kw_zh" and s["text"] in ("村庄", "城堡", "城镇"):
        r = next((x for x in res if x.get("name") == name), {})
        probe.setdefault(s["text"], []).append(r.get("literal_keyword_hits"))

print("\n" + "=" * 78)
print("三个泛词：复刻穷举 vs 真探针自报")
print("=" * 78)
for g in ["村庄", "城堡", "城镇"]:
    cands = set()
    matched_keys = []
    for key, owners in index.items():
        if g in key or key in g:
            matched_keys.append((key, owners))
            cands.update(owners)
    pv = probe.get(g, [])
    print("\n── 「%s」 ──" % g)
    print("   复刻：命中索引键 %d 个 %s" % (len(matched_keys),
                                        [(k, len(o)) for k, o in matched_keys]))
    print("   复刻：候选条目 %s" % (sorted(cands) or "（空）"))
    print("   真探针自报 literal_keyword_hits：%s" % (
        "全部=%d、唯一值=%s" % (len(pv), set(pv)) if pv else "无记录"))
    ok = (len(matched_keys) == (set(pv).pop() if pv and len(set(pv)) == 1 else -9))
    print("   ⇒ 对表：%s" % ("一致 ✔（复刻与真件同源）" if ok else "**不一致**（先怀疑我的复刻）"))

# ---- 反向：如果 R1 不生效，「村庄」会捞回几条（说明 R1 在挡什么）----
print("\n" + "=" * 78)
print("反事实：若**停掉 R1**，「村庄」会捞回多少条（说明 R1 确实在干活）")
print("=" * 78)
loose = collections.defaultdict(list)
for eid, kw in per.items():
    for w in kw:
        if w.lower().startswith("doc.") or looks_like_internal_identifier(w):
            continue
        loose[w].append(eid)
for g in ["村庄", "城堡", "城镇"]:
    c = set()
    for key, owners in loose.items():
        if g in key or key in g:
            c.update(owners)
    print("   「%s」：停 R1 后候选 %d 条 ／ 现状候选见上" % (g, len(c)))
