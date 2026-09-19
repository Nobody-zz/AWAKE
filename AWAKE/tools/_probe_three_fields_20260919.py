# -*- coding: utf-8 -*-
"""量作者层三个字段的实际内容：status / era / lifecycle（482 份 yaml）。

只读。用于回答"这三个字段的含义与对应内容"。
"""
import glob
import io
import os
from collections import Counter

import yaml

ROOT = r"D:\AWAKE-Dev\AWAKE\docs\worldbook-migration\projection\authoring-out"
files = sorted(glob.glob(os.path.join(ROOT, "*.yaml")))
print("作者层文件数:", len(files))

status_c = Counter()
era_key_c = Counter()
era_cert_c = Counter()
life_key_c = Counter()
life_vals = Counter()
era_year = 0
no_era_key = 0
life_samples = []
era_samples = []
bad = []

for path in files:
    try:
        doc = yaml.safe_load(io.open(path, encoding="utf-8"))
    except Exception as exc:          # noqa: BLE001
        bad.append((os.path.basename(path), str(exc)[:80]))
        continue
    if not isinstance(doc, dict):
        bad.append((os.path.basename(path), "not a mapping"))
        continue

    status_c[str(doc.get("status"))] += 1

    era = doc.get("era")
    if isinstance(era, dict):
        era_key_c[str(era.get("key"))] += 1
        era_cert_c[str(era.get("certainty"))] += 1
        if era.get("start_year") is not None or era.get("end_year") is not None:
            era_year += 1
        if len(era_samples) < 6:
            era_samples.append((os.path.basename(path), era))
    else:
        no_era_key += 1

    life = doc.get("lifecycle")
    if isinstance(life, dict):
        for k, v in life.items():
            life_key_c[k] += 1
            if k in ("valid_from", "valid_to", "event_id", "revision"):
                life_vals[k + "=" + str(v)] += 1
        if len(life_samples) < 8:
            life_samples.append((os.path.basename(path), life))
    else:
        life_key_c["(无 lifecycle 字段)"] += 1

print()
print("=== status 取值分布 ===")
for k, v in status_c.most_common():
    print(f"  {k:20s} {v}")
print()
print("=== era ===")
print("  无 era（或缺 key 的对象）:", no_era_key)
print("  era.key 取值:", dict(era_key_c.most_common(12)))
print("  era.certainty 取值:", dict(era_cert_c))
print("  带 start_year/end_year 的条数:", era_year)
print("  样例:")
for name, era in era_samples:
    print("   ", name, "->", era)
print()
print("=== lifecycle ===")
print("  各键出现次数:", dict(life_key_c))
print("  值的样子:", dict(life_vals.most_common(12)))
print("  样例:")
for name, life in life_samples:
    print("   ", name, "->", life)
print()
print("=== 读取失败 ===", len(bad))
for name, err in bad[:5]:
    print("  ", name, err)
