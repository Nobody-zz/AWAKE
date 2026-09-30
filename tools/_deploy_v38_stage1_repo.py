# -*- coding: utf-8 -*-
"""阶段一：把 v38（R1 引文处置）编译产物落进**仓库侧部署根** AWAKE/ModuleData/Worldbook/。

契约（docs/worldbook-studio-plan/RUNTIME-MAPPING-CONTRACT.md「仓库侧包形态」）：
  AWAKE/ModuleData/Worldbook/
  ├─ manifest.json                 awake.worldbook.registry.v1  ← 唯一入口，需同步 3 哈希
  └─ packages/calradia/
      ├─ manifest.json  runtime.json  index.json    ← 包只认这 3 个文件（ReadAndVerify 不扫目录）

v38 vs v37 的实质差异：
  · index.json **完全相同**（b14c9222…）—— id 集合与索引未变
  · runtime.json **变了**（bb8df569… → eb87b44e…）—— 仅 war-prisoners 一条正文改
  · 包内 hashes：manifestHash 未变（67D39F44…）；contentHash / packageHash 变

要点：
  · 包只认 3 个文件 ⇒ 只投这 3 个，报告文件留在 tools 侧。
  · registry 里的 manifestHash/contentHash/packageHash **必须等于包内 manifest.hashes 三值**
    （仅大小写不同：registry 小写、包内大写）。
  · 字节复制，不重序列化（三哈希是按字节算的）。

运行：python -u D:/AWAKE-Dev/tools/_deploy_v38_stage1_repo.py
"""
import hashlib
import io
import json
import os
import shutil
import time

ROOT = r"D:/AWAKE-Dev"
SRC = os.path.join(ROOT, "AWAKE/tools/worldbook-studio/workspace/full-geo1/compiled/geo1-v38-r1")
DEPLOY_ROOT = os.path.join(ROOT, "AWAKE/ModuleData/Worldbook")
PKG_REL = "packages/calradia"
PKG_DIR = os.path.join(DEPLOY_ROOT, PKG_REL)
REGISTRY = os.path.join(DEPLOY_ROOT, "manifest.json")
PACKAGE_ID = "awake:worldbook.calradia"

FILES = ["manifest.json", "runtime.json", "index.json"]
LOG = os.path.join(ROOT, "AWAKE/tools/worldbook-studio/workspace/full-geo1/_deploy_v38_stage1.txt")
logf = io.open(LOG, "w", encoding="utf-8", newline="\n")


def log(m):
    line = "[%s] %s" % (time.strftime("%H:%M:%S"), m)
    print(line, flush=True)
    logf.write(line + "\n")
    logf.flush()


def die(m):
    log("FATAL " + m)
    logf.close()
    raise SystemExit(1)


def sha(path):
    return hashlib.sha256(open(path, "rb").read()).hexdigest()


