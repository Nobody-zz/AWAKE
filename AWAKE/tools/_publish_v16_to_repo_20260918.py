# -*- coding: utf-8 -*-
"""把 v16（护甲批分层表达定点修复）产物发到仓库侧包目录，并同步注册表里的三个哈希。

与 v15 的区别：**不是加条目，是原地更新其中 7 档**（482 → 482）：
  · torso-ring / shield-wicker 补了"通用 detail 层"表达；
  · torso-civil / torso-gambeson / torso-mail / cape-mantle / hands 的表达 id 去重。
详见 `tools/worldbook-studio/workspace/full-geo1/_v16_armor_detailfix_20260918.py` 文件头。

`ModuleData/Worldbook/` 是**仓库侧上线件**，注册表 `ModuleData/Worldbook/manifest.json`
逐包记着 contentHash / manifestHash / packageHash，运行时按 canonical-JSON 重算并比对
⇒ 换包必须同步这三个哈希，只拷文件会校验不过。

运行（仓库根为 CWD）：
  python -u tools/_publish_v16_to_repo_20260918.py [--apply]
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
SRC = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1/compiled/geo1-v16-armor-detail")
DST = os.path.join(ROOT, "ModuleData/Worldbook/packages/calradia")
REG = os.path.join(ROOT, "ModuleData/Worldbook/manifest.json")
FILES = ("index.json", "manifest.json", "runtime.json")
APPLY = "--apply" in sys.argv

EXPECT_ENTRIES = 482
PRIOR_ENTRIES = 482
# 本批原地更新、**内容应当变了**的 7 档
FIXED = ["war.weapons-armor-torso-ring", "war.weapons-armor-shield-wicker",
         "war.weapons-armor-torso-civil", "war.weapons-armor-torso-gambeson",
         "war.weapons-armor-torso-mail", "war.weapons-armor-cape-mantle",
         "war.weapons-armor-hands"]
# 必须补出"通用 detail 层"的两张卡
NEED_GENERIC_DETAIL = ["war.weapons-armor-torso-ring", "war.weapons-armor-shield-wicker"]
ARMORS = ["war.weapons-armor-torso-civil", "war.weapons-armor-torso-cloth",
          "war.weapons-armor-torso-gambeson", "war.weapons-armor-torso-leather",
          "war.weapons-armor-torso-mail", "war.weapons-armor-torso-ring",
          "war.weapons-armor-torso-lamellar", "war.weapons-armor-torso-scale",
          "war.weapons-armor-torso-plate", "war.weapons-armor-torso-brigandine",
          "war.weapons-armor-torso-fur", "war.weapons-armor-cape-mantle",
          "war.weapons-armor-cape-fur", "war.weapons-armor-cape-pauldron",
          "war.weapons-armor-cape-shoulders", "war.weapons-armor-shield-kite",
          "war.weapons-armor-shield-round", "war.weapons-armor-shield-heater",
          "war.weapons-armor-shield-wicker", "war.weapons-armor-legs",
          "war.weapons-armor-hands"]
HEADS = ["war.weapons-head-cheekguard", "war.weapons-head-closed", "war.weapons-head-cloth-coif",
         "war.weapons-head-crown", "war.weapons-head-fur-cap", "war.weapons-head-kettle",
         "war.weapons-head-layered", "war.weapons-head-mail-coif", "war.weapons-head-nasal",
         "war.weapons-head-oddity"]

stamp = time.strftime("%Y%m%d-%H%M%S")
BACKUP = os.path.join(ROOT, "tools/worldbook-studio/artifacts", "repo-package-before-v16-" + stamp)


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

os.makedirs(BACKUP, exist_ok=True)
for f in FILES:
    p = os.path.join(DST, f)
    if os.path.exists(p):
        shutil.copy2(p, os.path.join(BACKUP, f))
shutil.copy2(REG, os.path.join(BACKUP, "registry-manifest.json"))
print("备份 ->", os.path.relpath(BACKUP, ROOT))

for f in FILES:
    shutil.copy2(os.path.join(SRC, f), os.path.join(DST, f))
    print("COPY %-16s sha256=%s" % (f, sha(os.path.join(DST, f))[:16]))

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

# ① 21 条护甲卡都在包里，且各自有中文标题 / 关键词 / 锚点 / 来源
for short in ARMORS:
    eid = "awake:entry:" + short
    e = by_id.get(eid)
    title = ((e or {}).get("title") or {}).get("zh-CN")
    kw = (e or {}).get("keywords") or []
    refs = ((e or {}).get("extensions") or {}).get("entityRefs") or []
    print("READBACK %-40s 在包=%s title=%s 关键词=%d 锚点=%d"
          % (short.replace("war.weapons-armor-", ""), e is not None, title, len(kw), len(refs)))
    ok = ok and e is not None and bool(title)

# ② 上一版的 482 条**一条不少、一条不多**，且**只有那 7 档变了内容**
print("READBACK 条目数 = %d（期望 %d）" % (len(entries), PRIOR_ENTRIES))
ok = ok and len(entries) == PRIOR_ENTRIES
old_rt = load(os.path.join(BACKUP, "runtime.json")) if os.path.exists(os.path.join(BACKUP, "runtime.json")) else None
if old_rt:
    old_by = {e["id"]: e for e in old_rt["entries"]}
    id_ok = set(old_by) == set(by_id)
    changed = sorted(i for i in old_by
                     if json.dumps(old_by[i], sort_keys=True, ensure_ascii=False)
                     != json.dumps(by_id.get(i), sort_keys=True, ensure_ascii=False))
    want = sorted("awake:entry:" + s for s in FIXED)
    print("READBACK id 集合与上版一致 =%s" % id_ok)
    print("READBACK 相对上版变化 %d 条（期望恰好 %d）：%s"
          % (len(changed), len(want), [c.replace("awake:entry:war.weapons-armor-", "") for c in changed]))
    ok = ok and id_ok and changed == want
for short in HEADS:
    ok = ok and ("awake:entry:" + short) in by_id
print("READBACK 头盔 10 条仍在包 =%s" % all(("awake:entry:" + s) in by_id for s in HEADS))

# ③ 本批修复的**阳性对照**：两张卡现在必须能给出"不带 culture 的 detail 层"表达，
#    且每张卡的表达 id 不得重复。缺了就是这次修复没生效（别只看 VERDICT）。
for short in NEED_GENERIC_DETAIL:
    e = by_id["awake:entry:" + short]
    # 文化限定挂在 `grants[].conditions.culture_ids`（不是 grants 顶层 —— 第一版这条读错了层级，
    # 把 culture 表达也算成通用，读数虚高；已按真实结构改正）。
    # 判据是 **≥1**，不是 ==1：有 5 张卡合法地有两条通用 detail（主档 a1 ＋ 补充档 a2），
    # 写死等于 1 会把它们误判成不合规（本脚本第一版就这么错过一次）。
    gen = [x for x in e["expressions"]
           if x.get("detail") == "detail"
           and not any((y.get("conditions") or {}).get("culture_ids")
                       for y in x.get("grants") or [])]
    print("FIXCHECK  %-34s 通用detail表达=%d（期望 ≥1）" % (short.replace("war.weapons-armor-", ""), len(gen)))
    ok = ok and len(gen) >= 1
for short in ARMORS:
    ids = [x["id"] for x in by_id["awake:entry:" + short]["expressions"]]
    if len(ids) != len(set(ids)):
        print("FIXCHECK  %-34s 表达 id 有重复！" % short.replace("war.weapons-armor-", ""))
        ok = False
print("FIXCHECK  21 张卡表达 id 均无重复 =%s"
      % all(len([x["id"] for x in by_id["awake:entry:" + s]["expressions"]])
            == len({x["id"] for x in by_id["awake:entry:" + s]["expressions"]}) for s in ARMORS))

# ④ 三条概念词条没被动
for eid, title in [("awake:entry:geography.settlement-types-village", "村庄"),
                   ("awake:entry:geography.settlement-types-castle", "城堡"),
                   ("awake:entry:geography.settlement-types-town", "城镇")]:
    e = by_id.get(eid)
    got = ((e or {}).get("title") or {}).get("zh-CN")
    print("REGRESS  %-46s title=%s（期望 %s）" % (eid.replace("awake:entry:", ""), got, title))
    ok = ok and got == title

print("VERDICT %s" % ("PASS" if ok else "FAIL"))
sys.exit(0 if ok else 1)
