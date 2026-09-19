# -*- coding: utf-8 -*-
"""重编并实跑知识识别回路各轴的闸，读数落一份档。

现在跑七路（五道闸 + 两次变异检验）：
  1 检索闸 / 2 合流闸 / 3 身份闸「谁知道」/ 4 身份闸[变异]
  5 时间闸「何时知道」(09-19 新增) / 6 时间闸[变异] / 7 生产闸

为什么先编再跑：现成 exe 可能过期（09-18 吃过一次：exe 构建于 10:04、
而最后一笔动 src 的提交在 10:05 ⇒ 那次读数不含最新改动）。

各闸的目标框架/程序集名都不同（net8.0 / net10.0-windows / net472 / net10.0），
产物有的落在 bin/，有的落在 artifacts/bin/（被 .gitignore 排除）——一律按实际产物解析。

两道变异检验用来证明闸有分辨力，而不是"全绿但没在测"：
  · AWAKE_GATE_MUTATE_ASSUME_ALLOWED=1 ⇒ 身份闸 ① 必须判红
  · AWAKE_GATE_MUTATE_FORGE_DAY=1     ⇒ 时间闸 ① 必须判红（把「当下」冒充成事实发生那天）

⚠️ 子进程若打印 GBK 编不出的字符（✓ / ✗ / ① …），落到管道会被按 GBK 编码
   ⇒ 读数档里静默变成 `?`（判阅证据失真）。验台自己要显式
   `Console.OutputEncoding = System.Text.Encoding.UTF8;`（time-gate 已这么做）。

用法：python _run_gates_20260919.py
输出：AWAKE/tools/_gates_now_20260919.txt
"""
import os
import subprocess
import sys
import pathlib
import datetime

ROOT = pathlib.Path(r"D:\AWAKE-Dev")
TOOLS = ROOT / "AWAKE" / "tools"
DOTNET = r"C:\Program Files\dotnet\dotnet.exe"
OUT = TOOLS / "_gates_now_20260919.txt"

REAL_MANIFEST = ROOT / "AWAKE/ModuleData/Worldbook/packages/calradia/manifest.json"
CASES = TOOLS / "_retrieval_cases_20260916.json"

GATES = [
    dict(idx="1/7", name="RETRIEVAL_GATE", dir="worldbook-runtime-smoke",
         stem="WorldbookRuntimeSmoke", asm="WorldbookRuntimeSmoke"),
    dict(idx="2/7", name="MERGE_GATE", dir="worldbook-rag-merge",
         stem="WorldbookRagMerge", asm="worldbook-rag-merge"),
    dict(idx="3/7", name="IDENTITY_GATE", dir="worldbook-runtime-sim",
         stem="WorldbookRuntimeSim", asm="WorldbookRuntimeSim",
         args=["identity-gate", str(REAL_MANIFEST), str(CASES),
               str(pathlib.Path(os.environ.get("TEMP", ".")) / "awake-identity-gate-20260919.json")]),
    dict(idx="4/7", name="IDENTITY_GATE[MUTANT]", dir="worldbook-runtime-sim",
         stem="WorldbookRuntimeSim", asm="WorldbookRuntimeSim",
         args=["identity-gate", str(REAL_MANIFEST), str(CASES),
               str(pathlib.Path(os.environ.get("TEMP", ".")) / "awake-identity-gate-mutant.json")],
         env={"AWAKE_GATE_MUTATE_ASSUME_ALLOWED": "1"}),
    # 「何时知道」这条轴的闸（09-19 新增）：事实的时点 —— 未来不得泄漏、到了必须出现、
    # 老料不回流、周窗形状。不需要外部输入（事实由真件现造）。
    dict(idx="5/7", name="TIME_GATE", dir="worldbook-runtime-sim",
         stem="WorldbookRuntimeSim", asm="WorldbookRuntimeSim",
         args=["time-gate", str(TOOLS / "out" / "time-gate.json")]),
    dict(idx="6/7", name="TIME_GATE[MUTANT]", dir="worldbook-runtime-sim",
         stem="WorldbookRuntimeSim", asm="WorldbookRuntimeSim",
         args=["time-gate", str(TOOLS / "out" / "time-gate-mutant.json")],
         env={"AWAKE_GATE_MUTATE_FORGE_DAY": "1"}),
    dict(idx="7/7", name="PRODUCTION_SMOKE", dir="worldbook-runtime-production-smoke",
         stem="WorldbookRuntimeProductionSmoke", asm="Awake.WorldbookRuntimeProductionSmoke"),
]


def run(cmd, env=None):
    e = dict(os.environ)
    if env:
        e.update(env)
    p = subprocess.run(cmd, cwd=str(ROOT), capture_output=True, env=e)
    return p.returncode, p.stdout.decode("utf-8", "replace"), p.stderr.decode("utf-8", "replace")


def resolve_artifact(proj_dir, asm):
    """按实际产物解析：优先 bin\\Debug，排除 obj\\Debug（obj 里那份缺依赖，跑不起来）。"""
    root = TOOLS / proj_dir
    hits = [
        p for p in root.rglob("*")
        if p.is_file() and p.suffix in (".dll", ".exe") and p.stem == asm
        and "\\obj\\" not in str(p)
    ]
    bins = [p for p in hits if "\\bin\\" in str(p)]
    pool = bins or hits
    if not pool:
        return None
    return max(pool, key=lambda p: p.stat().st_mtime)


def main():
    chunks = []
    for g in GATES:
        proj = TOOLS / g["dir"] / f"{g['stem']}.csproj"
        lines = [f"\n########## {g['idx']} {g['name']} ({g['dir']}) ##########",
                 datetime.datetime.now().strftime("%a %b %d %H:%M:%S %Y")]

        if not proj.exists():
            lines.append(f"PROJECT_MISSING {proj}")
            chunks.append("\n".join(lines))
            continue

        rc, out, err = run([DOTNET, "build", str(proj), "-c", "Debug", "-v", "q", "--nologo"])
        if rc != 0:
            lines += [f"BUILD_FAILED rc={rc}", out[-1500:], err[-1500:]]
            chunks.append("\n".join(lines))
            continue

        art = resolve_artifact(g["dir"], g["asm"])
        if art is None:
            lines.append(f"BUILT_BUT_ARTIFACT_MISSING name={g['asm']}")
            chunks.append("\n".join(lines))
            continue
        lines.append(f"ARTIFACT {art.relative_to(TOOLS)}")

        cmd = [str(art)] if art.suffix == ".exe" else [DOTNET, str(art)]
        cmd += g.get("args", [])
        # 必须是 Release 才有的 Release 行为不在考虑内；这里一律 Debug，与编译一致
        rc, out, err = run(cmd, g.get("env"))
        if g.get("env"):
            lines.append("ENV " + " ".join(f"{k}={v}" for k, v in g["env"].items()))
        lines.append(out.rstrip("\n"))
        if err.strip():
            lines += ["--- stderr ---", err.rstrip("\n")]
        lines.append(f"EXIT={rc}")
        chunks.append("\n".join(lines))

    OUT.write_text("\n".join(chunks) + "\n", encoding="utf-8")
    print(f"WROTE {OUT}  ({OUT.stat().st_size} bytes)")
    for c in chunks:
        for line in c.splitlines():
            if any(k in line for k in ("_GATE ", "_GATE[", "SUMMARY", "EXIT=", "FAILED",
                                       "MISSING", "扫描：", "点名题失败", "ARTIFACT ")):
                print("  " + line[:190])


if __name__ == "__main__":
    sys.exit(main())
