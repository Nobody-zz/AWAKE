# -*- coding: utf-8 -*-
"""护甲族 · 类目定稿（只读 XML，2026-09-18）。

把 `_probe_armor_shapes_20260918.py` 的粗切（躯干有 61 件落进 other/armor_other 两桶）
收成**可直接生成词条的 22 个类目**：

  躯干 11：平民便服 / 布袍束腰衣 / 填充衣 / 皮甲 / 锁甲 / 札甲 / 鳞甲 / 板甲 / 布面铁甲 / 环甲软甲 / 毛皮甲
  披风  4：披风与围巾 / 兽皮披挂 / 金属肩甲 / 护肩(软甲)
  盾    5：筝形盾 / 圆盾 / 扇形盾 / 柳条方盾 / 巨盾
  腿手  2：护腿与靴 / 护手与臂铠

三处口径收尾（上一份草案 §五 列的）：
  ① `dummy_armor_*` 4 件是游戏占位数据、中文名都是「Dummy …」⇒ **剔除，不做词条**；
  ② 平民衣着**独立成类**，并把混在里面的真战甲挑回甲类（用 OVERRIDE 表，可审计）；
  ③ `_armor` 杂项桶按材质 + 结构词再切一次。

⚠️ 只读。不写包、不改索引、不产词条。
"""
import collections
import io
import json
import os
import re
import sys
import xml.etree.ElementTree as ET

sys.stdout.reconfigure(encoding="utf-8")

GAME = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/SandBoxCore/ModuleData/items"
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "_armor_taxonomy_20260918.json")

PARTS = {"BodyArmor": "躯干", "LegArmor": "腿", "HandArmor": "手", "Cape": "披风", "Shield": "盾"}

# ---- 排除：游戏占位 / 非卖品道具 ----
EXCLUDE = re.compile(r"^dummy_|_dummy|^spawned_|^test_", re.I)

# ---- 主规则（先匹配者胜）----
RULES = [
    ("torso-brigandine", r"coat_of_plates|brigandine|brigantine|barded|fortified|_plated|plated"),
    ("torso-lamellar",   r"lamellar|lamelar"),
    ("torso-scale",      r"scale|scalemail"),
    ("torso-mail",       r"mail|hauberk"),
    ("torso-plate",      r"plate|cuirass|breastplate|_plate_"),
    ("torso-ring",       r"ringed|_ring"),
    ("torso-gambeson",   r"gambeson|padded|aketon|quilted"),
    ("torso-fur",        r"fur|bearskin|leopard|wolf|shaggy|hide"),
    ("torso-civil",      r"civil|peasant|rags|tattered|trunk|apron|waistcoat|bandit|_lady|moccasin"),
    ("torso-leather",    r"leather|suede"),
    ("torso-cloth",      r"cloth|tunic|robe|dress|coat|kaftan|caftan|kilt"),
    # 披风位（Cape）
    ("cape-fur",         r"bearskin|leopard|wolf|pelt|fur|shaggy"),
    ("cape-pauldron",    r"pauldron|shoulder.*plate|plate.*shoulder|neckguard|plated_shoulder"),
    ("cape-strap",       r"strap|bra_|harness|背带"),
    ("cape-mantle",      r"cloak|cape|scarf|shawl|hood|sash|robe|pad"),
    # 盾（Shield）
    ("shield-kite",      r"kite|teardrop|infantry_shield|horseman|scout|joust"),
    ("shield-heater",    r"heater"),
    ("shield-wicker",    r"wicker|adarga|rect|square|sparring"),
    ("shield-tower",     r"tower|pavise|_great_"),
    ("shield-round",     r"round|circle|oval|buckler|targe|shield"),
    # 腿 / 手
    ("leg-boot",         r"boot|shoe|moccasin|sandal"),
    ("leg-mail",         r"mail|hauberk"),
    ("leg-plate",        r"plate|plated|夹板|扎板"),
    ("leg-wrap",         r"wrap|bound|绑"),
    ("hand-mail",        r"mail|mitten"),
    ("hand-plate",       r"plate|plated|gauntlet|bracer|vambrace|bracers|armguard"),
    ("hand-wrap",        r"wrap|band|绑"),
]

