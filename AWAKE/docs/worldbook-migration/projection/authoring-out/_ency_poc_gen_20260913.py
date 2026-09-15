# -*- coding: utf-8 -*-
"""百科化 PoC 生成器（宪章 §四流水线，L1 底座全自动 + L2 观感人工文案内嵌）。
产出三件套 + 10 档 yaml：
  - sources/game-items-poc1.txt          来源快照（每物品一行属性转录）
  - sources/source-game-items-poc1.yaml  来源登记
  - item-*.yaml                          10 个 L 规格条目
红线：JSON 往返断引用 + ignore_aliases（WB-YAML-006）；slug 只作文件名不进文档体；
六身份显式落点；grant.min_detail==layer；quote 必须在快照行内（子串）。
"""
import io, os, json, re, hashlib, sqlite3, yaml, shutil

OUT = os.path.dirname(os.path.abspath(__file__))
WS_AUTH = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
SRC_ID = "source.calradia.game.items"
SRC_VER = "bannerlord-1.3.15.110062"
SNAP = "game-items-poc1.txt"

# ---------- L2 观感文案（人工层：断言 1 + rumor 表达 + detail 表达） ----------
# 每条：断言≤80字；rumor=市井口吻；detail=行家口吻（引属性成文）
L2 = {
    "salt": {
        "assert": "盐是卡拉迪亚餐桌上离不开的日用品，产地集中、价贱，一转手就是硬通货。",
        "rumor": "盐比肉还金贵，腌过冬的肉全指着它。听赶集的人说，盐价贱的地方都在海边和盐湖边。",
        "detail": "盐基准价 40，产地集中是它赚钱的根本：产地价贱，运进内陆翻着倍卖。接盐单先盯产地行情。",
    },
    "fur": {
        "assert": "毛皮是北方寒地的招牌货，保暖器物的头等原料，价高仅次于天鹅绒的体面商品。",
        "rumor": "北方猎户拿毛皮换盐换酒，一张好皮子能顶半个月嚼谷。听说南边老爷们就认这个。",
        "detail": "毛皮基准价 400，北方价贱南方价贵，贩皮走的是南北价差。压仓库要防潮，霉了皮子就不值钱了。",
    },
    "velvet": {
        "assert": "天鹅绒是最贵的织物，城里体面人家的门面货，工坊织造、价压群商。",
        "rumor": "天鹅绒是老爷太太们身上的料子，一卷顶咱一头牛。听说只有大城工坊才织得出来。",
        "detail": "天鹅绒基准价 575，全货物行当里最贵的一档。货紧，城里工坊出货慢，逢婚丧嫁娶旺季还能再抬价。",
    },
    "spice": {
        "assert": "香料是远方来的调味珍品，南方商队贩来的高值货物，餐桌与药柜都要用它。",
        "rumor": "听说南边沙漠那头来的驼队带着香料，撒一点在肉里香得不得了，贵人宴席上才见得着。",
        "detail": "香料基准价 300，产地远、路途长，价差全在路上了。跟阿塞莱驼队拿货最划算，但要好骡马。",
    },
    "silver": {
        "assert": "银矿石是山里挖出的矿产原料，熔炼铸币的上游货，矿镇附近才有得收。",
        "rumor": "山里有银矿的地方，连路上的石头都闪着光。矿工说一块好矿石够买十袋粮。",
        "detail": "银矿石基准价 100，看着不高，胜在产地直收、出货稳定——铸币坊和银匠常年要货，是压舱的买卖。",
    },
    "saddle_horse": {
        "assert": "旅行马是路上最常见的乘马，价廉耐用，商队赶路、传信跑腿都指着它。",
        "rumor": "出门赶路全靠旅行马，不值几个钱但耐使。庄稼人买马，头一个看的就是它。",
        "detail": "旅行马基准价 140，冲撞 12、速度 37、操控 52——样样平平但没短板，走商队最划算的驮乘两用马。",
    },
    "mule": {
        "assert": "骡子是驮货的行家，性子稳、负重大，商队和驮帮的当家脚力。",
        "rumor": "骡子不咬人不尥蹶子，驮上两百斤照走。赶驮的都说，宁要一头好骡子不要一匹病马。",
        "detail": "骡子基准价 120，跑不快但驮得稳，山路水路都吃得住。跑短途贩货，骡队的本钱回得最快。",
    },
    "pack_camel": {
        "assert": "驮运骆驼是沙地商路上的大王，耐渴耐热，沙漠商队的命根子。",
        "rumor": "听说沙漠里马走不了，全靠骆驼。那家伙几天不喝水照样走，沙漠商队离了它就得埋沙子里。",
        "detail": "驮运骆驼基准价 220，贵过骡马一截，但穿沙漠只有它行。接沙漠单子，骆驼和向导的钱都得先算进本里。",
    },
    "cow": {
        "assert": "牛是农家的活产，耕地挤奶两头忙，集市上论头卖的大家畜。",
        "rumor": "一头牛就是半份家业，春耕全指着它。谁家要卖牛，村里人都要念叨好几天。",
        "detail": "牛基准价 200，活畜里的大件。产地直收压价、城镇集市出手，牛市随季节走，春耕前价最好。",
    },
    "sheep": {
        "assert": "羊是牧地的当家牲畜，肉奶毛三样出产，赶着走的就是会动的钱袋。",
        "rumor": "牧民家的羊就是钱串子：卖羊毛、卖羊奶，过节宰一只，一年到头不缺进项。",
        "detail": "羊基准价 80，单只利薄、走量取胜。跟着牧群季节收，转场前出手最合适，羊毛还能再赚一道。",
    },
}

