# -*- coding: utf-8 -*-
"""百科化 PoC2 生成器：战役武器/甲（XML 提取通道）。
数据源＝游戏 items XML（Item 类，组件数据完备）＋ DB localization join 官方中文名。
产出三件套 + 10 档 yaml（L 规格与 PoC1 同构，全 economy/items）。
红线同 PoC1：JSON 往返断引用 + ignore_aliases（WB-YAML-006）；slug 不进文档体；
六身份显式落点；grant.min_detail==layer；quote 在快照行内（子串）。
"""
import io, os, json, hashlib, sqlite3, yaml
import xml.etree.ElementTree as ET

OUT = os.path.dirname(os.path.abspath(__file__))
WS_AUTH = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"
GAME = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/SandBoxCore/ModuleData/items"
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
SRC_ID = "source.calradia.game.items-xml"
SRC_VER = "bannerlord-1.3.15.110062"
SNAP = "game-items-poc2.txt"

# 10 件选品：品类多样（弓/弩/盾/身甲×2/盔×2/肩/腿/臂），全 Item 类组件数据完备
PICKS = [
    ("weapons.xml", "steppe_war_bow"),
    ("weapons.xml", "crossbow_c"),
    ("shields.xml", "heavy_round_shield"),
    ("body_armors.xml", "battania_mercenary_armor"),
    ("body_armors.xml", "desert_lamellar"),
    ("head_armors.xml", "sturgian_helmet_closed"),
    ("head_armors.xml", "western_plated_helmet"),
    ("shoulder_armors.xml", "scale_shoulder_armor"),
    ("leg_armors.xml", "battania_warlord_boots"),
    ("arm_armors.xml", "battania_warlord_bracers"),
]

# ---------- L2 观感文案（人工层；数值必须与快照一致） ----------
L2 = {
    "steppe_war_bow": {
        "assert": "草原战弓是库赛特骑手的当家硬弓，穿透力强、精准出众，是草原骑射的招牌兵器。",
        "rumor": "库赛特人从小在马背上拉弓，那战弓射起来又快又准。听说好手能在百步外钉中奔狐。",
        "detail": "草原战弓穿刺 54、弹速 80、精准 95——弹道又平又狠，配破甲箭对付中甲最划算；弓身 107 长，骑射步射两用。",
    },
    "crossbow_c": {
        "assert": "劲弩是瓦兰迪亚弩手里的重家伙，一击穿透重甲，装填慢但威力冠绝。",
        "rumor": "瓦兰迪亚的弩手装的弩劲儿大，一箭能洞穿铁甲，就是上弦慢得让人心焦。",
        "detail": "劲弩穿刺 89、弹速 87、精准 98——威力精准都顶格，贴脸放冷箭几乎不走空；射速档 60 是短板，装填要躲好掩护。",
    },
    "heavy_round_shield": {
        "assert": "重型圆盾是斯特吉亚步卒的看家家伙，盾面结实、耐打抗射，列阵堵路全靠它。",
        "rumor": "北方人的圆盾又大又厚，箭射上去跟挠痒似的。扛盾的兵往那一站，就是一堵墙。",
        "detail": "重型圆盾耐久 500、盾防 9、格挡速 86——扛线第一梯队；四公斤半的分量只适合步战，骑手嫌坠手。",
    },
    "battania_mercenary_armor": {
        "assert": "奢华布面铁甲是巴坦尼亚佣兵里的体面货，铁甲衬布面，防护顶尖还不失体面。",
        "rumor": "打过几场硬仗的佣兵才置办得起那样的甲，铁片钉在厚布上，刀砍上去梆梆响。",
        "detail": "奢华布面铁甲身甲 53、臂 10、腿 18，重 24——防护数值顶级但压秤，步战精锐穿它，轻骑嫌沉。",
    },
    "desert_lamellar": {
        "assert": "轻型扎甲是阿塞莱行家的护身甲，甲片编排透气轻便，热天作战的讲究货。",
        "rumor": "沙地里的武士穿那种甲片编的袍子，太阳晒不透，刀剑也近不了身。",
        "detail": "轻型扎甲身甲 48、臂 14，重 16.5——比同防护的铁甲轻一大截，热带作战的护甲标杆，南方市场常年紧俏。",
    },
    "sturgian_helmet_closed": {
        "assert": "闭面军阀盔是斯特吉亚头领级的全覆面盔，护头到位，寒地作战的体面装备。",
        "rumor": "北方头领戴的那种盔连脸都罩住，只露两条缝看人，看着就让人腿软。",
        "detail": "闭面军阀盔护头 53、重 3.8——全覆面里防护顶格，代价是视野呼吸受限；阵前头领与卫士配发。",
    },
    "western_plated_helmet": {
        "assert": "侧护覆板盔是瓦兰迪亚骑士的常用盔，护板延伸护颊护耳，防护均衡。",
        "rumor": "瓦兰迪亚老爷们的铁盔两侧带护板，马上冲杀时脸侧不露空门。",
        "detail": "侧护覆板盔护头 52、重 3.9，与闭面军阀盔同档；护板兼顾侧面劈砍，骑枪冲锋配它最稳。",
    },
    "scale_shoulder_armor": {
        "assert": "鳞甲护肩是瓦兰迪亚士兵的肩部护件，甲片编排灵活，护肩兼护臂根。",
        "rumor": "肩上披那么一圈铁鳞片，刀从上头劈下来会滑开，当兵的都说值。",
        "detail": "鳞甲护肩肩甲 16、臂 8，重 1.7——轻量补位件，常与锁甲衫叠穿补齐肩臂防护短板。",
    },
    "battania_warlord_boots": {
        "assert": "鳞甲靴是巴坦尼亚军阀级的小腿护具，甲片护胫，护腿装备里的顶格货。",
        "rumor": "头领脚上那双靴子缀满甲片，藤条砍上去跟砍石头一个声。",
        "detail": "鳞甲靴护腿 26——腿甲里防护顶格档；重装步兵配它顶正面，价钱也随之顶格。",
    },
    "battania_warlord_bracers": {
        "assert": "鳞甲军阀护腕是巴坦尼亚军阀级的前臂护件，硬甲片护臂，挥刀格挡两不误。",
        "rumor": "头领小臂上绑的甲片子，接刀都不带晃的，寻常兵的护腕比不了。",
        "detail": "鳞甲军阀护腕护臂 25——臂甲顶格档；分量轻不碍挥砍，斧手剑客都认它。",
    },
}

