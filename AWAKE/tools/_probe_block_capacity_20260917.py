# -*- coding: utf-8 -*-
"""量：一次问答实际能装进几条条目（只读，2026-09-17）。

起因 —— 甲方问「为什么一次只能命中一个词条？不能多几个？」

先看代码事实：`WorldKnowledgeQueryService.Query()`（`src/WorldKnowledgeQueryService.cs:82-106`）
是 `foreach (候选)` 全遍历，一条条往 `StringBuilder` 拼，**装不下才 `break`**；
比较的是字节数（`:98`），上限来自 `query.MaximumBytes`（NPC 对话传的是
`KnowledgeConstants.MaximumRetrievedBlockBytes = 4096`，见 `AwakeRuntime.cs:78`、`NpcDialogueService.cs:1060`）。

⇒ **限制是「4096 字节」，不是「几条」。** 那么一条块平均占多少字节，就是本脚本要量的事。

块格式见 `FormatEntry`（`:506`）：`【标题】\n综述\n选中档位的正文`。

口径：每条条目取**它最短的一档正文**（最低身份能拿到的那一档，块最小的情形），
拼出块、按 UTF-8 算字节；再看 4096 能装几条。

⚠️ 只读。不写任何包、不改任何索引。
"""
import json
import os
import statistics
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PKG = os.path.join(ROOT, "ModuleData", "Worldbook", "packages", "calradia", "runtime.json")
BUDGET = 4096


def txt(value):
    if isinstance(value, str):
        return value
    if isinstance(value, dict):
        for key in ("zh-CN", "zh", "en"):
            if isinstance(value.get(key), str):
                return value[key]
        for item in value.values():
            if isinstance(item, str):
                return item
    return ""


def block_of(entry, expression_text):
    title = txt(entry.get("title"))
    summary = txt(entry.get("summary"))
    head = "【%s】" % title
    parts = [head]
    if summary:
        parts.append(summary)
    parts.append(expression_text)
    return "\n".join(parts)


def main():
    with open(PKG, "r", encoding="utf-8") as handle:
        data = json.load(handle)
    entries = data.get("entries") or []
    print("PKG = %s" % PKG)
    print("条目数 = %d | 装块预算 = %d 字节" % (len(entries), BUDGET))
    print()

    sizes = []
    for entry in entries:
        texts = [txt(x.get("text")) for x in (entry.get("expressions") or []) if x.get("enabled", True)]
        texts = [t for t in texts if t]
        if not texts:
            continue
        sizes.append((len(block_of(entry, min(texts, key=len)).encode("utf-8")), entry.get("id", "")))

    values = sorted(size for size, _ in sizes)
    print("=== 一条块的字节数（取每条最短档，共 %d 条） ===" % len(values))
    print("  最小 %d | P10 %d | 中位 %d | P90 %d | 最大 %d"
          % (values[0], values[len(values) // 10], int(statistics.median(values)),
             values[len(values) * 9 // 10], values[-1]))
    print()

    median = int(statistics.median(values))
    print("=== 4096 字节能装几条 ===")
    print("  按中位块 %d 字节：约 %.1f 条" % (median, BUDGET / median))
    print("  按最大块 %d 字节：至少 %.1f 条" % (values[-1], BUDGET / values[-1]))
    print("  按最小块 %d 字节：最多 %.1f 条" % (values[0], BUDGET / values[0]))
    print()

    print("=== 逐条累加：装满 4096 需要几条（按中位块拼接） ===")
    total = 0
    count = 0
    for size in values:
        if total + size > BUDGET:
            break
        total += size
        count += 1
    print("  由小到大累加：%d 条 → %d 字节" % (count, total))
    total = 0
    count = 0
    for size in reversed(values):
        if total + size > BUDGET:
            break
        total += size
        count += 1
    print("  由大到小累加：%d 条 → %d 字节（最坏情形）" % (count, total))
    print()

    print("=== 实况：`西米拉堡是谁的城堡？` 那 3 条候选占多少 ===")
    targets = ["awake:entry:geography.castles-simira-castle",
               "awake:entry:geography.villages-simira",
               "awake:entry:geography.settlement-types-castle"]
    lookup = {e.get("id", ""): e for e in entries}
    used = 0
    for tid in targets:
        entry = lookup.get(tid)
        if entry is None:
            print("  %s —— 包里没有" % tid)
            continue
        texts = [txt(x.get("text")) for x in (entry.get("expressions") or []) if x.get("enabled", True)]
        texts = [t for t in texts if t]
        size = len(block_of(entry, min(texts, key=len)).encode("utf-8")) if texts else 0
        used += size + 2
        print("  %-52s 最短档块 %5d 字节" % (tid, size))
    print("  合计约 %d 字节 / 预算 %d ⇒ 用掉 %.0f%%，还剩 %.0f%%"
          % (used, BUDGET, used * 100.0 / BUDGET, (BUDGET - used) * 100.0 / BUDGET))
    print()
    print("PROBE_DONE")


main()