# ---- 显式特判：正则够不着、人工看过归属的（可审计）----
OVERRIDE = {
    # 躯干 other/armor_other 桶里的真战甲，挑回甲类
    "hauberk": "torso-mail",
    "nordic_hauberk": "torso-mail",
    "decorated_nordic_hauberk": "torso-mail",
    "empire_legion_a": "torso-mail",
    "empire_legion_b": "torso-scale",
    "aserai_horseman_armor": "torso-ring",
    "ringed_desert_armor": "torso-ring",
    "aserai_archer_armor": "torso-mail",
    "sturgia_cavalry_armor": "torso-lamellar",
    "aserai_armor_02": "torso-mail",
    "aserai_armor_02_b": "torso-mail",
    "armored_baggy_trunks": "torso-civil",
    "battanian_savage_armor": "torso-civil",
    "battania_mercenary_armor": "torso-brigandine",
    "battania_noble_armor": "torso-mail",
    "battania_warlord_armor": "torso-scale",
    "battania_light_armor_a": "torso-brigandine",
    "battania_light_armor_b": "torso-civil",
    "battania_light_armor_c": "torso-civil",
    "battania_light_armor_d": "torso-civil",
    "battania_light_armor_e": "torso-civil",
    "steppe_armor": "torso-civil",
    "reinforced_suede_armor": "torso-leather",
    "khuzait_fortified_armor": "torso-brigandine",
    "khuzait_sturdy_armor": "torso-leather",
    "khuzait_heavy_armor": "torso-brigandine",
    "veteran_mercenary_armor": "torso-mail",
    "sturgian_fortified_armor": "torso-brigandine",
    "empire_horseman_armor": "torso-mail",
    "buckled_wildsman_armor": "torso-mail",
    "assassin_armor": "torso-leather",
    # 躯干 other 里的便服（正则漏的）
    "battania_woodland_outfit": "torso-civil",
    "heavy_nordic_coat": "torso-civil",
    "nordic_sloven": "torso-civil",
    "vlandia_bandit_a": "torso-gambeson",       # 中文名就是「廉价填充衣」
    "burlap_waistcoat": "torso-civil",
    "half_apron": "torso-civil",
    "baggy_trunks": "torso-civil",
    # 披风
    "armored_bearskin": "cape-fur",
    "aserai_horseman_shoulder": "cape-mail",
    "battania_warlord_pauldrons": "cape-pauldron",
    "studded_imperial_neckguard": "cape-pauldron",
    "imperial_studded_strip_shoulders": "cape-strap",
    "varangian_bra_basic": "cape-strap",
    "varangian_bra_royal": "cape-strap",
    "battania_shoulder_strap": "cape-strap",
    "battania_shoulder_strap_cloak": "cape-mantle",
    "desert_fabric_shoulderpad": "cape-mantle",
    "reinforced_suede_shoulders": "cape-leather",
    "eastern_studded_shoulders": "cape-leather",
    # 盾
    "sturgia_old_shield_a": "shield-round",
    "sturgia_old_shield_b": "shield-round",
    "sturgia_old_shield_c": "shield-round",
    "battania_large_shield_a": "shield-round",
    "battania_large_shield_b": "shield-round",
    "battania_large_shield_c": "shield-round",
    "highland_scouts_shield": "shield-round",
    "highland_riders_shield": "shield-round",
    "tribal_steppe_shield": "shield-round",
    "decorated_steppe_shield": "shield-round",
    "noyans_shield": "shield-round",
    "steppe_guardian_shield": "shield-round",
    # 盾：名字里带"扇形/heater"的两个被 shield-kite 的 `horseman|joust` 抢走了，
    # 中文名分别叫"骑兵扇形小盾""骑士盾"⇒ 归扇形盾（名字是玩家会用来找的那条线索）
    "horsemans_heater_shield": "shield-heater",
    "jousting_shield": "shield-heater",
    "northern_scouts_shield": "shield-kite",
    "northern_horsemans_shield": "shield-kite",
    # 腿手
    "battania_warlord_boots": "leg-plate",
    "decorated_imperial_boots": "leg-plate",
    "battania_noble_bracers": "hand-leather",
    "highland_gloves": "hand-leather",
    "battania_warlord_bracers": "hand-plate",
    "northern_brass_bracers": "hand-plate",
    "decorated_imperial_gauntlets": "hand-plate",
    "khuzait_heavy_armor_bracer": "hand-plate",
    "studded_vambraces": "hand-leather",
    "eastern_wrapped_armguards": "hand-wrap",
}