def main():
    log("== v38 部署 阶段一：产物 -> 仓库侧部署根 ==")
    log("源: %s" % SRC)
    log("目标: %s" % PKG_DIR)

    # ---- [0] 源预检 ----
    for f in FILES:
        p = os.path.join(SRC, f)
        if not os.path.isfile(p):
            die("源缺文件: %s" % p)
    src_manifest = json.load(io.open(os.path.join(SRC, "manifest.json"), encoding="utf-8"))
    if src_manifest.get("schemaVersion") != "awake.worldbook.v2":
        die("源 manifest schemaVersion 不是 awake.worldbook.v2")
    if src_manifest.get("packageId") != PACKAGE_ID:
        die("源 packageId 不是 %s" % PACKAGE_ID)
    log("[0] 源三文件齐；schemaVersion=%s packageId=%s" % (src_manifest["schemaVersion"], src_manifest["packageId"]))

    # 源 runtime entries
    src_rt = json.load(io.open(os.path.join(SRC, "runtime.json"), encoding="utf-8"))
    log("    源 runtime entries=%d" % len(src_rt["entries"]))
    if len(src_rt["entries"]) != 790:
        die("源 entries 不是 790")

    new_hashes = src_manifest["hashes"]
    log("    包内 hashes: manifestHash=%s" % new_hashes["manifestHash"])
    log("                 contentHash =%s" % new_hashes["contentHash"])
    log("                 packageHash =%s" % new_hashes["packageHash"])

    # ---- [0b] 与上一版（v37）的差异自证 ----
    prev = os.path.join(ROOT, "AWAKE/tools/worldbook-studio/workspace/full-geo1/compiled/geo1-v37-polity-qa")
    if os.path.isdir(prev):
        for f in FILES:
            a = sha(os.path.join(prev, f))
            b = sha(os.path.join(SRC, f))
            log("    vs v37 %-14s %s -> %s %s" % (
                f, a[:16], b[:16], "(未变)" if a == b else "(已变)"))
    else:
        log("    ⚠️ 找不到 v37 产物，跳过差异对比")

    # ---- [1] 复制 3 文件（字节复制）----
    os.makedirs(PKG_DIR, exist_ok=True)
    for f in FILES:
        s = os.path.join(SRC, f)
        d = os.path.join(PKG_DIR, f)
        shutil.copyfile(s, d)
        if sha(s) != sha(d):
            die("复制校验失败: %s" % f)
    log("[1] 已复制 %d 文件（字节一致）" % len(FILES))

    # ---- [2] 目录净化：包目录只应留这 3 个文件 ----
    extra = sorted(x for x in os.listdir(PKG_DIR) if x not in FILES)
    if extra:
        log("    ⚠️ 包目录有额外文件（契约说不扫目录，但清掉更干净）：%s" % extra)
        for x in extra:
            p = os.path.join(PKG_DIR, x)
            if os.path.isfile(p):
                os.remove(p)
                log("       - 已删 %s" % x)
    log("[2] 包目录现含: %s" % sorted(os.listdir(PKG_DIR)))

    # ---- [3] 同步仓库侧 registry ----
    reg = json.load(io.open(REGISTRY, encoding="utf-8"))
    if reg.get("schemaVersion") != "awake.worldbook.registry.v1":
        die("registry schemaVersion 不对")
    pkgs = reg.get("packages") or []
    hit = [p for p in pkgs if p.get("packageId") == PACKAGE_ID]
    if len(hit) != 1:
        die("registry 里 %s 不是恰好 1 条（实际 %d）" % (PACKAGE_ID, len(hit)))
    entry = hit[0]
    old = {k: entry.get(k) for k in ("manifestHash", "contentHash", "packageHash")}
    entry["relativePath"] = PKG_REL
    entry["manifestHash"] = new_hashes["manifestHash"].lower()
    entry["contentHash"] = new_hashes["contentHash"].lower()
    entry["packageHash"] = new_hashes["packageHash"].lower()
    io.open(REGISTRY, "w", encoding="utf-8", newline="\n").write(
        json.dumps(reg, ensure_ascii=False, indent=1) + "\n")
    log("[3] registry 已同步（小写口径）")
    for k in old:
        log("    %-13s %s -> %s%s" % (k, old[k], entry[k], "  (未变)" if old[k] == entry[k] else ""))

    # ---- [4] 回读校验 ----
    ok = True
    reg2 = json.load(io.open(REGISTRY, encoding="utf-8"))
    e2 = [p for p in reg2["packages"] if p["packageId"] == PACKAGE_ID][0]
    for k, local in (("manifestHash", "manifestHash"), ("contentHash", "contentHash"), ("packageHash", "packageHash")):
        if e2[k].lower() != new_hashes[local].lower():
            log("    ✗ registry.%s 与包内不符" % k)
            ok = False
        else:
            log("    ✓ registry.%-13s == 包内 hashes.%s" % (k, local))
    if not ok:
        die("registry 同步校验失败")

    # 目标包 runtime 回读
    t_rt = json.load(io.open(os.path.join(PKG_DIR, "runtime.json"), encoding="utf-8"))
    log("[4] 目标 runtime entries=%d polity=%d" % (
        len(t_rt["entries"]), sum(1 for e in t_rt["entries"] if "polity" in e.get("id", ""))))
    if len(t_rt["entries"]) != 790:
        die("目标 entries 不是 790")

    # ---- [5] war-prisoners 正文抽查（v38 唯一变更点）----
    hit_wp = [e for e in t_rt["entries"] if e.get("id", "").endswith("war.war-prisoners")]
    if len(hit_wp) != 1:
        log("    ⚠️ war-prisoners 命中 %d 条，跳过正文抽查" % len(hit_wp))
    else:
        blob = json.dumps(hit_wp[0], ensure_ascii=False)
        checks = [("含「处决俘虏」", "处决俘虏" in blob, True),
                  ("不含「沙漠部族讲究赎」", "沙漠部族讲究赎" in blob, False),
                  ("不含「贾瓦勒」", "贾瓦勒" in blob, False)]
        for label, got, want in checks:
            log("    %s %s" % ("✓" if got == want else "✗", label))
        if any(got != want for _, got, want in checks):
            die("war-prisoners 正文抽查未通过")

    log("")
    log("== 阶段一完成 ==")
    log("  仓库侧包: %s" % PKG_DIR)
    log("  registry: %s" % REGISTRY)
    log("  下一步: AWAKE/tools/deploy_worldbook_to_game.ps1 -ConfirmDeploy（阶段二，仓库侧→游戏目录）")
    logf.close()


if __name__ == "__main__":
    main()
