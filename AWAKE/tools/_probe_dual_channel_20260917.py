# -*- coding: utf-8 -*-
"""量：「主路」和「兜底」各自的**召回池**覆盖了什么，以及「主路堵死兜底」漏掉几条（只读，2026-09-17）。

起因 —— 现状是「主路命中非空 ⇒ 兜底不跑」（`WorldKnowledgeQueryService.cs:363`）：
    主路命中哪怕 1 条（哪怕是错的），兜底就被跳过。本脚本把这个前提掀掉。

⚠️ **口径声明（很重要，别读错）**
  · 本脚本算的是**候选池**（`FindCandidates` / `FindLiteralCandidates` 的返回），
    **不含身份筛**（`SelectExpression`）、**不含装块**。
  · C# 验台的 `hits=` 是 `result.HitIds` ——**已经过身份筛**（`RetrievalProbeCases.cs:122`），
    所以两边数字天然不同：**本脚本的池子 ⊇ 验台的 hits**。第一版误把两者当同一个量，
    阳性对照差了一条（B 组 8/11 vs 验台 9/11），根因就是验台那一条被身份筛剔了。
  · ⇒ **这里不比 hit@3**。要比的是**集合层面**的问题：「答案在不在这个池子里」。

  · 这是**复算件，不是真代码**。真结论以 C# 验台为准；本件只用来看方向。

要看的那个数（`BLOCKED`）：**主路池非空、答案不在主路池、但在兜底池** ——
这些题现态永远不会去问兜底，答案就此漏掉。这就是「主路命中堵死兜底」的实际代价。

运行：python -u tools/_probe_dual_channel_20260917.py
"""
import json
import os
import sys
import unicodedata

sys.stdout.reconfigure(encoding="utf-8")

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PKG = os.path.join(ROOT, "ModuleData/Worldbook/packages/calradia/runtime.json")
CASES = os.path.join(ROOT, "tools/_retrieval_cases_20260916.json")

MAX_DF_KEYWORD = 40
MAX_DF_TERM = 40
FALLBACK_TAKE = 5
NAME_LIKE_MAX = 6
GENERIC_WORDS = ["村庄", "村子", "村落", "城堡", "城砦", "堡垒", "城镇", "镇子", "城市"]


def loc(value):
    if isinstance(value, dict):
        return value.get("zh-CN") or value.get("zh") or value.get("en") or ""
    return value or ""


def is_sep(ch):
    # 与真代码同源（`WorldbookTermIndex.IsSeparator` = char.IsPunctuation || char.IsWhiteSpace
    # || char.IsSymbol）。**不要用字符白名单** —— 第一版就是白名单，漏掉一个 Unicode 标点就会切错段。
    return unicodedata.category(ch)[0] in ("P", "Z", "S")


def segments(text):
    out, start = [], 0
    for i, ch in enumerate(text):
        if not is_sep(ch):
            continue
        if i > start:
            out.append(text[start:i])
        start = i + 1
    if start < len(text):
        out.append(text[start:])
    return out


def terms_of(text):
    out = []
    if not text:
        return out
    for seg in segments(text):
        if len(seg) >= 2:
            out.append(seg)
        if all(ord(c) <= 0x7F for c in seg):
            continue
        for i in range(len(seg) - 1):
            out.append(seg[i:i + 2])
    return out


def looks_like_single_short(text):
    segs = segments(text or "")
    return len(segs) == 1 and len(segs[0]) <= NAME_LIKE_MAX


def looks_like_internal_id(keyword):
    has_underscore = False
    for ch in keyword:
        if ch == "_":
            has_underscore = True
            continue
        if ord(ch) > 0x7E or ord(ch) < 0x20:
            return False
    return has_underscore


def wraps_generic(keyword):
    for word in GENERIC_WORDS:
        if len(keyword) <= len(word):
            continue
        if word.lower() in keyword.lower():
            return True
    return False


