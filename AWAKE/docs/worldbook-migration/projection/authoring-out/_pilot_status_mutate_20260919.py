# -*- coding: utf-8 -*-
"""变异检验：证上面那次「Valid: true」有分辨力。

不看红不算验过。两处变异：
  M1 把一个 quote 改一个字（原文里定位不到）—— 期望 WB-SOURCE-001
  M2 把一个 kind 写成不在 enum 里的值       —— 期望 WB-SCHEMA-*
两处都只动试点档、只动一处；跑完按字节还原。
"""
import io
import os
import shutil
import subprocess

REPO = r"D:\AWAKE-Dev\AWAKE"
PILOT = os.path.join(REPO, r"docs\worldbook-migration\pilot-status-20260919")
SRC = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1\authoring\sources")
DLL = os.path.join(REPO, r"tools\worldbook-studio\src\Awake.WorldbookStudio.Cli\bin\Release\net10.0\worldbook-studio.dll")
W = os.path.join(os.environ.get("TEMP", r"C:\Windows\Temp"), "awake-pilot-validate-20260919")
TARGET = os.path.join(W, "authoring", "villages-diantogmail.yaml")

MUT = [
    ("M1 quote 改一个字（定位不到）", "这里的村民会从湖边收集黏土，供附近的工匠使用。",
     "这里的村民会从湖滨收集黏土，供附近的工匠使用。"),
    ("M2 kind 写成 enum 外的值", "  kind: rumor\n", "  kind: canon\n"),
]


def run():
    p = subprocess.run(["dotnet", DLL, "validate", "--workspace", W], cwd=REPO, capture_output=True)
    out = p.stdout.decode("utf-8", "replace")
    err = p.stderr.decode("utf-8", "replace")
    codes = sorted({t for t in out.replace('"', " ").split() if t.startswith("WB-")})
    return p.returncode, codes, out, err


for label, old, new in MUT:
    orig = io.open(TARGET, encoding="utf-8", newline="").read()
    assert old in orig, "变异锚点找不到: " + label
    io.open(TARGET, "w", encoding="utf-8", newline="").write(orig.replace(old, new, 1))
    rc, codes, out, err = run()
    back = io.open(TARGET, "w", encoding="utf-8", newline="")
    back.write(orig)
    back.close()
    restored = io.open(TARGET, encoding="utf-8", newline="").read() == orig
    print("== %s" % label)
    print("   rc=%d  还原精确=%s  WB 码=%s" % (rc, restored, codes))
    for ln in out.splitlines():
        if "WB-" in ln or '"code"' in ln or "message" in ln.lower():
            print("   ", ln.strip()[:200])
    if err.strip():
        print("   stderr:", err.strip()[:200])
    print()

rc, codes, out, err = run()
print("还原后复跑: rc=%d WB 码=%s" % (rc, codes))
