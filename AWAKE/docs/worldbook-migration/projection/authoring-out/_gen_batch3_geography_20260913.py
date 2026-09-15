# -*- coding: utf-8 -*-
"""地点批第三批（geography 第二批 6 档）：塔奈西斯湖/珀拉斯海/德律亚山/德夫赛格高原/弥戎河/纳哈撒沙漠。
工序：拆→核→重写。A=官方文本（程序化拉取）；B=编年史原声（变体切片带校验）。
"""
import io, os, json, hashlib, sqlite3, yaml

WS = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"
AO = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out"
DB = r"file:C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db?mode=ro"
RULES = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/AnimusForge/PlayerExports/卡拉迪亚编年史/knowledge/rules"

db = sqlite3.connect(DB, uri=True)
cur = db.cursor()

def loc_cn(strid):
    r = cur.execute("SELECT text FROM localization_entries WHERE stringId=? AND language='CNs'", (strid,)).fetchone()
    return r[0] if r else None

def rule_variant(rulefile, idx):
    d = json.load(open(os.path.join(RULES, rulefile), encoding="utf-8-sig"))
    return (d.get("Variants") or [])[idx].get("Content", "")

# ---------------- A 级引文（stringId, None=程序拉全文） ----------------
A_FULL = {
    # 塔奈西斯湖
    "chaikand":   ("SUnaVliN", None),
    "pons":       ("Settlements.Settlement.text.castle_village_EN7_2", None),
    "epinosa":    ("Settlements.Settlement.text.castle_village_EN7_1", None),
    "syronea":    ("UHvEEEBb", None),
    "makeb":      ("bPDqyqkb", None),
    "usek":       ("Settlements.Settlement.text.castle_village_K1_1", None),
    "lartusys":   ("Settlements.Settlement.text.village_ES5_2", None),
    # 珀拉斯海
    "quyaz":      ("1ImuW3My", None),
    "ortysia":    ("JhBSnWAe", None),
    "jalmarys":   ("TI3fS2xN", None),
    "lysia":      ("Settlements.Settlement.text.village_EW4_2", None),
    "onica":      ("Settlements.Settlement.text.castle_village_EW3_1", None),
    "zeonica":    ("kghCLS9q", None),
    # 德律亚山
    "aracathos":  ("Settlements.Settlement.text.village_EN1_1", None),
    "ataconia":   ("Settlements.Settlement.text.castle_village_EN6_1", None),
    "jeracos":    ("Settlements.Settlement.text.village_EN2_2", None),
    "stathymos":  ("Settlements.Settlement.text.village_EN1_2", None),
    "enoisa":     ("Settlements.Settlement.text.village_EN3_1", None),
    "hetania":    ("Settlements.Settlement.text.village_EN4_4", None),
    "vealos":     ("Settlements.Settlement.text.village_EN5_1", None),
    "rhesos":     ("Settlements.Settlement.text.castle_village_EN3_1", None),
    # 德夫赛格高原
    "ortongard":  ("Dxnj5JQE", None),
    "esme":       ("Settlements.Settlement.text.castle_village_K1_2", None),
    "karakalat":  ("Settlements.Settlement.text.village_K2_1", None),
    "asalig":     ("Settlements.Settlement.text.village_K1_4", None),
    "odokh":      ("ygJkPxyc", None),
    # 弥戎河
    "rhemtoil":   ("Settlements.Settlement.text.castle_village_B5_1", None),
    "mecalovea":  ("Settlements.Settlement.text.castle_village_EN9_1", None),
    "ismilkorg":  ("Settlements.Settlement.text.castle_village_S4_2", None),
    "montos":     ("Settlements.Settlement.text.village_EW1_2", None),
    "chornobas":  ("Settlements.Settlement.text.village_S3_1", None),
    "epicrotea":  ("n9WMUuSp", None),
    # 纳哈撒
    "nahasa_cul": ("a2AjI8nc", None),
    "aserai_cul": ("Khr5yETv", None),
    "jawwal":     ("RWOYui01", None),
    "sandstorm":  ("yJMpjiP7", None),
    "zalm":       ("Settlements.Settlement.text.village_A7_4", None),
}
for k, (sid, q) in A_FULL.items():
    if q is None:
        A_FULL[k] = (sid, loc_cn(sid))
        assert A_FULL[k][1], "A text missing in DB: %s" % k

