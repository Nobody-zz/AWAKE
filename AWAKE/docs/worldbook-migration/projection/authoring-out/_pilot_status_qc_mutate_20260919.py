# -*- coding: utf-8 -*-
"""文本质检的阳性对照：证 `硬=0` 是真干净，不是扫描器没扫到。

三处变异（都在临时目录里做，原档只读）：
  M1 往一条**断言**正文塞硬项词「地质」        —— 期望 硬 由 0 变 >=1
  M2 往同一条断言塞硬项词「系统」              —— 期望同样命中（换一个词，防"只认一个词"）
  M3 往一条**表达**（expressions）正文塞「地质」—— 期望**不**命中
     （该工具自述扫描范围＝仅 `assertions[].text`；这条是范围对照，防把"没扫"当成"干净"）
"""
import io
import os
import shutil
import subprocess

REPO = r"D:\AWAKE-Dev\AWAKE"
PILOT = os.path.join(REPO, r"docs\worldbook-migration\pilot-status-20260919")
QC = r"C:\Users\26811\.workbuddy\skills\awake-prose-qc\scripts\prose_qc.py"
PY = r"C:\Users\26811\.workbuddy\binaries\python\envs\default\Scripts\python.exe"
W = os.path.join(os.environ.get("TEMP", r"C:\Windows\Temp"), "awake-pilot-qc-20260919")
NAME = "villages-diantogmail.yaml"
PRISTINE = io.open(os.path.join(PILOT, NAME), encoding="utf-8", newline="").read()

if os.path.isdir(W):
    shutil.rmtree(W)
os.makedirs(W)
for f in sorted(os.listdir(PILOT)):
    if f.endswith(".yaml"):
        shutil.copy(os.path.join(PILOT, f), os.path.join(W, f))

T = os.path.join(W, NAME)


def run(tag):
    p = subprocess.run([PY, QC, "--worldbook", W], cwd=REPO, capture_output=True)
    out = p.stdout.decode("utf-8", "replace")
    lines = out.splitlines()
    print("  [%s] %s" % (tag, lines[0] if lines else "(空)"))
    hits = [ln.strip()[:140] for ln in lines if "地质" in ln or "系统" in ln]
    for h in hits:
        print("     命中:", h)
    return out


run("基线（原样）")
print()

ANCHOR_A = "迪安托格麦尔在“林·泰瓦尔”，也就是暗湖之畔；"
ANCHOR_E = "暗湖边上就是我们村，走一段滩就到。"
assert ANCHOR_A in PRISTINE and ANCHOR_E in PRISTINE

for tag, anchor, repl in [
    ("M1 断言里塞「地质」", ANCHOR_A, "此地地质属暗湖之畔；"),
    ("M2 断言里塞「系统」", ANCHOR_A, "此村的系统沿湖而立；"),
    ("M3 表达里塞「地质」（范围对照，预期不命中）", ANCHOR_E, "暗湖边上就是我们村，滩上有地质埋着。"),
]:
    io.open(T, "w", encoding="utf-8", newline="").write(PRISTINE.replace(anchor, repl, 1))
    run(tag)
    print()

io.open(T, "w", encoding="utf-8", newline="").write(PRISTINE)
run("还原后")
print()
now = io.open(os.path.join(PILOT, NAME), encoding="utf-8", newline="").read()
print("原档逐字未动:", now == PRISTINE)