RUMOR_GRANTS = [
    ("profile.commoner", "local"), ("profile.villager", "local"),
    ("profile.tavernkeeper", "faction"), ("profile.ransom_broker", "faction"),
    ("profile.townsfolk", "regional"), ("profile.notable", "regional"),
    ("profile.merchant", "faction"), ("profile.headman", "national"),
    ("profile.soldier", "national"), ("profile.noble", "elite"),
]
DETAIL_GRANTS = [
    ("profile.headman", "national"), ("profile.merchant", "faction"),
    ("profile.soldier", "national"), ("profile.noble", "elite"),
]

REG_BIND = {
    "profile_registry_version": "1.0.0",
    "profile_registry_hash": "309D10583E7B4AF05F28DE2C2E67A2528CB3D6C45752369C0F7D160FABC3F3A5",
    "referral_registry_version": "1.0.0",
    "referral_registry_hash": "6E17075F1F967BE28C0437121DB50E5C30809C4EFC3D2A5F22AD9BB0B3B926CD",
}


def sha(b):
    return hashlib.sha256(b).hexdigest().upper()


def cn_name(con, token):
    row = con.execute(
        "SELECT text FROM localization_entries WHERE stringId=? AND language='CNs' LIMIT 1",
        (token,)).fetchone()
    return row["text"] if row else None


def extract(fn, wid):
    root = ET.parse(os.path.join(GAME, fn)).getroot()
    for it in root.iter("Item"):
        if it.get("id") == wid:
            return fn, it
    raise SystemExit(f"not found: {fn}#{wid}")


