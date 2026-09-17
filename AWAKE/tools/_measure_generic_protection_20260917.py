# -*- coding: utf-8 -*-
"""B 项**判据换向**：上一轮三个方案（段继承/题面继承/关键词继承）都会连坐 67 个正当的 `X Castle`。

为什么它们错：
    「专名 + 类别词」这个形态**本身就是全库正当写法** —— 英文侧 67 个城堡条目全都叫
    `Akiser Castle` / `Lavenia Castle`……中文侧 `德里亚特·村庄` 是同一种写法。
    剔掉它们 = 把正当入口词当病灶打，损失召回。

那病灶到底是什么？（把两次观测摆在一起看）
    · 不剔泛词时：查「村庄」→ 返回 272 条（过匹配）。
    · 剔了泛词＋有复合词时：查「村庄」→ 返回 1 条 `deriat`（劫持，唯一命中，理直气壮答错）。
    ⇒ **病不在"复合词"，在"裸泛词被剔了、复合词留下"** —— 泛问词从索引里消失，
      于是唯一含它的那个复合词成了主人。

⇒ B 的最小正确判据：**给"泛问词"上保护，不让它被包进更长的词里去顶替它。**

    IsExcluded(K):
      if KeywordDf[K] > 40                  -> true   (原 R1)
      if K 起头 doc. / 纯 ASCII 含下划线      -> true   (原 R2 / R3)
      if K 的真子串里含 G 中任一整词          -> true   (新：泛问词保护)
    其中 G = **类别/概念词条自己的入口词**（中文）。
    ⚠️ 判据是「**真子串包含 G 的整词**」，不是「含 G 的 2-gram」——
       `拉文尼亚` 含 2-gram `尼亚`，但它**不含** `村庄` ⇒ 不误伤。
    ⚠️ G 里只放**中文**入口词：英文侧 `X Castle` 是全库正当写法，放进去会连坐 67 条。

本脚本：
    ① 用**未上线**的近似 G（三条概念词条手写的那 9 个中文入口词）量"额外剔几个 / 误伤几个"；
    ② 变异检验（阳性 `德里亚特·村庄` / `厄尔凡尼亚·村庄`；阴性 `X Castle` / `拉文尼亚` / 湖泊类）；
    ③ 穷举当前包里"哪些关键词真子串含 G"，逐个人工看一眼是不是真劫持。
"""
import collections
import io
import json
import sys

sys.stdout.reconfigure(encoding="utf-8")

PKG = "ModuleData/Worldbook/packages/calradia/runtime.json"
MAXDF = 40
DROP_EXACT = {"村庄", "城堡", "城镇"}
DROP_VALUES = {"德里亚特·村庄"}

# 三条概念词条（settlement-types-*）即将上线的中文入口词
G = ["村庄", "村子", "村落", "城堡", "城砦", "堡垒", "城镇", "镇子", "城市"]

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


# 模拟 A
sim = []
for e in entries:
    sim.append((e, [w for w in kw_of(e) if w not in DROP_EXACT and w not in DROP_VALUES]))

kwdf = collections.Counter()
for e, kw in sim:
    for w in set(w.strip() for w in kw if w and w.strip()):
        kwdf[w] += 1

allkw = sorted(kwdf)
base = {k for k in allkw if kwdf[k] > MAXDF or k.lower().startswith("doc.") or looks_internal(k)}


def protected_substring(k):
    """K 的真子串里含 G 中任一整词 -> 返回 (词, 位置)。"""
    L = len(k)
    for g in G:
        if len(g) > L:
            continue
        idx = k.find(g)
        while idx >= 0:
            if not (idx == 0 and idx + len(g) == L):      # 排除自身
                return (g, idx)
            idx = k.find(g, idx + 1)
    return None


print("模拟 A 后：关键词全集 %d；旧规则剔 %d；留下 %d"
      % (len(allkw), len(base), len(allkw) - len(base)))
print("G（泛问词保护表，%d 个）：%s" % (len(G), " ".join(G)))
print()

extra = []
for k in allkw:
    if k in base:
        continue
    hit = protected_substring(k)
    if hit:
        extra.append((k, kwdf[k], hit))

print("=" * 78)
print("B''（泛问词保护）比旧规则**额外**剔掉的关键词：%d 个" % len(extra))
for k, c, (g, i) in extra:
    print("   「%s」(覆盖 %d)  含泛词「%s」@%d" % (k, c, g, i))
if not extra:
    print("   （无）")

print()
print("=" * 78)
print("变异检验")
CASES = [("阳性-必须剔", "德里亚特·村庄"), ("阳性-必须剔", "厄尔凡尼亚·村庄"),
         ("阳性-必须剔", "阿特费尼亚城堡"), ("阳性-必须剔", "某某城市"),
         ("阴性-必须留", "村庄"), ("阴性-必须留", "城堡"), ("阴性-必须留", "城镇"),
         ("阴性-必须留", "村庄的产出"), ("阴性-必须留", "拉文尼亚"),
         ("阴性-必须留", "塔奈西斯湖"), ("阴性-必须留", "帝国之湖"),
         ("阴性-必须留", "拉科尼斯湖·水为何变红"),
         ("阴性-必须留", "Akiser Castle"), ("阴性-必须留", "Lavenia Castle"),
         ("阴性-必须留", "Ab Comer Castle")]
for tag, k in CASES:
    hit = protected_substring(k)
    inbase = k in base
    verdict = "剔" if (inbase or hit) else "留"
    why = "旧规则" if inbase else ("含泛词「%s」" % hit[0] if hit else "—")
    ok = ("✔" if (tag.startswith("阳性") and verdict == "剔")
          else "✔" if (tag.startswith("阴性") and verdict == "留") else "❌")
    print("   %-12s %-24s → %s（%s） %s" % (tag, k, verdict, why, ok))

print()
print("=" * 78)
print("反查：G 自己的 9 个词会不会被剔（必须全留，否则泛问没人接）")
for g in G:
    hit = protected_substring(g)
    ok = "✔" if not (g in base or hit) else "❌"
    print("   %-6s KeywordDf(模拟后)=%-3d 旧规则剔=%-5s 新规则剔=%-5s %s"
          % (g, kwdf.get(g, 0), g in base, bool(g in base or hit), ok))

print()
print("=" * 78)
print("抽样：随机 20 个覆盖=1、长度>=3 的专名关键词，看误伤几个")
import random
random.seed(11)
pool = [k for k in allkw if kwdf[k] == 1 and len(k) >= 3 and not looks_internal(k)]
bad = 0
for k in random.sample(pool, 20):
    hit = protected_substring(k)
    if hit:
        bad += 1
    print("   %-26s %s %s" % (k, "❌ 被剔" if hit else "✔ 保留", hit or ""))
print("   20 个里被误伤 %d 个" % bad)
