# -*- coding: utf-8 -*-
"""Overlay 半提交判据的变异检验（2026-09-22）。

做法：对**最终版**源码逐个注释掉新加的关键行，重建、只跑那一条用例，看判据是否回到红，
然后按字节还原。三个变异分别守判据的三处断言：

  A  cursor++                    -> 期望红（游标不前进 ⇒ 后面的 op 撞 CAS）
  B  门牌日志 AwakeLog.Write     -> 期望红（断言③ 门牌日志）
  C  逐条尽力（改成首败即 return） -> 期望红（断言② 第 3 条不落地）

⚠️ 全程按字节还原，任何一步失败都要能把文件恢复原样（失败也还原）。
⚠️ 子进程不传 text=，自己按 utf-8 解码（本项目既有坑）。
"""
import io
import os
import re
import subprocess
import sys

ROOT = r"D:\AWAKE-Dev"
SRC = os.path.join(ROOT, r"AWAKE\src\WorldKnowledgeQueryService.cs")
PROJ = os.path.join(ROOT, r"AWAKE.Tests\AWAKE.Tests.csproj")
EXE = os.path.join(ROOT, r"AWAKE.Tests\bin\Debug\net472\Awake.SdkSmoke.exe")
CASE = "worldbook-overlay-import-partial"

MUTATIONS = [
    (
        "A cursor++",
        "cursor++;{nl}",
        "/* MUTATION-A: cursor++; */{nl}",
    ),
    (
        "B 门牌日志",
        'AwakeLog.Write("worldbook_overlay_import_partial " + failure);',
        '/* MUTATION-B: AwakeLog.Write(...); */',
    ),
    (
        "C 逐条尽力",
        "cursor++;{nl}                if (applied) continue;{nl}",
        "cursor++;{nl}                if (!applied) {{ error = operationError; return false; }}{nl}                if (applied) continue;{nl}",
    ),
]


def read_bytes(path):
    with open(path, "rb") as handle:
        return handle.read()


def write_bytes(path, raw):
    with open(path, "wb") as handle:
        handle.write(raw)


def decode_output(raw):
    """子进程 stdout 的编码不一定是 utf-8（重定向成管道时 .NET 用的是本地码页）。
    逐个编码试，失败才 replace —— 别静默把中文变成乱码（本轮踩过）。"""
    for enc in ("utf-8", "gbk", "cp936"):
        try:
            return raw.decode(enc)
        except UnicodeDecodeError:
            continue
    return raw.decode("utf-8", "replace")


def run_suite():
    build = subprocess.run(
        ["dotnet", "build", PROJ, "-c", "Debug", "-v:q", "--nologo",
         "-p:UseSharedCompilation=false"],
        cwd=ROOT, capture_output=True)
    if build.returncode != 0:
        return None, "BUILD_FAILED: " + decode_output(build.stdout)[-600:]
    run = subprocess.run([EXE], cwd=os.path.dirname(EXE), capture_output=True)
    text = decode_output(run.stdout)
    # ⚠️ 锚点别写成 "[" + CASE —— 实际输出是 `[65/66] FAIL worldbook-overlay-import-partial`，
    #    `[` 并不紧挨着用例名；那样写 find() 返回 -1，红因会**静默为空**（本轮已踩）。
    mark = text.find("FAIL " + CASE)
    if mark < 0:
        mark = text.find(CASE)
    fail_msg = "**没抓到 FAIL_MSG（解析锚点失效）**"
    tail = text[mark:] if mark >= 0 else ""
    if "FAIL_MSG" in tail:
        fail_msg = tail.split("FAIL_MSG ", 1)[1].split("\n", 1)[0]
    passed = re.search(r"RESULT total=(\d+) passed=(\d+) failed=(\d+)", text)
    return (passed.group(0) if passed else "NO_RESULT"), fail_msg


original = read_bytes(SRC)
had_bom = original.startswith(b"\xef\xbb\xbf")
body = original.decode("utf-8-sig")
# 行尾按**文件实际**的来：本仓源码是 CRLF，写死 \n 会让锚点一条都命不中（本轮已踩）。
NL = "\r\n" if "\r\n" in body else "\n"
MUTATIONS = [(n, t.format(nl=NL), r.format(nl=NL)) for (n, t, r) in MUTATIONS]

print("=== 基线（未变异）===")
base_result, _ = run_suite()
print("   " + base_result)
if "failed=0" not in base_result:
    print("!! 基线不是全绿，先修基线再谈变异")
    write_bytes(SRC, original)
    sys.exit(1)

rows = []
try:
    for name, target, replacement in MUTATIONS:
        hit = body.count(target)
        if hit != 1:
            rows.append((name, "SKIP", "锚点命中 %d 次（应为 1）" % hit))
            continue
        mutated = body.replace(target, replacement)
        write_bytes(SRC, ("\ufeff" if had_bom else "").encode("utf-8") + mutated.encode("utf-8"))
        result, fail_msg = run_suite()
        rows.append((name, result, fail_msg))
        write_bytes(SRC, original)
finally:
    write_bytes(SRC, original)

print()
print("=== 变异检验结果（每条都要红，且红因要落在预期断言上）===")
report = []
for name, result, fail_msg in rows:
    verdict = "RED-ok" if result != "SKIP" and "failed=" in result and "failed=0" not in result else "NOT-RED"
    print("  %-14s %s  %s" % (name, verdict, result))
    if fail_msg:
        print("       reason captured: %s" % ("yes" if "\ufffd" not in fail_msg else "GARBLED"))
    report.append((name, verdict, result, fail_msg))

print()
restored = read_bytes(SRC) == original
print("source restored byte-exact: %s" % ("OK" if restored else "FAILED"))
print("detail (utf-8): AWAKE/tools/_overlay_mutation_result_20260922.txt")

with io.open(os.path.join(ROOT, r"AWAKE\tools\_overlay_mutation_result_20260922.txt"),
             "w", encoding="utf-8") as handle:
    handle.write("=== 基线 ===\n" + base_result + "\n\n")
    for name, verdict, result, fail_msg in report:
        handle.write("[%s] %s\n  %s\n  红因: %s\n\n" % (name, verdict, result, fail_msg))
    handle.write("源码按字节还原: %s\n" % ("OK" if restored else "FAILED"))

sys.exit(0 if restored else 1)
