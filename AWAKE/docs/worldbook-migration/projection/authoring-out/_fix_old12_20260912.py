# -*- coding: utf-8 -*-
"""老12档修复脚本（IMPL-GEO1-PERMISSION §4.2-4.5，2026-09-12）
- 授权落上限 + 不变式 grant.min_detail == 表达 layer
- entity.lore.* 锚点迁移 / 删除
- aliases 补齐
- dawn-stew 正文 合儿必特→库吉特（quote 字段不动，B源引文保持原字）
- corrections_20260912/ 逐档留痕（原值逐字保留）
"""
import io, os, re, sys

ROOT = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out"
CORR = os.path.join(ROOT, "corrections_20260912")
os.makedirs(CORR, exist_ok=True)

T1 = """        grants:
          - {profile_id: profile.commoner, scope: local, min_detail: rumor}
          - {profile_id: profile.villager, scope: local, min_detail: rumor}
          - {profile_id: profile.tavernkeeper, scope: faction, min_detail: rumor}
          - {profile_id: profile.ransom_broker, scope: faction, min_detail: rumor}
          - {profile_id: profile.townsfolk, scope: regional, min_detail: rumor}
          - {profile_id: profile.notable, scope: regional, min_detail: rumor}
          - {profile_id: profile.merchant, scope: faction, min_detail: rumor}
          - {profile_id: profile.headman, scope: national, min_detail: rumor}
          - {profile_id: profile.soldier, scope: national, min_detail: rumor}
          - {profile_id: profile.noble, scope: elite, min_detail: rumor}"""

T2 = """        grants:
          - {profile_id: profile.townsfolk, scope: regional, min_detail: summary}
          - {profile_id: profile.notable, scope: regional, min_detail: summary}
          - {profile_id: profile.merchant, scope: faction, min_detail: summary}
          - {profile_id: profile.headman, scope: national, min_detail: summary}
          - {profile_id: profile.soldier, scope: national, min_detail: summary}
          - {profile_id: profile.noble, scope: elite, min_detail: summary}"""

OLD_CRS = "        grants: [{profile_id: profile.commoner, scope: regional, min_detail: summary}]"

# 每档：grants 替换表（old_line -> new），entity_ids 处置，aliases，文本修正，留痕依据
G_VILL_SUM = "        grants: [{profile_id: profile.villager, scope: local, min_detail: summary}]"
G_VILL_DET = "        grants: [{profile_id: profile.villager, scope: local, min_detail: detail}]"
G_TOWN_DET = "        grants: [{profile_id: profile.townsfolk, scope: regional, min_detail: detail}]"
G_NOT_SUM  = "        grants: [{profile_id: profile.notable, scope: regional, min_detail: summary}]"
G_NHS_DET  = "        grants: [{profile_id: profile.noble_high_steward, scope: elite, min_detail: detail}]"

G_T1 = {"old": OLD_CRS, "new": T1, "clause": "4.2 白描类→layer降rumor+T1全员OR"}
G_T2 = {"old": OLD_CRS, "new": T2, "clause": "4.2 叙事类→grant改挂T2（commoner读summary=越限泄漏）"}
V_RUMOR = {"old": G_VILL_SUM, "new": "        grants: [{profile_id: profile.villager, scope: local, min_detail: rumor}]",
           "clause": "4.2 villager行：rumor层表达 min_detail 落 rumor（不变式）"}
V2NOT = {"old": G_VILL_DET, "new": "        grants: [{profile_id: profile.notable, scope: regional, min_detail: detail}]",
         "clause": "4.2 villager行：detail层本不该给村民（detail>村民上限rumor），改挂notable"}
T2NOT = {"old": G_TOWN_DET, "new": "        grants: [{profile_id: profile.notable, scope: regional, min_detail: detail}]",
         "clause": "4.2 townsfolk行：detail>市民上限summary，内行细节改挂notable"}
N_RUMOR = {"old": G_NOT_SUM, "new": "        grants: [{profile_id: profile.notable, scope: regional, min_detail: rumor}]",
           "clause": "4.2 notable行核对：rumor层表达 min_detail 落 rumor（不变式）"}
NHS = {"old": G_NHS_DET, "new": "        grants: [{profile_id: profile.noble, scope: elite, min_detail: detail}]",
       "clause": "4.2 noble_high_steward→profile.noble（registry链即含noble；运行时只产profile.noble，链中无noble_high_steward）"}

