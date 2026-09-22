# -*- coding: utf-8 -*-
"""双写一致性核对：现役 WS vs 镜像 AO，逐档做**结构化**差异（不只看文本）。

用途：同步前先证「镜像没有独有内容」，否则覆盖 = 单向丢数据。
输出：差异路径分类计数 ＋ 每类样例 ＋ AO 独有项（最高危）全列。
"""
import io
import os
from collections import Counter, defaultdict

import yaml

WS = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"
AO = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out"


def deep_diff(a, b, path=""):
    out = []
    if isinstance(a, dict) and isinstance(b, dict):
        for k in sorted(set(a) | set(b)):
            if k not in b:
                out.append(("WS独有", path + "/" + k, a[k], None))
            elif k not in a:
                out.append(("AO独有", path + "/" + k, None, b[k]))
            else:
                out += deep_diff(a[k], b[k], path + "/" + k)
    elif isinstance(a, list) and isinstance(b, list):
        if [str(x) for x in a] != [str(x) for x in b]:
            sa, sb = set(str(x) for x in a), set(str(x) for x in b)
            if sa == sb:
                out.append(("list顺序", path, a, b))
            else:
                out.append(("list内容", path, sorted(sa - sb), sorted(sb - sa)))
    else:
        if a != b:
            out.append(("值不同", path, a, b))
    return out


def load(p):
    return yaml.safe_load(io.open(p, encoding="utf-8"))


files = [f for f in sorted(os.listdir(WS)) if f.endswith(".yaml")]
diff_files = []
kind_counter = Counter()
path_counter = Counter()
ao_only = []
val_diff = []
samples = defaultdict(list)

for f in files:
    pw, pa = os.path.join(WS, f), os.path.join(AO, f)
    if not os.path.exists(pa):
        kind_counter["AO缺文件"] += 1
        diff_files.append(f)
        continue
    try:
        dw, da = load(pw), load(pa)
    except Exception as ex:
        kind_counter["解析失败"] += 1
        continue
    if dw == da:
        continue
    diff_files.append(f)
    for kind, path, x, y in deep_diff(dw, da):
        kind_counter[kind] += 1
        path_counter[path] += 1
        if kind == "AO独有":
            ao_only.append((f, path, y))
        if kind == "值不同" and len(val_diff) < 12:
            val_diff.append((f, path, x, y))
        if len(samples[kind]) < 5:
            samples[kind].append((f, path, x, y))

print("现役档 %d；内容不等的档 %d" % (len(files), len(diff_files)))
print("=" * 74)
print("差异类型计数")
for k, v in kind_counter.most_common():
    print("  %-10s %d" % (k, v))
print()
print("差异字段路径（前 15）")
for k, v in path_counter.most_common(15):
    print("  %-34s %d" % (k, v))
print()
print("=" * 74)
print("🚨 AO 独有项（镜像有、现役没有）—— 全列")
if not ao_only:
    print("  （无）⇒ 镜像没有独有内容，覆盖安全")
else:
    for f, path, y in ao_only[:40]:
        print("  %-42s %-26s %s" % (f, path, str(y)[:60]))
print()
print("='值不同' 样例（前 12）")
for f, path, x, y in val_diff:
    print("  %-42s %-24s WS=%s | AO=%s" % (f, path, str(x)[:40], str(y)[:40]))
