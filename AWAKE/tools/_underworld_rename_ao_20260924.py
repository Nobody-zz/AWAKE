# -*- coding: utf-8 -*-
"""暗面批前缀归一 · 第二段：把镜像目录（AO）跟着改名，并同步 L2 数据与生成器。

为什么必须做这一步：
  `_gen_dark_20260920.py:245` 写的是 `fn = doc["slug"] + ".yaml"`，而 `:247-248` 对
  `(WS, AO)` 两个目录**双写同一个文件名**。所以只改 WS 一侧，下次任何人重跑生成器，
  AO 会重新长出 `alley-*.yaml` 这份旧名副本 ⇒ 现役侧被静默退回。这条不能只记账。

铁律不变：**文件改名，不碰档内任何一行**（含 id / assertion / expression id / 条目 id）。
备份 -> 改名 -> 复核（档数 / 字节 / sha / docid 四道）。
"""
import hashlib
import io
import os
import shutil

ROOT = r"D:\AWAKE-Dev\AWAKE"
AO = os.path.join(ROOT, "docs", "worldbook-migration", "projection", "authoring-out")
ARCH = os.path.join(AO, "_archive-20260924")
REPORT = os.path.join(ROOT, "tools", "_underworld_rename_ao_20260924.txt")

# 与第一段（WS）完全同一张表
RENAME = [
    ("town-alleys.yaml",        "underworld-alleys.yaml"),
    ("alley-gang-leaders.yaml", "underworld-gang-leaders.yaml"),
    ("alley-struggle.yaml",     "underworld-struggle.yaml"),
    ("town-gangs.yaml",         "underworld-gangs.yaml"),
    ("crime-rating.yaml",       "underworld-crime-rating.yaml"),
    ("blood-money.yaml",        "underworld-blood-money.yaml"),
    ("bandits.yaml",            "underworld-bandits.yaml"),
    ("smuggling.yaml",          "underworld-smuggling.yaml"),
]

lines = []


def log(s):
    lines.append(s)


def sha256(p):
    with open(p, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest().upper()


def docid_of(p):
    with io.open(p, encoding="utf-8") as f:
        for ln in f.read(6000).splitlines():
            if ln.startswith("id:"):
                return ln.split(":", 1)[1].strip()
    return None


def main():
    log("== 暗面批前缀归一 · 第二段（镜像目录 AO）==")
    log("")

    all_before = sorted(x for x in os.listdir(AO)
                        if x.endswith(".yaml") and not x.startswith(("_", "source-")))
    log("[0] 改名前 AO 档数 = %d" % len(all_before))

    before = {}
    for src, dst in RENAME:
        sp = os.path.join(AO, src)
        dp = os.path.join(AO, dst)
        if not os.path.exists(sp):
            log("  !! 缺档：%s" % src)
            return 1
        if os.path.exists(dp):
            log("  !! 目标已存在：%s" % dst)
            return 1
        before[src] = {"sha256": sha256(sp), "size": os.path.getsize(sp), "docid": docid_of(sp)}
        log("  %-26s -> %-32s  id=%s" % (src, dst, before[src]["docid"]))

    if not os.path.isdir(ARCH):
        os.makedirs(ARCH)
    for src, _ in RENAME:
        shutil.copy2(os.path.join(AO, src), os.path.join(ARCH, src))
    log("")
    log("[1] 已备份 %d 档到 %s" % (len(RENAME), ARCH))

    log("")
    log("[2] 执行改名")
    for src, dst in RENAME:
        os.rename(os.path.join(AO, src), os.path.join(AO, dst))
        log("  renamed: %s -> %s" % (src, dst))

    log("")
    log("[3] 复核")
    ok = True
    all_after = sorted(x for x in os.listdir(AO)
                       if x.endswith(".yaml") and not x.startswith(("_", "source-")))
    log("  档数 %d -> %d  %s" % (len(all_before), len(all_after),
                                 "OK" if len(all_before) == len(all_after) else "!! 不一致"))
    if len(all_before) != len(all_after):
        ok = False
    for src, dst in RENAME:
        dp = os.path.join(AO, dst)
        a, sz, di, b = sha256(dp), os.path.getsize(dp), docid_of(dp), before[src]
        good = (a == b["sha256"] and sz == b["size"] and di == b["docid"])
        log("  %-32s bytes %d/%d | sha %s | id %s" % (
            dst, sz, b["size"], "OK" if a == b["sha256"] else "!!", di))
        if not good:
            ok = False
    for src, _ in RENAME:
        if os.path.exists(os.path.join(AO, src)):
            log("  !! 旧名仍在：%s" % src)
            ok = False

    # [4] WS 与 AO 必须逐字节一致（双写约束）
    log("")
    log("[4] WS 与 AO 双写一致性")
    WS = os.path.join(ROOT, "tools", "worldbook-studio", "workspace", "full-geo1", "authoring")
    mismatch = 0
    for _, dst in RENAME:
        wp, ap = os.path.join(WS, dst), os.path.join(AO, dst)
        if not os.path.exists(wp):
            log("  !! WS 侧缺：%s" % dst)
            mismatch += 1
            continue
        same = sha256(wp) == sha256(ap)
        if not same:
            mismatch += 1
            log("  !! 不一致：%s" % dst)
    log("  8 档比对，不一致 %d 档  %s" % (mismatch, "OK" if mismatch == 0 else "!!"))
    if mismatch:
        ok = False

    log("")
    log("结论：%s" % ("全部通过" if ok else "有问题"))
    with io.open(REPORT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("\n".join(lines))
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