# 类目中文名（生成器会用它当 title 的形制名部分）
CLASS_TITLE = {
    "torso-civil": "平民便服", "torso-cloth": "布袍束腰衣", "torso-gambeson": "填充衣",
    "torso-leather": "皮甲", "torso-mail": "锁甲", "torso-lamellar": "札甲",
    "torso-scale": "鳞甲", "torso-plate": "板甲", "torso-brigandine": "布面铁甲",
    "torso-ring": "环甲软甲", "torso-fur": "毛皮甲",
    "cape-mantle": "披风与围巾", "cape-fur": "兽皮披挂", "cape-pauldron": "金属肩甲",
    "cape-leather": "皮护肩与背带", "cape-mail": "链甲护肩", "cape-strap": "肩带与背带",
    "shield-kite": "筝形盾", "shield-round": "圆盾", "shield-heater": "扇形盾",
    "shield-wicker": "柳条方盾", "shield-tower": "巨盾",
    "leg-boot": "护腿与靴", "leg-mail": "链甲护腿", "leg-plate": "夹板护腿", "leg-wrap": "绑腿",
    "hand-mail": "链甲连指手套", "hand-plate": "板护臂与臂铠", "hand-leather": "皮护臂与护腕",
    "hand-wrap": "绑臂与护腕",
}

# ---- 卡片归并：细分 key → 卡片 key（一张卡可盖若干细分；件数 <3 的细分不单独成卡）----
CARD = {
    "cape-mail": "cape-shoulders", "cape-leather": "cape-shoulders", "cape-strap": "cape-shoulders",
    "shield-tower": "shield-heater",
    "leg-boot": "legs", "leg-mail": "legs", "leg-plate": "legs", "leg-wrap": "legs",
    "hand-mail": "hands", "hand-plate": "hands", "hand-leather": "hands", "hand-wrap": "hands",
}
CARD_TITLE = {
    "cape-shoulders": "软甲护肩与背带",
    "legs": "护腿与靴",
    "hands": "护手与臂铠",
}


def card_of(key):
    return CARD.get(key, key)


def card_title(key):
    return CARD_TITLE.get(key, CLASS_TITLE.get(key, "【未归类】"))


# 21 张卡的固定顺序（躯干 11 → 披风 4 → 盾 4 → 腿手 2）
ORDER = ["torso-civil", "torso-cloth", "torso-gambeson", "torso-leather", "torso-mail",
         "torso-ring", "torso-lamellar", "torso-scale", "torso-plate", "torso-brigandine",
         "torso-fur", "cape-mantle", "cape-fur", "cape-pauldron", "cape-shoulders",
         "shield-kite", "shield-round", "shield-heater", "shield-wicker", "legs", "hands"]


# 默认材质兜底：规则全落空时按 armor.material_type 归
MATERIAL_FALLBACK = {
    ("躯干", "Chainmail"): "torso-mail", ("躯干", "Plate"): "torso-plate",
    ("躯干", "Leather"): "torso-leather", ("躯干", "Cloth"): "torso-cloth",
    ("披风", "Chainmail"): "cape-mail", ("披风", "Plate"): "cape-pauldron",
    ("披风", "Leather"): "cape-leather", ("披风", "Cloth"): "cape-mantle",
    ("盾", "-"): "shield-round",
    ("腿", "Plate"): "leg-plate", ("腿", "Leather"): "leg-boot", ("腿", "Cloth"): "leg-wrap",
    ("手", "Plate"): "hand-plate", ("手", "Leather"): "hand-leather",
    ("手", "Cloth"): "hand-wrap", ("手", "Chainmail"): "hand-mail",
}


def classify(item):
    eid = item["entityId"]
    if eid in OVERRIDE:
        return OVERRIDE[eid]
    for key, pat in RULES:
        # 只在该部位适用的规则里找：前缀部位键必须对得上（torso-/cape-/shield-/leg-/hand-）
        pfx = {"torso": "躯干", "cape": "披风", "shield": "盾", "leg": "腿", "hand": "手"}
        head = key.split("-")[0]
        if pfx.get(head) != item["part"]:
            continue
        if re.search(pat, eid, re.I):
            return key
    fb = MATERIAL_FALLBACK.get((item["part"], item["material"] or "-"))
    return fb or "未归类"


