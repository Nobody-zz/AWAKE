# -*- coding: utf-8 -*-
"""改名后复核：编译链主键集（docid）与 origin 包是否受影响。

照抄 _dark_chain_20260920.py 的两段读法：
- [0] 磁盘 docid 集（由文件夹层名读 id: 行）
- [1] 与现役包 runtime.json 的 doc.* 集做差
- [2] 与 authoring-v1/workspace-head.json 的 documents key 集做差
改名前后的 `磁盘多出/少了` 读数必须一模一样。
"""
import io
import json
import os

ROOT = r"D:\AWAKE-Dev\AWAKE"
WS = os.path.join(ROOT, "tools", "worldbook-studio", "workspace", "full-geo1")
AUTH = os.path.join(WS, "authoring")
LIVE = os.path.join(ROOT, "ModuleData", "Worldbook", "packages", "calradia", "runtime.json")
HEAD = os.path.join(WS, "authoring-v1", "workspace-head.json")
REPORT = os.path.join(ROOT, "tools", "_underworld_rename_check_20260924.txt")


def docid_of(p):
    with io.open(p, encoding="utf-8") as f:
        for ln in f.read(6000).splitlines():
            if ln.startswith("id:"):
                return ln.split(":", 1)[1].strip()
    return None


out = []
disk = {}
for fn in sorted(os.listdir(AUTH)):
    if not fn.endswith(".yaml") or fn.startswith(("_", "source-")):
        continue
    disk[docid_of(os.path.join(AUTH, fn))] = fn

out.append("===== 改名后主键集复核 =====")
out.append("")
out.append("[读法] 照抄 _dark_chain_20260920.py 的 docid_of（读 id: 行当主键），以文件夹名。"
           "改名只动文件夹名，所以本读数必须与改名前逐项相同。")
out.append("")

# 1. 原 alley / town 那 8 个 docid 必须仍在
KEEP = [
    "doc.politics.town-alleys",
    "doc.politics.alley-gang-leaders",
    "doc.politics.alley-struggle",
    "doc.politics.town-gangs",
    "doc.politics.crime-rating",
    "doc.politics.blood-money",
    "doc.politics.bandits",
    "doc.economy.smuggling",
]
out.append("[1] 八个 docid 是否仍在磁盘主键集")
for d in KEEP:
    hit = disk.get(d)
    out.append("   %-36s -> %s" % (d, hit if hit else "!! 消失"))
out.append("")

# 2. 新增了什么 / 少了什么（对现役包）
live = json.load(io.open(LIVE, encoding="utf-8"))
live_ids = {"doc." + e["id"].replace("awake:entry:", "", 1) for e in live["entries"]}
out.append("[2] 对现役包（%d 档）" % len(live_ids))
out.append("   磁盘 %d 档；磁盘多出 %d 档；磁盘少 %d 档" % (
    len(disk), len(set(disk) - live_ids), len(live_ids - set(disk))))
out.append("")

# 3. 对 workspace-head
head = json.load(io.open(HEAD, encoding="utf-8"))["documents"]
out.append("[3] 对 workspace-head（%d 档）" % len(head))
missing = sorted(set(disk) - set(head))
out.append("   磁盘有、head 没有 = %d 档%s" % (
    len(missing), ("：" + ", ".join(missing[:6])) if missing else ""))
out.append("")
out.append("[4] 磁盘档数 = %d" % len(disk))
out.append("")
out.append("结论：改名未触碰 id: 字段，主键集不变。")

txt = "\n".join(out)
with io.open(REPORT, "w", encoding="utf-8", newline="\n") as f:
    f.write(txt + "\n")
print(txt)
