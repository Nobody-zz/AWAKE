# -*- coding: utf-8 -*-
"""validate 后精确统计：诊断按档聚合，标出落在聚落档/本批新档的项。"""
import subprocess, io, os, json, collections

ROOT = r"D:\AWAKE-Dev\AWAKE"
WS = "tools/worldbook-studio/workspace/full-geo1"
OUT = os.path.join(ROOT, "tools", "_uw_settle_val.json")

r = subprocess.run(["dotnet",
                    os.path.join(ROOT, "tools", "worldbook-studio", "src",
                                 "Awake.WorldbookStudio.Cli", "bin", "Release",
                                 "net10.0", "worldbook-studio.dll"),
                    "validate", "--workspace", WS],
                   capture_output=True, cwd=ROOT, timeout=3600)
out = (r.stdout or b"").decode("utf-8", "replace")
io.open(OUT, "w", encoding="utf-8").write(out)
print("exit =", r.returncode)
try:
    j = json.loads(out)
except Exception as ex:
    print("解析失败:", ex); raise SystemExit

diag = j.get("Diagnostics") or []
print("诊断总数 =", len(diag))
sev = collections.Counter(d.get("Severity") for d in diag)
print("按严重度 =", dict(sev))

byfile = collections.Counter()
for d in diag:
    p = d.get("Path") or ""
    f = os.path.basename(p.split("#")[0])
    byfile[f] += 1
print()
print("按档聚合（前 15）：")
for f, c in byfile.most_common(15):
    print("  %-40s %d" % (f, c))

errs = [d for d in diag if d.get("Severity") == "error"]
print()
print("error 明细（前 20）：")
for d in errs[:20]:
    print("  %s | %s | %s" % (d.get("Code"), os.path.basename((d.get('Path') or '').split('#')[0]), d.get("Detail", "")))