def main():
    import sqlite3
    con = sqlite3.connect("file:%s?mode=ro" % DB, uri=True)
    cn = {r[0]: r[1] for r in con.execute(
        "SELECT stringId, text FROM localization_entries WHERE language='CNs'").fetchall()}

    items, dropped = [], []
    for fn in sorted(f for f in os.listdir(GAME) if f.lower().endswith(".xml")):
        root = ET.parse(os.path.join(GAME, fn)).getroot()
        for it in root.iter("Item"):
            typ = it.get("Type") or ""
            if typ not in PARTS:
                continue
            eid = it.get("id") or ""
            if eid.startswith("mp_"):
                continue
            if EXCLUDE.search(eid):
                dropped.append((typ, eid))
                continue
            nm = it.get("name") or ""
            m = re.match(r"^\{=([^}]+)\}(.*)$", nm)
            tok, en = (m.group(1), m.group(2).strip()) if m else ("", nm.strip())
            arm = it.find(".//Armor")
            a = dict(arm.attrib) if arm is not None else {}
            items.append({
                "type": typ, "part": PARTS[typ], "entityId": eid, "en": en,
                "zh": cn.get(tok) or en, "culture": (it.get("culture") or "").replace("Culture.", ""),
                "armor": a.get("body_armor") or a.get("leg_armor") or a.get("arm_armor") or "-",
                "weight": it.get("weight"), "material": a.get("material_type"),
                "gender_var": a.get("has_gender_variations"),
                "layered": bool(re.search(r"over_|_over", eid, re.I)),
            })
            items[-1]["klass"] = classify(items[-1])

    print("剔掉的占位/测试件 = %d：%s" % (len(dropped), [d[1] for d in dropped]))
    print("定稿件数 = %d" % len(items))
    print()

    by = collections.defaultdict(list)        # 细分
    byc = collections.defaultdict(list)       # 卡片
    for x in items:
        by[x["klass"]].append(x)
        byc[card_of(x["klass"])].append(x)

    def summarize(rows):
        rows = sorted(rows, key=lambda x: -int(x["armor"]) if str(x["armor"]).isdigit() else 0)
        hv = [int(r["armor"]) for r in rows if str(r["armor"]).isdigit()]
        return {
            "n": len(rows),
            "armor_min": hv[-1] if hv else None, "armor_max": hv[0] if hv else None,
            "cultures": dict(collections.Counter(r["culture"] or "-" for r in rows).most_common()),
            "materials": dict(collections.Counter(r["material"] or "-" for r in rows).most_common()),
            "items": [{"entityId": r["entityId"], "zh": r["zh"], "en": r["en"], "armor": r["armor"],
                       "culture": r["culture"], "material": r["material"], "klass": r["klass"]}
                      for r in rows],
        }

    cards = []
    print("===== 卡片层：要生成的 %d 条词条 =====" % len(byc))
    for key in sorted(byc, key=lambda k: (ORDER.index(k) if k in ORDER else 999, k)):
        s = summarize(byc[key])
        subs = sorted({x["klass"] for x in byc[key]})
        print("-- %-18s %-16s n=%-3d 护值 %s–%s | 文化 %s | 材质 %s"
              % (key, card_title(key), s["n"], s["armor_min"], s["armor_max"],
                 dict(list(s["cultures"].items())[:3]), dict(list(s["materials"].items())[:3])))
        if len(subs) > 1:
            print("      覆盖细分：%s" % "、".join(subs))
        print("      %s" % "、".join(r["zh"][:12] for r in s["items"][:6]))
        cards.append(dict(key=key, title=card_title(key), part=byc[key][0]["part"],
                          sub_klasses=subs, **s))

    print()
    print("===== 细分层：生成器组织正文用 =====")
    classes = []
    for key in sorted(by, key=lambda k: (list(CLASS_TITLE).index(k) if k in CLASS_TITLE else 999, k)):
        s = summarize(by[key])
        print("-- %-16s %-12s n=%-3d 护值 %s–%s" % (key, CLASS_TITLE.get(key, "?"), s["n"],
                                                 s["armor_min"], s["armor_max"]))
        classes.append(dict(key=key, title=CLASS_TITLE.get(key, "?"), card=card_of(key), **s))

    unassigned = [x for x in items if card_of(x["klass"]) not in ORDER]
    print()
    print("卡片数 = %d（覆盖 %d 件；剔除占位 %d 件）" % (len(cards), len(items), len(dropped)))
    if unassigned:
        print("⚠️ 未成卡明细：")
        for x in unassigned:
            print("   %-40s %-12s %s" % (x["entityId"], x["zh"][:12], x["part"]))

    with io.open(OUT, "w", encoding="utf-8", newline="\n") as fh:
        json.dump({"cards": cards, "classes": classes, "dropped": dropped, "total": len(items)},
                  fh, ensure_ascii=False, indent=1)
    print()
    print("written:", OUT)
    print("PROBE_DONE")


main()
