# -*- coding: utf-8 -*-
"""地点批第一批：4 档成品（拆解重写工序）+ 源文件 + 来源登记。
档：sethys-river(转正) / mount-iltan / llyn-modris / mount-erithrys
"""
import io, os, json, hashlib, sqlite3, yaml

WS = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"
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

# ---------------- 引文登记 ----------------
A_QUOTES = {
    "sethys_morenia": ("Settlements.Settlement.text.castle_village_ES5_1", "摩雷尼亚俯瞰着塞堤斯河，位于通往吕卡里亚谷的低矮入口处。"),
    "sethys_chanopsis": ("Settlements.Settlement.text.castle_village_ES8_1", "卡诺普西斯依着密泽亚德高原上的塞堤斯河源头而建。"),
    "sethys_atphynia": ("Settlements.Settlement.text.village_ES2_1", "阿特费尼亚村靠近塞堤斯河口，再向下游河流随即分为三支，形成三河河谷。"),
    "sethys_salt": ("Settlements.Settlement.text.village_ES2_1", "在塞堤斯河流入珀拉斯海的河口附近，村民们开出了盐田。"),
    "sethys_gorcorys": ("Settlements.Settlement.text.village_ES2_2", "戈耳科律斯坐落于歌里亚河畔，那是塞堤斯河流经三河河谷最南端的支流。"),
    "sethys_clay": ("Settlements.Settlement.text.village_ES6_1", "山上的细土被水流冲刷下来形成黏土，这是能让帝国南方的陶工们两眼放光的东西。"),
    "iltan_tyal": ("F5HlQdgf", "蒂亚尔坐落于斯特吉亚东部边境的伊勒坦山西麓，该地区长期远离其他斯特吉亚公国的权力争斗。"),
    "iltan_gorcas": ("Settlements.Settlement.text.village_S4_2", "格拉夫斯特伦坐落于东卡拉迪亚的一座峻峭险峰——伊勒坦山脚下，这座山得名自草原民族的一位神灵。"),
    "iltan_pons": ("Settlements.Settlement.text.castle_village_EN7_2", "庞斯是帝国最北端的定居点之一。该地因位于伊勒坦运输线的“交叉口”而得名，一系列冰川湖构成的网络令船只得以通行于拉科尼斯湖与塔奈西斯湖之间。"),
    "iltan_uxkhal": ("Settlements.Settlement.text.castle_village_S7_1", "乌里克斯卡拉位于伊勒坦运输线上，那是一张由冰川湖构成的网络，拉科尼斯湖与塔奈西斯湖之间往来的长船可以在上面拖动与航行。"),
    "iltan_baltakhan": ("YNm9vaHw", "巴尔塔罕，即“斧头堡垒”，由居住在山地的库赛特表亲伊勒坦人建造。"),
    "iltan_bukits": ("Settlements.Settlement.text.village_S5_2", "布基茨坐落于库赛特边境上伊勒坦山的东坡。"),
    "iltan_vladiv": ("Settlements.Settlement.text.castle_village_S8_1", "弗拉基夫坐落于伊勒坦山脚下寒冷阴暗的林谷中。"),
    "iltan_alovi": ("Settlements.Settlement.text.village_S5_3", "村民们在俯瞰湖泊的高地上种植黑麦与大麦"),
    "modris_ocshall": ("nhmBQIC4", "这片山谷曾一度是一块巴旦尼亚人的飞地，以莫德里斯湖为中心——这座湖泊位于火山口，传说是一道巨人的足印。"),
    "modris_oca": ("nhmBQIC4", "这里后来被瓦兰迪亚的一位军阀奥克征服，附近的奥克斯湖也是以他的名字命名。该地的巴旦尼亚部落带着他们的传说一起被赶回到了内陆。"),
    "erithrys_rhotae": ("5SpYVtie", None),
    "erithrys_sarapios": ("5SpYVtie", None),
}
for k, (sid, q) in list(A_QUOTES.items()):
    if q is None:
        full = loc_cn(sid)
        sents = [s for s in full.replace("。", "。|").split("|") if s.strip()]
        n = 2 if k.endswith("rhotae") else 4
        A_QUOTES[k] = (sid, "".join(sents[:n]).strip())