# ---------------- B 级引文（编年史变体切片） ----------------
B_QUOTES = {
    "tan_V0":  ("rule_塔奈西斯湖__塔奈西斯湖.json", 0, "塔奈西斯湖，草原民族称之为达那孜海，帝国人则叫它塔奈西斯湖。"),
    "tan_V2":  ("rule_塔奈西斯湖__塔奈西斯湖.json", 2, "达那孜海是我们祖辈放牧的地方。草原上的游牧民族世世代代生活在湖的东岸，喝着湖里的水，在湖边的草场上牧马。"),
    "tan_V7":  ("rule_塔奈西斯湖__塔奈西斯湖.json", 7, "塔奈西斯湖——草原民族称达那孜海——是卡拉迪亚东部最大的淡水湖，也是帝国与库赛特汗国之间的天然分界线。"),
    "per_V0":  ("rule_珀拉斯海__珀拉斯海.json", 0, "珀拉斯海是卡拉迪亚南方一片广阔的海域，连接着阿塞莱、帝国和瓦兰迪亚的海岸。"),
    "per_V3":  ("rule_珀拉斯海__珀拉斯海.json", 3, "你知道阿塞莱人管珀拉斯海叫什么吗？他们叫它“南方的怀抱”。"),
    "per_V12": ("rule_珀拉斯海__珀拉斯海.json", 12, "珀拉斯海在帝国史书中曾被称为“帝国之湖”。"),
    "dry_V0":  ("rule_德律亚山__德律亚山.json", 0, "德律亚山是帝国北方的一组高大山脉，山体呈红色，因富含铁矿脉而得名，与曾居于此地的德律亚人共享同一个古老名称。"),
    "dev_V0":  ("rule_德夫赛格高原__德夫赛格高原.json", 0, "德夫赛格高原是卡拉迪亚与达西的分界处，目前被库赛特以及达西沙阿共同统治着，这里有着很好的草原，算是个养马的好地方，但是绝对不是种地的好地方"),
    "mir_V0":  ("rule_弥戎河__弥戎河.json", 0, "弥戎河是巴旦尼亚，北帝国，斯特吉亚的界河，流量不大，但汛期无法徒涉，结冰期有两个半月的时间，是北帝国西北防线的重要天堑"),
    "nah_V1":  ("rule_纳哈撒沙漠__纳哈撒沙漠.json", 1, "纳哈撒是我们的母亲，也是我们的试炼场。"),
    "nah_V5":  ("rule_纳哈撒沙漠__纳哈撒沙漠.json", 5, "纳哈撒沙漠是帝国南方的天然屏障，也是我们永远的耻辱。几百年来，帝国的军团无数次试图征服那片沙海，但每次都铩羽而归。"),
}
for k, (rf, vi, q) in B_QUOTES.items():
    c = rule_variant(rf, vi)
    assert q in c, "B quote not in rule: %s\n  want: %s\n  have: %s" % (k, q, c[:120])

# ---------------- 源文件与登记 ----------------
def H(x):
    return hashlib.sha256(x.encode("utf-8")).hexdigest().upper()

def hlower(x):
    return hashlib.sha256(x.encode("utf-8")).hexdigest()

game_txt = "\n".join("%s => %s" % (A_FULL[k][0], A_FULL[k][1]) for k in sorted(A_FULL)) + "\n"
chron_txt = "\n\n".join("%s [V%d] %s" % (B_QUOTES[k][0], B_QUOTES[k][1], B_QUOTES[k][2]) for k in sorted(B_QUOTES)) + "\n"

GAME_TXT_F = "game-settlements-geography2.txt"
CHRON_TXT_F = "chronicle-animusforge-geography2.txt"
io.open(os.path.join(WS, "sources", GAME_TXT_F), "w", encoding="utf-8", newline="\n").write(game_txt)
io.open(os.path.join(WS, "sources", CHRON_TXT_F), "w", encoding="utf-8", newline="\n").write(chron_txt)
game_hash = hlower(game_txt)
chron_hash = hlower(chron_txt)

GAME_SRC = dict(source_id="source.calradia.game.settlements.geography2", source_version="bannerlord-1.3.15.110062",
                source_nature="game_snapshot", universe="awake_current", era="current", locator_root=GAME_TXT_F,
                source_content_hash=game_hash, content_tier="base", license_status="permitted",
                use_status="active", valid_until=None, imported_at="2026-09-13T00:00:00Z",
                normalization_version="utf8-lf-no-bom-v1")
CHRON_SRC = dict(source_id="source.calradia.chronicle.animusforge.geography2", source_version="animusforge-20260913",
                 source_nature="chronicle", universe="awake_current", era="historical", locator_root=CHRON_TXT_F,
                 source_content_hash=chron_hash, content_tier="base", license_status="permitted",
                 use_status="active", valid_until=None, imported_at="2026-09-13T00:00:00Z",
                 normalization_version="utf8-lf-no-bom-v1")

