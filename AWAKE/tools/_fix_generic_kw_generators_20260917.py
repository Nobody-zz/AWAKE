# -*- coding: utf-8 -*-
"""防复发：把 9 个铺开批生成器里「类别词进 aliases」那行改掉，并就地写下为什么。

改的依据（甲方 2026-09-17 裁决）：
  类别词（村庄/城堡/城镇）**不再挂在每一条聚落上**，而是**升格成概念词条**
  （`doc.geography.settlement-types-{village,castle,town}`）。
  理由：挂在 388 条上时覆盖 > 40 ⇒ 被索引卫生 R1 剔掉 ⇒ **既不指向任何东西**，
  又留了一个"谁都能沾"的坑 —— 德里亚特手写的「德里亚特·村庄」覆盖只有 1、绕过 R1，
  于是成了「村庄」在索引里的**唯一主人**，把 272 条村庄的泛问全劫走（详见
  `docs/FIT-20260917-识别链路与条目的契合度.md` §三）。

用法：python -u tools/_fix_generic_kw_generators_20260917.py [--write]
"""
import io
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = r"D:/AWAKE-Dev/AWAKE"
GEN = os.path.join(ROOT, "docs/worldbook-migration/projection/authoring-out")

NOTE = {
    "村庄": "doc.geography.settlement-types-village",
    "城堡": "doc.geography.settlement-types-castle",
    "城镇": "doc.geography.settlement-types-town",
}

JOBS = []
for i, f in enumerate(["_rollout_villages%d_gen_%s.py" % (n, "20260913" if n <= 3 else "20260914")
                       for n in range(1, 8)]):
    JOBS.append((f, "村庄", '            "aliases": {"zh-CN": [m["cn"], "村庄"], "en": [m["en"], sid]},',
                 '            "aliases": {"zh-CN": [m["cn"]], "en": [m["en"], sid]},'))
JOBS.append(("_rollout_towns_gen_20260913.py", "城镇",
             '            "aliases": {"zh-CN": [m["cn"], "城镇"], "en": [m["en"], sid]},',
             '            "aliases": {"zh-CN": [m["cn"]], "en": [m["en"], sid]},'))
JOBS.append(("_rollout_castles1_gen_20260914.py", "城堡",
             '            "aliases": {"zh-CN": [cn, "城堡"] + vcn, "en": [en, sid] + ven},',
             '            "aliases": {"zh-CN": [cn] + vcn, "en": [en, sid] + ven},'))


def note_lines(word, indent):
    pad = " " * indent
    return [
        "%s# 2026-09-17：类别词「%s」不再进 aliases。" % (pad, word),
        "%s# 它挂在几百条上 ⇒ 覆盖 > 40 ⇒ 被索引卫生 R1 剔掉 ⇒ 指不到任何东西，" % pad,
        "%s# 还留了「谁都能沾」的坑（德里亚特的「德里亚特·村庄」就是这么劫走 272 条泛问的）。" % pad,
        "%s# 类别词已升格为概念词条 %s。" % (pad, NOTE[word]),
    ]


def main():
    write = "--write" in sys.argv
    for fn, word, old, new in JOBS:
        p = os.path.join(GEN, fn)
        if not os.path.exists(p):
            print("MISS %s" % p)
            continue
        t = io.open(p, encoding="utf-8", newline="").read()
        if old not in t:
            print("SKIP %-40s 待改行不在（可能已改）" % fn)
            continue
        n = t.count(old)
        if n != 1:
            print("WARN %-40s 出现 %d 次，未动" % (fn, n))
            continue
        indent = len(old) - len(old.lstrip())
        rep = "\n".join(note_lines(word, indent)) + "\n" + new
        t2 = t.replace(old, rep)
        print("%-42s %s -> 去掉类别词＋留注 %d 行" % (fn, word, len(note_lines(word, indent))))
        if write:
            io.open(p, "w", encoding="utf-8", newline="").write(t2)

    # 德里亚特那条复合词：来源是 _fix_title_aliases_20260913.py
    p = os.path.join(GEN, "_fix_title_aliases_20260913.py")
    old = '    ("village-deriat.yaml", ["德里亚特·村庄"]),\n'
    if os.path.exists(p):
        t = io.open(p, encoding="utf-8", newline="").read()
        if old in t:
            new = ("    # 2026-09-17 撤：这条复合别名覆盖只有 1、绕过索引卫生 R1，成了「村庄」在索引里的\n"
                   "    # 唯一主人，把 272 条村庄的泛问全劫走（见 docs/FIT-20260917-识别链路与条目的契合度.md §三）。\n"
                   "    # 「村庄」已升格为概念词条 doc.geography.settlement-types-village，此处不再补。\n"
                   "    # (\"village-deriat.yaml\", [\"德里亚特·村庄\"]),\n")
            print("%-42s 撤掉「德里亚特·村庄」的补词" % os.path.basename(p))
            if write:
                io.open(p, "w", encoding="utf-8", newline="").write(t.replace(old, new))
        else:
            print("SKIP %s 待撤行不在" % os.path.basename(p))
    if not write:
        print("\n（未加 --write，只做预览）")


if __name__ == "__main__":
    main()
