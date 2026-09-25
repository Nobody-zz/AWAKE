# -*- coding: utf-8 -*-
"""跑 validate（全工作区），只报 error 与涉及 321 聚落档的诊断。"""
import subprocess, io, os, json, sys

ROOT = r"D:\AWAKE-Dev\AWAKE"
DLL = os.path.join(ROOT, "tools", "worldbook-studio", "src",
                   "Awake.WorldbookStudio.Cli", "bin", "Release", "net10.0",
                   "worldbook-studio.dll")
WS = "tools/worldbook-studio/workspace/full-geo1"

r = subprocess.run(["dotnet", DLL, "validate", "--workspace", WS],
                   capture_output=True, cwd=ROOT, timeout=3600)
out = (r.stdout or b"").decode("utf-8", "replace")
err = (r.stderr or b"").decode("utf-8", "replace")
print("exit =", r.returncode)
print("---- stdout tail ----")
print("\n".join(out.strip().splitlines()[-40:]))
if err.strip():
    print("---- stderr tail ----")
    print("\n".join(err.strip().splitlines()[-25:]))