class ND(yaml.SafeDumper):
    def ignore_aliases(self, data):
        return True

def ydump(obj):
    return yaml.dump(obj, Dumper=ND, allow_unicode=True, sort_keys=False, width=4096, default_flow_style=False)

for nm, src in [("source-game-settlements-geography2.yaml", GAME_SRC), ("source-chronicle-animusforge-geography2.yaml", CHRON_SRC)]:
    io.open(os.path.join(WS, "sources", nm), "w", encoding="utf-8", newline="\n").write(ydump(src))

RB = {"profile_registry_version": "1.0.0",
      "profile_registry_hash": "309D10583E7B4AF05F28DE2C2E67A2528CB3D6C45752369C0F7D160FABC3F3A5",
      "referral_registry_version": "1.0.0",
      "referral_registry_hash": "6E17075F1F967BE28C0437121DB50E5C30809C4EFC3D2A5F22AD9BB0B3B926CD"}

def aref(key):
    sid, q = A_FULL[key]
    return {"source_id": GAME_SRC["source_id"], "source_version": GAME_SRC["source_version"],
            "source_content_hash": game_hash, "locator": "bannerlord.db#localization." + sid,
            "quote_hash": H(q), "quote": q}

def bref(key):
    rf, vi, q = B_QUOTES[key]
    return {"source_id": CHRON_SRC["source_id"], "source_version": CHRON_SRC["source_version"],
            "source_content_hash": chron_hash, "locator": "rules/%s#/Variants/%d/Content" % (rf, vi),
            "quote_hash": H(q), "quote": q}

def G(pid, scope, detail, **cond):
    g = {"profile_id": "profile." + pid, "scope": scope, "min_detail": detail}
    for k, v in cond.items():
        if k.endswith("_ids"):
            g[k] = ["entity.%s.%s" % (k[:-4], x) for x in v]
        else:
            g[k] = v
    return g

def E(docslug, eid, layer, text, grants, srcs):
    return {"id": "expr.%s-%s" % (docslug, eid), "revision": 1, "layer": layer,
            "text": {"zh-CN": text}, "sources": srcs, "grants": grants, "denies": []}

def A(docslug, aid, text, kind, exprs):
    allsrcs, seen = [], set()
    for e in exprs:
        for s in e["sources"]:
            k = (s["source_id"], s["quote_hash"])
            if k not in seen:
                seen.add(k)
                allsrcs.append(s)
    return {"id": "assertion.%s-%s" % (docslug, aid), "revision": 1, "kind": kind,
            "text": {"zh-CN": text}, "sources": allsrcs, "expressions": exprs}

def doc(did, zh, en, domain, sub, aliases, summary, assertions):
    allsrcs, seen = [], set()
    for a in assertions:
        for src in a["sources"]:
            k = (src["source_id"], src["quote_hash"])
            if k not in seen:
                seen.add(k)
                allsrcs.append(src)
    return {"schema_version": "awake.worldbook.authoring.v1", "revision": 1, "id": did,
            "title": {"zh-CN": zh, "en": en}, "status": "needs_review",
            "domain": domain, "subdomain": sub, "universe": "awake_current",
            "era": {"key": "current", "certainty": "bounded"}, "content_tier": "base",
            "aliases": {"zh-CN": aliases[0], "en": aliases[1]},
            "summary": {"zh-CN": summary},
            "registry_bindings": RB,
            "authority": {"owner": "awake_canon", "conflict_policy": "canon_wins"},
            "sources": allsrcs,
            "assertions": assertions}

docs = []

