# -*- coding: utf-8 -*-
"""跑 studio CLI 的 validate，把工作区校验报告抓下来（中文输出，两种编码都试）。"""
import os
import subprocess
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = r"D:/AWAKE-Dev/AWAKE"
WS_REL = "tools/worldbook-studio/workspace/full-geo1"
DLL = os.path.join(
    ROOT,
    "tools/worldbook-studio/src/Awake.WorldbookStudio.Cli/bin/Release/net10.0/worldbook-studio.dll",
)

r = subprocess.run(["dotnet", DLL, "validate", "--workspace", WS_REL],
                   capture_output=True, cwd=ROOT)
print("rc=%d" % r.returncode)
for name, enc in [("utf-8", "utf-8"), ("gbk", "gbk")]:
    try:
        txt = r.stdout.decode(enc)
        ok = True
    except UnicodeDecodeError:
        txt, ok = r.stdout.decode(enc, "replace"), False
    print("==== stdout as %s（自洽=%s）====" % (name, ok))
    print(txt[:6000])
    break
print("==== stderr ====")
print(r.stderr.decode("utf-8", "replace")[:3000])
out_path = os.path.join(ROOT, "tools", "_validate_report_v13_20260917.txt")
open(out_path, "wb").write(r.stdout)
print("原始字节已存：%s（%d 字节）" % (out_path, len(r.stdout)))