class Pkg(object):
    def __init__(self, entries):
        self.by_id = {e["id"]: e for e in entries}
        self.title = {e["id"]: loc(e.get("title")) for e in entries}
        self.summary = {e["id"]: loc(e.get("summary")) for e in entries}
        self.keywords = {e["id"]: [loc(k) for k in (e.get("keywords") or []) if loc(k)] for e in entries}
        self.keyword_index = self._build_keywords()
        self.term_index = self._build_terms()

    def _distinct(self, eid):
        seen, out = set(), []
        for kw in self.keywords[eid]:
            kw = kw.strip()
            if not kw or kw.lower() in seen:
                continue
            seen.add(kw.lower())
            out.append(kw)
        return out

    def _build_keywords(self):
        df = {}
        for eid in self.by_id:
            for kw in self._distinct(eid):
                df[kw] = df.get(kw, 0) + 1
        index = {}
        for eid in self.by_id:
            for kw in self._distinct(eid):
                if df[kw] > MAX_DF_KEYWORD:
                    continue
                if kw.lower().startswith("doc."):
                    continue
                if looks_like_internal_id(kw):
                    continue
                if wraps_generic(kw):
                    continue
                index.setdefault(kw, []).append(eid)
        return index

    def _build_terms(self):
        per, df = {}, {}
        for eid in self.by_id:
            s = set(terms_of(self.title[eid])) | set(terms_of(self.summary[eid]))
            per[eid] = s
            for t in s:
                df[t] = df.get(t, 0) + 1
        index = {}
        for eid, s in per.items():
            for t in s:
                if df[t] <= MAX_DF_TERM:
                    index.setdefault(t, []).append(eid)
        return index

    def match_quality(self, eid, text):
        title = self.title[eid]
        if title and title.lower() == text.lower():
            return (0, len(title))
        exact = [len(k) for k in self.keywords[eid] if k.lower() == text.lower()]
        if exact:
            return (1, max(exact))
        if title and (text.lower() in title.lower() or title.lower() in text.lower()):
            return (2, len(title))
        matched = [len(k) for k in self.keywords[eid]
                   if k.lower() in text.lower() or text.lower() in k.lower()]
        return (3, max(matched) if matched else 0)

    def literal_pool(self, text):
        low = text.lower()
        ids = set()
        for kw, lst in self.keyword_index.items():
            k = kw.lower()
            if k not in low and low not in k:
                continue
            ids.update(lst)
        if not ids:
            return []
        return sorted(ids, key=lambda i: (self.match_quality(i, text)[0],
                                          -self.match_quality(i, text)[1], i))

    def fallback_pool(self, text):
        terms = set(terms_of(text))
        if not terms:
            return []
        required = 2 if looks_like_single_short(text) else 1
        shared, longest = {}, {}
        for t in terms:
            for i in self.term_index.get(t, []):
                shared[i] = shared.get(i, 0) + 1
                longest[i] = max(longest.get(i, 0), len(t))
        order = sorted([i for i in shared if shared[i] >= required],
                       key=lambda i: (-shared[i], -longest[i], i))
        return order[:FALLBACK_TAKE]


def main():
    with open(PKG, encoding="utf-8") as handle:
        entries = json.load(handle)["entries"]
    with open(CASES, encoding="utf-8") as handle:
        cases = json.load(handle)["cases"]
    pkg = Pkg(entries)
    print("PKG = %s | 条目 %d" % (PKG, len(pkg.by_id)))
    print("口径：候选池（不含身份筛、不含装块）；**不比 hit@3**")
    print()

    total = in_lit = in_fb = in_any = blocked = 0
    blocked_rows = []
    for c in cases:
        if not c.get("countInGate", True):
            continue
        total += 1
        target, query = c["target"], c["query"]
        lit = pkg.literal_pool(query)
        fb = pkg.fallback_pool(query)
        a = target in lit
        b = target in fb
        in_lit += 1 if a else 0
        in_fb += 1 if b else 0
        in_any += 1 if (a or b) else 0
        routed = "主路" if lit else "兜底"
        note = ""
        if lit and not a and b:
            blocked += 1
            note = "   <<< 兜底本来能捞到，但主路有命中 ⇒ 兜底被跳过"
            blocked_rows.append((c["group"], query))
        # 答案位次：够得着的话，排第几？（只报 1~5，超出不报）
        rank = ""
        pool = lit if lit else fb
        if target in pool:
            pos = pool.index(target) + 1
            rank = " ｜ 答案在第 %d 位%s" % (pos, "（进不了前 3）" if pos > 3 else "")
        print("CASE group=%s q=%s" % (c["group"], query))
        print("   主路池 %2d 条 含答案=%s ｜ 兜底池 %d 条 含答案=%s ｜ 现态走 %s%s%s"
              % (len(lit), "是" if a else "否", len(fb), "是" if b else "否", routed, rank, note))

    print()
    print("=== 集合层面覆盖（分母 %d） ===" % total)
    print("  主路池含答案          %d/%d" % (in_lit, total))
    print("  兜底池含答案          %d/%d" % (in_fb, total))
    print("  两池并集含答案        %d/%d" % (in_any, total))
    print("  BLOCKED（主路堵死兜底）%d/%d" % (blocked, total))
    for group, query in blocked_rows:
        print("      group=%s q=%s" % (group, query))
    print()
    print("读法：BLOCKED 那一栏就是「主路命中就堵死兜底」的实际代价 ——")
    print("      答案在兜底池里，现态永远不会去问。")
    print("PROBE_DONE")


main()