DOCS = {
    "dawn-mtn.yaml": {
        "entity": ("[entity.lore.dawn_mountains]", None,
                   "4.3 山脉概念无聚落可挂，去掉entity_ids+补aliases"),
        "grants": [G_T1, G_T1, G_T1],
        "aliases": ("黎明山脉", ["黎明山脉", "黎明山"], ["Dawn Mountains"]),
    },
    "dawn-stew.yaml": {
        "entity": ("[entity.lore.dawn_mountains, entity.lore.khergit_wardens]", None,
                   "4.3 山脉与看守人概念均无聚落可挂；khergit_wardens概念不硬挂"),
        "grants": [G_T2],
        "aliases": ("黎明山脉", ["黎明山脉", "库吉特", "看守人"], ["Dawn Mountains", "Khergit", "Warden"]),
        "texts": [
            ('summary: {zh-CN: "合儿必特部西征后进入山地并被解释为融入守护的沿革；当前实际控制未决。"}',
             'summary: {zh-CN: "库吉特西征后进入山地并被解释为融入守护的沿革；当前实际控制未决。"}'),
            ('text: {zh-CN: "合儿必特部进入山地被来源解释为通过通婚、共俗和守护者身份融入当地。"}',
             'text: {zh-CN: "库吉特进入山地被来源解释为通过通婚、共俗和守护者身份融入当地。"}'),
            ('库赛特西征之后，山地便归了合儿必特部治下。合儿必特人不强改当地风俗',
             '库赛特西征之后，山地便归了库吉特人治下。库吉特人不强改当地风俗'),
        ],
        "text_clause": "4.4 译名三审定案：编年史转写「合儿必特」废弃（合儿必特=Harfit=另一封臣家族），Khergit官方CN=库吉特；quote字段保持B源原字不动",
    },
    "dawn-taboo.yaml": {
        "entity": ("[entity.lore.dawn_mountains]", None,
                   "4.3 山脉概念无聚落可挂"),
        "grants": [V_RUMOR, V2NOT],
        "aliases": ("黎明山脉", ["黎明山脉", "阿赫哈克", "山灵禁忌"], ["Dawn Mountains"]),
    },
    "der-furs.yaml": {
        "entity": (None, "keep", "4.3 castle_village_v6_2 已合法，不动"),
        "grants": [G_T1],
        "aliases": ("德里亚特", ["德里亚特", "毛皮", "海豹油"], ["Deriat"]),
    },
    "der-vill.yaml": {
        "entity": (None, "keep", "4.3 castle_village_v6_2 已合法，不动"),
        "grants": [G_T1],
        "aliases": ("德里亚特", ["德里亚特", "德里亚特村"], ["Deriat"]),
    },
    "kach-land.yaml": {
        "entity": ("[entity.lore.kachar_peninsula]", "[entity.settlement.town_s1]",
                   "4.3 半岛簇挂半岛上的城镇（官方文：瓦尔切格正在卡恰尔半岛深处）"),
        "grants": [G_T1, G_T1, G_T1, T2NOT],
        "aliases": ("卡恰尔半岛", ["卡恰尔半岛", "卡恰尔", "伊卡拉荒原"], ["Kachyar Peninsula", "Kachyar"]),
    },
    "kach-own.yaml": {
        "entity": ("[entity.lore.kachar_peninsula]", "[entity.settlement.town_s1]",
                   "4.3 半岛簇挂半岛上的城镇"),
        "grants": [G_T2],
        "aliases": ("卡恰尔半岛", ["卡恰尔半岛", "卡恰尔归属"], ["Kachyar Peninsula"]),
    },
    "kach-tales.yaml": {
        "entity": ("[entity.lore.kachar_peninsula]", "[entity.settlement.town_s1]",
                   "4.3 半岛簇挂半岛上的城镇"),
        "grants": [T2NOT],
        "aliases": ("卡恰尔半岛", ["卡恰尔半岛", "卡恰尔旧闻"], ["Kachyar Peninsula"]),
    },
    "lac-lake.yaml": {
        "entity": ("[entity.lore.lakonis_lake]", None,
                   "4.3 湖泊概念，本批无邻近聚落可挂"),
        "grants": [G_T1, NHS],
        "aliases": ("拉科尼斯湖", ["拉科尼斯湖", "弥戎河", "喀拉卡兹河"], ["Lakonis Lake"]),
    },
    "lac-tales.yaml": {
        "entity": ("[entity.lore.lakonis_lake]", None,
                   "4.3 湖泊概念无聚落可挂"),
        "grants": [V_RUMOR],
        "aliases": ("拉科尼斯湖", ["拉科尼斯湖", "血染湖水"], ["Lakonis Lake"]),
    },
    "sara-bay.yaml": {
        "entity": ("[entity.lore.shalas_bay]", "[entity.settlement.town_v7]",
                   "4.3 湾与沙拉斯港本就一体（官方文：沙拉斯=海务贸易港+周边海湾）"),
        "grants": [G_T1],
        "aliases": ("沙拉斯湾", ["沙拉斯湾", "加隆托海峡", "沙拉斯港"], ["Shalas Bay"]),
    },
    "sara-tales.yaml": {
        "entity": ("[entity.lore.shalas_bay]", None,
                   "4.3 起源传说是帝国级叙事，非该聚落本身；概念类不硬挂"),
        "grants": [N_RUMOR, V_RUMOR],
        "aliases": ("沙拉斯湾", ["沙拉斯湾", "帝国摇篮", "卡拉德登陆"], ["Shalas Bay"]),
    },
}

