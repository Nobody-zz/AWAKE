# -*- coding: utf-8 -*-
"""
探针自证（变异检验）· 2026-09-20
------------------------------------------------------------------
问题：_wb_id_stability_probe 里那行「非规范形 = 0」到底是真读数，
      还是因为判据写死了恒 False（即没有分辨力）？
做法：把 Sanitize 的字符白名单动掉一格（去掉 '-'），拿同一份真产物重算。
      若读数从 0 变成大批量，则证明那行有分辨力。
预期：482 条里绝大多数含 '-'，变异后应大面积报「非规范形」。
"""
import io
import json
import os

ROOT = r"D:\AWAKE-Dev\AWAKE"
RUNTIME = os.path.join(ROOT, "ModuleData", "Worldbook", "packages", "calradia", "runtime.json")

# 基线：白名单含 '-'
BASE_KEEP = ("_", "-", ".")
# 变异：白名单去掉 '-'
MUT_KEEP = ("_", ".")


def sanitize(value, keep):
    s = str(value).strip().lower()
    out = []
    for ch in s:
        if ch.isalnum() or ch in keep:
            out.append(ch)
        else:
            out.append("_")
    return "".join(out).strip("_")


with io.open(RUNTIME, encoding="utf-8") as fh:
    rt = json.load(fh)

tail = []
for e in rt.get("entries", []):
    pid = str(e.get("id", ""))
    if pid.startswith("awake:entry:"):
        tail.append(pid[len("awake:entry:"):])

base_bad = [t for t in tail if sanitize(t, BASE_KEEP) != t]
mut_bad = [t for t in tail if sanitize(t, MUT_KEEP) != t]

print("条目总数                        = %d" % len(tail))
print("基线（白名单含 -）非规范形      = %d" % len(base_bad))
print("变异（白名单去掉 -）非规范形    = %d" % len(mut_bad))
print("变异样本前 5                    = %s" % mut_bad[:5])
print("")
if len(mut_bad) > len(base_bad):
    print("结论：该判据有分辨力（改一格白名单即大面积报警）。")
else:
    print("结论：判据可能没有分辨力，需重做。")