# ================= 档1 塔奈西斯湖 =================
SL = "tanaesis-lake"
docs.append(doc("doc.geography." + SL, "塔奈西斯湖", "Lake Tanaesis", "geography", "terrain",
    (["塔奈西斯湖", "达那孜海"], ["Lake Tanaesis", "Tanaz Sea"]),
    "塔奈西斯湖：帝国东部大湖，库赛特人称达那孜海；伊勒坦运输线东端，帝国与草原贸易的分界水域。马凯布、柴坎、席隆尼亚三城环抱。",
    [
        A(SL, "1", "塔奈西斯湖（Lake Tanaesis，库赛特语称达那孜海/Tanaz）是卡拉迪亚东部最大的淡水湖：北端马凯布扼喀拉卡孜河入湖的大瀑布，东岸柴坎居芦苇甸旁，西南岸是席隆尼亚；伊勒坦运输线的冰川湖网令船只得以在拉科尼斯湖与塔奈西斯湖之间拖行。", "fact",
          [
              E(SL, "portage-summary", "summary",
                "这片大湖连着西边那串冰川湖，长船能从拉科尼斯湖一路拖到塔奈西斯湖来——帝国和草原的货，都打这儿过。",
                [G("townsfolk", "regional", "summary")],
                [aref("pons"), aref("epinosa")]),
              E(SL, "three-towns-detail", "detail",
                "湖周三城各有各的算盘：北端马凯布守着喀拉卡孜河的大瀑布收过路钱，东岸柴坎是贸易中转兼兵站，西南的席隆尼亚从前是帝国丝绸的重镇。",
                [G("merchant", "faction", "detail")],
                [aref("makeb"), aref("chaikand"), aref("syronea"), bref("tan_V0")]),
          ]),
        A(SL, "2", "此湖今为帝国与库赛特汗国的分界水域：帝国控西南岸（席隆尼亚一方），东岸与北端归库赛特（柴坎为兀儿浑乃特部封地，马凯布由库赛特统治者家族亲领）；帝国旧日的「外塔奈西亚」保护领已被库赛特夺去。", "fact",
          [
              E(SL, "steppe-rumor", "rumor",
                "老辈管这湖叫达那孜海——祖辈就在东岸喝这湖的水、牧马。帝国人来圈地那些年不算数，如今湖还是咱们的湖。",
                [G("villager", "local", "rumor", culture_ids=["khuzait"])],
                [bref("tan_V2"), aref("usek")]),
              E(SL, "imperial-rumor", "rumor",
                "湖对岸就是库赛特人的地界。听跑船的说，从前帝国商船能沿湖一路北上到柴坎，如今到马凯布瀑布就得停下来交钱了。",
                [G("villager", "local", "rumor", culture_ids=["empire"])],
                [bref("tan_V7"), aref("makeb")]),
              E(SL, "frontier-noble", "detail",
                "外塔奈西亚丢了以后，席隆尼亚的元老们被迫重新拾起兵法——镀金甲掸了灰，宝石剑磨了刃，带着家兵对抗兀儿浑的勇士。湖上这道分界线，是打出来的。",
                [G("noble", "elite", "detail")],
                [aref("syronea"), bref("tan_V7")]),
          ]),
        A(SL, "3", "湖区物产：湖中产鲈鱼、鲷鱼，偶见巨型梭鱼；沿湖出长毛细羊毛（东岸）、亚麻（北岸与西岸洼地）、盐（庞斯山丘与拉耳图绪斯盐泉）与桑蚕丝绸（南岸帝国殖民地）。", "fact",
          [
              E(SL, "produce-detail", "detail",
                "湖里的鲈鱼鲷鱼肥，运气好能见着巨型梭鱼；岸上的营生更多——东岸的长毛羊、北岸的亚麻、盐矿盐泉，南岸还种桑养蚕。",
                [G("merchant", "faction", "detail")],
                [aref("usek"), aref("lartusys")]),
              E(SL, "fish-rumor", "rumor",
                "湖里鱼多，渔村一根接一根的下网。老人讲湖心见过比船还长的梭鱼——信不信由你。",
                [G("villager", "local", "rumor")],
                [aref("lartusys")]),
          ]),
    ]))

