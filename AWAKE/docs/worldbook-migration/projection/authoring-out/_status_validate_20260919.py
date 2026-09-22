# -*- coding: utf-8 -*-
"""把 21 档落盘的聚落放进一个空工作区跑 validate，只抓 schema 层问题。

`validate` 不吃 --path，恒校验全工作区；丢进 full-geo1 会被既有诊断淹掉。
"""
import io
import json
import os
import shutil
import subprocess

import yaml

REPO = r"D:\AWAKE-Dev\AWAKE"
AUTH = os.path.join(REPO, r"docs\worldbook-migration\projection\authoring-out")
SRC = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1\authoring\sources")
DLL = os.path.join(REPO, r"tools\worldbook-studio\src\Awake.WorldbookStudio.Cli\bin\Release\net10.0\worldbook-studio.dll")
W = os.path.join(os.environ.get("TEMP", r"C:\Windows\Temp"), "awake-status-validate-20260919")

J = {}
for n in (1, 2, 3):
    J.update(json.load(io.open(os.path.join(AUTH, "_status_l2_towns_%d_20260919.json" % n), encoding="utf-8")))
targets = sorted(k for k in J if not k.startswith("_"))

used = set()
for t in targets:
    doc = yaml.safe_load(io.open(os.path.join(AUTH, t), encoding="utf-8"))
    for s in doc.get("sources") or []:
        used.add(s["source_id"])

if os.path.isdir(W):
    shutil.rmtree(W)
os.makedirs(os.path.join(W, "authoring", "sources"))
for f in targets:
    shutil.copy(os.path.join(AUTH, f), os.path.join(W, "authoring", f))

copied = []
for f in sorted(os.listdir(SRC)):
    if not f.startswith("source-") or not f.endswith(".yaml"):
        continue
    d = yaml.safe_load(io.open(os.path.join(SRC, f), encoding="utf-8"))
    if d.get("source_id") in used:
        shutil.copy(os.path.join(SRC, f), os.path.join(W, "authoring", "sources", f))
        copied.append(f)
        root = d.get("locator_root")
        if root and os.path.exists(os.path.join(SRC, root)):
            shutil.copy(os.path.join(SRC, root), os.path.join(W, "authoring", "sources", root))

print("档:", len(targets), " 来源登记:", copied)
print("用到 source_id:", sorted(used))
print()
p = subprocess.run(["dotnet", DLL, "validate", "--workspace", W], cwd=REPO, capture_output=True)
out = p.stdout.decode("utf-8", "replace")
err = p.stderr.decode("utf-8", "replace")
print("rc =", p.returncode)
print(out)
if err.strip():
    print("--- stderr ---")
    print(err)
