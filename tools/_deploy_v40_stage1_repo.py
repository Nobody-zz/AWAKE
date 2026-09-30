# -*- coding: utf-8 -*-
"""
v40 阶段一：产物 -> 仓库侧

compiled/geo1-v40-polity-c/ 的 3 文件（manifest.json / runtime.json / index.json）
字节复制进 AWAKE/ModuleData/Worldbook/packages/calradia/，
并同步仓库侧 registry（AWAKE/ModuleData/Worldbook/manifest.json）的三哈希（小写）。

复核：
  [1] 3 文件字节相等（sha256 对拍）
  [2] registry 三哈希小写 == 包内三哈希（大写 → 小写）
  [3] 回读 runtime.json entries / 4 条新件在册
"""
import hashlib
import io
import json
import os
import shutil
import sys

STUDIO = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio"
# ⚠️ 产物在 workspace 的 compiled 下（不是 studio 根）
SRC = os.path.join(STUDIO, "workspace/full-geo1/compiled/geo1-v40-polity-c")
REPO = r"D:/AWAKE-Dev/AWAKE/ModuleData/Worldbook"                  # 仓库侧
PKG = os.path.join(REPO, "packages/calradia")
REG = os.path.join(REPO, "manifest.json")
FILES = ["manifest.json", "runtime.json", "index.json"]
EXPECT_ENTRIES = 800
EXPECT_POLITY = 15
NEW_IDS = [
    "doc.politics.polity-senate-body",
    "doc.politics.polity-tribunes",
    "doc.politics.polity-marshals",
    "doc.politics.polity-royal-privilege",
]
LOG = os.path.join(STUDIO, "_v40_stage1_log.txt")
_fh = io.open(LOG, "w", encoding="utf-8", buffering=1)


def log(*a):
    s = " ".join(str(x) for x in a)
    print(s)
    _fh.write(s + "\n")


def sha(p):
    return hashlib.sha256(open(p, "rb").read()).hexdigest()


log("=" * 70)
log("v40 阶段一：产物 -> 仓库侧")
log("SRC  = %s" % SRC)
log("PKG  = %s" % PKG)

if not os.path.isdir(SRC):
    log("[FAIL] 产物目录不存在")
    sys.exit(1)

# 备份仓库侧旧包
bak = os.path.join(REPO, "artifacts/repo-side-backup-v39-%s" % __import__("datetime").datetime.now().strftime("%Y%m%d-%H%M%S"))
os.makedirs(bak, exist_ok=True)
for f in FILES + ["../manifest.json"]:
    pass
for f in FILES:
    src = os.path.join(PKG, f)
    if os.path.isfile(src):
        shutil.copy2(src, os.path.join(bak, f))
shutil.copy2(REG, os.path.join(bak, "registry-manifest.json"))
log("[0] 旧包已备份 -> %s" % bak)

# --- 步骤 1：字节复制 3 文件 ---
log("\n[1] 字节复制 3 文件")
before = {f: sha(os.path.join(PKG, f)) for f in FILES if os.path.isfile(os.path.join(PKG, f))}
for f in FILES:
    s = os.path.join(SRC, f)
    if not os.path.isfile(s):
        log("[FAIL] 产物缺 %s" % f)
        sys.exit(1)
    shutil.copyfile(s, os.path.join(PKG, f))
after = {f: sha(os.path.join(PKG, f)) for f in FILES}
for f in FILES:
    ok = sha(os.path.join(SRC, f)) == after[f]
    log("  %-14s %s  %s" % (f, after[f][:16], "✔" if ok else "✘"))
    if not ok:
        log("[FAIL] %s 字节不一致" % f)
        sys.exit(1)

# --- 步骤 2：同步 registry 三哈希（小写）---
log("\n[2] 同步 registry 三哈希")
pkgman = json.load(io.open(os.path.join(PKG, "manifest.json"), encoding="utf-8"))
h = pkgman["hashes"]
low = {k: v.lower() for k, v in h.items()}
reg = json.load(io.open(REG, encoding="utf-8"))
for p in reg["packages"]:
    if p["packageId"] == "awake:worldbook.calradia":
        log("  before: content=%s pkg=%s" % (p["contentHash"][:16], p["packageHash"][:16]))
        p["manifestHash"] = low["manifestHash"]
        p["contentHash"] = low["contentHash"]
        p["packageHash"] = low["packageHash"]
        log("  after : content=%s pkg=%s" % (p["contentHash"][:16], p["packageHash"][:16]))
io.open(REG, "w", encoding="utf-8").write(json.dumps(reg, ensure_ascii=False, indent=1) + "\n")

# 回读校验
reg2 = json.load(io.open(REG, encoding="utf-8"))
rp = [p for p in reg2["packages"] if p["packageId"] == "awake:worldbook.calradia"][0]
for k in ("manifestHash", "contentHash", "packageHash"):
    ok = rp[k] == low[k]
    log("  registry.%-13s %s  %s" % (k, rp[k][:16], "✔" if ok else "✘"))
    if not ok:
        log("[FAIL] registry %s 不符" % k)
        sys.exit(1)

# --- 步骤 3：回读 entries / 新件在册 ---
log("\n[3] 回读 runtime.json")
rt = json.load(io.open(os.path.join(PKG, "runtime.json"), encoding="utf-8"))
docs = rt.get("documents") or rt.get("entries") or []
if isinstance(docs, dict):
    docs = list(docs.values())
log("  entries=%d" % len(docs))
if len(docs) != EXPECT_ENTRIES:
    log("[FAIL] entries 期望 %d" % EXPECT_ENTRIES)
    sys.exit(1)
blob = json.dumps(docs, ensure_ascii=False)
missing = [i for i in NEW_IDS if i not in blob]
pol = blob.count("polity-")
log("  新件在册: %d/%d %s" % (len(NEW_IDS) - len(missing), len(NEW_IDS), "✔" if not missing else "✘ " + str(missing)))
if missing:
    log("[FAIL] 新件未全部在册")
    sys.exit(1)

log("\n" + "=" * 70)
log("阶段一完成 ✔  entries=%d  polity 提及=%d" % (len(docs), pol))
log("下一步：deploy_worldbook_to_game.ps1 -ConfirmDeploy")
_fh.close()