# ================= 档2 珀拉斯海 =================
SL = "perassic-sea"
docs.append(doc("doc.geography." + SL, "珀拉斯海", "Perassic Sea", "geography", "sea_routes",
    (["珀拉斯海", "帝国之湖"], ["Perassic Sea"]),
    "珀拉斯海：卡拉迪亚南方大海，经吕西亚海峡通西海。北岸今属西帝国与南帝国，南岸归阿塞莱；帝国史称其为「帝国之湖」，阿塞莱称「南方的怀抱」。",
    [
        A(SL, "1", "珀拉斯海（Perassic Sea）是卡拉迪亚南方广阔海域，连接阿塞莱、帝国与瓦兰迪亚的海岸；其水经吕西亚海峡与西海相通，俄耳堤西亚湾与俄里斯托科律斯一线是西大洋方向来的航路要冲。", "fact",
          [
              E(SL, "seaway-summary", "summary",
                "这是南边的大海。西大洋来的船穿过吕西亚那条窄海峡就进了珀拉斯海，再往东就是阿塞莱人的口岸。",
                [G("townsfolk", "regional", "summary")],
                [aref("lysia"), bref("per_V0")]),
              E(SL, "nowadays-detail", "detail",
                "鼎盛时这海叫「帝国之湖」，帝国舰队就是法律；如今帝国裂成三块，北岸归西帝国和南帝国，南岸的港口全归了阿塞莱——出海不请护卫，能不能回来全看命。",
                [G("merchant", "faction", "detail")],
                [bref("per_V12"), bref("per_V0")]),
          ]),
        A(SL, "2", "沿岸殖民史：俄耳堤西亚是卡拉德人在珀拉斯海的第一块殖民地，此前坎族城邦古亚兹（讲近于消亡的坎族语的商业共和国）曾主导西海与珀拉斯海的贸易，后被帝国霸权挤压、统治者被纳哈撒商人取代；贾尔马律斯则是帝国最早的内陆殖民地之一，用以控御西帕拉诸部落。", "fact",
          [
              E(SL, "colony-noble", "detail",
                "俄耳堤西亚是卡拉德人在这片海的第一块殖民地，早年还受坎族城邦古亚兹管——那是个讲坎族语的商人的共和国，如今坎族话几近没人说了，地盘也归了阿塞莱。贾尔马律斯更早，是帝国往内陆楔进去压西帕拉诸部落的钉子。",
                [G("noble", "elite", "detail")],
                [aref("ortysia"), aref("quyaz"), aref("jalmarys")]),
              E(SL, "lighthouse-rumor", "rumor",
                "老水手讲，从沙拉斯湾那片海盗窝出来的船，远远望见俄耳堤西亚的灯塔就念神——那光一亮，就知道进了帝国海巡队巡逻的水面。",
                [G("villager", "local", "rumor")],
                [aref("ortysia")]),
          ]),
        A(SL, "3", "沿岸风物：北岸俄尼卡的梯田葡萄园产干型葡萄酒；俄耳堤西亚湾山坡产橄榄；南岸与北岸多处辟盐田（加卜拉卜、阿利西翁、波利西亚、吕西亚）；夏季「泽翁娜之风」自南携纳哈撒的炎热越海北上。", "fact",
          [
              E(SL, "wine-detail", "detail",
                "北岸的梯田葡萄园出的干酒全帝国都认；湾里山坡的橄榄就着南边的日头长。做南岸生意的还得多懂一样——夏天那阵「泽翁娜之风」一起来，纳哈撒的热气就过海了。",
                [G("merchant", "faction", "detail")],
                [aref("onica"), aref("zeonica")]),
              E(SL, "south-rumor", "rumor",
                "阿塞莱人管这海叫「南方的怀抱」——如今南岸确实都归了他们。咱帝国老人还是叫它内湖，叫顺口了，改不了。",
                [G("villager", "local", "rumor", culture_ids=["aserai"])],
                [bref("per_V3")]),
          ]),
    ]))

