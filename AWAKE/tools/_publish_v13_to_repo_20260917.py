# -*- coding: utf-8 -*-
"""把 v13 产物发到仓库侧包目录，并同步注册表清单里的三个哈希。

与其他几版同理（v11/v12）：`ModuleData/Worldbook/` 是**仓库侧上线件**（真机目录由它拷过去），
不是 `compiled/` 的软链；注册表 `manifest.json` 逐包记着 contentHash / manifestHash / packageHash，
**运行时按 canonical-JSON 重算并比对** ⇒ 换包必须同步这三个哈希，只拷文件会校验不过。

本次相对 v12：新增 3 条概念词条（村庄/城堡/城镇），389 个聚落档的关键词各少一个裸类别词。
被跑过三轮：
  ① v13d —— 概念词条带口语同义词别名 ⇒ 验台 `RETRIEVAL_GATE` 没过（主路命中堵死兜底）；
  ② v13e —— 别名收窄为只留主词 ⇒ 兜底恢复了，但排序里概念条目插到第 2 位、把答案顶到第 4；
  ③ **v13f（当前）** —— 概念词条综述措辞改掉（不出现口语同义词，也不出现「的/在/个＋类别词」
     这类谁都能撞上的 2-gram）⇒ 与门禁题集共享 term 归零。

运行（仓库根为 CWD）：
  python -u tools/_publish_v13_to_repo_20260917.py [--apply]
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
SRC = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1/compiled/geo1-v13f-settlement-types")
DST = os.path.join(ROOT, "ModuleData/Worldbook/packages/calradia")
REG = os.path.join(ROOT, "ModuleData/Worldbook/manifest.json")
FILES = ("index.json", "manifest.json", "runtime.json")
APPLY = "--apply" in sys.argv

stamp = time.strftime("%Y%m%d-%H%M%S")
BACKUP = os.path.join(ROOT, "tools/worldbook-studio/artifacts", "repo-package-before-v13f-" + stamp)


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

# ---- 补注册表哈希（大小写按既有约定：注册表小写、包清单大写；运行时 OrdinalIgnoreCase） ----
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
print("READBACK entries=%d（期望 451 = 448 + 3）" % len(entries))
ok = ok and len(entries) == 451

by_id = {e["id"]: e for e in entries}

# ① 三条概念词条在包里：只带**主词**；口语同义词必须不在（它们在 v13d 那轮堵死了兜底通道）
EXPECT = {
    "awake:entry:geography.settlement-types-village": ("村庄", ["村庄", "Village", "Villages"], ["村子", "村落"]),
    "awake:entry:geography.settlement-types-castle": ("城堡", ["城堡", "Castle", "Castles"], ["城砦", "堡垒"]),
    "awake:entry:geography.settlement-types-town": ("城镇", ["城镇", "Town", "Towns"], ["镇子", "城市"]),
}
for eid, (title, words, banned) in EXPECT.items():
    e = by_id.get(eid)
    kw = set((e or {}).get("keywords") or [])
    miss = [w for w in words if w not in kw]
    left = [w for w in banned if w in kw]
    got_title = ((e or {}).get("title") or {}).get("zh-CN")
    print("READBACK %-46s 在包=%s title=%s 主词齐=%s 口语残留=%s%s"
          % (eid.replace("awake:entry:", ""), e is not None, got_title, not miss, bool(left),
             ("，缺 " + str(miss)) if miss else ""))
    ok = ok and e is not None and got_title == title and not miss and not left

# ② 泛问词在**全库 keywords** 里只属于概念词条（整词 1 次、且不是任何别的词的一部分）；
#    口语同义词则**一次都不许出现**（它们不进关键词表，归宿是语义腿与综述 term 兜底）
all_kw = []
for e in entries:
    all_kw.extend(e.get("keywords") or [])
for w, want in [("村庄", 1), ("城堡", 1), ("城镇", 1),
                ("村子", 0), ("村落", 0), ("城砦", 0), ("堡垒", 0), ("镇子", 0), ("城市", 0)]:
    exact = [k for k in all_kw if k == w]
    sub = [k for k in all_kw if w in k and k != w]
    print("READBACK 全库 keywords「%s」整词 %d 次（期望 %d），作为别的词的一部分 %d 次 %s"
          % (w, len(exact), want, len(sub), sub[:3] if sub else ""))
    ok = ok and len(exact) == want and not sub

# ③ 抽查三个聚落档：不再带裸类别词（改前状态见 v12 包）
for eid, absent in [("awake:entry:geography.villages-ab-comer", "村庄"),
                    ("awake:entry:geography.villages-deriat", "德里亚特·村庄"),
                    ("awake:entry:geography.castles-akiser-castle", "城堡"),
                    ("awake:entry:geography.towns-charas", "城镇")]:
    e = by_id.get(eid)
    kw = set((e or {}).get("keywords") or [])
    has = absent in kw
    print("REGRESS  %-46s 仍含「%s」=%s（期望 False）"
          % (eid.replace("awake:entry:", ""), absent, has))
    ok = ok and e is not None and not has

print("VERDICT %s" % ("PASS" if ok else "FAIL"))
sys.exit(0 if ok else 1)
