# -*- coding: utf-8 -*-
"""B 项**判据收窄**：上一版用 term 表的 2-gram（`尼亚`/`斯湖`）当"泛词"，误伤 11 个真专名。

本轮换一个更窄、更切题的定义来量：

  「复合词劫持」的真形态 = **「别人的名字 + 分隔符 + 别的字」** ——
  典型的 `德里亚特·村庄`：它借了泛类别词「村庄」当后缀，于是成为「村庄」在索引里的唯一主人。

三个备选，逐个量"剔几个 / 误伤几个 / 拦不拦得住 `德里亚特·村庄`"：

  方案①（段继承 / term 侧）：K 的真子串里存在**只由"段"产生**的 term，且其段频 > 40。
      与上一版唯一区别：**不查 2-gram**（2-gram 是给「共享字兜底」用的，不适合当"专名是否含泛词"的判据）。
  方案②（题面继承 / title 侧）：K 含分隔符 ⇒ 按分隔符切段 ⇒ 若某段 == 某条已入库条目的 title ⇒ 剔。
  方案③（关键词继承 / keyword 侧）：K 含分隔符 ⇒ 切段 ⇒ 若某段 == 某条**别的条目**的关键词 ⇒ 剔。

⚠️ 三方案都**模拟 A 已做**（把 村庄/城堡/城镇 从 388 条上摘掉、把 德里亚特·村庄 删掉）。
⚠️ 变异检验（必须做，全绿不算证据）：
     阳性 → `厄尔凡尼亚·村庄` / `德里亚特·村庄` 必须被判剔；
     阴性 → `拉文尼亚` / `塔奈西斯湖` / `拉科尼斯湖·水为何变红` / `帝国之湖` 必须被保留。
"""
import collections
import io
import json
import sys
import unicodedata

sys.stdout.reconfigure(encoding="utf-8")

PKG = "ModuleData/Worldbook/packages/calradia/runtime.json"
MAXDF = 40
DROP_EXACT = {"村庄", "城堡", "城镇"}
DROP_VALUES = {"德里亚特·村庄"}
GENERIC = {"村庄", "城堡", "城镇"}

pkg = json.load(io.open(PKG, encoding="utf-8"))
entries = pkg["entries"]


def kw_of(e):
    t = e.get("keywords")
    if isinstance(t, dict):
        out = []
        for v in t.values():
            out.extend(v or [])
        return out
    return t or []


def title_of(e):
    t = e.get("title")
    if isinstance(t, dict):
        return [v for v in t.values() if v]
    return [t] if t else []


def summ_of(e):
    s = e.get("summary")
    return (s.get("zh-CN") or "") if isinstance(s, dict) else (s or "")


def is_sep(ch):
    return unicodedata.category(ch)[0] in ("P", "Z", "S")


def split_segments(text):
    start, out = 0, []
    for i, ch in enumerate(text):
        if not is_sep(ch):
            continue
        if i > start:
            out.append(text[start:i])
        start = i + 1
    if start < len(text):
        out.append(text[start:])
    return out


def enumerate_terms(text):
    if not text or not text.strip():
        return
    for seg in split_segments(text):
        if len(seg) >= 2:
            yield seg
        if seg.isascii():
            continue
        for i in range(len(seg) - 1):
            yield seg[i:i + 2]


# ---------- 模拟 A ----------
sim = []
dropped = collections.Counter()
for e in entries:
    kw = []
    for w in kw_of(e):
        if w in DROP_EXACT or w in DROP_VALUES:
            dropped[w] += 1
            continue
        kw.append(w)
    sim.append((e, kw))
print("模拟 A：删掉的关键词槽位 %s" % dict(dropped))
print("条目数 %d" % len(sim))

# ---------- 两张频次表 ----------
kwdf = collections.Counter()
for e, kw in sim:
    for w in set(w.strip() for w in kw if w and w.strip()):
        kwdf[w] += 1

segdf = collections.Counter()      # 只统计"段"
g2df = collections.Counter()       # 只统计 2-gram
for e, kw in sim:
    segs, grams = set(), set()
    for text in title_of(e) + [summ_of(e)]:
        if not text or not text.strip():
            continue
        for seg in split_segments(text):
            if len(seg) >= 2:
                segs.add(seg)
            if seg.isascii():
                continue
            for i in range(len(seg) - 1):
                grams.add(seg[i:i + 2])
    for s in segs:
        segdf[s] += 1
    for g in grams:
        g2df[g] += 1

print("段频>40 的词 %d 个，Top12：%s"
      % (sum(1 for v in segdf.values() if v > 40),
         sorted(((v, k) for k, v in segdf.items() if v > 40), reverse=True)[:12]))
