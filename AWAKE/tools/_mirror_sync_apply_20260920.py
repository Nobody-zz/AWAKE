# -*- coding: utf-8 -*-
"""镜像同步：以现役为准，覆盖镜像（仅覆盖内容不等的档）。

前置（已验，见 _mirror_sync_check_20260920.py）：差异只有
  /entity_ids 301 档（镜像缺）、/title/zh-CN 1 档（kettle 镜像落后）、同档 /aliases/zh-CN
且 **镜像独有项 = 无** ⇒ 覆盖不会丢任何内容。

先备份当前镜像全量（镜像工作区已有未提交改动，git checkout 回不到「同步前」状态）。
"""
import io
import os
import shutil
from collections import Counter

import yaml

REPO = r"D:/AWAKE-Dev/AWAKE"
WS = os.path.join(REPO, r"tools/worldbook-studio/workspace/full-geo1/authoring")
AO = os.path.join(REPO, r"docs/worldbook-migration/projection/authoring-out")
BK = os.path.join(REPO, r"docs/worldbook-migration/projection/_archive-alias-tighten-20260920/AO-before-mirror-sync")

os.makedirs(BK, exist_ok=True)
copied, same, missing = [], 0, []
for f in sorted(os.listdir(WS)):
    if not f.endswith(".yaml"):
        continue
    pw, pa = os.path.join(WS, f), os.path.join(AO, f)
    if not os.path.exists(pa):
        missing.append(f)
        continue
    a = io.open(pw, "rb").read()
    b = io.open(pa, "rb").read()
    if a == b:
        same += 1
        continue
    # 备份镜像旧版
    shutil.copy2(pa, os.path.join(BK, f))
    # 覆盖
    shutil.copy2(pw, pa)
    copied.append(f)

print("现役档备份前：镜像旧版已存 %d 档 -> %s" % (len(copied), BK))
print("覆盖 %d 档；本来一致 %d 档；镜像缺文件 %d %s" % (len(copied), same, len(missing), missing[:5]))

# 复验：逐字节比对
bad = []
for f in sorted(os.listdir(WS)):
    if not f.endswith(".yaml"):
        continue
    pw, pa = os.path.join(WS, f), os.path.join(AO, f)
    if not os.path.exists(pa):
        bad.append(f + " (缺)")
        continue
    if io.open(pw, "rb").read() != io.open(pa, "rb").read():
        bad.append(f)
print("复验：逐字节不一致的档 %d %s" % (len(bad), bad[:5]))
