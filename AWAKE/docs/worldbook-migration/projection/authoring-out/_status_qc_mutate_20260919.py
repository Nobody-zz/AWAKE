# -*- coding: utf-8 -*-
"""对**落盘后**的 21 档做文本质检阳性对照：证 `硬=0` 是真干净，不是新文本没进扫描范围。

三处变异（都在临时目录里做，原档只读）：
  M1 往一条**新写的断言**正文塞硬项词「地质」      —— 期望 硬 由 0 变 >=1
  M2 往一条**新写的表达**（expressions）正文塞「地质」—— 期望同样命中（证表达层在扫描范围内）
  M3 往 `summary` 塞「地质」                        —— 期望**不**命中（范围对照：summary 不算正文）
"""
import io
import os
import shutil
import subprocess

REPO = r"D:\AWAKE-Dev\AWAKE"
MIRR = os.path.join(REPO, r"docs\worldbook-migration\projection\authoring-out")
QC = r"C:\Users\26811\.workbuddy\skills\awake-prose-qc\scripts\prose_qc.py"
PY = r"C:\Users\26811\.workbuddy\binaries\python\envs\default\Scripts\python.exe"
W = os.path.join(os.environ.get("TEMP", r"C:\Windows\Temp"), "awake-status-qc-20260919")
NAME = "villages-diantogmail.yaml"

import json
J = {}
for n in (1, 2, 3):
    J.update(json.load(io.open(os.path.join(MIRR, "_status_l2_towns_%d_20260919.json" % n), encoding="utf-8")))
targets = sorted(k for k in J if not k.startswith("_"))

if os.path.isdir(W):
    shutil.rmtree(W)
os.makedirs(W)
for f in targets:
    shutil.copy(os.path.join(MIRR, f), os.path.join(W, f))

T = os.path.join(W, NAME)
PRISTINE = io.open(os.path.join(MIRR, NAME), encoding="utf-8", newline="").read()


def run(tag):
    p = subprocess.run([PY, QC, "--worldbook", W], cwd=REPO, capture_output=True)
    out = p.stdout.decode("utf-8", "replace")
    head = out.splitlines()[0] if out.splitlines() else "(空)"
    print("  [%s] %s" % (tag, head))
    return out


run("基线（落盘后原样）")
print()

ANCHOR_A = "村民自湖岸取黏土，供附近的工匠制器。"
ANCHOR_E = "滩上有挖泥的坑，谁家要烧罐子就下来挑。"
assert ANCHOR_A in PRISTINE and ANCHOR_E in PRISTINE, "锚点不在新文本里"

MUT = [
    ("M1 新断言里塞「地质」", ANCHOR_A, "村民自湖岸取黏土，地质供附近的工匠制器。"),
    ("M2 新表达里塞「地质」", ANCHOR_E, "滩上有挖泥的坑，地质谁家要烧罐子就下来挑。"),
]
for tag, anchor, repl in MUT:
    io.open(T, "w", encoding="utf-8", newline="").write(PRISTINE.replace(anchor, repl, 1))
    run(tag)
    print()

# M3 — summary 范围对照（yaml 里 summary: 一行）
import re
m = re.search(r"(?m)^summary: .*$", PRISTINE)
if m:
    io.open(T, "w", encoding="utf-8", newline="").write(
        PRISTINE[:m.start()] + "summary: 地质" + PRISTINE[m.end():])
    run("M3 summary 里塞「地质」（预期不命中）")
    print()
else:
    print("  (无 summary 行，跳过 M3)")
    print()

io.open(T, "w", encoding="utf-8", newline="").write(PRISTINE)
run("还原后")
print()
now = io.open(os.path.join(MIRR, NAME), encoding="utf-8", newline="").read()
print("原档逐字未动:", now == PRISTINE)
