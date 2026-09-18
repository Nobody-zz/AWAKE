# -*- coding: utf-8 -*-
"""改写 knowledge-taxonomy.v1.json（2026-09-14 分类统一）。
1) geography: terrain -> mountain/plateau/peninsula/desert；rivers -> waters；settlements -> castle/village/town
2) culture: faith -> tale（整块替换）
3) war: military_system -> military（仅改 id，标签/说明不动）
原档备份到 corrections_20260914/。"""
import io
import json
import os
import shutil

ROOT = r"D:\AWAKE-Dev\AWAKE"
TAX = os.path.join(ROOT, "docs", "worldbook-studio-plan", "knowledge-taxonomy.v1.json")
BK = os.path.join(ROOT, "docs", "worldbook-migration", "corrections_20260914")
os.makedirs(BK, exist_ok=True)


def sub(sid, label, help_, examples):
    return {"id": sid, "label": {"zh-CN": label}, "help": {"zh-CN": help_},
            "examples": {"zh-CN": examples}}


NEW = {
    "mountain": sub("mountain", "山岭", "山脉、山峰、山隘与高地。", ["哪座山横在两城之间", "山口在何处"]),
    "plateau": sub("plateau", "高原", "高原、台地与开阔高地。", ["高原上如何行军"]),
    "peninsula": sub("peninsula", "半岛", "伸入水域的陆地与海岬。", ["半岛上住着谁"]),
    "desert": sub("desert", "荒漠", "沙漠、荒原与缺水之地。", ["沙漠在哪里", "穿越沙漠要几天"]),
    "waters": sub("waters", "水域", "河流、湖泊、海湾、海域、渡口和水域通行。", ["哪条河流经城镇", "哪里可以渡河"]),
    "castle": sub("castle", "城堡", "城堡、要塞化聚落和它们所在的位置。", ["某城堡下辖哪些村庄"]),
    "village": sub("village", "村庄", "村庄和它们所在的位置。", ["某村庄位于哪片土地"]),
    "town": sub("town", "城镇", "城镇和它们所在的位置。", ["某城镇在何处"]),
}
TALE = sub("tale", "传说", "传说、怪谈和不同口径并存的说法。", ["此地流传什么传说"])

EXPAND = {
    "terrain": ["mountain", "plateau", "peninsula", "desert"],
    "rivers": ["waters"],
    "settlements": ["castle", "village", "town"],
}
RENAME = {"military_system": "military"}
REPLACE = {"faith": TALE}

raw = io.open(TAX, encoding="utf-8").read()
obj = json.loads(raw)
old_stats = {}

for dom in obj["domains"]:
    subs = dom["subdomains"]
    old_stats[dom["id"]] = [s["id"] for s in subs]
    out = []
    for s in subs:
        sid = s["id"]
        if sid in EXPAND:
            for nid in EXPAND[sid]:
                out.append(NEW[nid])
        elif sid in REPLACE:
            out.append(REPLACE[sid])
        elif sid in RENAME:
            s = dict(s)
            s["id"] = RENAME[sid]
            out.append(s)
        else:
            out.append(s)
    dom["subdomains"] = out

new_text = json.dumps(obj, indent=2, ensure_ascii=False) + "\n"

# 备份原档
shutil.copy2(TAX, os.path.join(BK, "knowledge-taxonomy.v1.json.bak"))
io.open(TAX, "w", encoding="utf-8", newline="").write(new_text)

print("已改写:", TAX)
for dom in obj["domains"]:
    o, n = old_stats[dom["id"]], [s["id"] for s in dom["subdomains"]]
    flag = "  <== 变" if o != n else ""
    print("  %-10s %2d -> %2d%s" % (dom["id"], len(o), len(n), flag))
    if o != n:
        print("       old:", ",".join(o))
        print("       new:", ",".join(n))
tot = sum(len(d["subdomains"]) for d in obj["domains"])
print("子域合计:", tot)
