# -*- coding: utf-8 -*-
"""由经济批探针机械生成暗面批探针。只替换 产物路径 / L2 文件名 / 中间产物名 / 批名。"""
import io

SRC = r"D:/AWAKE-Dev/AWAKE/tools/_eco_gate_probe_20260920.py"
DST = r"D:/AWAKE-Dev/AWAKE/tools/_dark_gate_probe_20260920.py"

t = io.open(SRC, encoding="utf-8").read()
subs = [
    ('compiled\\geo1-v20-goods', 'compiled\\geo1-v21-dark'),
    ('"_eco_A1_20260920.json", "_eco_A2B_20260920.json"', '"_dark_A_20260920.json"'),
    ('_eco_gate_%s_spec_20260920.json', '_dark_gate_%s_spec_20260920.json'),
    ('_eco_gate_%s_result_20260920.json', '_dark_gate_%s_result_20260920.json'),
    ('_eco_gate_%s_summary_20260920.json', '_dark_gate_%s_summary_20260920.json'),
    ('"""经济批（09-20）gate 可达性探针', '"""暗面批（09-20）gate 可达性探针'),
    ('python _eco_gate_probe_20260920.py', 'python _dark_gate_probe_20260920.py'),
]
for a, b in subs:
    assert a in t, "没找到待替换串: %s" % a
    t = t.replace(a, b)
io.open(DST, "w", encoding="utf-8", newline="\n").write(t)
print("已写出:", DST)
