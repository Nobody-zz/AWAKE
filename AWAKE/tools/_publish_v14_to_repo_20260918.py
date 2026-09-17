# -*- coding: utf-8 -*-
"""把 v14（头盔批并线）产物发到仓库侧包目录，并同步注册表里的三个哈希。

与其他几版同理（v11/v12/v13）：`ModuleData/Worldbook/` 是**仓库侧上线件**（真机目录由它拷过去），
不是 `compiled/` 的软链；注册表 `ModuleData/Worldbook/manifest.json` 逐包记着
contentHash / manifestHash / packageHash，**运行时按 canonical-JSON 重算并比对**
⇒ 换包必须同步这三个哈希，只拷文件会校验不过。

本次相对上一版（v13f）：**只加 10 条头盔形制卡**（`war.weapons-head-*`），
451 → 461。其余一条不动（v14 链里已逐条对拍过）。

运行（仓库根为 CWD）：
  python -u tools/_publish_v14_to_repo_20260918.py [--apply]
不带 --apply 只做预演（不改盘）。
"""
import hashlib
import io
import json
import os
import shutil
import sys
import time

ROOT = r"D:/AWAKE-Dev/AWAKE"
SRC = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1/compiled/geo1-v14-head-merge")
DST = os.path.join(ROOT, "ModuleData/Worldbook/packages/calradia")
REG = os.path.join(ROOT, "ModuleData/Worldbook/manifest.json")
FILES = ("index.json", "manifest.json", "runtime.json")
APPLY = "--apply" in sys.argv

EXPECT_ENTRIES = 461
HEADS = ["war.weapons-head-cheekguard", "war.weapons-head-closed", "war.weapons-head-cloth-coif",
         "war.weapons-head-crown", "war.weapons-head-fur-cap", "war.weapons-head-kettle",
         "war.weapons-head-layered", "war.weapons-head-mail-coif", "war.weapons-head-nasal",
         "war.weapons-head-oddity"]

stamp = time.strftime("%Y%m%d-%H%M%S")
BACKUP = os.path.join(ROOT, "tools/worldbook-studio/artifacts", "repo-package-before-v14-" + stamp)


def load(p):
    with io.open(p, encoding="utf-8") as fh:
        return json.load(fh)


def sha(p):
    return hashlib.sha256(open(p, "rb").read()).hexdigest()


print("== 预演 ==" if not APPLY else "== 落盘 ==")
new_man = load(os.path.join(SRC, "manifest.json"))
old_man = load(os.path.join(DST, "manifest.json"))
new_h, old_h = new_man["hashes"], old_man["hashes"]

print("源 :", os.path.relpath(SRC, ROOT))
for k in ("contentHash", "manifestHash", "packageHash"):
    mark = "  (不变)" if new_h.get(k) == old_h.get(k) else "  **变**"
    print("  %-13s %s -> %s%s" % (k, str(old_h.get(k))[:16], str(new_h.get(k))[:16], mark))

reg = load(REG)
print("注册表 :", os.path.relpath(REG, ROOT), " schemaVersion =", reg.get("schemaVersion"))
pkg = [p for p in reg["packages"] if p.get("packageId") == new_man["packageId"]]
if len(pkg) != 1:
    print("FATAL 注册表里 packageId=%s 的条目不是恰好 1 条：%d" % (new_man["packageId"], len(pkg)))
    sys.exit(1)
pkg = pkg[0]
for k in ("contentHash", "manifestHash", "packageHash"):
    print("    注册表 %-13s %s -> %s" % (k, str(pkg.get(k))[:16], str(new_h.get(k))[:16]))

if not APPLY:
    print("（预演结束，未改盘。加 --apply 落盘）")
    sys.exit(0)

# ---- 备份 ----
os.makedirs(BACKUP, exist_ok=True)
for f in FILES:
    p = os.path.join(DST, f)
    if os.path.exists(p):
        shutil.copy2(p, os.path.join(BACKUP, f))
shutil.copy2(REG, os.path.join(BACKUP, "registry-manifest.json"))
print("备份 ->", os.path.relpath(BACKUP, ROOT))

# ---- 拷文件（逐字节，不重新序列化） ----
for f in FILES:
    shutil.copy2(os.path.join(SRC, f), os.path.join(DST, f))
    print("COPY %-16s sha256=%s" % (f, sha(os.path.join(DST, f))[:16]))

# ---- 补注册表哈希（注册表小写、包清单大写；运行时 OrdinalIgnoreCase） ----
for k in ("contentHash", "manifestHash", "packageHash"):
    if k in pkg:
        pkg[k] = new_h[k].lower() if isinstance(new_h[k], str) else new_h[k]
with io.open(REG, "w", encoding="utf-8", newline="\n") as fh:
    json.dump(reg, fh, ensure_ascii=False, indent=1)
    fh.write("\n")
print("PATCH", os.path.relpath(REG, ROOT))

# ---- 回读验收 ----
chk = load(REG)
cp = [p for p in chk["packages"] if p.get("packageId") == new_man["packageId"]][0]
dst_man = load(os.path.join(DST, "manifest.json"))
ok = all(dst_man["hashes"][k] == new_h[k] for k in ("contentHash", "manifestHash", "packageHash"))
ok = ok and all((cp.get(k) or "").upper() == new_h[k] for k in ("contentHash", "manifestHash", "packageHash"))
print("READBACK 三哈希 包清单与注册表均已同步 =%s" % ok)

rt = load(os.path.join(DST, "runtime.json"))
entries = rt["entries"]
by_id = {e["id"]: e for e in entries}
print("READBACK entries=%d（期望 %d）" % (len(entries), EXPECT_ENTRIES))
ok = ok and len(entries) == EXPECT_ENTRIES

# ① 10 条头盔卡都在包里，且各自有中文标题 / 锚点 / 来源
for short in HEADS:
    eid = "awake:entry:" + short
    e = by_id.get(eid)
    title = ((e or {}).get("title") or {}).get("zh-CN")
    kw = (e or {}).get("keywords") or []
    refs = ((e or {}).get("extensions") or {}).get("entityRefs") or []
    print("READBACK %-42s 在包=%s title=%s 关键词=%d 锚点=%d"
          % (short, e is not None, title, len(kw), len(refs)))
    ok = ok and e is not None and bool(title)

# ② 原来那 451 条**一条不少**
print("READBACK 原有 451 条仍在包 =%s" % (len(entries) == len(by_id) and len(entries) - len(HEADS) == 451))
ok = ok and len(entries) - len(HEADS) == 451

# ③ 三条概念词条没被动（上一版刚修好的）
for eid, title in [("awake:entry:geography.settlement-types-village", "村庄"),
                   ("awake:entry:geography.settlement-types-castle", "城堡"),
                   ("awake:entry:geography.settlement-types-town", "城镇")]:
    e = by_id.get(eid)
    got = ((e or {}).get("title") or {}).get("zh-CN")
    print("REGRESS  %-46s title=%s（期望 %s）" % (eid.replace("awake:entry:", ""), got, title))
    ok = ok and got == title

print("VERDICT %s" % ("PASS" if ok else "FAIL"))
sys.exit(0 if ok else 1)
