# -*- coding: utf-8 -*-
"""剔除规则预览（2026-09-17）

Gap 5 的修法是「让兜底/内部标识关键词不参与检索」。落规则之前先算清**会剔掉什么、挡住什么**，
免得写完代码才发现要么剔不干净、要么把真关键词一起删了。

三条候选规则：
  R1 覆盖条目数 > 阈值      —— 与 term 索引已有的 `MaxTermDocumentFrequency=40` 同一条道理
  R2 形状像内部标识（纯 ASCII 且含下划线）—— `castle_village_EN1_2` / `heavy_round_shield`
  R3 前缀 `doc.`           —— 内部文档 id（编译器 09-16 已决定不再写进包，但出厂包是旧的）

⚠️ 只读预览，不改包、不改代码。
"""
import io
import json
import re

PKG = "ModuleData/Worldbook/packages/calradia/runtime.json"
FREQ_CEILING = 40
AScii_ID = re.compile(r"^[\x20-\x7e]+$")

INPUTS = ["doc", "economy", "geography", "entry", "war", "Goods", "HeadArmor",
          "sturgian", "castle_village", "盔", "军阀", "村", "村庄", "皮", "货",
          "军事", "力量", "斯特基亚"]


def load():
    with io.open(PKG, encoding="utf-8") as fh:
        return json.load(fh)["entries"]


def classify(entries):
    """给每个去重关键词打上该不该剔的标记，并算出它的覆盖条目数。"""
    from collections import Counter
    freq = Counter()
    for e in entries:
        for k in set(k for k in (e.get("keywords") or []) if k):
            freq[k] += 1
    drop_reason = {}
    for k, n in freq.items():
        if k.startswith("doc."):
            drop_reason[k] = "R3 doc."
        elif AScii_ID.match(k) and "_" in k:
            drop_reason[k] = "R2 内部标识"
        elif n > FREQ_CEILING:
            drop_reason[k] = "R1 覆盖 %d>%d" % (n, FREQ_CEILING)
    return freq, drop_reason


def hits(entries, text, drop_reason):
    """复刻字面侧双向子串，按需跳过被剔关键词。"""
    out = []
    low = text.lower()
    for e in entries:
        for k in (e.get("keywords") or []):
            if not k or k in drop_reason:
                continue
            kl = k.lower()
            if low in kl or kl in low:
                out.append((e["id"], k))
                break
    return out


def main():
    entries = load()
    freq, drop = classify(entries)

    print("== 会被剔掉的关键词（共 %d 个）==" % len(drop))
    by_reason = {}
    for k, r in drop.items():
        by_reason.setdefault(r.split()[0], []).append((k, freq[k]))
    for reason, items in sorted(by_reason.items()):
        样例 = "、".join("%s(覆盖 %d)" % (k, n) for k, n in sorted(items, key=lambda x: -x[1])[:6])
        print("  %s：%d 个   例：%s" % (reason, len(items), 样例))
    print()

    print("== 剔前 / 剔后，每条输入的命中条目数 ==")
    print("%-18s %8s %8s" % ("输入", "剔前", "剔后"))
    print("-" * 40)
    for text in INPUTS:
        a = len(hits(entries, text, {}))
        b = len(hits(entries, text, drop))
        flag = "  ← 变了" if a != b else ""
        print("%-18s %8d %8d%s" % (repr(text)[:16], a, b, flag))
    print()

    # 反向核查：剔掉的关键词里，有没有**只属于某一条**、且看着像真名的
    risky = [(k, freq[k]) for k, r in drop.items() if not k.startswith("doc.")
             and not (AScii_ID.match(k) and "_" in k)]
    print("== 只因 R1（覆盖过广）被剔的关键词（%d 个）==" % len(risky))
    for k, n in sorted(risky, key=lambda x: -x[1]):
        print("  %-12s 覆盖 %3d 条" % (k, n))
    print()
    print("（这些都是「分类名」性质：能被 40 条以上条目共用 ⇒ 按定义不具区分度，与 term 索引早已剔掉 `城堡/村庄` 同一条道理。）")


main()
