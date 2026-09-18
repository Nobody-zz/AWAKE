# -*- coding: utf-8 -*-
"""量 B 项的**影响面**：新规则（键里含泛词就剔）会额外剔掉哪些关键词。

新规则（拟）：
    IsExcluded(K):
      if KeywordDf[K] > 40                                   -> true   (原 R1)
      if K 的某个**真子串** S（|S|>=2）满足 TermDf[S] > 40      -> true   (新)
      if K 起头 doc.                                         -> true
      if K 是纯 ASCII 且含下划线                                -> true

⚠️ 为什么用 **TermDf**（正文/标题的词频）而不是 KeywordDf：
   清掉 A 之后「村庄」在 keywords 里只剩概念词条 1 条（KeywordDf=1），
   拿 KeywordDf 去算复合词 ⇒ `德里亚特·村庄` 继承到 1 ⇒ 照样放过，**B 就白做了**。
   TermDf 量的是"这个词在整个语料里到处都是" —— 与谁拥有它无关，才是真判据。

同时模拟 A（把 村庄/城堡/城镇 从 388 条上摘掉、把 德里亚特·村庄 删掉）后的状态。
"""
import collections
import io
import json
import sys
import unicodedata

sys.stdout.reconfigure(encoding="utf-8")

PKG = "ModuleData/Worldbook/packages/calradia/runtime.json"
PFX = "awake:entry:"
MAXDF = 40
GENERIC = {"村庄", "城堡", "城镇"}
DROP_EXACT = {"村庄", "城堡", "城镇"}
DROP_VALUES = {"德里亚特·村庄"}

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
    return (t.get("zh-CN") or "") if isinstance(t, dict) else (t or "")


def summ_of(e):
    s = e.get("summary")
    return (s.get("zh-CN") or "") if isinstance(s, dict) else (s or "")


def is_sep(ch):
    c = unicodedata.category(ch)
    return c[0] in ("P", "Z", "S")


def split_segments(text):
    start = 0
    out = []
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


# ---------- 模拟 A：先清数据 ----------
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

# ---------- 两张频次表 ----------
kwdf = collections.Counter()
for e, kw in sim:
    for w in set(w.strip() for w in kw if w and w.strip()):
        kwdf[w] += 1

termdf = collections.Counter()
for e, kw in sim:
    terms = set()
    for t in enumerate_terms(title_of(e)):
        terms.add(t)
    for t in enumerate_terms(summ_of(e)):
        terms.add(t)
    for t in terms:
        termdf[t] += 1

print("TermDf>40 的词 %d 个，Top12：%s"
      % (sum(1 for v in termdf.values() if v > 40),
         sorted(((v, k) for k, v in termdf.items() if v > 40), reverse=True)[:12]))
print("  其中「村庄」TermDf=%d、「城堡」TermDf=%d、「城镇」TermDf=%d"
      % (termdf.get("村庄", 0), termdf.get("城堡", 0), termdf.get("城镇", 0)))


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


def generic_substrings(k):
    """返回 k 的真子串里 TermDf>40 的那些。"""
    out = []
    L = len(k)
    for i in range(L):
        for j in range(i + 2, L + 1):
            if i == 0 and j == L:
                continue          # 排除自身（自身由原 R1 判）
            s = k[i:j]
            if termdf.get(s, 0) > MAXDF:
                out.append((s, termdf[s]))
    return out


allkw = sorted(kwdf)
old_ex = {k for k in allkw if excluded_old(k)}
new_ex = set(old_ex)
extra = []
for k in allkw:
    if excluded_old(k):
        continue
    gs = generic_substrings(k)
    if gs:
        new_ex.add(k)
        extra.append((k, kwdf[k], sorted(set(gs), key=lambda x: -x[1])[:3]))

print()
print("=" * 78)
print("影响面：新规则比旧规则**额外**剔掉的关键词（已模拟 A）")
print("   旧规则剔 %d 个；新规则剔 %d 个；额外 %d 个" % (len(old_ex), len(new_ex), len(extra)))
for k, c, gs in extra:
    print("   「%s」 自身覆盖 %d 条，含泛词：%s" % (k, c, gs))

print()
print("=" * 78)
print("反查：概念词条自带的六个检索词，会不会被新规则误剔")
for k in ["村庄", "村子", "村落", "城堡", "城砦", "堡垒", "城镇", "镇子", "城市"]:
    gs = generic_substrings(k)
    print("   %-6s KeywordDf(模拟后)=%-3d 旧规则剔=%-5s 新规则剔=%-5s 命中的泛子串=%s"
          % (k, kwdf.get(k, 0), excluded_old(k), (excluded_old(k) or bool(gs)), gs or "无"))

print()
print("=" * 78)
print("抽样：抽 12 个普通专名关键词，看新规则有没有误伤")
import random
random.seed(7)
sample = [k for k in allkw if kwdf[k] == 1 and len(k) >= 3 and not looks_internal(k)]
for k in random.sample(sample, 12):
    gs = generic_substrings(k)
    flag = "❌ 会被剔" if gs else "✔ 保留"
    print("   %-24s %s %s" % (k, flag, gs[:2] if gs else ""))