# ================= 档3 德律亚山 =================
SL = "dryatic-mountains"
docs.append(doc("doc.geography." + SL, "德律亚山", "Dryatic Mountains", "geography", "terrain",
    (["德律亚山", "德律亚山脉"], ["Dryatic Mountains", "Mount Aracathos"]),
    "德律亚山脉：帝国北方高大山脉，最高峰阿拉卡托斯山；山间多盐、银、铁矿，山谷为帝国北方粮仓，东北部林农仍说帕拉语。",
    [
        A(SL, "1", "德律亚山脉（Dryatic Mountains）是帝国北方的高大山系，构成天然屏障；最高峰为阿拉卡托斯山，山脉西侧以弥戎河谷与库耳西翁峭壁相隔，东麓缓坡渐入帝国东境。", "fact",
          [
              E(SL, "range-summary", "summary",
                "北边那道大山就是德律亚山，最高叫阿拉卡托斯。山把路一挡，翻山就得走那几条谷口。",
                [G("townsfolk", "regional", "summary")],
                [aref("aracathos"), bref("dry_V0")]),
              E(SL, "barrier-detail", "detail",
                "山体泛红，老辈说山名里带着古时德律亚人的名字。军事上这道山脉是北方的天然屏障，两侧交通全靠有限的山口。",
                [G("soldier", "national", "detail")],
                [bref("dry_V0")]),
          ]),
        A(SL, "2", "山间矿藏丰厚：溪流冲出的盐矿（阿塔科尼亚）、数百年仍在出银的老矿井（耶拉科斯、弥戎河谷）、悬崖铁矿（马拉忒亚、瓦忒亚）；高地牧场养牛（雷索斯，自前卡拉德时代），东坡自古是帝国军马育种地（威亚罗斯）；山下水土肥沃，是帝国北方的「粮仓」（斯塔堤摩斯、底俄帕利斯）。", "fact",
          [
              E(SL, "mines-detail", "detail",
                "山里三样宝：盐、银、铁。阿塔科尼亚的盐是山水冲出来的，耶拉科斯的银井挖了几百年还有货，马拉忒亚的铁就出在悬崖上。",
                [G("merchant", "faction", "detail")],
                [aref("ataconia"), aref("jeracos"), aref("aracathos")]),
              E(SL, "breadbasket-summary", "summary",
                "山溪把沃土冲下山坡，谷里的麦子养活半个北帝国——人家管这叫帝国北方的粮仓。坡上还放牛放马，军马场的马种就出在这儿。",
                [G("townsfolk", "regional", "summary")],
                [aref("stathymos"), aref("vealos"), aref("rhesos")]),
          ]),
        A(SL, "3", "山中犹存帕拉遗风：东北部山嘴下的林农仍说帕拉语——帝国到来之前此地诸部落通行的语言；德律亚山南部的深湖「大地之眼」俄佛堤斯被传为古帕拉人的圣地，他们相信湖水是通往另一个世界的门户。", "fact",
          [
              E(SL, "palaic-rumor", "rumor",
                "山里有些村子，林农开口还是帕拉话——帝国来了这么多年，山里人舌头没换。「大地之眼」那片深湖，老话说是通另一个世界的门，是老辈帕拉人的圣地。",
                [G("villager", "local", "rumor")],
                [aref("hetania"), aref("enoisa")]),
              E(SL, "shrine-headman", "detail",
                "俄佛堤斯湖被称作「大地之眼」，古帕拉人奉为圣地。如今湖畔的小麦照种，可山里人对那片水仍存敬畏——旧信仰的影子比帝国老。",
                [G("headman", "national", "detail")],
                [aref("enoisa")]),
          ]),
    ]))

# ================= 档4 德夫赛格高原 =================
SL = "devseg-plateau"
docs.append(doc("doc.geography." + SL, "德夫赛格高原", "Devseg Plateau", "geography", "terrain",
    (["德夫赛格高原"], ["Devseg Plateau"]),
    "德夫赛格高原：卡拉迪亚与达西之间的干旱高地，库赛特及其盟友的主要牧场；东缘入大草海，奥通加德与奥多赫为库赛特授封的两大重镇。",
    [
        A(SL, "1", "德夫赛格高原（Devseg Plateau）地处卡拉迪亚与达西之间，终年干旱、春季宜牧；东缘渐入大草海，边缘山地有巴尔思山（以雪豹得名）与速仑山；斡祖河发源于高原南端。", "fact",
          [
              E(SL, "land-summary", "summary",
                "这片高地一年到头旱，就春天草好——种地不行，放牲口是块宝地。往东走就是大草海了。",
                [G("townsfolk", "regional", "summary")],
                [aref("ortongard"), aref("esme"), bref("dev_V0")]),
              E(SL, "edge-rumor", "rumor",
                "巴尔思山那名字就是雪豹的意思——牧马的人得上心。山里石头缝里全是这种猫大的豹子。",
                [G("villager", "local", "rumor")],
                [aref("asalig")]),
          ]),
        A(SL, "2", "高原是库赛特及其盟友的主要放牧地：东缘喀拉卡拉特等地以繁育良马闻名（游牧风习未远）；奥通加德由讲达西语的商人建城，向周边牧民村庄收购马匹牛羊、编队护卫西驱帝国市场售卖，兀儿浑汗破城后将其授予库吉特部；奥多赫在库赛特征服后归合儿必特部——该部与山民通婚、尊重当地习俗并守护其秘密。", "fact",
          [
              E(SL, "trade-detail", "detail",
                "奥通加德的买卖是达西语商人起的头：收周边的马、羊、牛，编成大队由护卫押着往西赶去帝国市场卖。城破之后换了东家——兀儿浑汗把它赏给了库吉特部。",
                [G("merchant", "faction", "detail")],
                [aref("ortongard"), aref("karakalat")]),
              E(SL, "odokh-headman", "detail",
                "奥多赫归了合儿必特部。这家人跟山民通婚、照他们的规矩办事、也替他们守着黎明山脉里那些不外传的东西——外人进不了山，也不全是山险的缘故。",
                [G("headman", "national", "detail")],
                [aref("odokh")]),
          ]),
        A(SL, "3", "高原边缘犹存旧日风习：喀拉卡拉特等地「不久前」尚在游牧民族支配之下，村民精于养马；奥多赫所倚的黎明山脉（柯希·罗希尼）有黑魔法与秘术之地的传言，外来者不得擅入——此说为当地秘守习俗的一部分。", "fact",
          [
              E(SL, "nomad-rumor", "rumor",
                "咱们的爷爷辈还住毡帐呢。如今村里孩儿都会侍弄马——高原上别的不敢说，马是祖传的本事。",
                [G("villager", "local", "rumor", culture_ids=["khuzait"])],
                [aref("karakalat")]),
              E(SL, "mountain-noble", "detail",
                "黎明山脉有黑魔法秘术的传言，外界当它是邪地。合儿必特家不肯多解释——他们参与山民的仪式，也守他们的秘密。传言本身，就是这道山墙的一部分。",
                [G("noble", "elite", "detail")],
                [aref("odokh")]),
          ]),
    ]))

