# -*- coding: utf-8 -*-
"""落盘后核对：① 现役/镜像 21 档逐字节一致；② 只这 21 档变化，其余档字节不动。"""
import glob
import hashlib
import io
import json
import os
import sys

REPO = r"D:\AWAKE-Dev\AWAKE"
LIVE = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1\authoring")
MIRR = os.path.join(REPO, r"docs\worldbook-migration\projection\authoring-out")
J = {}
for n in (1, 2, 3):
    J.update(json.load(io.open(os.path.join(MIRR, "_status_l2_towns_%d_20260919.json" % n), encoding="utf-8")))
targets = sorted(k for k in J if not k.startswith("_"))

diffs = []
for f in targets:
    a = io.open(os.path.join(LIVE, f), "rb").read()
    b = io.open(os.path.join(MIRR, f), "rb").read()
    if a != b:
        diffs.append(f)
print("目标档:", len(targets))
print("现役/镜像不一致:", len(diffs), diffs[:5])

# 全量：逐字节比对 live 与 mirror 的所有同名档（镜像可能多/少档）
lv = {os.path.basename(p): p for p in glob.glob(os.path.join(LIVE, "*.yaml"))}
mi = {os.path.basename(p): p for p in glob.glob(os.path.join(MIRR, "*.yaml"))}
common = sorted(set(lv) & set(mi))
print("现役档:", len(lv), "镜像档:", len(mi), "同名:", len(common))
mismatch = [f for f in common
            if io.open(lv[f], "rb").read() != io.open(mi[f], "rb").read()]
print("同名但字节不同:", len(mismatch))
only_live = sorted(set(lv) - set(mi))
only_mirr = sorted(set(mi) - set(lv))
print("只在现役:", len(only_live), only_live[:5])
print("只在镜像:", len(only_mirr), only_mirr[:5])
