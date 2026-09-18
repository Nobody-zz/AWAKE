# -*- coding: utf-8 -*-
"""落地 taxonomy v2（2026-09-14 Max 裁定：题材树＋类型轴）。

做三件事：
  A. taxonomy：把 48 版改回「原 43 版 + 3 处改动」：
       geography: 撤 mountain/plateau/peninsula/desert/waters/castle/village/town，
                  恢复 terrain/rivers/settlements（rivers 措辞扩为水域）
       culture:   tale → faith（措辞补「民间怪谈」）
       war:       military_system → military 已是 v2 名（48 版里已改，此处幂等确认）
     版本 1.0.0 → 1.1.0
  B. 448 档（两个目录）：档名 + id/domain/subdomain 字段按 v2 落点改写
  C. dry-run 默认；--apply 落盘；幂等（已是 v2 态的档原样跳过）

用法：python _apply_v2_20260914.py [--apply]
"""
import io
import json
import os
import re
import sys
from collections import defaultdict

ROOT = r"D:\AWAKE-Dev\AWAKE"
DIRS = [
    os.path.join(ROOT, "tools", "worldbook-studio", "workspace", "full-geo1", "authoring"),
    os.path.join(ROOT, "docs", "worldbook-migration", "projection", "authoring-out"),
]
TAX = os.path.join(ROOT, "docs", "worldbook-studio-plan", "knowledge-taxonomy.v1.json")
# 原 43 版真本：从 git HEAD 导出一次，落成临时件（见同目录 _orig_tax_tmp.json）
ORIG_TAX = os.path.join(ROOT, "docs", "worldbook-migration",
                        "corrections_20260914", "knowledge-taxonomy.v1.json.bak")

APPLY = "--apply" in sys.argv

# ---------------------------------------------------------------- A. taxonomy
# v2 geography 子域顺序 = 原 43 版；rivers 就地扩措辞
GEO_V2 = ["terrain", "climate", "directions", "rivers", "roads",
          "settlements", "natural_boundaries", "resources", "sea_routes"]

WATERS_HELP = "河流、湖泊、海湾、海域、渡口和水域通行。"
WATERS_EX = ["哪条河流经城镇", "哪里可以渡河", "湖湾海域的来由"]
WATERS_HINT = ["水域本体归这里；海上航行路线归航路。"]
FAITH_HELP = "神祇、教义、仪式、宗教组织与民间怪谈。"
FAITH_EX = ["人们敬奉的神", "葬礼上的仪式", "流传于乡野的怪谈"]
FAITH_HINT = ["教义与仪式归这里；以传说形式讲述的地方风物归习俗。"]

# 保留原设计措辞（严格取自 git HEAD 原 43 版，仅 rivers/faith 两处按 Max 裁定微调）
TERRAIN = {
    "label": {"zh-CN": "地形"},
    "help": {"zh-CN": "山地、平原、沙漠、森林和海岸。"},
    "examples": {"zh-CN": ["某地是平原还是山地", "地形如何影响通行"]},
}
SETTLEMENTS = {
    "label": {"zh-CN": "聚落"},
    "help": {"zh-CN": "村庄、城镇、城堡和它们所在的位置。"},
    "examples": {"zh-CN": ["某村庄位于哪片土地", "城堡附近有什么地形"]},
}
RIVERS = {   # 原名 rivers，label 由「河流」改为「水域」（Max 裁：命名太局限）
    # 注意：taxonomy schema 的 $defs.subdomain 是 additionalProperties:False，
    #      子域只允许 id/label/help/examples 四键 ⇒ 不能加 conflict_hints（只有域级能加）。
    "label": {"zh-CN": "水域"},
    "help": {"zh-CN": "河流、湖泊、海湾、海域、渡口和水域通行。"},
    "examples": {"zh-CN": ["哪条河流经城镇", "哪里可以渡河"]},
}
FAITH = {    # 措辞微调：help 补「与民间怪谈」
    "label": {"zh-CN": "信仰"},
    "help": {"zh-CN": "神祇、教义、仪式、宗教组织与民间怪谈。"},
    "examples": {"zh-CN": ["人们敬奉的神", "葬礼上的仪式"]},
}

