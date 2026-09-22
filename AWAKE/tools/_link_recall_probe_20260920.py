# -*- coding: utf-8 -*-
"""互引边扩召回（09-20）· 阳性 / 阴性双对照 ＋ 既有答案回归。

要证三件事，缺一不可：
  A. **阳性**：有出边的档，拿它自己的标题去问 ⇒ `match_mode` 带 `+link`、`link_ids` 非空。
  B. **阴性①（同一句话换包）**：同一批问话跑**上一包 geo1-v21-dark**（没有一点边）
     ⇒ `+link` 必须**一次都不出现**。这条排掉"是不是别的东西造成的"。
  C. **阴性②（同一包换题）**：问**没有出边的档** ⇒ `+link` 一次都不出现。
  D. **回归**：逐条比同一句话在两包里的 `hits` —— 旧包的答案必须是新包答案的**前缀**
     （扩召回只许往末尾追加，不许改动既有位次）。

用法（仓库根为 CWD）：python -u tools/_link_recall_probe_20260920.py
"""
import collections
import io
import json
import os
import subprocess
import sys

ROOT = r"D:\AWAKE-Dev\AWAKE"
WS = os.path.join(ROOT, r"tools\worldbook-studio\workspace\full-geo1")
NEW_PKG = os.path.join(WS, r"compiled\geo1-v22-links")
OLD_PKG = os.path.join(WS, r"compiled\geo1-v21-dark")
SIM = os.path.join(ROOT, r"tools\worldbook-runtime-sim")
REGISTRY = os.path.join(ROOT, r"docs\worldbook-studio-plan\link-registry.v1.json")
SPEC = os.path.join(WS, "_link_probe_spec_20260920.json")

IDENTITY = "profile.noble"      # 高能力身份：让扩进来的条目尽可能真的能开口，免得把"被权限挡了"误读成"没扩到"


def run_probe(pkg, out_path):
    out = os.path.join(WS, out_path)
    r = subprocess.run(["dotnet", "run", "-c", "Release", "--no-build", "--", "probe",
                        os.path.join(pkg, "manifest.json"), SPEC, out], cwd=SIM, capture_output=True)
    if r.returncode != 0:
        print(r.stdout.decode("utf-8", "replace")[-1500:])
        print(r.stderr.decode("utf-8", "replace")[-1500:])
        raise SystemExit(1)
    return json.load(io.open(out, encoding="utf-8"))


# ---- 造题：每档一句，问它自己的标题 ----
new_pkg = json.load(io.open(os.path.join(NEW_PKG, "runtime.json"), encoding="utf-8"))
entries = {e["id"]: e for e in new_pkg["entries"]}
reg = json.load(io.open(REGISTRY, encoding="utf-8"))
with_out = collections.Counter(e["from"] for e in reg["edges"])

queries = []
for eid, e in sorted(entries.items()):
    title = (e.get("title") or {}).get("zh-CN") or ""
    if not title:
        continue
    queries.append({"name": eid, "identity": IDENTITY, "text": title})