# ================= 档5 弥戎河 =================
SL = "miron-river"
docs.append(doc("doc.geography." + SL, "弥戎河", "River Miron", "geography", "rivers",
    (["弥戎河"], ["River Miron"]),
    "弥戎河：北方大河，源出乌卡利翁高原，经伽尔喀斯大瀑布汇入拉科尼斯湖；巴旦尼亚人称「米尔」。巴旦尼亚、北帝国、斯特吉亚三族界河。",
    [
        A(SL, "1", "弥戎河（River Miron，巴旦尼亚语称「米尔」/Myr）源出乌卡利翁高原东部，穿行于库耳西翁峭壁与德律亚山脉之间，最终经伽尔喀斯大瀑布注入拉科尼斯湖；支流有伽尔喀斯河、加尔察河、瓦斯特拉河。", "fact",
          [
              E(SL, "course-summary", "summary",
                "弥戎河打西边高原下来，切在峭壁和大山中间，到伽尔喀斯瀑布一段飞流直下，最后灌进拉科尼斯湖。",
                [G("townsfolk", "regional", "summary")],
                [aref("rhemtoil"), aref("mecalovea")]),
              E(SL, "myr-rumor", "rumor",
                "巴旦尼亚人不叫它弥戎，叫「米尔」。河就是那条河，各叫各的名——反正水是从咱林子里流出去的。",
                [G("villager", "local", "rumor", culture_ids=["battania"])],
                [aref("rhemtoil")]),
          ]),
        A(SL, "2", "河谷物产：上游雷姆托伊尔一带是巴旦尼亚最肥沃的小麦地；弥戎河谷出白银（革耳塞戈斯山嘴矿井）与铁矿（马拉忒亚）；瀑布飞流冲出矿石（墨卡罗维亚）；瀑布水雾田种黑麦与大麦（乔诺巴斯）。", "fact",
          [
              E(SL, "ore-detail", "detail",
                "这条河养两头：上游麦子最肥，中下游石头里出银子出铁——革耳塞戈斯的银井、马拉忒亚的铁崖都在河谷里。瀑布底下冲出来的矿石也有人捡着吃饭。",
                [G("merchant", "faction", "detail")],
                [aref("rhemtoil"), aref("mecalovea")]),
              E(SL, "wheat-rumor", "rumor",
                "瀑布边上种麦子？能。水雾天天浇着，黑麦大麦长得比别处壮。就是守着河界，得留神对面过来的生人。",
                [G("villager", "local", "rumor")],
                [aref("chornobas")]),
          ]),
        A(SL, "3", "此河是巴旦尼亚、北帝国、斯特吉亚三方界河：汛期不可徒涉，结冰期约两个半月，向为北帝国西北防线的天堑；边村往来有之——阿加尔蒙村民与斯特吉亚人世代通婚，伊斯米尔科格的牧民与东西两边的帝国村、巴旦尼亚村互有劫掠，厄庇克洛忒亚的塔楼俯瞰弥戎河，常是北方人望见南方大军的第一眼。", "fact",
          [
              E(SL, "frontier-detail", "detail",
                "这河是三家的界：巴旦尼亚的林子、帝国的田、斯特吉亚人的北边。汛期马都蹚不过去，封冻两个月倒是能走——所以河边村子要么通婚，要么结仇，没中间道。",
                [G("headman", "national", "detail")],
                [bref("mir_V0"), aref("ismilkorg")]),
              E(SL, "watch-rumor", "rumor",
                "老人说厄庇克洛忒亚的塔是给北边人看的——北方人顺着河下来，头一眼望见的就是那座塔，和塔后头的南方大军。",
                [G("villager", "local", "rumor")],
                [aref("epicrotea")]),
          ]),
    ]))

