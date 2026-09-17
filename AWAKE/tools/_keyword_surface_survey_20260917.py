# -*- coding: utf-8 -*-
"""关键词覆盖面普查（2026-09-17）

红测显示 `doc`/`geography`/`economy`/`castle_village` 这类输入能炸出一大片。
根因不在匹配规则，而在**索引里有哪些键**。这张普查回答三件事：

  ① 覆盖条目最多的关键词是哪些（过匹配面有多大）；
  ② 这些键的**形状**有几类（内部 id / 原始游戏名 / 正常可读名）；
  ③ 按形状定一条能落地的规则，而不是删词。

⚠️ 只读，不改任何包。
"""
import io
import json
import re
from collections import Counter

PKG = "ModuleData/Worldbook/packages/calradia/runtime.json"

SHAPES = [
    ("doc.<域>.<名>",      re.compile(r"^doc\.[a-z]+\.")),
    ("带下划线的小写串",      re.compile(r"^[a-z][a-z0-9]*(_[a-z0-9]+)+$")),
    ("首字母大写驼峰",       re.compile(r"^[A-Z][a-zA-Z0-9]*$")),
    ("含点号（多段）",       re.compile(r"\.")),
    ("全中文",             re.compile(r"^[\u4e00-\u9fff·]+$")),
    ("含空格（西文短语）",    re.compile(r"[A-Za-z] [A-Za-z]")),
]


def shape_of(k):
    for name, pat in SHAPES:
        if pat.search(k):
            return name
    return "其他"


def main():
    with io.open(PKG, encoding="utf-8") as fh:
        entries = json.load(fh)["entries"]

    freq = Counter()
    per_entry = 0
    shapes = Counter()
    shape_keys = {}
    for e in entries:
        ks = [k for k in (e.get("keywords") or []) if k]
        per_entry += len(ks)
        for k in ks:
            freq[k] += 1
            s = shape_of(k)
            shapes[s] += 1
            shape_keys.setdefault(s, []).append(k)

    print("条目 %d，关键词总数 %d（平均 %.1f 条/条目），去重后 %d"
          % (len(entries), per_entry, per_entry / len(entries), len(freq)))
    print()
    print("== 形状分布（按出现次数）==")
    for s, n in shapes.most_common():
        print("  %-18s %5d 条   例：%s" % (s, n, "、".join(shape_keys[s][:3])[:60]))
    print()
    print("== 覆盖条目最多的 20 个关键词（＝过匹配面）==")
    for k, n in freq.most_common(20):
        print("  %5d 条  %-28s [%s]" % (n, k[:26], shape_of(k)))
    print()
    for lo, hi in ((2, 5), (6, 20), (21, 50), (51, 452)):
        hit = [k for k, n in freq.items() if lo <= n <= hi]
        print("  覆盖 %3d–%3d 条的关键词：%4d 个 %s" % (lo, hi, len(hit),
              ("例：" + "、".join(hit[:4])) if hit else ""))
    print()
    doc = [k for k in freq if k.startswith("doc.")]
    print("`doc.` 前缀关键词：%d 个（覆盖条目 %d）" % (len(doc), len(entries)))
    unders = [k for k in freq if shape_of(k) == "带下划线的小写串"]
    print("下划线小写串（原始游戏名类）：%d 个，其中覆盖≥2 条的：%s"
          % (len(unders), "、".join("%s(%d)" % (k, freq[k]) for k in sorted(unders, key=lambda x: -freq[x])[:6])))


main()
