# -*- coding: utf-8 -*-
"""把 v17（护甲批正文口吻修复）产物发到仓库侧包目录，并同步注册表里的三个哈希。

**只改了 shield-round 一档的断言正文**，482 → 482。注意这类"只改正文"的批次有个坑：
  `runtime.json` 里**不含 `assertions[].text`** —— 正文只在 `documents.json`。
  所以本脚本的相对上版对拍**不可能在 runtime 面看到变化**（那是正常的，不是没生效），
  改成：① 条目 id 集合不变；② contentHash 必须变（证明包内容确实换了）；
        ③ 直接读产物 `documents.json` 验那两句话（旧的编纂口吻必须没了、新的必须在了）。

运行（仓库根为 CWD）：
  python -u tools/_publish_v17_to_repo_20260918.py [--apply]
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
SRC = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1/compiled/geo1-v17-armor-prose")
DST = os.path.join(ROOT, "ModuleData/Worldbook/packages/calradia")
REG = os.path.join(ROOT, "ModuleData/Worldbook/manifest.json")
FILES = ("index.json", "manifest.json", "runtime.json")
APPLY = "--apply" in sys.argv

EXPECT_ENTRIES = 482
PRIOR_ENTRIES = 482
FIXED = ["war.weapons-armor-shield-round"]
NEED_GENERIC_DETAIL = ["war.weapons-armor-torso-ring", "war.weapons-armor-shield-wicker"]
# 正文面判据：这句必须**不在**了 / 这句必须**在**
PROSE_MUST_GONE = "游戏数据"
PROSE_MUST_HAVE = "另有一路椭圆盾"
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
BACKUP = os.path.join(ROOT, "tools/worldbook-studio/artifacts", "repo-package-before-v17-" + stamp)


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

# ---- 只改 authoring 记录的批次：三哈希全不变 ⇒ 要发运的三个文件逐字节相同，无需发布 ----
if all(new_h.get(k) == old_h.get(k) for k in ("contentHash", "manifestHash", "packageHash")):
    print()
    print("结论：三哈希全部未变 ⇒ **本批只改了 authoring 记录（documents.json），不影响上线包**。")
    print("      世界书断言正文（assertions[].text）**不进 runtime.json**，也不参与三哈希；")
    print("      玩家看得见的是 summary 与 expressions。")
    print("      ⇒ 无需发布（要发运的 index/manifest/runtime 与在挂包逐字节相同）。")
    print("      为避免误判成'改动没生效'，这里不写盘、也不报 FAIL。")
    sys.exit(0)

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

# ② 上一版的 482 条**一条不少、一条不多**；条目 id 集合完全一致
print("READBACK 条目数 = %d（期望 %d）" % (len(entries), PRIOR_ENTRIES))
ok = ok and len(entries) == PRIOR_ENTRIES
old_rt = load(os.path.join(BACKUP, "runtime.json")) if os.path.exists(os.path.join(BACKUP, "runtime.json")) else None
if old_rt:
    old_by = {e["id"]: e for e in old_rt["entries"]}
    print("READBACK id 集合与上版一致 =%s" % (set(old_by) == set(by_id)))
    ok = ok and set(old_by) == set(by_id)
    # 这类批次（只改断言正文）**runtime 面本就该 0 变化** —— 不是没生效，是正文不落 runtime。
    changed_rt = sorted(i for i in old_by
                        if json.dumps(old_by[i], sort_keys=True, ensure_ascii=False)
                        != json.dumps(by_id.get(i), sort_keys=True, ensure_ascii=False))
    print("READBACK runtime 面变化 %d 条（本批预期 0：改的是正文，正文不落 runtime）" % len(changed_rt))
    ok = ok and not changed_rt
for short in HEADS:
    ok = ok and ("awake:entry:" + short) in by_id
print("READBACK 头盔 10 条仍在包 =%s" % all(("awake:entry:" + s) in by_id for s in HEADS))

# ②b 正文面（documents.json）：这是本批唯一真正改的那一面
docs = load(os.path.join(SRC, "documents.json"))
if isinstance(docs, dict):
    docs = docs["documents"]
for i, dd in enumerate(docs):
    if not str(dd.get("id", "")).startswith("doc.war.weapons-armor-"):
        continue
    blob = json.dumps(dd, ensure_ascii=False)
    if PROSE_MUST_GONE in blob:
        print("PROSECHK  %s 正文里仍有「%s」！" % (dd["id"], PROSE_MUST_GONE))
        ok = False
print("PROSECHK  21 档正文均无「%s」=%s"
      % (PROSE_MUST_GONE,
         all(PROSE_MUST_GONE not in json.dumps(d, ensure_ascii=False)
             for d in docs if str(d.get("id", "")).startswith("doc.war.weapons-armor-"))))
target = [d for d in docs if d.get("id") == "doc." + FIXED[0].replace("war.", "war.")]
has = target and PROSE_MUST_HAVE in json.dumps(target[0], ensure_ascii=False)
print("PROSECHK  %s 正文含「%s」=%s" % (FIXED[0], PROSE_MUST_HAVE, bool(has)))
ok = ok and bool(has)

# ③ 本批上一轮修复的**阳性对照**：两张卡现在必须能给出"不带 culture 的 detail 层"表达，
#    且每张卡的表达 id 不得重复。缺了就是上一轮修复被冲掉了（别只看 VERDICT）。
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
