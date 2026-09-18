# -*- coding: utf-8 -*-
"""量：全库哪些条目会因「泛问词」被主路捞到（只读，2026-09-17）。

用途：回答「含类别词的问句里，讲定义的概念词条为什么会占住一个位次」——
不靠猜，把全库 title/keywords 与九个泛问词的包含关系一条条数出来。

口径（三条，分开报）：
  A. 入口词**正好等于**泛问词 —— 设计上「允许被泛问捞起来」的那几条（= 概念词条）；
  B. 入口词**含**泛问词但不等于 —— 运行时已由 `WrapsGenericCategoryWord` 剔掉，这里只是复核那道闸不是空转；
  C. 每个泛问词在库里的分布（当关键词的条目数 / 标题含它的条目数）。

⚠️ 只读。不写任何包、不改任何索引。
"""
import collections
import json
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PKG = os.path.join(ROOT, "ModuleData", "Worldbook", "packages", "calradia", "runtime.json")

GENERIC = ["村庄", "村子", "村落", "城堡", "城砦", "堡垒", "城镇", "镇子", "城市"]


def kw_text(value):
    """入口词在包里可能是纯字符串，也可能是 {zh-CN: ...} 这种本地化对象 —— 两种都取成字符串。"""
    if isinstance(value, str):
        return value
    if isinstance(value, dict):
        for key in ("zh-CN", "zh", "en"):
            if isinstance(value.get(key), str):
                return value[key]
        for item in value.values():
            if isinstance(item, str):
                return item
    return ""


def main():
    with open(PKG, "r", encoding="utf-8") as handle:
        data = json.load(handle)
    entries = data.get("entries") or []
    print("PKG = %s" % PKG)
    print("条目数 = %d" % len(entries))
    if entries:
        sample = entries[0]
        print("样本键 = %s" % sorted(sample.keys()))
        print("样本 keywords 类型 = %s" % type(sample.get("keywords")).__name__)
    print()

    exact, wrapped = set(), set()
    kw_df, title_df = collections.Counter(), collections.Counter()
    title_kw = {}
    for entry in entries:
        eid = entry.get("id", "")
        title = kw_text(entry.get("title"))
        keywords = [k for k in (kw_text(x) for x in (entry.get("keywords") or [])) if k]
        title_kw[eid] = (title, keywords)
        for word in GENERIC:
            if title == word:
                exact.add((eid, title, "title", word))
            for keyword in keywords:
                if keyword == word:
                    exact.add((eid, title, "keyword", keyword))
                elif word in keyword:
                    wrapped.add((eid, title, "keyword", keyword))
            if word in title and title != word:
                wrapped.add((eid, title, "title", word))
            if word in title:
                title_df[word] += 1
        for keyword in keywords:
            if keyword in GENERIC:
                kw_df[keyword] += 1

    print("=== A. 入口词正好等于泛问词（概念词条的入口） ===")
    for row in sorted(exact):
        print("  %-42s | title=%-6s | 命中处=%-8s | 词=%s" % row)
    print("  小计 %d 条" % len(exact))
    print()

    print("=== A'. 上表涉及的条目，它们的全部入口词 ===")
    for eid in sorted({row[0] for row in exact}):
        title, keywords = title_kw[eid]
        print("  %s | title=%s | keywords=%s" % (eid, title, keywords))
    print()

    print("=== B. 入口词含泛问词但不等于（运行时该被剔掉；小计应为 0） ===")
    for row in sorted(wrapped):
        print("  %-42s | title=%-16s | 命中处=%-8s | 词=%s" % row)
    print("  小计 %d 条" % len(wrapped))
    print()

    print("=== C. 泛问词在库里的分布 ===")
    for word in GENERIC:
        print("  %-4s 当关键词的条目 %3d 条 | 标题含它的条目 %3d 条" % (word, kw_df[word], title_df[word]))
    print()
    print("PROBE_DONE")


main()
