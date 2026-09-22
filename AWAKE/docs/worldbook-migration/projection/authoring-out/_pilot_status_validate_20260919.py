# -*- coding: utf-8 -*-
"""把 3 份试点档放进一个空工作区跑 validate，只抓 schema 层问题。

为什么要搭空工作区：`validate` 不吃 --path，恒校验全工作区；丢进 full-geo1 会被
482 档的既有诊断淹没，看不出试点档本身过不过。
（`WB-SOURCE-*` 只跟来源登记有关，空工作区里若出现按需另判。）
"""
import io
import os
import shutil
import subprocess
import sys

REPO = r"D:\AWAKE-Dev\AWAKE"
AUTH = os.path.join(REPO, r"docs\worldbook-migration\projection\authoring-out")
PILOT = os.path.join(REPO, r"docs\worldbook-migration\pilot-status-20260919")
SRC = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1\authoring\sources")
DLL = os.path.join(REPO, r"tools\worldbook-studio\src\Awake.WorldbookStudio.Cli\bin\Release\net10.0\worldbook-studio.dll")
W = os.path.join(os.environ.get("TEMP", r"C:\Windows\Temp"), "awake-pilot-validate-20260919")

want_srcs = ["source-game-villages-desc-battania.yaml",
             "source-game-settlements-desc.yaml"]

if os.path.isdir(W):
    shutil.rmtree(W)
os.makedirs(os.path.join(W, "authoring", "sources"))

for f in sorted(os.listdir(PILOT)):
    if f.endswith(".yaml"):
        shutil.copy(os.path.join(PILOT, f), os.path.join(W, "authoring", f))

for f in want_srcs:
    shutil.copy(os.path.join(SRC, f), os.path.join(W, "authoring", "sources", f))

# 来源登记指向的 locator_root 也要在
import yaml
for f in want_srcs:
    d = yaml.safe_load(io.open(os.path.join(SRC, f), encoding="utf-8"))
    root = d.get("locator_root")
    shutil.copy(os.path.join(SRC, root), os.path.join(W, "authoring", "sources", root))

print("工作区:", W)
for dirpath, _dirs, files in os.walk(W):
    for n in sorted(files):
        print("  ", os.path.relpath(os.path.join(dirpath, n), W))

print()
print("=== validate ===")
p = subprocess.run(["dotnet", DLL, "validate", "--workspace", W],
                   cwd=REPO, capture_output=True)
out = p.stdout.decode("utf-8", "replace")
err = p.stderr.decode("utf-8", "replace")
print("rc =", p.returncode)
print(out)
if err.strip():
    print("--- stderr ---")
    print(err)
