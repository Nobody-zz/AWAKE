# -*- coding: utf-8 -*-
"""红测归因探针（2026-09-17）

目的：红测只看到"命中了哪几条"，看不到"为什么中"。这里在 **Python 里复刻字面侧那条规则**
（`FindLiteralCandidates` 的双向子串 + `MatchQuality` 四级），把每条输入命中的**具体关键词**
打出来，先判清责任，再决定改哪里。

⚠️ 这是**推演**，不是验台。所有结论必须再由 C# 验台复核一遍（纪律：平行实现必须同源 / 自说自话不算数）。

用法：python _redtest_chain_attrib_20260917.py
"""
import io
import json

PKG = "ModuleData/Worldbook/packages/calradia/runtime.json"

# 红测里那些红/可疑的输入（前 5 组来自"过匹配"组，后 4 条来自"仍漏/错条目"）
INPUTS = [
    "doc", "economy", "geography", "entry", "war", "Goods",
    "HeadArmor", "sturgian", "castle_village",
    "盔", "军阀", "村", "村庄", "皮", "货", "军事", "力量",
    "斯特基亚", "拉邁薩", "Ｃｌｏｓｅｄ Ｗａｒｌｏｒｄ Ｈｅｌｍｅｔ",
]


def load():
    with io.open(PKG, encoding="utf-8") as fh:
        return json.load(fh)["entries"]


def literal_hits(entries, text, drop_doc_fallback):
    """复刻 `FindLiteralCandidates` 的双向子串命中（only ids）。"""
    hits = []
    low = text.lower()
    for e in entries:
        for k in (e.get("keywords") or []):
            if not k:
                continue
            if drop_doc_fallback and k.startswith("doc."):
                continue
            kl = k.lower()
            if low in kl or kl in low:
                hits.append(e["id"])
                break
    return hits


def matched_keywords(entries, text, drop_doc_fallback):
    """同一趟，但把命中的关键词也带出来 —— 归因靠它。"""
    out = []
    low = text.lower()
    for e in entries:
        for k in (e.get("keywords") or []):
            if not k:
                continue
            if drop_doc_fallback and k.startswith("doc."):
                continue
            kl = k.lower()
            if low in kl or kl in low:
                out.append((e["id"], k))
                break
    return out


def main():
    entries = load()
    print("条目 %d" % len(entries))
    print()
    print("%-34s %10s %10s   %s" % ("输入", "含兜底词", "去兜底词", "去兜底后还中的关键词（前 4）"))
    print("-" * 110)
    for text in INPUTS:
        a = literal_hits(entries, text, False)
        b = matched_keywords(entries, text, True)
        shown = "、".join(k for _i, k in b[:4]) or "—"
        print("%-34s %10d %10d   %s" % (repr(text)[:32], len(a), len(b), shown[:70]))
    print()
    total_doc = sum(1 for e in entries if any((k or "").startswith("doc.") for k in (e.get("keywords") or [])))
    print("含 `doc.` 兜底词的条目：%d / %d" % (total_doc, len(entries)))
    print("带 `doc.` 的条数（关键词级，可能一条多条）：%d"
          % sum(1 for e in entries for k in (e.get("keywords") or []) if (k or "").startswith("doc.")))


main()
