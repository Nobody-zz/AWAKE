# -*- coding: utf-8 -*-
"""盘点：游戏单人（非 mp_）物品按 Type 的件数规模（只读 XML，2026-09-18）。

用途：回答「物品类全量铺开还有多大盘子」，为下一批选题提供依据。
口径与头盔批一致（`_head_types_20260916.py`）：横扫 SandBoxCore/ModuleData/items/*.xml，
`<Item Type="...">`，剔除 `mp_` 前缀（多人件与战役无关）。

⚠️ 只读。不写包、不改索引。
"""
import collections
import os
import xml.etree.ElementTree as ET

GAME = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/SandBoxCore/ModuleData/items"
# 头盔批只扫了 SandBoxCore；这里把原版其余模块也带上，避免漏件
EXTRA = [
    r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/Native/ModuleData",
    r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/SandBox/ModuleData",
]

# 在挂包里已经铺过的（按类，不按件）
COVERED = {
    "HeadArmor": "头盔形制批 10 张类卡（未上线）",
    "Horse": "economy.items 3 条（旅行马/骡子/驮运骆驼）",
    "Goods": "economy.goods 6 条（毛皮/盐/银矿石/香料/天鹅绒…）",
}


def scan(root_dir):
    counts = collections.Counter()
    for dirpath, _dirnames, filenames in os.walk(root_dir):
        for fn in filenames:
            if not fn.lower().endswith(".xml"):
                continue
            try:
                root = ET.parse(os.path.join(dirpath, fn)).getroot()
            except Exception:
                continue
            for it in root.iter("Item"):
                typ = it.get("Type") or "(无 Type)"
                eid = it.get("id") or ""
                if eid.startswith("mp_"):
                    continue
                counts[typ] += 1
    return counts


def main():
    total = collections.Counter()
    total.update(scan(GAME))
    for d in EXTRA:
        if os.path.isdir(d):
            total.update(scan(d))

    print("沙盒根：%s" % GAME)
    print("单人（非 mp_）物品总数 = %d" % sum(total.values()))
    print()
    print("=== 按 Type ===")
    for typ, n in total.most_common():
        mark = "  ← 已铺：" + COVERED[typ] if typ in COVERED else ""
        print("  %-22s %5d%s" % (typ, n, mark))

    weave = sum(n for t, n in total.items()
                if t in ("Bow", "Crossbow", "Arrows", "Bolts", "OneHandedWeapon",
                         "TwoHandedWeapon", "Polearm", "ThrowingWeapon", "CraftingPiece"))
    armor = sum(n for t, n in total.items()
                if t in ("BodyArmor", "Cape", "Gloves", "LegArmor", "HeadArmor"))
    print()
    print("兵器族（弓弩箭矢+近战+投掷+锻造件）合计 = %d" % weave)
    print("护甲族（躯干+披风+手套+腿+头）合计 = %d" % armor)
    print("PROBE_DONE")


main()
