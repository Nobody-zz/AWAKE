# -*- coding: utf-8 -*-
"""村庄铺开进度：档内 sid ↔ 273 村清单，按文化圈算已入档/待铺。"""
import os, re, json, glob

AO = os.path.dirname(os.path.abspath(__file__))
inv = json.load(open(os.path.join(AO, "_village_inventory_20260913.json"), encoding="utf-8"))
print("清单村数:", len(inv))

done = {}   # sid -> 档名
for p in sorted(glob.glob(os.path.join(AO, "village-*.yaml"))):
    t = open(p, encoding="utf-8").read()
    sids = set(re.findall(r"bannerlord\.villages#([A-Za-z0-9_]+)", t))
    sids |= set(re.findall(r"entity\.settlement\.([A-Za-z0-9_]+)", t))
    # 兼容 B 级编年史档（无 locator，用 source 内村庄名兜底）
    if not sids:
        m = re.search(r"source\.calradia\.chronicle\.([a-z0-9\-]+)-village", t)
        if m:
            sids = {"__slug__" + m.group(1)}
            print("  兜底(编年史档):", os.path.basename(p), "→", m.group(1))
    if not sids:
        print("  ⚠️ 无 sid:", os.path.basename(p)); continue
    for s in sids:
        done.setdefault(s.lower(), os.path.basename(p))
print("入档 sid 数:", len(done), " 档数:", len(glob.glob(os.path.join(AO, "village-*.yaml"))))

CN = {"aserai": "阿塞莱", "empire": "帝国", "vlandia": "瓦兰迪亚",
      "khuzait": "库赛特", "battania": "巴旦尼亚", "sturgia": "斯特吉亚"}
tot, dn = {}, {}
for v in inv:
    c = v.get("culture", "?")
    tot[c] = tot.get(c, 0) + 1
    if v["sid"].lower() in done:
        dn[c] = dn.get(c, 0) + 1

print("\n%-10s %6s %6s %6s" % ("文化圈", "总", "已入档", "待铺"))
for c in sorted(tot, key=lambda x: -tot[x]):
    print("%-10s %6d %6d %6d" % (CN.get(c, c), tot[c], dn.get(c, 0), tot[c] - dn.get(c, 0)))
print("%-10s %6d %6d %6d" % ("合计", sum(tot.values()), sum(dn.values()), sum(tot.values()) - sum(dn.values())))

miss = [v for v in inv if v["sid"] not in done]
print("\n待铺明细（按文化圈）：")
for c in sorted(tot, key=lambda x: -tot[x]):
    xs = [v for v in miss if v.get("culture") == c]
    if xs:
        print(f"  [{CN.get(c,c)}] {len(xs)} 村：", ", ".join(v["cn"] for v in xs))
