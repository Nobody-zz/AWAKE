# -*- coding: utf-8 -*-
"""盘点：在挂包里现在有哪些条目、各域各子类各多少（只读，2026-09-18）。

用途：回答「新知识词条之前做到哪里了」。判据不靠记忆，靠包本体数出来。

口径：读 `ModuleData/Worldbook/packages/calradia/runtime.json`（在挂上线件）。
id 形状 `awake:entry:<domain>.<subdomain>-<slug>` ⇒ 按 `domain.subdomain` 分组。

⚠️ 只读。不写任何包、不改任何索引。
"""
import collections
import json
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PKG = os.path.join(ROOT, "ModuleData", "Worldbook", "packages", "calradia", "runtime.json")


def loc(value):
    if isinstance(value, dict):
        return value.get("zh-CN") or value.get("zh") or value.get("en") or ""
    return value or ""


def main():
    with open(PKG, encoding="utf-8") as handle:
        entries = json.load(handle)["entries"]
    print("PKG = %s" % PKG)
    print("条目总数 = %d" % len(entries))
    print()

    by_domain = collections.Counter()
    by_group = collections.Counter()
    names = collections.defaultdict(list)
    for entry in entries:
        eid = entry.get("id", "")
        short = eid.replace("awake:entry:", "")
        if "." in short:
            domain, rest = short.split(".", 1)
        else:
            domain, rest = short, ""
        sub = rest.split("-")[0] if rest else "(无)"
        by_domain[domain] += 1
        by_group["%s.%s" % (domain, sub)] += 1
        names["%s.%s" % (domain, sub)].append(loc(entry.get("title")))

    print("=== 按域 ===")
    for domain, count in by_domain.most_common():
        print("  %-10s %4d" % (domain, count))
    print()

    print("=== 按域.子类（id 前缀） ===")
    for group, count in by_group.most_common():
        sample = "、".join(names[group][:4])
        print("  %-26s %4d   例：%s" % (group, count, sample))
    print()
    print("PROBE_DONE")


main()
