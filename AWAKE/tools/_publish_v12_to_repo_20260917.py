# -*- coding: utf-8 -*-
"""把 v12 产物发到仓库侧包目录，并同步注册表清单里的三个哈希。

同 v11 那份（`_publish_v11_to_repo_20260917.py`）的道理：
`ModuleData/Worldbook/` 是**仓库侧**上线件（真机目录由它拷过去），不是 `compiled/` 的软链；
注册表 `manifest.json` 逐包记着 contentHash / manifestHash / packageHash，
**运行时按 canonical-JSON 重算并比对** ⇒ 换包必须同步这三个哈希，只拷文件会校验不过。

本次三份文件里 `manifestHash` 不变；`contentHash`(runtime) 与 `packageHash`(整体) 会变。

运行（仓库根为 CWD）：
  python -u tools/_publish_v12_to_repo_20260917.py [--apply]
不带 --apply 只做预演（不改盘）。
"""
import io
import json
import os
import shutil
import sys
import time

ROOT = r"D:/AWAKE-Dev/AWAKE"
SRC = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1/compiled/geo1-v12-cortain-summary")
DST = os.path.join(ROOT, "ModuleData/Worldbook/packages/calradia")
REG = os.path.join(ROOT, "ModuleData/Worldbook/manifest.json")
FILES = ("index.json", "manifest.json", "runtime.json")
APPLY = "--apply" in sys.argv

stamp = time.strftime("%Y%m%d-%H%M%S")
BACKUP = os.path.join(ROOT, "tools/worldbook-studio/artifacts", "repo-package-before-v12-" + stamp)


def load(p):
    with io.open(p, encoding="utf-8") as fh:
        return json.load(fh)


def sha(p):
    import hashlib
    return hashlib.sha256(open(p, "rb").read()).hexdigest()


print("== 预演 ==" if not APPLY else "== 落盘 ==")
new_man = load(os.path.join(SRC, "manifest.json"))
old_man = load(os.path.join(DST, "manifest.json"))
new_h = new_man["hashes"]
old_h = old_man["hashes"]

print("源 :", SRC)
for k in ("contentHash", "manifestHash", "packageHash"):
    mark = "  (不变)" if new_h.get(k) == old_h.get(k) else "  **变**"
    print("  %-13s %s -> %s%s" % (k, str(old_h.get(k))[:16], str(new_h.get(k))[:16], mark))

reg = load(REG)
print("注册表 :", REG, " schemaVersion =", reg.get("schemaVersion"))
pkg = [p for p in reg["packages"] if p.get("packageId") == new_man["packageId"]]
if len(pkg) != 1:
    print("FATAL 注册表里 packageId=%s 的条目不是恰好 1 条：%d"
          % (new_man["packageId"], len(pkg)))
    sys.exit(1)
pkg = pkg[0]
print("  注册表里的键 :", sorted(pkg.keys()))
for k in ("contentHash", "manifestHash", "packageHash"):
    print("    %-13s %s -> %s" % (k, str(pkg.get(k))[:16], str(new_h.get(k))[:16]))

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
print("备份 ->", BACKUP)
for f in sorted(os.listdir(BACKUP)):
    print("   %-24s sha256=%s" % (f, sha(os.path.join(BACKUP, f))[:16]))

# ---- 拷文件（**逐字节**，不重新序列化；哈希靠字节一致才成立） ----
for f in FILES:
    shutil.copy2(os.path.join(SRC, f), os.path.join(DST, f))
    print("COPY %-16s sha256=%s" % (f, sha(os.path.join(DST, f))[:16]))

# ---- 补注册表哈希 ----
# ⚠️ 大小写按**既有约定**：注册表里是小写、包清单里是大写。
# 运行时比较是 `OrdinalIgnoreCase`（`WorldbookPackageIntegrity.StringEquals`），大小写不影响校验；
# 但按约定写，免得以后有人拿它当真差异去查。
for k in ("contentHash", "manifestHash", "packageHash"):
    if k in pkg:
        pkg[k] = new_h[k].lower() if isinstance(new_h[k], str) else new_h[k]
with io.open(REG, "w", encoding="utf-8", newline="\n") as fh:
    json.dump(reg, fh, ensure_ascii=False, indent=1)
    fh.write("\n")
print("PATCH", REG)

# ---- 回读验收 ----
chk = load(REG)
cp = [p for p in chk["packages"] if p.get("packageId") == new_man["packageId"]][0]
dst_man = load(os.path.join(DST, "manifest.json"))
ok = all(dst_man["hashes"][k] == new_h[k] for k in ("contentHash", "manifestHash", "packageHash"))
ok = ok and all((cp.get(k) or "").upper() == new_h[k] for k in ("contentHash", "manifestHash", "packageHash"))
dst_rt = load(os.path.join(DST, "runtime.json"))
n = len(dst_rt["entries"])
tgt = {"awake:entry:politics.clans-charas-cortain-secret": "戴·科尔坦家与沙拉斯港的海务财富。"}
for e in dst_rt["entries"]:
    if e["id"] in tgt:
        got = (e.get("summary") or {}).get("zh-CN")
        same = got.strip() == tgt[e["id"]]
        print("READBACK %-38s %s  (逐字相同=%s)" % (e["id"].replace("awake:entry:", ""), got, same))
        ok = ok and same
# 反向：v11 的两条仍应保持 v11 的形态（没被这次带回去）
keep = {"awake:entry:geography.mines-lycaron": "吕卡隆银矿与兵祸的由来。",
        "awake:entry:politics.throne-saneopa":
            "萨涅俄帕：隘口之上的内陆商埠与涅雷采斯家的京城；旧都岁月与迁都后的迁回之争。"}
for e in dst_rt["entries"]:
    if e["id"] in keep:
        got = (e.get("summary") or {}).get("zh-CN")
        same = got.strip() == keep[e["id"]]
        print("REGRESS  %-38s %s  (逐字相同=%s)" % (e["id"].replace("awake:entry:", ""), got, same))
        ok = ok and same
print("READBACK entries=%d" % n)
print("VERDICT %s" % ("PASS" if ok and n == 448 else "FAIL"))
sys.exit(0 if ok and n == 448 else 1)
