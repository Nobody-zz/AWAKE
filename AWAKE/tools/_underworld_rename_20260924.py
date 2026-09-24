# -*- coding: utf-8 -*-
"""暗面批「前缀归一」改名：把 7 档文件名前缀统一成 underworld-。

铁律（本次）：
- **只改文件名，不动档内任何一行** —— 尤其 `id:` 字段（它是编译链主键，见 _dark_chain_20260920.py docid_of）。
- 改名前先按字节备份到 authoring/_archive-20260924/。
- 改完复核：档数不变 / 每档 id 未动 / 字节数一致 / head+tail sha256 一致。

只动这 7 个（甲方裁定范围）：
  town-alleys        -> underworld-alleys
  alley-gang-leaders -> underworld-gang-leaders
  alley-struggle     -> underworld-struggle
  town-gangs         -> underworld-gangs
  crime-rating       -> underworld-crime-rating
  blood-money        -> underworld-blood-money
  bandits            -> underworld-bandits
  smuggling          -> underworld-smuggling
（注：上表 8 项，其中 town-alleys / town-gangs 是表内补齐项）

不动：notables / serfs / small-factions
"""
import hashlib
import io
import os
import shutil

ROOT = r"D:\AWAKE-Dev\AWAKE"
AUTH = os.path.join(ROOT, "tools", "worldbook-studio", "workspace", "full-geo1", "authoring")
ARCH = os.path.join(AUTH, "_archive-20260924")
REPORT = os.path.join(ROOT, "tools", "_underworld_rename_20260924.txt")

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
    """照抄 _dark_chain_20260920.py 的读法：id: 那一行。"""
    with io.open(p, encoding="utf-8") as f:
        for ln in f.read(6000).splitlines():
            if ln.startswith("id:"):
                return ln.split(":", 1)[1].strip()
    return None


def main():
    log("== 暗面批前缀归一（文件名 -> underworld-*）==")
    log("")

    # [0] 前提核查
    all_before = sorted(x for x in os.listdir(AUTH)
                        if x.endswith(".yaml") and not x.startswith(("_", "source-")))
    log("[0] 改名前 authoring 下 yaml 档数 = %d" % len(all_before))

    before = {}
    for src, dst in RENAME:
        sp = os.path.join(AUTH, src)
        dp = os.path.join(AUTH, dst)
        if not os.path.exists(sp):
            log("  !! 缺档：%s" % src)
            return 1
        if os.path.exists(dp):
            log("  !! 目标已存在：%s" % dst)
            return 1
        before[src] = {
            "sha256": sha256(sp),
            "size": os.path.getsize(sp),
            "docid": docid_of(sp),
        }
        log("  %-26s -> %-32s  id=%-36s bytes=%d" % (
            src, dst, before[src]["docid"], before[src]["size"]))

    # [1] 备份（字节级）
    if not os.path.isdir(ARCH):
        os.makedirs(ARCH)
        log("")
        log("[1] 建备份目录 %s" % ARCH)
    else:
        log("")
        log("[1] 备份目录已存在，复用 %s" % ARCH)
    for src, _ in RENAME:
        shutil.copy2(os.path.join(AUTH, src), os.path.join(ARCH, src))
    log("  已备份 %d 档（副本文件名保持原名）" % len(RENAME))

    # [2] 改名
    log("")
    log("[2] 执行改名")
    for src, dst in RENAME:
        os.rename(os.path.join(AUTH, src), os.path.join(AUTH, dst))
        log("  renamed: %s -> %s" % (src, dst))

    # [3] 复核
    log("")
    log("[3] 复核")
    ok = True

    all_after = sorted(x for x in os.listdir(AUTH)
                       if x.endswith(".yaml") and not x.startswith(("_", "source-")))
    log("  改名前档数 %d / 改名后档数 %d  %s" % (
        len(all_before), len(all_after), "OK" if len(all_before) == len(all_after) else "!! 不一致"))
    if len(all_before) != len(all_after):
        ok = False

    for src, dst in RENAME:
        dp = os.path.join(AUTH, dst)
        a = sha256(dp)
        sz = os.path.getsize(dp)
        di = docid_of(dp)
        b = before[src]
        c_hash = "OK" if a == b["sha256"] else "!! 字节变了"
        c_size = "OK" if sz == b["size"] else "!! 大小变了"
        c_id = "OK" if di == b["docid"] else "!! id 变了"
        log("  %-32s bytes %d/%d %s | sha %s | id %s (%s)" % (
            dst, sz, b["size"], c_size, c_hash, di, c_id))
        if a != b["sha256"] or sz != b["size"] or di != b["docid"]:
            ok = False

    # 确认旧名已消失
    for src, _ in RENAME:
        if os.path.exists(os.path.join(AUTH, src)):
            log("  !! 旧名仍在：%s" % src)
            ok = False

    log("")
    log("结论：%s" % ("全部通过" if ok else "有问题，需人工复核"))

    with io.open(REPORT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("\n".join(lines))
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
