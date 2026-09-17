# -*- coding: utf-8 -*-
"""量 B 项（索引侧治本）的**泛问词保护表 G** 该用哪条判据 —— 几个候选并排量。

B 的目的不是修今天的 bug（今天 A 项已经把数据清了），是**防复发**：
以后谁再写一个把类别词裹进去的关键词（`X·村庄` / `某某城市`），索引要自己挡住。
所以 B 必须**内容驱动**（G 从语料自己长出来），而且今天必须**额外剔 0 条**（不误伤任何真实入口）。

四个候选判据并排量：
  A｜语料覆盖：中文关键词 K，在「全库 title+summary」里覆盖条目数 > 40
  B｜短标题  ：长度 2~3 字的中文 title（两三字的不是名字，是类别词）
  C｜A ∪ B
  D｜短标题 ∧ 语料覆盖：长度 2~3 字 **且** 语料覆盖 > 40（最窄）
共用剔除规则：关键词 K 的**真子串**里含 G 的整词 ⇒ K 不进索引（K 自己不受影响）。

每个候选都要看三样：G 有几个 · 真语料里剔掉几条（期望 0）· 变异检验有没有反例。
「全绿不算证据」——变异检验里必须有人造样本证明这条规则真会开火。

运行：python -u tools/_probe_b_generic_words_20260917.py [包路径]
"""
import io
import json
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = r"D:/AWAKE-Dev/AWAKE"
DEFAULT_PKG = os.path.join(ROOT, "ModuleData/Worldbook/packages/calradia/runtime.json")
MAX_DF = 40
MUST_EXCLUDE = ["德里亚特·村庄", "厄尔凡尼亚·村庄", "某某城市", "某某村庄"]
MUST_KEEP = ["拉文尼亚", "塔奈西斯湖", "帝国之湖", "Mecalovea Castle",
             "阿特费尼亚城堡", "村庄", "城堡", "城镇"]


def loc(v):
    if isinstance(v, dict):
        return v.get("zh-CN") or v.get("zh") or v.get("en") or ""
    return v or ""


def zh_only(s):
    return bool(s) and any(ord(c) > 0x7F for c in s)


def main():
    pkg = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_PKG
    entries = json.loads(io.open(pkg, encoding="utf-8").read())["entries"]
    print("包 = %s ｜ 条目 %d" % (os.path.relpath(pkg, ROOT), len(entries)))

    kws = {}
    titles = {}
    for e in entries:
        i = e["id"]
        s = []
        for k in (e.get("keywords") or []):
            k = (k or "").strip()
            if k and k not in s:
                s.append(k)
        kws[i] = s
        titles[i] = loc(e.get("title"))

    texts = []
    for e in entries:
        t = e.get("title") or {}
        parts = [loc(t)]
        if isinstance(t, dict):
            parts.append(t.get("en") or "")
        parts.append(loc(e.get("summary")))
        texts.append((e["id"], "\n".join(x for x in parts if isinstance(x, str) and x)))

    def coverage(w):
        return sum(1 for _i, t in texts if w in t)

    # 先看三个已知类别词自己长什么样
    print()
    print("── 三个类别词自身的语料覆盖（title+summary 含它的条目数） ──")
    for w in ["村庄", "城堡", "城镇"]:
        owners = [i for i in kws if w in kws[i]]
        print("  %-4s 语料覆盖 %3d 条；被 %d 条当关键词认领" % (w, coverage(w), len(owners)))

    all_kw = sorted({k for s in kws.values() for k in s})
    short_cn_titles = sorted({v for v in titles.values() if zh_only(v) and 2 <= len(v) <= 3})
    print()
    print("── 长度 2~3 字的中文 title（%d 个） ──" % len(short_cn_titles))
    print("  %s" % "、".join(short_cn_titles[:60]))

    def build(kind):
        if kind == "A":
            return {k for k in all_kw if zh_only(k) and coverage(k) > MAX_DF}
        if kind == "B":
            return set(short_cn_titles)
        if kind == "C":
            return {k for k in all_kw if zh_only(k) and coverage(k) > MAX_DF} | set(short_cn_titles)
        return {t for t in short_cn_titles if coverage(t) > MAX_DF}

    def hits(k, G):
        for g in G:
            if k == g or len(g) >= len(k):
                continue
            for i in range(len(k) - len(g) + 1):
                if k[i:i + len(g)] == g:
                    return g
        return None

    print()
    for kind, name in [("A", "A｜语料覆盖>40"), ("B", "B｜短标题(2~3字)"),
                       ("C", "C｜A ∪ B"), ("D", "D｜短标题 ∧ 覆盖>40")]:
        G = build(kind)
        extra = [(i, k, hits(k, G)) for i, s in kws.items() for k in s if hits(k, G)]
        bad = []
        for k in MUST_EXCLUDE:
            if not hits(k, G):
                bad.append(("该剔没剔", k))
        for k in MUST_KEEP:
            if hits(k, G):
                bad.append(("误伤", k))
        print("── %s ──" % name)
        print("  G（%d 个）：%s" % (len(G), "、".join(sorted(G)[:20])))
        print("  真语料里剔掉 %d 条 %s" % (len(extra), [(i.replace("awake:entry:", ""), k, g) for i, k, g in extra[:6]]))
        print("  变异检验反例 %d 个 %s" % (len(bad), bad))
        print("  判定：%s" % ("可用（额外剔 0 且变异全对）" if not extra and not bad else "不可用"))
        print()


if __name__ == "__main__":
    main()
