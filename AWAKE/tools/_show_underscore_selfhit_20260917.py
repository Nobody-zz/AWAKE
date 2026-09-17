# -*- coding: utf-8 -*-
"""找出「含下划线却自命中」的那 4 条 —— R2 应该把它们全剔掉，为什么还有 4 条能自命中。"""
import io
import json
import sys

sys.stdout.reconfigure(encoding="utf-8")

SIDE = "tools/_entry_fit_side_20260917.json"
JUDGE = "tools/_entry_fit_off_20260917_judge.json"

side = json.load(io.open(SIDE, encoding="utf-8"))["side"]
j = json.load(io.open(JUDGE, encoding="utf-8"))["per_q"]

print("含下划线且自命中的条目：")
n = 0
for x in j:
    s = side.get(x.get("name"), {})
    t = x.get("text") or s.get("text") or ""
    if "_" in t and x.get("self_hit"):
        n += 1
        print("   %-9s %-11s 词「%s」" % (s.get("kind"), x.get("entry"), t))
        print("        hits = %s" % (x.get("hits") or []))
print("   共 %d 条" % n)

print("\n对照：含下划线但**不自命中**的前 5 条：")
m = 0
for x in j:
    s = side.get(x.get("name"), {})
    t = x.get("text") or s.get("text") or ""
    if "_" in t and not x.get("self_hit") and m < 5:
        m += 1
        print("   %-9s %-38s 词「%s」 hits=%s" % (s.get("kind"), x.get("entry"), t, x.get("hits")))