# ================= 档6 纳哈撒沙漠 =================
SL = "nahasa-desert"
docs.append(doc("doc.geography." + SL, "纳哈撒沙漠", "The Nahasa", "geography", "terrain",
    (["纳哈撒沙漠", "青铜沙漠"], ["The Nahasa", "Bronze Desert"]),
    "纳哈撒沙漠（青铜沙漠）：卡拉迪亚以南的浩瀚沙海，阿塞莱人的家园；砾石平原与火山岩间散布绿洲，商路与水源即财富，帝国历次南征皆铩羽而归。",
    [
        A(SL, "1", "纳哈撒沙漠（the Nahasa，即「青铜沙漠」）是卡拉迪亚南方的浩瀚沙海：砾石平原与火山岩层间绵延沙丘，外人视作绝地；地下水源汇集于洼地与干涸河道之下，形成绿洲；达玛尔河自沙漠发源，穿杰尔贾赖峭壁的「枯焦之门」而出；沙尘暴恶名昭著，迷路者多渴毙沙丘。", "fact",
          [
              E(SL, "land-summary", "summary",
                "南边那片大沙漠叫青铜沙漠。沙子石头一望无边，可底下有水——水冒头的地方就是绿洲，人都聚在那儿住。",
                [G("townsfolk", "regional", "summary")],
                [aref("nahasa_cul")]),
              E(SL, "storm-rumor", "rumor",
                "沙漠里的沙暴是要命的。风一起，沙墙糊脸，几步外看不见人——迷了路的人，多半渴死在沙丘里，埋在沙丘里。",
                [G("villager", "local", "rumor")],
                [aref("sandstorm")]),
          ]),
        A(SL, "2", "阿塞莱人是纳哈撒的居民：游牧的贝都因人与绿洲农民的混合，各家族自溯血统于传奇族长，统称巴努·阿塞拉；绿洲种椰枣，家支皆有详尽族谱，以骑术与博识（尤以医学）闻名；贾瓦勒（「漫游者」）是沙漠中主要的贝都因联盟，划地为界向过路商队收保护费，以阿塞莱旧俗的守护者自居。", "fact",
          [
              E(SL, "clan-detail", "detail",
                "沙漠里的规矩都系在水上：谁攥着绿洲和井，谁就有说话的份。各家支的族谱背得比经文熟，对外都称巴努·阿塞拉——阿塞拉的子孙。贾瓦勒那些黑帐篷的还在沙漠里游荡，商队过境得给他们交钱。",
                [G("merchant", "faction", "detail")],
                [aref("nahasa_cul"), aref("jawwal")]),
              E(SL, "oasis-rumor", "rumor",
                "咱绿洲人家侍弄椰枣树，爹爹年轻时跟着埃米尔出征——部落里的男人都这样，种地是本分，打仗也是本分。",
                [G("villager", "local", "rumor", culture_ids=["aserai"])],
                [aref("aserai_cul")]),
          ]),
        A(SL, "3", "沙漠即屏障亦为财源：阿塞莱凭控制孤立水源而坐收商队之利，东方与西方的香料、丝绸、黄金在此流转；帝国军团数百年来屡次南征皆铩羽而归——外界视纳哈撒为帝国南方的天然屏障，阿塞莱人则称沙漠是公正的母亲与试炼场。", "fact",
          [
              E(SL, "imperial-noble", "detail",
                "帝国人管纳哈撒叫南方屏障，也当它是耻辱——军团进去多少回，回来的一回比一回少。太阳和风沙比阿塞莱人的刀更厉害。",
                [G("noble", "elite", "detail")],
                [bref("nah_V5")]),
              E(SL, "wind-detail", "detail",
                "夏季「泽翁娜之风」从沙漠过珀拉斯海直吹帝国南方——纳哈撒的热是能渡海的。懂行的商队首领看这风行事。",
                [G("headman", "national", "detail")],
                [aref("zeonica")]),
          ]),
    ]))

# ---------------- 落盘 ----------------
for d in docs:
    fname = d["id"].split(".")[-1] + ".yaml"
    body = ydump(d)
    io.open(os.path.join(AO, fname), "w", encoding="utf-8", newline="\n").write(body)
    io.open(os.path.join(WS, fname), "w", encoding="utf-8", newline="\n").write(body)
    print("written:", fname, "assertions:", len(d["assertions"]),
          "expressions:", sum(len(a["expressions"]) for a in d["assertions"]))
db.close()
print("OK", len(docs), "docs")
