# -*- coding: utf-8 -*-
"""变异检验：证 21 档那次「Valid: true」有分辨力。不看红不算验过。

两处变异（都在落盘后的空工作区副本上做，原档只读）：
  M1 把一条 quote 改一个字（原文里定位不到）—— 期望 WB-SOURCE-001
  M2 把一个 kind 写成不在 enum 里的值       —— 期望 WB-SCHEMA-*
跑完按字节还原。
"""
import io
import os
import subprocess

REPO = r"D:\AWAKE-Dev\AWAKE"
DLL = os.path.join(REPO, r"tools\worldbook-studio\src\Awake.WorldbookStudio.Cli\bin\Release\net10.0\worldbook-studio.dll")
W = os.path.join(os.environ.get("TEMP", r"C:\Windows\Temp"), "awake-status-validate-20260919")
TARGET = os.path.join(W, "authoring", "villages-diantogmail.yaml")

MUT = [
    ("M1 quote 改一个字（原文定位不到）", "这里的村民会从湖边收集黏土", "这里的村民会从湖滨收集黏土"),
    ("M2 kind 写成 enum 外的值", "  kind: rumor\n", "  kind: canon\n"),
]


def run():
    p = subprocess.run(["dotnet", DLL, "validate", "--workspace", W], cwd=REPO, capture_output=True)
    out = p.stdout.decode("utf-8", "replace")
    err = p.stderr.decode("utf-8", "replace")
    codes = sorted({t for t in out.replace('"', " ").replace(",", " ").split() if t.startswith("WB-")})
    return p.returncode, codes, out, err


rc, codes, out, _ = run()
print("基线（未变异）: rc=%d WB 码=%s\n" % (rc, codes))

for label, old, new in MUT:
    orig = io.open(TARGET, encoding="utf-8", newline="").read()
    if old not in orig:
        print("== %s\n   锚点找不到，跳过\n" % label)
        continue
    io.open(TARGET, "w", encoding="utf-8", newline="").write(orig.replace(old, new, 1))
    rc, codes, out, err = run()
    io.open(TARGET, "w", encoding="utf-8", newline="").write(orig)
    restored = io.open(TARGET, encoding="utf-8", newline="").read() == orig
    print("== %s" % label)
    print("   rc=%d  还原精确=%s  WB 码=%s" % (rc, restored, codes))
    for ln in out.splitlines():
        s = ln.strip()
        if "WB-" in s and len(s) < 300:
            print("   ", s[:200])
    print()

rc, codes, out, _ = run()
print("还原后复跑: rc=%d WB 码=%s" % (rc, codes))