# 六身份 + 全谱系扩展（rumor 全谱系抄现有档口径；detail 按身份上限直配）
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

def sha(b): return hashlib.sha256(b).hexdigest().upper()

def main():
    con = sqlite3.connect(f"file:{DB}?mode=ro", uri=True)
    con.row_factory = sqlite3.Row
    picks = ["salt", "fur", "velvet", "spice", "silver",
             "saddle_horse", "mule", "pack_camel", "cow", "sheep"]
    items = {}
    for wid in picks:
        r = con.execute("SELECT * FROM bannerlord_items WHERE entityId=?", (wid,)).fetchone()
        assert r, wid
        tok = re.search(r"\{=(\w+)\}", r["name"] or "")
        cnrow = con.execute(
            "SELECT text FROM localization_entries WHERE stringId=? AND language='CNs' LIMIT 1",
            (tok.group(1),)).fetchone() if tok else None
        cn_full = cnrow["text"] if cnrow else ""
        cn = cn_full.split("{@Plural}")[0].strip()
        items[wid] = dict(r) | {"cn": cn, "token": tok.group(1) if tok else "",
                                "file": (r["filePath"] or "").split("/")[-1]}
    # 战马等 value 为 None 的不收；此处 10 件全有数值
    for wid in picks:
        assert items[wid]["value"] is not None, wid

    # ---- 1) 快照：每物品一行属性转录（quote 子串定位的锚） ----
    lines = []
    for wid in picks:
        it = items[wid]
        seg = f"{it['cn']} | {it['itemType']} | 基准价 {it['value']}"
        if it["itemType"] in ("Horse", "Animal"):
            for lab, col in (("负重", "weight"), ("冲撞", "horseChargeDamage"),
                             ("速度", "horseSpeed"), ("操控", "horseManeuver")):
                if it[col] is not None:
                    seg += f" | {lab} {it[col]}"
        seg += f" | 出处 {it['file']} | 译名token {it['token']}"
        lines.append(f"item.{wid} => {seg}")
    snap_text = "\n".join(lines) + "\n"
    snap_b = snap_text.encode("utf-8")
    file_hash = sha(snap_b)
    io.open(os.path.join(WS_AUTH, "sources", SNAP), "w", encoding="utf-8", newline="\n").write(snap_text)

    # ---- 2) 来源登记 ----
    reg = {
        "source_id": SRC_ID, "source_version": SRC_VER, "source_nature": "game_snapshot",
        "universe": "awake_current", "era": "current", "locator_root": SNAP,
        "source_content_hash": file_hash, "content_tier": "base",
        "license_status": "permitted", "use_status": "active", "valid_until": None,
        "imported_at": "2026-09-13T00:00:00Z", "normalization_version": "utf8-lf-no-bom-v1",
    }
    with io.open(os.path.join(WS_AUTH, "sources", "source-game-items-poc1.yaml"), "w",
                 encoding="utf-8", newline="\n") as f:
        yaml.safe_dump(reg, f, allow_unicode=True, sort_keys=False)

    # ---- 3) 10 档 ----
    REG_BIND = {  # 全库常量（与 40 档一致）
        "profile_registry_version": "1.0.0",
        "profile_registry_hash": "309D10583E7B4AF05F28DE2C2E67A2528CB3D6C45752369C0F7D160FABC3F3A5",
        "referral_registry_version": "1.0.0",
        "referral_registry_hash": "6E17075F1F967BE28C0437121DB50E5C30809C4EFC3D2A5F22AD9BB0B3B926CD",
    }
    EN_NAME = {"salt": "Salt", "fur": "Fur", "velvet": "Velvet", "spice": "Spice",
               "silver": "Silver Ore", "saddle_horse": "Saddle Horse", "mule": "Mule",
               "pack_camel": "Pack Camel", "cow": "Cow", "sheep": "Sheep"}
    made = []
    for wid in picks:
        it = items[wid]
        cn_name = it["cn"]
        seg = snap_text.split(f"item.{wid} => ", 1)[1].split("\n", 1)[0]
        quote = seg.split(" | 出处", 1)[0]          # 快照行属性段作 quote
        qhash = sha(quote.encode("utf-8"))
        src = {"source_id": SRC_ID, "source_version": SRC_VER,
               "source_content_hash": file_hash,
               "locator": f"bannerlord.items#{wid}",
               "quote_hash": qhash, "quote": quote}
        doc = {
            "schema_version": "awake.worldbook.authoring.v1",
            "revision": 1,
            "id": f"doc.economy.item-{wid}",
            "title": {"zh-CN": cn_name, "en": EN_NAME[wid]},
            "status": "needs_review",
            "domain": "economy",
            "subdomain": "items" if it["itemType"] in ("Horse", "Animal") else "goods",
            "universe": "awake_current",
            "era": {"key": "current", "certainty": "bounded"},
            "content_tier": "base",
            "aliases": {"zh-CN": [cn_name, it["itemType"]], "en": [wid]},
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
        # 红线：JSON 往返断共享引用 + ignore_aliases
        doc = json.loads(json.dumps(doc))
        class NoAlias(yaml.dumper.Dumper):
            def ignore_aliases(self, data): return True
        fn = f"item-{wid}.yaml"
        text = yaml.dump(doc, Dumper=NoAlias, allow_unicode=True, sort_keys=False,
                         default_flow_style=False, width=100)
        assert "&id" not in text and "*id" not in text
        io.open(os.path.join(OUT, fn), "w", encoding="utf-8", newline="\n").write(text)
        made.append(fn)
    print("generated:", len(made), "entries;", "snapshot hash:", file_hash[:16])
    for fn in made: print("  ", fn)

if __name__ == "__main__":
    main()
