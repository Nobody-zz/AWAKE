# -*- coding: utf-8 -*-
"""跑 studio CLI 的 validate 并按错误码 / 文件汇总（重点看三条概念档还有没有 WB-SOURCE-001）。"""
import collections
import io
import json
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

r = subprocess.run(["dotnet", DLL, "validate", "--workspace", WS_REL], capture_output=True, cwd=ROOT)
raw = r.stdout.decode("utf-8", "replace")
report = json.loads(raw)
print("rc=%d  Valid=%s  诊断 %d 条" % (r.returncode, report.get("Valid"), len(report.get("Diagnostics") or [])))

by_code = collections.Counter()
by_file = collections.Counter()
sev = collections.Counter()
for d in report.get("Diagnostics") or []:
    code = d.get("Code") or "?"
    path = (d.get("Path") or "").replace("\\", "/").split("/")[-1]
    by_code[code] += 1
    by_file[path] += 1
    sev[d.get("Severity") or "?"] += 1

print("严重度：%s" % dict(sev))
print("按错误码：")
for k, v in by_code.most_common():
    print("   %-16s %d" % (k, v))
print("按文件（前 12）：")
for k, v in by_file.most_common(12):
    print("   %-46s %d" % (k, v))

print()
print("== 三条概念档相关诊断 ==")
hits = [d for d in (report.get("Diagnostics") or []) if "settlement-types" in (d.get("Path") or "")]
if not hits:
    print("   无 ✔")
for d in hits[:20]:
    print("   %s %s | %s" % (d.get("Code"), os.path.basename(d.get("Path") or ""), d.get("Message")))
print("   共 %d 条" % len(hits))

open(os.path.join(ROOT, "tools", "_validate_report_v13_20260917.json"), "w", encoding="utf-8").write(raw)
print()
print("报告已存 tools/_validate_report_v13_20260917.json")
