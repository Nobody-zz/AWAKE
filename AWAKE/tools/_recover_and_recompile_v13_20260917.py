# -*- coding: utf-8 -*-
"""v13 编译失败恢复（第 2 版）：清掉失败留下的 operation / 陈旧 lease，再用**正确路径**重跑 compile。

第 1 版的教训：
  · `--out` 必须传**相对仓库根**的完整路径（`tools/worldbook-studio/workspace/full-geo1/compiled/...`）。
    只写 `compiled/...` 会被解析到仓库根下 ⇒ `WB-PATH-003`（路径不在工作区 allowedRoot 内）。
  · 清理时只许动**本次那个** operation / reservation，别用通配（第 1 版误移了 52 个历史 op 文件，已全部还原）。

本脚本要回答的问题：上次 `WB-AUTHORITY-MUTATION-UNKNOWN` 的真因 ——
  CLI 会把真因写到 stderr（`Program.cs:212`），上一跑的子进程因未指定 encoding 解码失败把它吃了。
"""
import io
import os
import shutil
import subprocess
import sys
import time

sys.stdout.reconfigure(encoding="utf-8")

ROOT = r"D:/AWAKE-Dev/AWAKE"
WS_REL = "tools/worldbook-studio/workspace/full-geo1"
WS = os.path.join(ROOT, WS_REL)
DLL = os.path.join(
    ROOT,
    "tools/worldbook-studio/src/Awake.WorldbookStudio.Cli/bin/Release/net10.0/worldbook-studio.dll",
)
PROOF = "compile.53e034b284444c1f8e37e8412ad10fc5"
OUT = WS_REL + "/compiled/geo1-v13-settlement-types"          # ⚠️ 必须含工作区前缀
OP_FILE = os.path.join(
    WS, "authoring-v1/operations",
    "k1_op_cedeef5bf5355d7639b57892dfcfb383089fd3237eb6020d549c7bb53be48040.json")
LEASE = os.path.join(WS, "compiled", ".geo1-v13-settlement-types.compile-lease")

for p, what in [(OP_FILE, "失败 operation"), (LEASE, "陈旧 target lease")]:
    if os.path.exists(p):
        os.remove(p)
        print("已移除%s：%s" % (what, os.path.relpath(p, ROOT)))
    else:
        print("（无%s）" % what)

res_dir = os.path.join(WS, "authoring-v1/operations/reservations")
print("reservations 目录现有 %d 个文件" % len(os.listdir(res_dir)))
print("compiled/geo1-v13-settlement-types 存在=%s"
      % os.path.isdir(os.path.join(WS, "compiled/geo1-v13-settlement-types")))

print()
print("=" * 78)
print("重跑：dotnet <dll> compile --workspace %s --proof %s --out %s" % (WS_REL, PROOF, OUT))
t0 = time.time()
r = subprocess.run(["dotnet", DLL, "compile", "--workspace", WS_REL,
                    "--proof", PROOF, "--out", OUT],
                   capture_output=True, cwd=ROOT)
print("rc=%d  用时 %.1fs" % (r.returncode, time.time() - t0))
print("---- stdout ----")
print(r.stdout.decode("utf-8", "replace")[:3000])
print("---- stderr ----")
print(r.stderr.decode("utf-8", "replace")[:6000])
sys.exit(0)
