# -*- coding: utf-8 -*-
"""把 A 项（清掉 389 条聚落档上的裸类别词）的**删除原值**逐条导出成留痕文档。

为什么单独留痕：这一步是**删数据**（不是索引侧过滤），原值必须逐字可查 —— 以后谁要问
「德里亚特那条原来挂的是什么键」，不该只能翻 git diff。

对拍两边：
  旧值 = tools/_baseline/geo1-v12-runtime.json（v12 冻结副本，hash 6f796dfe…）
  新值 = ModuleData/Worldbook/packages/calradia/runtime.json（现挂）

运行：python -u tools/_make_corrections_removed_keywords_20260917.py [--write]
不加 --write 只打印摘要。
"""
import io
import json
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = r"D:/AWAKE-Dev/AWAKE"
OLD = os.path.join(ROOT, "tools/_baseline/geo1-v12-runtime.json")
NEW = os.path.join(ROOT, "ModuleData/Worldbook/packages/calradia/runtime.json")
OUT = os.path.join(ROOT, "docs/worldbook-migration/corrections_20260917/REMOVED-BARE-CATEGORY-KEYWORDS-20260917.md")


def load(p):
    return json.loads(io.open(p, encoding="utf-8").read())["entries"]


def loc(v):
    if isinstance(v, dict):
        return v.get("zh-CN") or v.get("zh") or v.get("en") or ""
    return v or ""


def main():
    write = "--write" in sys.argv
    old = {e["id"]: e for e in load(OLD)}
    new = {e["id"]: e for e in load(NEW)}
    rows, added_only = [], []
    for i in sorted(set(old) | set(new)):
        a = old.get(i)
        b = new.get(i)
        if b is None:
            rows.append((i, "（条目已不在新包里）", "", "", ""))
            continue
        if a is None:
            added_only.append(i)
            continue
        ka = list(a.get("keywords") or [])
        kb = list(b.get("keywords") or [])
        ra = [k for k in ka if k not in kb]
        ad = [k for k in kb if k not in ka]
        if not ra and not ad:
            continue
        rows.append((i, "、".join(ra) if ra else "（无删除）", "、".join(ad) if ad else "（无新增）",
                     json.dumps(ka, ensure_ascii=False), json.dumps(kb, ensure_ascii=False)))

    print("对比：%s（%d 条）→ %s（%d 条）" % (os.path.basename(OLD), len(old),
                                          os.path.basename(NEW), len(new)))
    print("keywords 有增删的条目 = %d" % len(rows))
    print("只在新包里出现的条目（新增档）= %d：%s" % (len(added_only), added_only))
    from collections import Counter
    c = Counter()
    for r in rows:
        for k in r[1].split("、"):
            c[k] += 1
    print("删除词分布：%s" % dict(c))
    print()
    for r in rows[:6]:
        print("  %-46s 删[%s]" % (r[0].replace("awake:entry:", ""), r[1]))

    if not write:
        print()
        print("（未加 --write，只做摘要）")
        return

    L = []
    L.append("# 留痕：A 项删掉的裸类别词（逐条原值）")
    L.append("")
    L.append("甲方 2026-09-17 裁决「B+A」里的 **A（清数据）**：把 389 条聚落条目上挂的"
             "**裸类别词**去掉 —— 它们是「谁都能沾」的键，既不指向任何东西，又给"
             "「德里亚特·村庄」这类手写复合词留了坑（272 个村庄的泛问被它劫走）。")
    L.append("")
    L.append("| 项 | 值 |")
    L.append("|---|---|")
    L.append("| 旧值（对拍基线） | `tools/_baseline/geo1-v12-runtime.json`（v12 冻结副本，sha256 `6f796dfe…`）|")
    L.append("| 新值（现挂） | `ModuleData/Worldbook/packages/calradia/runtime.json`（v13f，451 条）|")
    L.append("| keywords 有增删的条目 | %d |" % len(rows))
    L.append("| 其中新增档（只在新包里） | %d：%s |" % (len(added_only),
                                                    "、".join("`%s`" % x for x in added_only)))
    L.append("| 删除词分布 | %s |" % ("；".join("`%s` %d 条" % (k, v) for k, v in sorted(c.items()))))
    L.append("")
    L.append("⚠️ 这一步**只动 keywords**：三条概念词条的 `summary` 是本轮另一笔改动（见报告），"
             "不在本表内。")
    L.append("")
    L.append("## 逐条原值")
    L.append("")
    L.append("| # | 条目 | 删掉 | 新增 | 旧 keywords（原值逐字） | 新 keywords |")
    L.append("|---:|---|---|---|---|---|")
    for n, (i, rm, ad, ka, kb) in enumerate(rows, 1):
        L.append("| %d | `%s` | %s | %s | %s | %s |"
                 % (n, i.replace("awake:entry:", ""), rm, ad,
                    ka.replace("|", "\\|"), kb.replace("|", "\\|")))
    L.append("")
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    io.open(OUT, "w", encoding="utf-8", newline="\n").write("\n".join(L))
    print()
    print("写入 %s（%d 行）" % (os.path.relpath(OUT, ROOT), len(L)))


if __name__ == "__main__":
    main()