# ------------------------------------------------- B. 档页映射（当前态 → v2 态）
# key = (当前前缀) ; value = (v2 前缀, v2 subdomain, v2 domain 或 None 保持)
# 说明：当前态是 48 版（上轮 _apply_reclass 结果）。
FWD = {
    # 聚落：前缀不变，subdomain 回 settlements
    "castle": ("castle", "settlements", "geography"),
    "village": ("village", "settlements", "geography"),
    "town": ("town", "settlements", "geography"),
    # 地貌：前缀归一到 v2，subdomain 回 terrain
    "mountain": ("mountain", "terrain", "geography"),
    "plateau": ("plateau", "terrain", "geography"),
    "peninsula": ("peninsula", "terrain", "geography"),
    "desert": ("desert", "terrain", "geography"),
    # 水域：恢复原名前缀，subdomain 回 rivers
    "waters": (None, "rivers", "geography"),   # 前缀按内层名判定（见 resolve）
    # 资源：
    "resources": ("mine", "resources", "geography"),
    # 器物 / 物产 / 领地 / 王权 / 氏族 / 军制 / 兵种 / 武器：字段不变
    "items": ("items", "items", None),
    "goods": ("goods", "goods", None),
    "territories": ("territories", "territories", None),
    "throne": ("throne", "throne", None),
    "clans": ("clans", "clans", None),
    "military": ("military", "military", None),
    "troops": ("troops", "troops", None),
    "weapons": ("weapons", "weapons", None),
    # tale：前缀不变，subdomain 按内容逐档指定（见 TALE_SUB）
    "tale": ("tale", None, "culture"),
}

# tale 6 档分挂（v2 §五）
TALE_SUB = {
    "tale-charas-origin": "faith",
    "tale-dawn-taboo": "faith",
    "tale-lakonis-lake": "customs",
    "tale-lycaron-rock": "customs",
    "tale-husn-fulq": "arts",
    "tale-kachar-three": "identity",
}

# waters 档内层名 → v2 前缀（原名：43 版是「名-类型」倒装，48 版丢了类型 ⇒ 用此表恢复）
# key = 48 版 waters-<rest> 的 rest
WATERS_PRE = {
    "charas": "bay",        # 沙拉斯湾·湾澳与港口
    "lakonis": "lake",      # 拉科尼斯湖
    "llyn-modris": "lake",  # 林·莫德里斯（火山口湖）
    "miron": "river",       # 弥戎河
    "perassic": "sea",      # 珀拉斯海
    "sethys": "river",      # 塞堤斯河
    "tanaesis": "lake",     # 塔奈西斯湖
}
# v2 档名（类型-名）。名段沿用 48 版 rest（保持原有 slug 稳定）。
# 依据：43 版原名 charas-bay/miron-river/lac-lake/tanaesis-lake/perassic-sea/sethys-river/llyn-modris
WATERS_NAME = {
    "charas": "charas",           # 沙拉斯湾 → bay-charas
    "lakonis": "lakonis",         # 拉科尼斯湖 → lake-lakonis
    "llyn-modris": "llyn-modris",  # 林·莫德里斯 → lake-llyn-modris
    "miron": "miron",             # 弥戎河 → river-miron
    "perassic": "perassic",       # 珀拉斯海 → sea-perassic
    "sethys": "sethys",           # 塞堤斯河 → river-sethys
    "tanaesis": "tanaesis",       # 塔奈西斯湖 → lake-tanaesis
}

RE_ID = re.compile(r"(?m)^id: [^\r\n]*")
RE_DOM = re.compile(r"(?m)^domain: [^\r\n]*")
RE_SUB = re.compile(r"(?m)^subdomain: [^\r\n]*")


def resolve(stem):
    """→ (new_prefix, new_subdomain, new_domain|None) 或 None（无规则）"""
    pre = stem.split("-", 1)[0]
    rest = stem.split("-", 1)[1] if "-" in stem else ""
    if pre == "waters":
        inner = rest
        npre = WATERS_PRE.get(inner)
        nname = WATERS_NAME.get(inner)
        if not npre or not nname:
            return None
        return npre, "rivers", "geography", nname
    if pre == "tale":
        nsub = TALE_SUB.get(stem)
        if not nsub:
            return None
        return "tale", nsub, "culture", rest
    if pre not in FWD:
        return None
    npre, nsub, ndom = FWD[pre]
    if npre is None:
        return None
    return npre, nsub, ndom, rest


def build_taxonomy_v2():
    """从 backups 的原 43 版重建 v2：恢复 geography/culture 两域的 v2 形状，
    其余域原样。rivers/faith 两格按 Max 裁定微调措辞。"""
    orig = json.loads(io.open(ORIG_TAX, encoding="utf-8").read())
    tax = json.loads(io.open(TAX, encoding="utf-8").read())
    orig_by_dom = {d["id"]: d for d in orig["domains"]}
    changed = False
    for d in tax["domains"]:
        if d["id"] == "geography":
            d["subdomains"] = json.loads(json.dumps(orig_by_dom["geography"]["subdomains"]))
            for s in d["subdomains"]:
                if s["id"] == "rivers":
                    s.update(json.loads(json.dumps(RIVERS)))
            changed = True
        elif d["id"] == "culture":
            d["subdomains"] = json.loads(json.dumps(orig_by_dom["culture"]["subdomains"]))
            for s in d["subdomains"]:
                if s["id"] == "faith":
                    s.update(json.loads(json.dumps(FAITH)))
            changed = True
    # ⚠️ taxonomy_version 必须保持 1.0.0：schema 的 `const` 与 C# `SupportedVersion`
    #    都是硬编码 "1.0.0"。内容变更靠「包 hash / manifest.taxonomyHash」识别，不动版本号。
    return tax, orig


