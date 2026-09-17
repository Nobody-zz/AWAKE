# -*- coding: utf-8 -*-
"""临时：解析 _probe_dual_channel_20260917.py 的输出，按组复核 REVIEW-锐评 §二 的口径声明。只读。"""
import io, re, subprocess, sys

PY = r"C:\Users\26811\.workbuddy\binaries\python\versions\3.13.12\python.exe"
out = subprocess.run([PY, "tools/_probe_dual_channel_20260917.py"], capture_output=True, cwd=r"D:\AWAKE-Dev\AWAKE")
txt = out.stdout.decode("utf-8", errors="replace")

cur = None
rows = []
for line in txt.splitlines():
    s = line.strip()
    m = re.match(r"CASE group=(\w) q=(.*)", s)
    if m:
        cur = (m.group(1), m.group(2))
        continue
    m = re.match(r"主路池\s+(\d+) 条 含答案=(\S+) ｜ 兜底池\s+(\d+) 条 含答案=(\S+) ｜ 现态走 (\S+)(.*)", s)
    if m and cur:
        rows.append(dict(g=cur[0], q=cur[1], lit=int(m.group(1)), litok=m.group(2),
                         fb=int(m.group(3)), fbok=m.group(4), route=m.group(5), rest=m.group(6)))
        cur = None

print("解析到用例数 =", len(rows))
for g in ("A", "B"):
    sub = [r for r in rows if r["g"] == g]
    print("--- group %s: %d 题 ---" % (g, len(sub)))
    print("  主路池含答案 =", sum(1 for r in sub if r["litok"] == "是"))
    print("  兜底池含答案 =", sum(1 for r in sub if r["fbok"] == "是"))
    print("  并集含答案   =", sum(1 for r in sub if r["litok"] == "是" or r["fbok"] == "是"))

print("--- 主路池有答案但现态不走主路的（'主路命中就跳过兜底'的代价） ---")
n = 0
for r in rows:
    if r["litok"] == "是" and r["route"] != "主路":
        n += 1
        print("   ", r["g"], r["q"], r["route"], r["rest"])
print("   小计", n)

print("--- 现态取不到的（两池都无答案，或答案不在第 1 位） ---")
for r in rows:
    if r["litok"] != "是" and r["fbok"] != "是":
        print("   ", r["g"], r["q"], "| rest=", r["rest"])
    elif "第" in r["rest"] and "第 1 位" not in r["rest"]:
        print("   ", r["g"], r["q"], "| rest=", r["rest"])