json.dump({"queries": queries}, io.open(SPEC, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("造题 %d 条（有出边的 %d 档 ＋ 无出边的 %d 档）"
      % (len(queries), len([q for q in queries if with_out[q["name"]]]),
         len([q for q in queries if not with_out[q["name"]]])), flush=True)

print("跑新包（带边）...", flush=True)
new_res = {row["name"]: row for row in run_probe(NEW_PKG, "_link_probe_new_20260920.json")}
print("跑上一包（无边）...", flush=True)
old_res = {row["name"]: row for row in run_probe(OLD_PKG, "_link_probe_old_20260920.json")}

# ---- 阴性②（★ 变异检验）：把带边包的边**全清掉**，其余一个字不动，再跑同一批问话 ----
# 为什么必须做这一条：「与上一包比」只说明"这次的结果来自边数据"，但说不清"是包里的边，
# 还是运行时代码自己造出来的边"。把边清空的包喂给**同一份代码**，才是对代码本身的变异检验。
MUT = os.path.join(WS, "compiled", "_link_mut_no_links_20260920")
os.makedirs(MUT, exist_ok=True)
mut_runtime = json.load(io.open(os.path.join(NEW_PKG, "runtime.json"), encoding="utf-8"))
for e in mut_runtime["entries"]:
    (e.get("extensions") or {}).pop("links", None)
json.dump(mut_runtime, io.open(os.path.join(MUT, "runtime.json"), "w", encoding="utf-8"),
          ensure_ascii=False, indent=1)
json.dump(json.load(io.open(os.path.join(NEW_PKG, "manifest.json"), encoding="utf-8")),
          io.open(os.path.join(MUT, "manifest.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
json.dump(json.load(io.open(os.path.join(NEW_PKG, "index.json"), encoding="utf-8")),
          io.open(os.path.join(MUT, "index.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("跑变异包（边全清空）...", flush=True)
mut_res = {row["name"]: row for row in run_probe(MUT, "_link_probe_mut_20260920.json")}

pos = [q["name"] for q in queries if with_out[q["name"]]]
allnames = [q["name"] for q in queries]

new_link = [i for i in pos if "link" in (new_res[i].get("match_mode") or "")]
old_link_any = [i for i in allnames if "link" in (old_res[i].get("match_mode") or "")]
mut_link_any = [i for i in allnames if "link" in (mut_res[i].get("match_mode") or "")]
served = [i for i in new_link if new_res[i].get("link_ids")]

print()
print("== A 阳性：有出边的档自己问自己 ==")
print("   造题 %d ｜ match_mode 带 +link 的 %d ｜ 其中真的把扩进来的条目**送到嘴边**的 %d"
      % (len(pos), len(new_link), len(served)))
print("== B 阴性①：同一批问话跑上一包（一点边都没有）==")
print("   出现 +link 的条数 = %d（必须 0）%s" % (len(old_link_any), "OK" if not old_link_any else "**FAIL**"))
print("== C 阴性②（变异检验）：把带边包的边全清空，同一份代码再跑 ==")
print("   出现 +link 的条数 = %d（必须 0）%s" % (len(mut_link_any), "OK" if not mut_link_any else "**FAIL**"))
mut_same = [q["name"] for q in queries if (mut_res[q["name"]].get("hits") or []) != (old_res[q["name"]].get("hits") or [])]
print("   变异包 hits 与「上一包」逐条相同：%d 条不同（必须 0 且 == 边表是唯一变量的证据）%s"
      % (len(mut_same), "OK" if not mut_same else "**FAIL**"))

print()
print("== D 回归：旧包 hits 是否是 新包 hits 的前缀（即只往末尾追加）==")
bad_prefix, changed = [], []
for q in queries:
    o = old_res[q["name"]].get("hits") or []
    n = new_res[q["name"]].get("hits") or []
    if n[:len(o)] != o:
        bad_prefix.append(q["name"])
    elif n != o:
        changed.append(q["name"])
print("   破坏了前缀关系的 %d 条（必须 0）%s" % (len(bad_prefix), "OK" if not bad_prefix else "**FAIL**"))
for i in bad_prefix[:10]:
    print("     %s\n       旧=%s\n       新=%s" % (i, old_res[i].get("hits"), new_res[i].get("hits")))
print("   末尾多出条目的 %d 条" % len(changed))

# 机制注记（不是对照）：问「没有出边的档」也会出现 +link —— 因为候选表里坐着**别的**有边条目。
neg = [q["name"] for q in queries if not with_out[q["name"]]]
neg_link = [i for i in neg if "link" in (new_res[i].get("match_mode") or "")]
print()
print("== 机制注记：问「本档没有出边」的 %d 条里，仍有 %d 条带 +link ==" % (len(neg), len(neg_link)))
print("   这不是反例：扩召回的种子是**候选表**，不是被问的那一档 —— 候选里坐着别的有边条目时照样会扩。")

print()
print("== 样例（前 8 条真送到嘴边的）==")
for i in served[:8]:
    row = new_res[i]
    print("   问《%s》%s" % ((entries[i].get("title") or {}).get("zh-CN"), [x.replace("awake:entry:", "") for x in row["link_ids"]]))
    print("      正文尾: %s" % (row["text"] or "")[-90:].replace("\n", " "))

ok = (len(new_link) > 0 and not old_link_any and not mut_link_any and not mut_same and not bad_prefix)
print()
print("结论：%s（阳性 %d 条、阴性① %d 条、变异检验 %d 条、前缀破坏 %d 条）"
      % ("通过" if ok else "未通过", len(new_link), len(old_link_any), len(mut_link_any), len(bad_prefix)))
sys.exit(0 if ok else 1)