def main():
    con = sqlite3.connect(f"file:{DB}?mode=ro", uri=True)
    con.row_factory = sqlite3.Row

    # ---- 1) 快照：每物品一行属性转录 ----
    lines, meta = [], {}
    ATTR_LABELS = [
        ("weapon_class", None), ("thrust_damage", "穿刺"), ("missile_speed", "弹速"),
        ("accuracy", "精准"), ("speed_rating", "射速档"), ("weapon_length", "弓身长"),
        ("body_armor", "身甲"), ("head_armor", "护头"), ("arm_armor", "护臂"),
        ("leg_armor", "护腿"), ("hit_points", "耐久"), ("weight", "重"),
    ]
    for fn, wid in PICKS:
        fn2, it = extract(fn, wid)
        name = it.get("name") or ""
        tok = name.split("{=")[1].split("}")[0] if "{=" in name else ""
        en = name.split("}", 1)[1].strip() if "}" in name else wid
        cn = cn_name(con, tok)
        assert cn, wid
        cn = cn.split("{@Plural}")[0].strip()
        # Weapon/Armor 组件属性
        attrs = {}
        w = it.find(".//Weapon")
        if w is not None:
            attrs.update({k: w.get(k) for k in w.keys() if k not in ("physics_material", "item_usage", "position", "rotation")})
        a = it.find(".//Armor")
        if a is not None:
            attrs.update({k: a.get(k) for k in a.keys() if a.get(k) and k not in ("covers_legs", "covers_body", "modifier_group", "material_type")})
        seg = f"{cn} | {it.get('Type')}"
        for k, lab in ATTR_LABELS:
            v = attrs.get(k)
            if v is not None and lab:
                seg += f" | {lab} {v}"
        seg += f" | 出处 {fn}#{wid} | 译名token {tok}"
        lines.append(f"item2.{wid} => {seg}")
        meta[wid] = {"cn": cn, "en": en, "token": tok, "type": it.get("Type"),
                     "culture": (it.get("culture") or "").replace("Culture.", "")}
    snap_text = "\n".join(lines) + "\n"
    file_hash = sha(snap_text.encode("utf-8"))
    io.open(os.path.join(WS_AUTH, "sources", SNAP), "w", encoding="utf-8", newline="\n").write(snap_text)

    # ---- 2) 来源登记 ----
    reg = {
        "source_id": SRC_ID, "source_version": SRC_VER, "source_nature": "game_snapshot",
        "universe": "awake_current", "era": "current", "locator_root": SNAP,
        "source_content_hash": file_hash, "content_tier": "base",
        "license_status": "permitted", "use_status": "active", "valid_until": None,
        "imported_at": "2026-09-13T00:00:00Z", "normalization_version": "utf8-lf-no-bom-v1",
    }
    with io.open(os.path.join(WS_AUTH, "sources", "source-game-items-poc2.yaml"), "w",
                 encoding="utf-8", newline="\n") as f:
        yaml.safe_dump(reg, f, allow_unicode=True, sort_keys=False)

    # ---- 3) 10 档 ----
    made = []
    for fn, wid in PICKS:
        m = meta[wid]
        seg = snap_text.split(f"item2.{wid} => ", 1)[1].split("\n", 1)[0]
        quote = seg.split(" | 出处", 1)[0]
        qhash = sha(quote.encode("utf-8"))
        src = {"source_id": SRC_ID, "source_version": SRC_VER,
               "source_content_hash": file_hash,
               "locator": f"bannerlord.items.xml#{wid}",
               "quote_hash": qhash, "quote": quote}
        doc = {
            "schema_version": "awake.worldbook.authoring.v1",
            "revision": 1,
            "id": f"doc.economy.item-{wid}",
            "title": {"zh-CN": m["cn"], "en": m["en"]},
            "status": "needs_review",
            "domain": "economy",
            "subdomain": "items",
            "universe": "awake_current",
            "era": {"key": "current", "certainty": "bounded"},
            "content_tier": "base",
            "aliases": {"zh-CN": [m["cn"], m["type"]], "en": [wid]},
            "summary": {"zh-CN": L2[wid]["assert"]},
            "registry_bindings": dict(REG_BIND),
            "sources": [src],
            "authority": {"owner": "awake_canon", "conflict_policy": "canon_wins"},
            "assertions": [{
                "id": f"assertion.item-{wid}-1", "revision": 1, "kind": "fact",
                "text": {"zh-CN": L2[wid]["assert"]},
                "sources": [src],
                "expressions": [
                    {"id": f"expr.item-{wid}-rumor", "revision": 1, "layer": "rumor",
                     "text": {"zh-CN": L2[wid]["rumor"]}, "sources": [src],
                     "grants": [{"profile_id": p, "scope": s, "min_detail": "rumor"}
                                for p, s in RUMOR_GRANTS],
                     "denies": []},
                    {"id": f"expr.item-{wid}-detail", "revision": 1, "layer": "detail",
                     "text": {"zh-CN": L2[wid]["detail"]}, "sources": [src],
                     "grants": [{"profile_id": p, "scope": s, "min_detail": "detail"}
                                for p, s in DETAIL_GRANTS],
                     "denies": []},
                ],
            }],
        }
        doc = json.loads(json.dumps(doc))
        class NoAlias(yaml.dumper.Dumper):
            def ignore_aliases(self, data):
                return True
        text = yaml.dump(doc, Dumper=NoAlias, allow_unicode=True, sort_keys=False,
                         default_flow_style=False, width=100)
        assert "&id" not in text and "*id" not in text
        fnout = f"item-{wid}.yaml"
        io.open(os.path.join(OUT, fnout), "w", encoding="utf-8", newline="\n").write(text)
        made.append(fnout)
    print("generated:", len(made), "entries; snapshot hash:", file_hash[:16])
    for x in made:
        print("  ", x)


if __name__ == "__main__":
    main()