B_QUOTES = {
    "sethys_V0": ("rule_塞堤斯河__塞堤斯河.json", 0, "塞堤斯河是吕卡隆的东墙。它从北边的丘陵里流出来，一路往南，经过吕卡里亚平原，最后灌进珀拉斯海。"),
    "sethys_V2": ("rule_塞堤斯河__塞堤斯河.json", 2, "吕卡隆被围过那么多次，从来没被饿垮过，就是因为这条河。"),
    "sethys_V3": ("rule_塞堤斯河__塞堤斯河.json", 3, "我们在北方的冰天雪地里练出来的围城本事，到了南方全用不上——因为那条该死的河从来不给帝国人断水。"),
    "sethys_V5": ("rule_塞堤斯河__塞堤斯河.json", 5, "我们阿塞莱的商船从撒纳拉出发，穿过珀拉斯海，进了塞堤斯河口就能一路漂到吕卡隆城下。"),
    "iltan_V2": ("rule_伊勒坦山__伊勒坦山.json", 2, "伊勒坦山坐落于卡拉迪亚大陆东北角，是库赛特与斯特基亚的界山，著名的伊勒坦运输线就是从这里发源"),
    "modris_V1": ("rule_奥克斯湖__奥克斯湖.json", 1, "这里后来被瓦兰迪亚的一位军阀奥克征服，奥克斯湖也是以他的名字命名。"),
    "erithrys_V0": ("rule_厄吕特律斯山__厄吕特律斯山.json", 0, "厄吕特律斯山是卡拉迪亚大陆最雄伟的山峰，坐落于阿米尼斯河谷的怀抱之中。整座山由深色的玄武巨岩构成，陡峭的岩壁从河谷中拔地而起，山顶终年积雪不化。"),
    "erithrys_V2": ("rule_厄吕特律斯山__厄吕特律斯山.json", 2, "谁控制了洛泰，谁就握住了整个阿米尼斯河谷的粮食命脉。"),
    "erithrys_V4": ("rule_厄吕特律斯山__厄吕特律斯山.json", 4, "它离我们不算近，但天气特别好的时候，站在高原的西南边缘，能看到它的雪顶在极远处闪着微光"),
    "erithrys_V10": ("rule_厄吕特律斯山__厄吕特律斯山.json", 10, "这座山给了我们水，也给了他们藏身的地方，各取所需。"),
}
for k, (sid, q) in A_QUOTES.items():
    full = loc_cn(sid)
    assert q in (full or ""), "A quote not in DB text: %s" % k
for k, (rf, vi, q) in B_QUOTES.items():
    c = rule_variant(rf, vi)
    assert q in c, "B quote not in rule: %s" % k

# ---------------- 源文件 ----------------
def H(x):
    return hashlib.sha256(x.encode("utf-8")).hexdigest().upper()

def hlower(x):
    return hashlib.sha256(x.encode("utf-8")).hexdigest()

game_txt = "\n\n".join("%s => %s" % (A_QUOTES[k][0], loc_cn(A_QUOTES[k][0])) for k in sorted(A_QUOTES)) + "\n"
chron_txt = "\n\n".join("%s [V%d] %s" % (B_QUOTES[k][0], B_QUOTES[k][1], B_QUOTES[k][2]) for k in sorted(B_QUOTES)) + "\n"

GAME_TXT = "game-settlements-geo2.txt"
CHRON_TXT = "chronicle-animusforge-geo2.txt"
io.open(os.path.join(WS, "sources", GAME_TXT), "w", encoding="utf-8", newline="\n").write(game_txt)
io.open(os.path.join(WS, "sources", CHRON_TXT), "w", encoding="utf-8", newline="\n").write(chron_txt)
game_hash = hlower(game_txt)
chron_hash = hlower(chron_txt)

GAME_SRC = dict(source_id="source.calradia.game.settlements.geo2", source_version="bannerlord-1.3.15.110062",
                source_nature="game_snapshot", universe="awake_current", era="current", locator_root=GAME_TXT,
                source_content_hash=game_hash, content_tier="base", license_status="permitted",
                use_status="active", valid_until=None, imported_at="2026-09-12T00:00:00Z",
                normalization_version="utf8-lf-no-bom-v1")
CHRON_SRC = dict(source_id="source.calradia.chronicle.animusforge.geo2", source_version="animusforge-20260912",
                 source_nature="chronicle", universe="awake_current", era="historical", locator_root=CHRON_TXT,
                 source_content_hash=chron_hash, content_tier="base", license_status="permitted",
                 use_status="active", valid_until=None, imported_at="2026-09-12T00:00:00Z",
                 normalization_version="utf8-lf-no-bom-v1")
class ND(yaml.SafeDumper):
    def ignore_aliases(self, data):
        return True

