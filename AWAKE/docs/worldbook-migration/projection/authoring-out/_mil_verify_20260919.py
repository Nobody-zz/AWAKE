# -*- coding: utf-8 -*-
"""军事批 · 强校验（三件）：
  ① 变异检验：quote 改一字 → 必须红；kind 写非法值 → 必须红；按字节还原后复跑必须绿。
  ② 引文归属核：每条 source 的 locator 自取原文，quote 必须在该处（validate 查不出 locator 错）。
  ③ 阳性对照：故意把 A 的 locator 配 B 的 quote → 必须判 BAD。
用法：python _mil_verify_20260919.py troops-cataphract
"""
import io
import json
import os
import shutil
import sqlite3
import subprocess
import sys

import yaml

REPO = r"D:\AWAKE-Dev\AWAKE"
AUTH = os.path.join(REPO, r"docs\worldbook-migration\projection\authoring-out")
SRC = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1\authoring\sources")
DLL = os.path.join(REPO, r"tools\worldbook-studio\src\Awake.WorldbookStudio.Cli\bin\Release\net10.0\worldbook-studio.dll")
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
RULES = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/AnimusForge/PlayerExports/卡拉迪亚编年史/knowledge/rules"
TMP = os.path.join(os.environ.get("TEMP", r"C:\Windows\Temp"), "awake-mil-verify-20260919")

name = (sys.argv[1] if len(sys.argv) > 1 else "troops-cataphract") + ".yaml"
path = os.path.join(AUTH, name)
orig = io.open(path, "rb").read()
doc = yaml.safe_load(orig.decode("utf-8"))


def run_validate():
    if os.path.isdir(TMP):
        shutil.rmtree(TMP)
    os.makedirs(os.path.join(TMP, "authoring", "sources"))
    shutil.copy(path, os.path.join(TMP, "authoring", name))
    used = {s["source_id"] for s in doc.get("sources") or []}
    for f in sorted(os.listdir(SRC)):
        if not (f.startswith("source-") and f.endswith(".yaml")):
            continue
        d = yaml.safe_load(io.open(os.path.join(SRC, f), encoding="utf-8"))
        if d.get("source_id") in used:
            shutil.copy(os.path.join(SRC, f), os.path.join(TMP, "authoring", "sources", f))
            r = d.get("locator_root")
            if r and os.path.exists(os.path.join(SRC, r)):
                shutil.copy(os.path.join(SRC, r), os.path.join(TMP, "authoring", "sources", r))
    p = subprocess.run(["dotnet", DLL, "validate", "--workspace", TMP], cwd=REPO, capture_output=True)
    return p.returncode, json.loads(p.stdout.decode("utf-8", "replace"))


print("=" * 70)
print("① 变异检验")
print("=" * 70)
_, base = run_validate()
print("  基线：Valid=%s  rc=%d" % (base["Valid"], 0 if base["Valid"] else 2))

# M1: quote 改一字（从档里自动取一句，换档无需改脚本）
txt = orig.decode("utf-8")
q0 = doc["sources"][0]["quote"]
old_q = q0[-8:]
new_q = q0[-8:-1] + ("丙" if q0[-1] != "丙" else "丁")
assert txt.count(old_q) >= 1, "M1 目标句未找到"
io.open(path, "wb").write(txt.replace(old_q, new_q, 1).encode("utf-8"))
_, r1 = run_validate()
codes1 = [d.get("Code") or d.get("code") or d.get("Message") or d for d in r1.get("Diagnostics") or []]
print("  M1 quote 改一字 → Valid=%s  诊断=%s" % (r1["Valid"], codes1[:4]))
io.open(path, "wb").write(orig)

# M2: kind 写非法值
old_k = "  kind: fact"
assert txt.count(old_k) == 1, "M2 目标未找到"
io.open(path, "wb").write(txt.replace(old_k, "  kind: canon", 1).encode("utf-8"))
_, r2 = run_validate()
codes2 = [d.get("Code") or d.get("code") or d.get("Message") or d for d in r2.get("Diagnostics") or []]
print("  M2 kind=canon   → Valid=%s  诊断=%s" % (r2["Valid"], codes2[:2]))
io.open(path, "wb").write(orig)

_, back = run_validate()
print("  还原后        → Valid=%s" % back["Valid"])
assert io.open(path, "rb").read() == orig, "字节未还原！"

print()
print("=" * 70)
print("② 引文归属核（locator 自取原文，quote 必须在该处）")
print("=" * 70)
con = sqlite3.connect("file:" + DB + "?mode=ro", uri=True)
cur = con.cursor()


def fetch(locator):
    if locator.startswith("bannerlord.db#localization."):
        tok = locator.split(".", 2)[-1]
        r = cur.execute("SELECT text FROM localization_entries WHERE stringId=? AND language='CNs'", (tok,)).fetchone()
        return r[0] if r else None
    if locator.startswith("rules/"):
        f, frag = locator[len("rules/"):].split("#", 1)
        idx = int(frag.rstrip("/Content").rsplit("/", 1)[-1])
        d = json.load(io.open(os.path.join(RULES, f), encoding="utf-8-sig"))
        return (d.get("Variants") or [])[idx].get("Content")
    return None


srcs = []
seen = set()
for a in doc["assertions"]:
    for s in a["sources"]:
        k = (s["source_id"], s["quote_hash"])
        if k not in seen:
            seen.add(k)
            srcs.append(s)

bad = 0
for s in srcs:
    body = fetch(s["locator"])
    if body is None:
        print("  MISS  %s  取不到原文" % s["locator"])
        bad += 1
        continue
    ok = s["quote"] in body
    if not ok:
        bad += 1
    print("  %-4s %-52s %s" % ("OK" if ok else "BAD", s["locator"], s["quote"][:34] + "…"))
print("  合计 %d 条，坏 %d 条" % (len(srcs), bad))

# 阳性对照：A 的 locator 配 B 的 quote
pa = next(s for s in srcs if s["locator"].startswith("bannerlord.db#"))
pb = next(s for s in srcs if s["locator"].startswith("rules/"))
body_a = fetch(pa["locator"])
print()
print("=" * 70)
print("③ 阳性对照（必须判 BAD / MISS）")
print("=" * 70)
ctl1 = pb["quote"] in body_a
print("  P1 A的locator配B的quote → 命中=%s  期望 False ⇒ %s" % (ctl1, "PASS" if not ctl1 else "FAIL"))
ctl2 = fetch("bannerlord.db#localization.ZZZZZZZZ")
print("  P2 不存在的locator      → 取到=%s  期望 None  ⇒ %s" % (ctl2, "PASS" if ctl2 is None else "FAIL"))
ctl3 = pa["quote"] in body_a
print("  P3 原样配对             → 命中=%s  期望 True  ⇒ %s" % (ctl3, "PASS" if ctl3 else "FAIL"))
con.close()
