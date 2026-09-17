# -*- coding: utf-8 -*-
"""检查「概念词条的正文会不会跟具体问句撞字」——撞上就会在兜底通道里抢位子。

为什么必须查这个（2026-09-17 实测）：
  兜底通道按「与查询共享多少个 term」排序，且 term 是**2-gram**。
  概念词条的正文只要含「底下的村子」，就会切出 `的村`、`村子` 两个 2-gram；
  问句 `有大瀑布的村子是哪个？` 里的 `的村`/`村子` 正好也在 ⇒ 两者共享数**打平（各 2）**，
  再按 id 排序，`geography.settlement-types-castle` 恰好排在 `geography.villages-*` 前面
  ⇒ 正确的村庄被挤出前 3（验台 `RETRIEVAL_GATE` B 组掉 1 条）。**这不是检索规则的毛病，
  是概念词条正文用了"谁都会用"的字。**

本脚本对每一句门禁题 算「概念词条 title+summary 与它的共享 term」，要求全部为 0。
撞了就改正文措辞 —— 改的是"别用那类字"，不是"把题面词补进条目"（两回事）。

运行：python -u tools/_probe_concept_collision_20260917.py [候选措辞.json] [--pkg 包路径]
  不带参数＝读仓库在挂的包；给 --pkg 可以指向刚编出来的产物（重编链就是这么调它的）。
"""
import io
import json
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = r"D:/AWAKE-Dev/AWAKE"
CASES = os.path.join(ROOT, "tools/_retrieval_cases_20260916.json")
PKG = os.path.join(ROOT, "ModuleData/Worldbook/packages/calradia/runtime.json")
MAX_TERM_DF = 40
CONCEPT = ["awake:entry:geography.settlement-types-village",
           "awake:entry:geography.settlement-types-castle",
           "awake:entry:geography.settlement-types-town"]


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


def main():
    global PKG
    args = sys.argv[1:]
    if "--pkg" in args:
        i = args.index("--pkg")
        PKG = args[i + 1]
        args = args[:i] + args[i + 2:]
    entries = json.loads(io.open(PKG, encoding="utf-8").read())["entries"]
    print("包 = %s" % os.path.relpath(PKG, ROOT))
    # term 索引的 df（全库 title+summary），用于剔除高频 term
    df = {}
    for e in entries:
        t = e.get("title") or {}
        txt = " ".join([loc(t), (t.get("en") if isinstance(t, dict) else "") or "",
                        loc(e.get("summary"))])
        for x in set(terms_of(txt)):
            df[x] = df.get(x, 0) + 1
    live = {e["id"]: e for e in entries}

    # 可选：用候选措辞覆盖包里的正文（改措辞时先在这里试，别先重编）
    cand = {}
    if args and os.path.exists(args[0]):
        cand = json.loads(io.open(args[0], encoding="utf-8").read())
        print("（用候选措辞：%s）" % os.path.relpath(args[0], ROOT))
        print()

    cases = json.loads(io.open(CASES, encoding="utf-8").read())
    qs = []
    for c in (cases if isinstance(cases, list) else cases.get("cases") or []):
        q = c.get("query") or c.get("playerText") or ""
        if q:
            qs.append((c.get("group") or "?", q))

    print("题集 %s ｜ %d 句" % (os.path.relpath(CASES, ROOT), len(qs)))
    print()
    bad = 0
    for cid in CONCEPT:
        e = live.get(cid)
        if e is None:
            print("%s 不在包里" % cid)
            continue
        t = e.get("title") or {}
        smy = cand.get(cid) or loc(e.get("summary"))
        txt = loc(t) + " " + smy
        et = set(x for x in terms_of(txt) if df.get(x, 0) <= MAX_TERM_DF)
        print("── %s ──" % cid.replace("awake:entry:", ""))
        print("   正文：%s" % smy)
        print("   进索引的 term（%d）：%s" % (len(et), "、".join(sorted(et))))
        hit = []
        for g, q in qs:
            sh = et & set(x for x in terms_of(q) if df.get(x, 0) <= MAX_TERM_DF)
            if sh:
                hit.append((g, q, sorted(sh)))
        if not hit:
            print("   与题集 0 句撞字 ✔")
        else:
            bad += len(hit)
            for g, q, sh in hit:
                print("   ✘ 撞上 group=%s「%s」共享 %s" % (g, q, "、".join(sh)))
        print()
    print("合计撞字 %d 处 %s" % (bad, "✔" if not bad else "（要改措辞）"))
    return 0 if not bad else 1


if __name__ == "__main__":
    sys.exit(main())