def ydump(obj):
    return yaml.dump(obj, Dumper=ND, allow_unicode=True, sort_keys=False, width=4096, default_flow_style=False)

for nm, src in [("source-game-settlements-geo2.yaml", GAME_SRC), ("source-chronicle-animusforge-geo2.yaml", CHRON_SRC)]:
    io.open(os.path.join(WS, "sources", nm), "w", encoding="utf-8", newline="\n").write(ydump(src))

# ---------------- 引用与结构构造 ----------------
RB = {"profile_registry_version": "1.0.0",
      "profile_registry_hash": "309D10583E7B4AF05F28DE2C2E67A2528CB3D6C45752369C0F7D160FABC3F3A5",
      "referral_registry_version": "1.0.0",
      "referral_registry_hash": "6E17075F1F967BE28C0437121DB50E5C30809C4EFC3D2A5F22AD9BB0B3B926CD"}

def aref(key):
    sid, q = A_QUOTES[key]
    return {"source_id": GAME_SRC["source_id"], "source_version": GAME_SRC["source_version"],
            "source_content_hash": game_hash,
            "locator": "bannerlord.db#localization." + sid,
            "quote_hash": H(q), "quote": q}

def bref(key):
    rf, vi, q = B_QUOTES[key]
    return {"source_id": CHRON_SRC["source_id"], "source_version": CHRON_SRC["source_version"],
            "source_content_hash": chron_hash,
            "locator": "rules/%s#/Variants/%d/Content" % (rf, vi),
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

# ================= 档1 塞堤斯河（转正修正） =================
SL = "sethys-river"
sethys = doc("doc.geography." + SL, "塞堤斯河", "Sethys", "geography", "rivers",
    (["塞堤斯河", "塞堤斯谷", "三河河谷", "塞堤斯"], ["Sethys", "Tripotamia"]),
    "塞堤斯河的走向与三河河谷；全年水路与南岸生计；东岸滩涂的攻防分量。两说如实分挂。",
    [
        A(SL, "1", "塞堤斯河发源于密泽亚德高原，向南穿过吕卡里亚谷与上游平原，至下游分作三支注入珀拉斯海，人称三河河谷；两岸谷地是帝国南方的粮仓。", "fact",
          [
              E(SL, "geo-rumor", "rumor",
                "这河打北边高原里流出来，一路往南入海。老辈人说，河口分成三股水岔子，一股水养活一片田。",
                [G("villager", "local", "rumor")],
                [aref("sethys_morenia"), aref("sethys_chanopsis"), bref("sethys_V0")]),
              E(SL, "geo-summary", "summary",
                "塞堤斯河是南方的水脉：源头出自密泽亚德高原，穿吕卡里亚谷南下，末了分作三支归入珀拉斯海——官府文书里管那片水口叫三河河谷。",
                [G("townsfolk", "regional", "summary"), G("headman", "national", "summary")],
                [aref("sethys_atphynia"), aref("sethys_gorcorys")]),
          ]),
        A(SL, "2", "河水全年可航，把上游平原的麦粮、河谷的陶土与河口的海盐连成一条水路；凭这条河，被围的城池不必忍饥挨饿。", "fact",
          [
              E(SL, "trade-merchant", "detail",
                "这条河一年到头行得了船。上游平原收的麦子装上平底驳船，顺水几日便到海口；河口晒盐、河谷烧陶，都是稳当的营生——做南方的买卖，绕不开这条河。",
                [G("merchant", "faction", "detail"), G("ransom_broker", "faction", "detail")],
                [aref("sethys_salt"), aref("sethys_clay"), bref("sethys_V2"), bref("sethys_V5")]),
              E(SL, "trade-summary", "summary",
                "吕卡隆的粮船一年四季不断——南方天暖，河面冬天也不封。",
                [G("townsfolk", "regional", "summary")],
                [bref("sethys_V3")]),
          ]),
        A(SL, "3", "河的东岸滩涂泥泞难行，重装人马难以立足；自东面进逼吕卡隆，须先渡河。", "fact",
          [
              E(SL, "war-soldier", "detail",
                "从东面打吕卡隆，先得渡塞堤斯。河不深，可两岸滩地吃马蹄，重甲陷进去拔不出——守军只管在岸上等。多少年来，东边的兵多半没望见城墙就折在河滩上了。",
                [G("soldier", "national", "detail")],
                [bref("sethys_V0")]),
              E(SL, "war-noble", "detail",
                "北地来的将领先是轻慢这条河——他们惯于等河封冻踏冰而过。可南方的河从不封冻，围城围到军粮见底，河上的粮船照样进出。在南方打仗，先得算水。",
                [G("noble", "elite", "detail")],
                [bref("sethys_V3")]),
          ]),
    ])

# ================= 档2 伊勒坦山 =================
SL = "mount-iltan"
iltan = doc("doc.geography." + SL, "伊勒坦山", "Mount Iltan", "geography", "terrain",
    (["伊勒坦山", "伊勒坦运输线", "伊勒坦人", "伊勒坦"], ["Iltan", "Portages of Iltan"]),
    "伊勒坦山的界山地位、得名与伊勒坦人；冰川湖运输线的通行与沿线生计。编年史两处坏数据已焚毁。",
    [
        A(SL, "1", "伊勒坦山耸立在大陆东北，是斯特吉亚林谷与库赛特草场之间的界山：西麓是斯特吉亚边境的寒冷林谷，东坡已属库赛特人的牧场。", "fact",
          [
              E(SL, "geo-rumor", "rumor",
                "山脚下冷得邪乎，一年里有半年是雪。可牛有牛的活法——刨开雪照样找着草吃，熬到开春就肥了。",
                [G("villager", "local", "rumor")],
                [aref("iltan_vladiv"), aref("iltan_bukits")]),
              E(SL, "geo-summary", "summary",
                "伊勒坦山在大陆东北角，是两边的界山：西边林谷归斯特吉亚，东坡草场已是库赛特人的地界。",
                [G("townsfolk", "regional", "summary"), G("soldier", "national", "summary")],
                [aref("iltan_tyal"), aref("iltan_bukits"), bref("iltan_V2")]),
          ]),
        A(SL, "2", "山名得自草原人敬的一位神灵；山里住着库赛特的表亲——伊勒坦人，数百年前他们的首领被流放到南边平原，筑起了斧头堡垒。", "fact",
          [
              E(SL, "name-rumor", "rumor",
                "这座山用的可是一位神的名字——草原上的人这么叫，咱们也跟着叫。",
                [G("villager", "local", "rumor")],
                [aref("iltan_gorcas")]),
              E(SL, "name-detail", "detail",
                "山里的伊勒坦人是库赛特的表亲，守着山地过了几百年。他们有一支被流放到南边平原，垒起那座斧头堡垒——巴尔塔罕，就是他们手笔。",
                [G("headman", "national", "detail"), G("soldier", "national", "detail")],
                [aref("iltan_baltakhan")]),
          ]),
        A(SL, "3", "高地上湖连着湖，人称伊勒坦运输线：拉科尼斯湖与塔奈西斯湖之间的长船靠人力拖过湖岸、在水中续行；沿岸村民放养耐寒的牛羊、种黑麦大麦过活。", "fact",
          [
              E(SL, "route-detail", "detail",
                "北边那条水路全仗着一片冰川湖——船从拉科尼斯湖进来，拖过岸，再下水接着走，一路通到塔奈西斯湖。沿线村子不愁生计：山坡养羊，湖畔种麦，牛刨雪找草也能熬冬。",
                [G("merchant", "faction", "detail"), G("tavernkeeper", "faction", "detail")],
                [aref("iltan_pons"), aref("iltan_uxkhal"), aref("iltan_alovi"), bref("iltan_V2")]),
          ]),
    ])

# ================= 档3 林·莫德里斯（奥克斯湖） =================
SL = "llyn-modris"
modris = doc("doc.geography." + SL, "林·莫德里斯", "Llyn Modris", "geography", "terrain",
    (["林·莫德里斯", "奥克斯湖", "莫德里斯湖"], ["Llyn Modris", "Ocspool"]),
    "火山口湖林·莫德里斯与巨人传说；瓦兰迪亚军阀奥克的征服与湖名更替。水灵女王献祭为编年史私设，已焚毁。",
    [
        A(SL, "1", "山谷深处有一座火山口湖，巴旦尼亚人叫它林·莫德里斯，相传是巨人在大地上留下的印迹；这片谷地曾是巴旦尼亚人的一块飞地。", "fact",
          [
              E(SL, "lake-battanian", "rumor",
                "老辈人说，那湖是巨人踩出来的脚印。巴旦尼亚人的地界原来到过那儿——后来，后来就没了。",
                [G("villager", "local", "rumor")],
                [aref("modris_ocshall")]),
              E(SL, "lake-summary", "summary",
                "林·莫德里斯是座火山口湖，巴旦尼亚人传说是巨人的足印；谷地原是他们的一块飞地。",
                [G("townsfolk", "regional", "summary"), G("headman", "national", "summary")],
                [aref("modris_ocshall")]),
          ]),
        A(SL, "2", "瓦兰迪亚军阀奥克夺下谷地，湖从此改随他的名字；巴旦尼亚部落被赶回内陆，把传说也一并带走了。", "fact",
          [
              E(SL, "conquest-vlandian", "detail",
                "奥克先祖当年打下这片谷地，湖都改跟他姓——奥克斯湖，奥克斯·霍尔，这名号就是家业的界碑。",
                [G("noble", "elite", "detail")],
                [aref("modris_oca"), bref("modris_V1")]),
              E(SL, "conquest-summary", "summary",
                "后来瓦兰迪亚的军阀奥克占了谷地，湖改叫了奥克斯湖；巴旦尼亚人退进内陆，传说也跟着散了。",
                [G("townsfolk", "regional", "summary")],
                [aref("modris_oca")]),
          ]),
    ])

# ================= 档4 厄里特律斯山 =================
SL = "mount-erithrys"
erithrys = doc("doc.geography." + SL, "厄里特律斯山", "Mount Erithrys", "geography", "terrain",
    (["厄里特律斯山", "厄吕特律斯山", "洛泰", "洛塔什"], ["Mount Erithrys", "Rhotae", "Rotash"]),
    "玄武巨岩之山的形胜；山影下的洛泰与萨拉庇俄斯焚城；山腰隐者的传闻。河名等编年史私造词已焚毁。",
    [
        A(SL, "1", "黑色的巨岩山体拔地而起，山顶积雪终年不化；平原上的人把它当作大陆最雄伟的山。", "fact",
          [
              E(SL, "mountain-summary", "summary",
                "黑石头山，雪顶一年到头不化——走过商路的人都说，那是大陆上最气派的一座山。",
                [G("townsfolk", "regional", "summary"), G("merchant", "faction", "summary")],
                [aref("erithrys_rhotae"), bref("erithrys_V0")]),
          ]),
        A(SL, "2", "山影下坐落着洛泰——旧名洛塔什的帕拉人山堡，帝国夺之；山上雪水滋养谷地，四里八乡的麦子由此装船南运。", "fact",
          [
              E(SL, "rhotae-detail", "detail",
                "洛泰挨着山根，打帕拉人的山堡过来的，旧名洛塔什——帝国当年为了它动过刀兵，萨拉庇俄斯一把火把旧城烧了个干净。如今谷里的麦子打这儿上船，往南一船一船地走。",
                [G("merchant", "faction", "detail"), G("noble", "elite", "detail")],
                [aref("erithrys_rhotae"), aref("erithrys_sarapios"), bref("erithrys_V2")]),
              E(SL, "rhotae-battanian", "rumor",
                "天好的时候，站在高原西南边上，能瞅见远处雪顶闪白光。老辈人有的管它叫黑壁，有的叫白冠——帝国人的粮城在山根下，与我们无干。",
                [G("villager", "local", "rumor", culture_ids=["battania"]), G("townsfolk", "regional", "rumor", culture_ids=["battania"])],
                [bref("erithrys_V4")]),
          ]),
        A(SL, "3", "山腰洞窟隐者之说流传于洛泰与各条商路：一群从不下山的人，来历无人说得清；有人敬他们，有人只当山里多了几个影子。", "interpretation",
          [
              E(SL, "hermit-townsfolk", "rumor",
                "上山采药的远远见过洞口有人影。他们不下山，我们不上山——这山给了我们水，也给了他们落脚的地方，各过各的。",
                [G("townsfolk", "regional", "rumor")],
                [bref("erithrys_V10")]),
              E(SL, "hermit-soldier", "summary",
                "驻在洛泰那阵子，伙食是各处驻地里最好的。新兵问山上那些人闷不闷——山下打成那样，山上闷一点有什么不好。",
                [G("soldier", "national", "summary")],
                [bref("erithrys_V10")]),
          ]),
    ])

# ---------------- 落盘 ----------------
AO = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out"
for d in [sethys, iltan, modris, erithrys]:
    fname = d["id"].split(".")[-1] + ".yaml"
    p = os.path.join(AO, fname)
    io.open(p, "w", encoding="utf-8", newline="\n").write(
        ydump(d))
    print("written:", fname, "assertions:", len(d["assertions"]),
          "expressions:", sum(len(a["expressions"]) for a in d["assertions"]))
db.close()
print("OK")
