# -*- coding: utf-8 -*-
"""2026-09-18 · 「字面兜底通道」单独读数用的规格。

为什么要这份：`probe` 只回「命中条数 + 最终 hits」，看不出**兜底那一腿自己捞回了什么**。
这里把 24 道门禁题（真人问法）＋ 8 句日常闲话（书里根本没有对应的东西）＋ 3 条对照
摆成一份清单，交给验台新加的 `fallback-probe` 子命令逐条读。
"""
import json
import os

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
CASES = os.path.join(ROOT, "tools", "_retrieval_cases_20260916.json")
OUT = os.path.join(ROOT, "tools", "_fallback_probe_spec_20260918.json")

gate = json.load(open(CASES, encoding="utf-8"))["cases"]

rows = []
for i, c in enumerate(gate, 1):
    rows.append({
        "name": "G%02d" % i,
        "kind": "gate",
        "group": c["group"],
        "text": c["query"],
        "target": c["target"],
    })

# 日常闲话 / 指代句：书里没有对应的东西，理想是「空手」
junk = [
    ("你好啊，最近怎么样？", "打招呼"),
    ("今天天气不错。", "寒暄"),
    ("这附近有吃的吗？", "日常、无专名"),
    ("你叫什么名字？", "问 NPC 自己"),
    ("那个东西多少钱？", "指代，没说出是什么"),
    ("谁在那边站着？", "指代，句子空"),
    ("听说北方出了事，是真的吗？", "模糊传闻"),
    ("你觉得我该怎么办？", "征询，不是查书"),
]
for i, (t, why) in enumerate(junk, 1):
    rows.append({"name": "J%d" % i, "kind": "junk", "why": why, "text": t})

# 对照：阳性（必须捞得出、且排第一）／阴性（书里确实没有）
controls = [
    ("P1", "pos", "拉迈萨", "awake:entry:geography.villages-lamesa", "短名，名字形状"),
    ("P2", "pos", "皇家侍卫是什么兵？", "awake:entry:war.troops-royal-guard", "含登记关键词"),
    ("N1", "neg", "阿米巴原虫是什么？", "", "书里没有、世上也没有"),
]
for n, kind, t, tgt, why in controls:
    rows.append({"name": n, "kind": kind, "text": t, "target": tgt, "why": why})

# ⚠️ P1/P2 是**关键词层**的对照，跟兜底这一腿无关（实测 kw≥1 ⇒ 走的是关键词层）。
#    兜底这一腿要有自己的阳性对照：**从条目正文里抠一段它自己的话**（保证不与任何登记
#    关键词互为子串 ⇒ 关键词层必然空手，kw=0），正确答案必须在兜底里排第 1。
#    没有这条，"兜底捞不到正确答案"就分不清是「通道弱」还是「我的探针坏了」。
RUNTIME = os.path.join(ROOT, "ModuleData", "Worldbook", "packages", "calradia", "runtime.json")


def _text(v):
    if isinstance(v, str):
        return v
    if isinstance(v, dict):
        return v.get("zh-CN") or ""
    return ""


def real_fallback_controls(want=3):
    ents = json.load(open(RUNTIME, encoding="utf-8"))["entries"]
    out, seen_domains = [], set()
    for e in ents:
        dom = e.get("domain") or ""
        if dom in seen_domains:
            continue
        summary = _text(e.get("summary"))
        title = _text(e.get("title"))
        kws = [k for k in (e.get("keywords") or []) if isinstance(k, str)]
        hit = None
        # 在正文里找一段 6~9 字的连续中文，要求：不与任何关键词互为子串、也不在标题里
        for i in range(len(summary) - 9):
            seg = summary[i:i + 7]
            if not all("\u4e00" <= ch <= "\u9fff" for ch in seg):
                continue
            if any(seg in k or k in seg for k in kws) or seg in title:
                continue
            hit = seg
            break
        if not hit:
            continue
        out.append({
            "name": "F%d" % (len(out) + 1), "kind": "pos_fallback", "text": hit,
            "target": e["id"], "why": "正文原话（%s），关键词层应空手" % dom,
        })
        seen_domains.add(dom)
        if len(out) >= want:
            break
    return out


fb_controls = real_fallback_controls()
rows += fb_controls
print("兜底专用阳性对照: " + ", ".join("%s=%s→%s" % (r["name"], r["text"], r["target"].split(":")[-1]) for r in fb_controls))

json.dump({"schema": "awake.worldbook.fallback-probe.v1", "createdAt": "2026-09-18",
           "note": "只看「字面兜底通道」自己捞回什么；不含身份门、不含语义臂。",
           "queries": rows}, open(OUT, "w", encoding="utf-8"),
          ensure_ascii=False, indent=1)
print("写出 %d 条 -> %s" % (len(rows), OUT))
print("  gate=%d junk=%d control=%d" % (sum(1 for r in rows if r["kind"] == "gate"),
                                        sum(1 for r in rows if r["kind"] == "junk"),
                                        sum(1 for r in rows if r["kind"] in ("pos", "neg"))))