print("2-gram 频>40 的词 %d 个，Top12：%s"
      % (sum(1 for v in g2df.values() if v > 40),
         sorted(((v, k) for k, v in g2df.items() if v > 40), reverse=True)[:12]))

# 全库名字 / 关键词（用于方案②③）
all_titles = set()
for e, kw in sim:
    for t in title_of(e):
        all_titles.add(t.strip())
all_kw = set(kwdf)

# ---------- 旧规则 ----------
def looks_internal(s):
    has_us = False
    for ch in s:
        if ch == "_":
            has_us = True
            continue
        o = ord(ch)
        if o > 0x7E or o < 0x20:
            return False
    return has_us


def excluded_old(k):
    return kwdf[k] > MAXDF or k.lower().startswith("doc.") or looks_internal(k)


def parts_of(k):
    """K 里被"非字母数字"切开、长度>=2 的段（真子段，排除整条自身）。"""
    return [s for s in split_segments(k) if s != k and len(s) >= 2]


def rule_seg(k):        # 方案①：段继承（term 侧，只看段）
    if not parts_of(k):
        return None
    for s in parts_of(k):
        if segdf.get(s, 0) > MAXDF:
            return "seg:%s(%d)" % (s, segdf[s])
    return None


def rule_title(k):      # 方案②：题面继承
    for s in parts_of(k):
        if s in all_titles:
            return "title:%s" % s
    return None


def rule_kw(k):         # 方案③：关键词继承（别的条目也有这个关键词）
    for s in parts_of(k):
        if s in all_kw:
            return "kw:%s(%d)" % (s, kwdf[s])
    return None


def compose_parts(k):   # 为了看清形态：K 的所有段
    return split_segments(k)


RULES = [("①段继承(term段频>40)", rule_seg),
         ("②题面继承(段==某条title)", rule_title),
         ("③关键词继承(段==别的关键词)", rule_kw)]

base = {k for k in all_kw if excluded_old(k)}
print()
print("=" * 78)
print("基线：旧规则（覆盖>40 / doc. / 纯ASCII含下划线）剔 %d 个关键词；关键词全集 %d"
      % (len(base), len(all_kw)))

for name, fn in RULES:
    extra = []
    for k in sorted(all_kw):
        if k in base:
            continue
        reason = fn(k)
        if reason:
            extra.append((k, kwdf[k], reason))
    print()
    print("-" * 78)
    print("%s ⇒ 额外剔 %d 个" % (name, len(extra)))
    for k, c, r in extra[:40]:
        print("     「%s」(覆盖 %d)  ← %s" % (k, c, r))
    if len(extra) > 40:
        print("     … 其余 %d 个" % (len(extra) - 40))

# ---------- 变异检验 ----------
print()
print("=" * 78)
print("变异检验：三个方案各自判什么（阳性=必须剔 / 阴性=必须留）")
CASES = [("阳性", "德里亚特·村庄"), ("阳性", "厄尔凡尼亚·村庄"),
         ("阴性", "拉文尼亚"), ("阴性", "塔奈西斯湖"), ("阴性", "帝国之湖"),
         ("阴性", "瓦兰迪亚军事力量"), ("阴性", "拉科尼斯湖·水为何变红"),
         ("阴性", "村庄"), ("阴性", "城堡"), ("阴性", "城镇")]
print("%-6s %-24s %-10s %-12s %-12s %-12s" % ("期望", "词", "旧规则", "①段继承", "②题面继承", "③关键词继承"))
for tag, k in CASES:
    if k not in all_kw:
        print("%-6s %-24s (不在当前关键词全集里，按模拟A结果另测)" % (tag, k))
        continue
    print("%-6s %-24s %-10s %-12s %-12s %-12s"
          % (tag, k, excluded_old(k), rule_seg(k) or "—", rule_title(k) or "—", rule_kw(k) or "—"))

# ---------- 抽样误伤 ----------
print()
print("=" * 78)
print("抽样：随机 20 个覆盖=1、长度>=3 的专名关键词，看各方案误伤几个")
import random
random.seed(11)
pool = [k for k in all_kw if kwdf[k] == 1 and len(k) >= 3 and not looks_internal(k)]
hits = collections.Counter()
for k in random.sample(pool, 20):
    row = []
    for name, fn in RULES:
        r = fn(k)
        if r:
            hits[name] += 1
            row.append("%s[%s]" % (name[:1], r))
    print("   %-26s %s %s" % (k, "❌" if row else "✔", " ".join(row)))
print("   20 个里被误伤：%s" % dict(hits))
