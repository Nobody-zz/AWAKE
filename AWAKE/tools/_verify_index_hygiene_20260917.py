# -*- coding: utf-8 -*-
"""(1) 含下划线却仍进索引的 4 个 ASCII 入口词是什么、为什么漏过 R2。
(2) 全库有多少入口词的形态是「专名·类别」（会被 R1 漏掉的那类复合词）。
(3) 裸类别词（村庄/城堡/城镇）在 aliases 里的条数 —— 即 R1 剔掉的死重。
"""
import io
import json
import sys
import collections

sys.stdout.reconfigure(encoding="utf-8")

PKG = "ModuleData/Worldbook/packages/calradia/runtime.json"
pkg = json.load(io.open(PKG, encoding="utf-8"))
entries = pkg["entries"]
PFX = "awake:entry:"


def short(i):
    return i.replace(PFX, "")


def kw_of(e):
    k = e.get("keywords")
    return (k.get("zh-CN") or []) if isinstance(k, dict) else (k or [])


allkw = collections.Counter()
for e in entries:
    for w in set(kw_of(e)):
        allkw[w] += 1

# ---------- (1) 含下划线却漏过 R2 ----------
print("=" * 78)
print("① 含下划线、但**不满足** R2（纯 ASCII＋含下划线）的入口词 ⇒ 仍进索引")
leak = []
for w, c in allkw.items():
    if "_" not in w:
        continue
    pure_ascii = all(0x20 <= ord(ch) <= 0x7E for ch in w)
    if not pure_ascii:
        leak.append((w, c))
print("   共 %d 个（这些词含非 ASCII 字符，所以 R2 的「纯 ASCII」条件不成立）：" % len(leak))
for w, c in sorted(leak, key=lambda x: -x[1])[:12]:
    nz = [ch for ch in w if ord(ch) > 0x7E]
    print("      %-28s 覆盖 %-3d 条   非 ASCII 字符：%s" % (w, c, "".join(nz)))

# ---------- (2) 「专名·类别」形态 ----------
print("\n" + "=" * 78)
print("② 含「·」的入口词（编目名形态）—— 这类词覆盖度低，绕开 R1")
dot = [(w, c) for w, c in allkw.items() if "·" in w]
print("   共 %d 个，覆盖分布：%s" % (len(dot), dict(collections.Counter(c for _, c in dot))))
print("   其中含泛词（村庄/城堡/城镇）的：")
for w, c in sorted(dot):
    if any(g in w for g in ["村庄", "城堡", "城镇"]):
        print("      「%s」 覆盖 %d 条" % (w, c))

# ---------- (3) 裸类别词 ----------
print("\n" + "=" * 78)
print("③ 裸类别词作为 aliases 的条数（会被 R1 剔掉 ⇒ 条目名面看着宽、实际没用）")
for g in ["村庄", "城堡", "城镇", "村", "城"]:
    print("   「%s」：%d 条" % (g, allkw.get(g, 0)))

# ---------- (4) R1/R2/R3 分别剔掉多少词、多少条目带 ----------
print("\n" + "=" * 78)
print("④ 索引卫生三规则的剔除规模（按 448 条包现况）")
r1 = [(w, c) for w, c in allkw.items() if c > 40]
r2 = [(w, c) for w, c in allkw.items() if c <= 40 and "_" in w and all(0x20 <= ord(ch) <= 0x7E for ch in w)]
r3 = [(w, c) for w, c in allkw.items() if c <= 40 and w.lower().startswith("doc.")]
print("   R1（覆盖>40）剔除 %d 个词" % len(r1))
for w, c in sorted(r1, key=lambda x: -x[1])[:10]:
    print("      「%s」 覆盖 %d 条" % (w, c))
print("   R2（纯 ASCII＋下划线）另剔 %d 个词，共挂在 %d 条上" % (len(r2), sum(c for _, c in r2)))
print("   R3（doc. 前缀）另剔 %d 个词" % len(r3))
print("   ⇒ 入库的词 %d 个 / 全部 %d 个" % (len(allkw) - len(r1) - len(r2) - len(r3), len(allkw)))
