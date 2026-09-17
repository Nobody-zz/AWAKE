# -*- coding: utf-8 -*-
"""把兜底通道（`FindFallbackCandidates`）在那句回归查询上的排序**逐步复算出来**。

为什么要复算而不是看验台：验台只给"前 3 是谁"，看不出**为什么**排成那样。
这里按真代码同一套规则走一遍（切词 `EnumerateTerms`／建表 df>40 剔除／排序
「共享 term 数 → 最长共享 term → id」），把 v12 与 v13e 两个包的读数并排摊开。

（这是**复算件，不是真代码**——只用来定位；结论必须回到验台验一次。）

运行：python -u tools/_probe_fallback_order_20260917.py
"""
import io
import json
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = r"D:/AWAKE-Dev/AWAKE"
PKGS = [
    ("v12 基线", os.path.join(ROOT, "tools/_baseline/geo1-v12-runtime.json")),
    ("v13e 现挂", os.path.join(ROOT, "ModuleData/Worldbook/packages/calradia/runtime.json")),
]
QUERY = "有大瀑布的村子是哪个？"
MAX_TERM_DF = 40
TAKE = 5
NAME_LIKE_MAX = 6


def loc(v):
    if isinstance(v, dict):
        return v.get("zh-CN") or v.get("zh") or v.get("en") or ""
    return v or ""


def is_sep(ch):
    return ch.isspace() or ch in "。，、！？；：（）【】《》…—·「」“”‘’,.!?;:()[]{}<>\"'~`|/\\-+=*&^%$#@"


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


def build_index(entries):
    per, df = {}, {}
    for e in entries:
        t = e.get("title") or {}
        txt = " ".join([loc(t), (t.get("en") if isinstance(t, dict) else "") or "",
                        loc(e.get("summary"))])
        s = set(terms_of(txt))
        per[e["id"]] = s
        for x in s:
            df[x] = df.get(x, 0) + 1
    idx = {}
    for i, s in per.items():
        for x in s:
            if df[x] <= MAX_TERM_DF:
                idx.setdefault(x, []).append(i)
    return per, df, idx


def run(tag, path):
    entries = json.loads(io.open(path, encoding="utf-8").read())["entries"]
    _by_id = {e["id"]: e for e in entries}
    _per, df, idx = build_index(entries)
    qt = set(terms_of(QUERY))
    segs = segments(QUERY)
    name_like = len(segs) == 1 and len(segs[0]) <= NAME_LIKE_MAX
    req = 2 if name_like else 1
    shared, longest, why = {}, {}, {}
    for term in qt:
        for i in idx.get(term, []):
            shared[i] = shared.get(i, 0) + 1
            longest[i] = max(longest.get(i, 0), len(term))
            why.setdefault(i, []).append(term)
    order = sorted([i for i in shared if shared[i] >= req],
                   key=lambda i: (-shared[i], -longest[i], i))
    print("── %s ｜ 条目 %d ｜ 查询切词 %s ｜ 名字形状=%s(要求共享 %d) ──"
          % (tag, len(entries), sorted(qt), name_like, req))
    print("   按「共享数 → 最长 → id」的前 8 名（括注每个共享 term 的 df，越小越罕见）：")
    for n, i in enumerate(order[:8], 1):
        mark = "  ★答案" if "chornobas" in i else ""
        ts = "、".join("%s(df=%d)" % (t, df.get(t, 0)) for t in sorted(why[i]))
        rarest = min((df.get(t, 0) for t in why[i]), default=0)
        print("    %d. %-46s 共享 %d ｜ 最罕见共享字 df=%d（%s）%s"
              % (n, i.replace("awake:entry:", ""), shared[i], rarest, ts, mark))
    # 按「共享数 → 最长 → 最罕见共享字 df 升序 → id」重排，看会不会把答案拉回前 3
    order2 = sorted([i for i in shared if shared[i] >= req],
                    key=lambda i: (-shared[i], -longest[i],
                                   min(df.get(t, 0) for t in why[i]), i))[:TAKE]
    print("   ── 若把「最罕见共享字」插进排序（共享数 → 最长 → 罕见度 → id）──")
    for n, i in enumerate(order2, 1):
        mark = "  ★答案" if "chornobas" in i else ""
        print("    %d. %-46s 共享 %d%s" % (n, i.replace("awake:entry:", ""), shared[i], mark))
    print()


for tag, p in PKGS:
    run(tag, p)