def main():
    # ---------- A. taxonomy ----------
    raw_before = io.open(TAX, "rb").read()
    old_tax = json.loads(raw_before.decode("utf-8"))
    old_doms = {d["id"]: [s["id"] for s in d["subdomains"]] for d in old_tax["domains"]}
    tax, _orig = build_taxonomy_v2()
    new_doms = {d["id"]: [s["id"] for s in d["subdomains"]] for d in tax["domains"]}
    changed_tax = (old_tax.get("taxonomy_version") != tax.get("taxonomy_version")
                   or old_doms != new_doms)
    print("=" * 72)
    print("A. taxonomy")
    for k in new_doms:
        if old_doms.get(k) != new_doms[k]:
            print("  %s:" % k)
            print("    %s" % old_doms.get(k))
            print("    -> %s" % new_doms[k])
    nsub = sum(len(v) for v in new_doms.values())
    print("  域 %d ｜ 子域合计 %d ｜ version %s -> %s"
          % (len(new_doms), nsub, old_tax.get("taxonomy_version"),
             tax.get("taxonomy_version")))

    # ---------- B. 档页 ----------
    for DIR in DIRS:
        print("=" * 72)
        print("B. 目录:", DIR)
        files = sorted(f for f in os.listdir(DIR) if f.endswith(".yaml"))
        rows, skips = [], []
        for f in files:
            txt = io.open(os.path.join(DIR, f), "rb").read().decode("utf-8")
            m_id, m_dom, m_sub = RE_ID.search(txt), RE_DOM.search(txt), RE_SUB.search(txt)
            if not (m_id and m_dom and m_sub):
                skips.append((f, "缺字段")); continue
            stem = f[:-5]
            r = resolve(stem)
            if r is None:
                skips.append((f, "无规则")); continue
            npre, nsub, ndom, nrest = r
            odom = m_dom.group(0)[8:].strip()
            ndom = ndom or odom
            nstem = npre + "-" + nrest if nrest else npre
            nfile = nstem + ".yaml"
            nt = RE_ID.sub("id: doc.%s.%s" % (ndom, nstem), txt)
            nt = RE_DOM.sub("domain: " + ndom, nt)
            nt = RE_SUB.sub("subdomain: " + nsub, nt)
            rows.append({"file": f, "nfile": nfile, "text": nt})
        tgt = defaultdict(list)
        for r in rows:
            tgt[r["nfile"]].append(r["file"])
        clash = {k: v for k, v in tgt.items() if len(v) > 1}
        renames = [r for r in rows if r["file"] != r["nfile"]]
        print("  总 %d ｜ 改名 %d ｜ 仅字段 %d ｜ 跳过 %d"
              % (len(files), len(renames), len(rows) - len(renames), len(skips)))
        if clash:
            print("  !! 撞车:", clash)
        else:
            print("  撞车: 无")
        for r in renames[:14]:
            print("    %-38s -> %s" % (r["file"], r["nfile"]))
        if len(renames) > 14:
            print("    ... 共 %d" % len(renames))
        for f, why in skips:
            print("    SKIP", f, why)
        if APPLY:
            import time
            for r in rows:
                dst = os.path.join(DIR, r["nfile"])
                blob = r["text"].encode("utf-8")
                for attempt in range(8):
                    try:
                        io.open(dst, "wb").write(blob)
                        break
                    except OSError:
                        if attempt == 7:
                            raise
                        time.sleep(0.4)
                if r["nfile"] != r["file"]:
                    src = os.path.join(DIR, r["file"])
                    for attempt in range(8):
                        try:
                            os.remove(src); break
                        except OSError:
                            if attempt == 7: raise
                            time.sleep(0.4)
            print("  已落盘")

    if APPLY and changed_tax:
        # 备份已存在于 corrections_20260914/（43 原版）。此处另存 48 版备份。
        bak48 = os.path.join(ROOT, "docs", "worldbook-migration",
                             "corrections_20260914", "knowledge-taxonomy.v1.json.48bak")
        if not os.path.exists(bak48):
            io.open(bak48, "wb").write(raw)
        io.open(TAX, "wb").write(
            (json.dumps(tax, ensure_ascii=False, indent=2) + "\n").encode("utf-8"))
        print("=" * 72)
        print("taxonomy 已写入；48 版备份 ->", bak48)


if __name__ == "__main__":
    main()