report = []
for fname, spec in DOCS.items():
    path = os.path.join(ROOT, fname)
    with io.open(path, "r", encoding="utf-8") as f:
        raw = f.read()
    orig = raw
    corr = ["# corrections_20260912 · " + fname, "",
            "> IMPL-GEO1-PERMISSION-20260912 §4（授权落上限+锚点迁移+aliases），原值逐字保留如下。", ""]

    # 1) 留痕：entity_ids 原值
    if spec["entity"][0] is not None:
        old_line = "entity_ids: " + spec["entity"][0]
        assert old_line in raw, fname + ": entity_ids line not found"
        corr.append("## entity_ids（原值）")
        corr.append("```")
        corr.append(old_line)
        corr.append("```")
        corr.append("处置: " + ("改为 `" + spec["entity"][1] + "`" if spec["entity"][1] else "整行删除") +
                    "。依据: " + spec["entity"][2] + "")
        corr.append("")
        # 2) 应用 entity_ids
        if spec["entity"][1] is None:
            raw = raw.replace("\n" + old_line, "", 1)
        else:
            raw = raw.replace(old_line, "entity_ids: " + spec["entity"][1], 1)

    # 3) 留痕+应用 grants（按出现次数展开替换表：同 old 串多表达共用一条规则）
    grant_rules = []
    for g in spec["grants"]:
        n = raw.count(g["old"])
        assert n > 0, fname + ": grant line not found: " + g["old"][:60]
        grant_rules.append((g, n))
    if grant_rules:
        corr.append("## grants（原值，逐字）")
        corr.append("```yaml")
        for g, n in grant_rules:
            corr.append(("# x%d " % n) + g["old"].strip())
        corr.append("```")
        corr.append("处置依据: 见各条对应 IMPL §" + "；§".join(sorted(set(g["clause"] for g, _ in grant_rules))) + "")
        corr.append("")
        for g, _ in grant_rules:
            raw = raw.replace(g["old"], g["new"])

    # 4) 文本修正（不碰 quote）
    if "texts" in spec:
        corr.append("## 文本修正（quote 字段不动）")
        corr.append("")
        for old, new in spec["texts"]:
            assert old in raw, fname + ": text not found: " + old[:40]
            raw = raw.replace(old, new, 1)
            corr.append("- 原: `" + old + "`")
            corr.append("- 改: `" + new + "`")
        corr.append("")
        corr.append("依据: " + spec["text_clause"])
        corr.append("")

    # 5) revision 1→2（仅文档级）
    raw = re.sub(r"(?m)^revision: 1$", "revision: 2", raw, count=1)

    # 6) aliases 插在 content_tier 行后
    zh_label, zh_list, en_list = spec["aliases"]
    alias_block = ("aliases:\n"
                   "  zh-CN: [" + ", ".join('"%s"' % x for x in zh_list) + "]\n"
                   "  en: [" + ", ".join('"%s"' % x for x in en_list) + "]")
    assert "\naliases:" not in orig, fname + ": already has aliases"
    anchor = re.search(r"(?m)^content_tier: base$", raw)
    assert anchor, fname + ": content_tier anchor not found"
    raw = raw[:anchor.end()] + "\n" + alias_block + raw[anchor.end():]
    corr.append("## aliases（新增）")
    corr.append("")
    corr.append("zh-CN: " + ", ".join(zh_list) + "（锚点缺失/概念档靠 aliases 进 K1 检索，IMPL §4.4）")
    corr.append("")

    with io.open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(raw)
    with io.open(os.path.join(CORR, fname + ".md"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(corr) + "\n")
    report.append(fname + " OK")

print("\n".join(report))
print("ALL-DONE")
