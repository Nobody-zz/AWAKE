# -*- coding: utf-8 -*-
"""10 新档生成器（IMPL-GEO1-PERMISSION §2/§3，2026-09-12）
A 源=官方聚落描述文（§2.3 母本）；B 源=编年史变体（直接读 rule json 全文）。
quote_hash=SHA256(quote UTF-8) 大写；A quote 断言为母本子串。
同时产出全量编译工作区所需的新来源登记+源文件。
"""
import hashlib, json, glob, io, os, yaml

AO = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out"
WS = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"
RULES = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/AnimusForge/PlayerExports/卡拉迪亚编年史/knowledge/rules"
os.makedirs(AO, exist_ok=True)
os.makedirs(os.path.join(WS, "sources"), exist_ok=True)

def H(s):
    return hashlib.sha256(s.encode("utf-8")).hexdigest().upper()

GAME_SID, GAME_VER = "source.calradia.game.settlements", "bannerlord-1.3.15.110062"
CHRON_SID, CHRON_VER = "source.calradia.chronicle.animusforge", "animusforge-20260912"
REG_BIND = {
    "profile_registry_version": "1.0.0",
    "profile_registry_hash": "309D10583E7B4AF05F28DE2C2E67A2528CB3D6C45752369C0F7D160FABC3F3A5",
    "referral_registry_version": "1.0.0",
    "referral_registry_hash": "6E17075F1F967BE28C0437121DB50E5C30809C4EFC3D2A5F22AD9BB0B3B926CD",
}

# ---- §2.3 官方中文母本（逐字） ----
MOTHER = {
"town_V7": "这座如今被称为沙拉斯的城市，其历史最早可以追溯到第一批卡拉德殖民者登上这片大陆的海岸之时。在帝国如日中天的时候，其首都迁到了北方的巴拉维诺斯，而沙拉斯则成为了一座海务和贸易的重要港口，帝国贵族们在周边海湾附近的和煦海滩上纷纷建起自己的纳凉别墅，时不时地划着小艇在小岛的周边游弋。瓦兰迪亚人到来后，沙拉斯落入了无情的戴·科尔坦家族手中，其无穷的财富助长了后者的野心。",
"town_S1": "曾经是诺德人殖民地的瓦尔切格位于风雨交加的海崖上，此地正是昔日巴旦尼亚领土的卡恰尔半岛深处。建立这个沿海前哨的雅尔希望与内陆进行贸易，于是向巴旦尼亚至高王戴恩玛查宣誓效忠，但随后却开始涉足巴旦尼亚政治。恼怒的戴恩玛查雇佣了斯特吉亚军队来击败他不可靠的新封臣。斯特吉亚人奉命击溃了诺德人，但随后要求把新夺得的城镇作为他们的报酬。他们将这个城镇称为“瓦尔切格”，这是诺德词“誓言之同伴”的变体，以嘲讽两个因争斗而将战利品拱手让给他们的敌人。",
"town_A2": "侯森·富勒格位于卡尔得亚的东南边缘，卡尔得亚是一片由河流和咸水潟湖构成的网络，它将帝国和阿塞莱的土地相连。富勒格，又名法耳科斯，是一位来自于极南之地的黑面孔雇佣兵酋长。他在此地建立了一座堡垒，又利用堡垒的战略位置发了财，从帝国和阿塞莱两边收取贿赂，并随时准备着选边站队。在玩了将近二十年的换队游戏之后，他驾着一艘满载黄金的船驶回了家，但许多阿塞莱的氏族仍然骄傲地自称是他的后裔。“富勒格的鬼点子”也成为了许多集市说书人口中经久不衰的故事桥段。",
"town_ES4": "吕卡隆这座天然要塞位于巍峨的奥尼石山向外突出的一处缓坡之上。在帝国征服的最后几年，帕拉部落同帝国协商，进行了和平的合邦，结果他们的家园又被叛军首领革图夺走。接踵而至的围城战仅仅是吕卡隆在接下来的几个世纪中屡遭围攻的第一次，因为该城周边有着丰富的银矿脉，任何想在内战中攫取权力的皇位觊觎者都会盯上这里。甚至有人传言，居住在石山顶上的秃鹫已经享用过卡拉迪亚大陆上的每一种民族。",
}

# ---- B 变体全文（读 rule json） ----
def rule_variants(name):
    fs = glob.glob(os.path.join(RULES, "rule_*%s*__.json")) if False else glob.glob(os.path.join(RULES, "rule_*%s__%s.json" % (name, name)))
    assert len(fs) == 1, name
    d = json.load(open(fs[0], encoding="utf-8-sig"))
    return [v["Content"] for v in d["Variants"]], os.path.basename(fs[0])

VAR = {}
CHRON_FILES = {}
for name in ["沙拉斯", "卡恰尔半岛", "吕卡隆"]:
    vs, fn = rule_variants(name)
    VAR[name] = vs
    CHRON_FILES[name] = fn

# ---- grant 模板（IMPL §3.2） ----
def g(pid, sc, md, **dims):
    d = {"profile_id": "profile." + pid, "scope": sc, "min_detail": md}
    d.update(dims)
    return d

T1 = lambda: [g("commoner","local","rumor"), g("villager","local","rumor"), g("tavernkeeper","faction","rumor"),
              g("ransom_broker","faction","rumor"), g("townsfolk","regional","rumor"), g("notable","regional","rumor"),
              g("merchant","faction","rumor"), g("headman","national","rumor"), g("soldier","national","rumor"),
              g("noble","elite","rumor")]
T2 = lambda: [g("townsfolk","regional","summary"), g("notable","regional","summary"), g("merchant","faction","summary"),
              g("headman","national","summary"), g("soldier","national","summary"), g("noble","elite","summary")]
T4 = lambda: [g("notable","regional","detail"), g("merchant","faction","detail"),
              g("headman","national","detail"), g("soldier","national","detail")]
T5 = lambda: [g("noble","elite","secret")]
PUB = lambda: T2() + [g("tavernkeeper","faction","summary")]
TALES = lambda: [g("tavernkeeper","faction","rumor"), g("villager","local","rumor")]
DENY_ANON = [{"profile_id": "profile.anonymous", "scope": "local", "min_detail": "rumor"}]
DENY_TAV = [{"profile_id": "profile.tavernkeeper", "scope": "local", "min_detail": "rumor"}]

# ---- 来源 ref 构造 ----
GAME_FILE = "game-settlements-geo1.txt"
CHRON_FILE = "chronicle-animusforge-geo1.txt"
game_file_hash = H("\n".join(MOTHER[k] for k in ["town_V7","town_S1","town_A2","town_ES4"]) + "\n")

def aref(city, quote):
    return {"source_id": GAME_SID, "source_version": GAME_VER, "source_content_hash": game_file_hash,
            "locator": "bannerlord.db#settlements.%s.descriptionText" % city,
            "quote_hash": H(quote), "quote": quote}

def bref(name, idx):
    text = VAR[name][idx]
    return {"source_id": CHRON_SID, "source_version": CHRON_VER, "source_content_hash": None,  # 占位，后面统一填
            "locator": "rules/%s#/Variants/%d/Content" % (CHRON_FILES[name], idx),
            "quote_hash": H(text), "quote": text}

# ---- 文档定义 ----
DOCS = []

def doc(id_, title, domain, subdomain, entity_ids, aliases, summary, assertions):
    DOCS.append({"id": id_, "title": title, "domain": domain, "subdomain": subdomain,
                 "entity_ids": entity_ids, "aliases": aliases, "summary": summary, "assertions": assertions})

def A(city, quote):  # A 源 ref（校验子串）
    assert quote in MOTHER[city], "quote 不在母本内: " + quote[:30]
    return aref(city, quote)

def expr(eid, layer, text, refs, grants, denies=None):
    return {"id": eid, "revision": 1, "layer": layer,
            "text": {"zh-CN": text}, "sources": refs, "grants": grants, "denies": denies or []}

def asrt(aid, kind, text, refs, expressions):
    return {"id": aid, "revision": 1, "kind": kind, "text": {"zh-CN": text},
            "sources": refs, "expressions": expressions}

# ============ 1. charas-town ============
q11 = "其历史最早可以追溯到第一批卡拉德殖民者登上这片大陆的海岸之时"
q12 = "沙拉斯则成为了一座海务和贸易的重要港口，帝国贵族们在周边海湾附近的和煦海滩上纷纷建起自己的纳凉别墅"
q13 = "时不时地划着小艇在小岛的周边游弋"
b11 = bref("沙拉斯", 4)   # V4 帝国平民
b12 = bref("沙拉斯", 2)   # V2 瓦兰迪亚平民
t11 = "沙拉斯的城史最早被追到第一批卡拉德殖民者登岸之时，是这片大陆上最老的城之一。"
t12 = "沙拉斯是海务与贸易的重要港口，帝国时代的贵族环湾建着纳凉别墅。"
t13 = "湾水暖、海滩缓，闲暇的贵族驾着小艇在群岛间游弋。"
doc("doc.geography.charas-town", "沙拉斯·城与港", "geography", "settlements",
    ["entity.settlement.town_v7"],
    {"zh-CN": ["沙拉斯", "Charas"], "en": ["Charas"]},
    "沙拉斯的位置、起源与海港生计；帝国与瓦兰迪亚两说并录。",
    [
     asrt("assertion.charas-town-1", "fact", t11, [A("town_V7", q11), b11], [
        expr("expr.charas-town-1-rumor", "rumor", t11, [A("town_V7", q11)], T1()),
        expr("expr.charas-town-1-summary", "summary", t11, [A("town_V7", q11)], T2()),
        expr("expr.charas-town-1-b-empire", "rumor",
             VAR["沙拉斯"][4], [b11],
             [g("commoner","local","rumor", culture_ids=["entity.culture.empire"])]),
     ]),
     asrt("assertion.charas-town-2", "fact", t12, [A("town_V7", q12), b12], [
        expr("expr.charas-town-2-rumor", "rumor", t12, [A("town_V7", q12)], T1()),
        expr("expr.charas-town-2-summary", "summary", t12, [A("town_V7", q12)], T2()),
        expr("expr.charas-town-2-b-vlandia", "rumor",
             VAR["沙拉斯"][2], [b12],
             [g("commoner","local","rumor", culture_ids=["entity.culture.vlandia"])]),
     ]),
     asrt("assertion.charas-town-3", "relation", t13, [A("town_V7", q13)], [
        expr("expr.charas-town-3-rumor", "rumor", t13, [A("town_V7", q13)], T1()),
        expr("expr.charas-town-3-local", "rumor", t13, [A("town_V7", q13)],
             [g("commoner","local","rumor", settlement_ids=["entity.settlement.town_v7"])]),
     ]),
    ])

# ============ 2. charas-reign ============
q21 = "瓦兰迪亚人到来后，沙拉斯落入了无情的戴·科尔坦家族手中"
q22 = "其无穷的财富助长了后者的野心"
b21 = bref("沙拉斯", 1)   # V1 瓦兰迪亚贵族(lord)
t21 = "瓦兰迪亚人到来之后，沙拉斯归了戴·科尔坦家族。"
t22 = "沙拉斯的财富成了科尔坦家势力的底气。"
doc("doc.politics.charas-reign", "沙拉斯·归属与科尔坦家", "politics", "territories",
    ["entity.settlement.town_v7"],
    {"zh-CN": ["沙拉斯", "戴·科尔坦", "科尔坦家"], "en": ["Charas", "dey Cortain"]},
    "沙拉斯的归属沿革与科尔坦家财富的事实面。",
    [
     asrt("assertion.charas-reign-1", "fact", t21, [A("town_V7", q21)], [
        expr("expr.charas-reign-1-summary", "summary", t21, [A("town_V7", q21)], T2()),
     ]),
     asrt("assertion.charas-reign-2", "fact", t22, [A("town_V7", q22), b21], [
        expr("expr.charas-reign-2-detail", "detail", t22, [A("town_V7", q22)], T4()),
        expr("expr.charas-reign-2-b-vlandia", "detail",
             VAR["沙拉斯"][1], [b21],
             [g("noble","elite","detail", culture_ids=["entity.culture.vlandia"])]),
     ]),
    ])

# ============ 3. charas-cortain-secret ============
b30 = bref("沙拉斯", 0)   # V0 因加泰尔第一人称
t30 = "沙拉斯的海务财富尽归戴·科尔坦家族。"
t31 = "这些财富正被拿来操办科尔坦家的图谋：港税、渔市、地租，尽入其囊中以养其势。"
doc("doc.politics.charas-cortain-secret", "沙拉斯·科尔坦家的账", "politics", "clans",
    ["entity.settlement.town_v7"],
    {"zh-CN": ["科尔坦家的账", "戴·科尔坦"], "en": ["dey Cortain"]},
    "科尔坦家财富的公开面（所有权事实）与秘密面（政治解读），分层分档。",
    [
     asrt("assertion.cortain-secret-1", "fact", t30, [A("town_V7", q21), A("town_V7", q22), b30], [
        expr("expr.cortain-secret-1-public", "summary", t30, [A("town_V7", q21)], PUB(), DENY_TAV),
        expr("expr.cortain-secret-1-secret", "secret", t31, [A("town_V7", q22)], T5(), DENY_ANON),
        expr("expr.cortain-secret-1-b-ingal", "secret", VAR["沙拉斯"][0], [b30], T5(), DENY_ANON),
     ]),
    ])

# ============ 4. varcheg-town ============
q41 = "瓦尔切格位于风雨交加的海崖上，此地正是昔日巴旦尼亚领土的卡恰尔半岛深处"
q42 = "建立这个沿海前哨的雅尔希望与内陆进行贸易"
b41 = bref("卡恰尔半岛", 1)   # V1 斯特吉亚
t41 = "瓦尔切格坐落在风雨交加的海崖上，深处昔日巴旦尼亚领土的卡恰尔半岛。"
t42 = "瓦尔切格本是诺德人的沿海前哨，建它是为了同内陆做买卖。"
doc("doc.geography.varcheg-town", "瓦尔切格·海崖与港", "geography", "settlements",
    ["entity.settlement.town_s1"],
    {"zh-CN": ["瓦尔切格", "卡恰尔半岛"], "en": ["Varcheg", "Kachyar Peninsula"]},
    "瓦尔切格的海崖地貌与港的生计由来。",
    [
     asrt("assertion.varcheg-town-1", "fact", t41, [A("town_S1", q41), b41], [
        expr("expr.varcheg-town-1-rumor", "rumor", t41, [A("town_S1", q41)], T1()),
        expr("expr.varcheg-town-1-summary", "summary", t41, [A("town_S1", q41)], T2()),
        expr("expr.varcheg-town-1-b-sturgia", "rumor",
             VAR["卡恰尔半岛"][1], [b41],
             [g("villager","local","rumor", culture_ids=["entity.culture.sturgia"], kingdom_ids=["entity.kingdom.sturgia"])]),
     ]),
     asrt("assertion.varcheg-town-2", "fact", t42, [A("town_S1", q42)], [
        expr("expr.varcheg-town-2-rumor", "rumor", t42, [A("town_S1", q42)], T1()),
        expr("expr.varcheg-town-2-summary", "summary", t42, [A("town_S1", q42)], T2()),
     ]),
    ])

# ============ 5. varcheg-swap ============
q51 = "于是向巴旦尼亚至高王戴恩玛查宣誓效忠，但随后却开始涉足巴旦尼亚政治。恼怒的戴恩玛查雇佣了斯特吉亚军队来击败他不可靠的新封臣"
q52 = "斯特吉亚人奉命击溃了诺德人，但随后要求把新夺得的城镇作为他们的报酬"
q53 = "他们将这个城镇称为“瓦尔切格”，这是诺德词“誓言之同伴”的变体，以嘲讽两个因争斗而将战利品拱手让给他们的敌人"
b51 = bref("卡恰尔半岛", 5)   # V5 巴旦尼亚
t51 = "诺德雅尔向巴旦尼亚至高王戴恩玛查宣誓效忠后又生异心，戴恩玛查雇斯特吉亚人击败了他。"
t52 = "斯特吉亚人击溃诺德人后索要城镇作酬劳，瓦尔切格由此易手。"
t53 = "“瓦尔切格”是诺德词“誓言之同伴”的变体——斯特吉亚人起这名嘲讽两个把战利品拱手让人的人。"
doc("doc.politics.varcheg-swap", "瓦尔切格·三易其手与镇名", "politics", "territories",
    ["entity.settlement.town_s1"],
    {"zh-CN": ["瓦尔切格", "戴恩玛查", "誓言之同伴"], "en": ["Varcheg", "Dernmachad"]},
    "瓦尔切格效忠—雇佣—击溃—夺城的易手链与镇名由来（斯特吉亚说法与巴旦尼亚说法并录）。",
    [
     asrt("assertion.varcheg-swap-1", "fact", t51, [A("town_S1", q51), b51], [
        expr("expr.varcheg-swap-1-detail", "detail", t51, [A("town_S1", q51)], T4()),
        expr("expr.varcheg-swap-1-b-battania", "rumor",
             VAR["卡恰尔半岛"][5], [b51],
             [g("villager","local","rumor", culture_ids=["entity.culture.battania"])]),
     ]),
     asrt("assertion.varcheg-swap-2", "fact", t52, [A("town_S1", q52)], [
        expr("expr.varcheg-swap-2-detail", "detail", t52, [A("town_S1", q52)], T4()),
     ]),
     asrt("assertion.varcheg-swap-3", "rumor", t53, [A("town_S1", q53)], [
        expr("expr.varcheg-swap-3-t3", "rumor", t53, [A("town_S1", q53)],
             [g("villager","local","rumor", culture_ids=["entity.culture.sturgia"], kingdom_ids=["entity.kingdom.sturgia"])]),
     ]),
    ])

# ============ 6. husn-fulq-town ============
q61 = "侯森·富勒格位于卡尔得亚的东南边缘，卡尔得亚是一片由河流和咸水潟湖构成的网络，它将帝国和阿塞莱的土地相连"
q62 = "他在此地建立了一座堡垒"
q63 = "从帝国和阿塞莱两边收取贿赂，并随时准备着选边站队"
t61 = "侯森·富勒格在卡尔得亚的东南边缘，那片河与咸水潟湖连成的水网把帝国和阿塞莱的土地相连。"
t62 = "雇佣兵酋长富勒格在此地筑起一座堡垒，城因他得名。"
t63 = "堡垒卡在两大势力之间，富勒格靠两边收贿赂、随时选边站队发财。"
doc("doc.geography.husn-fulq-town", "侯森·富勒格·卡尔得亚边上的城", "geography", "settlements",
    ["entity.settlement.town_a2"],
    {"zh-CN": ["侯森·富勒格", "卡尔得亚"], "en": ["Husn Fulq", "Caldea"]},
    "侯森·富勒格的区位、堡垒起源与两头取财的生计逻辑。",
    [
     asrt("assertion.husn-fulq-town-1", "fact", t61, [A("town_A2", q61)], [
        expr("expr.husn-fulq-town-1-rumor", "rumor", t61, [A("town_A2", q61)], T1()),
        expr("expr.husn-fulq-town-1-summary", "summary", t61, [A("town_A2", q61)], T2()),
     ]),
     asrt("assertion.husn-fulq-town-2", "fact", t62, [A("town_A2", q62)], [
        expr("expr.husn-fulq-town-2-summary", "summary", t62, [A("town_A2", q62)], T2()),
     ]),
     asrt("assertion.husn-fulq-town-3", "fact", t63, [A("town_A2", q63)], [
        expr("expr.husn-fulq-town-3-detail", "detail", t63, [A("town_A2", q63)], T4()),
     ]),
    ])

# ============ 7. husn-fulq-tales ============
q71 = "富勒格，又名法耳科斯，是一位来自于极南之地的黑面孔雇佣兵酋长"
q72 = "在玩了将近二十年的换队游戏之后，他驾着一艘满载黄金的船驶回了家，但许多阿塞莱的氏族仍然骄傲地自称是他的后裔"
q73 = "“富勒格的鬼点子”也成为了许多集市说书人口中经久不衰的故事桥段"
t71 = "富勒格又名法耳科斯，是极南之地来的黑面孔雇佣兵酋长，玩了近二十年换队游戏后载着满船黄金回了家，不少阿塞莱氏族仍自称是他的后裔。"
t72 = "“富勒格的鬼点子”是集市说书人口中经久不衰的桥段。"
doc("doc.culture.husn-fulq-tales", "富勒格其人与“鬼点子”", "culture", "arts",
    [],
    {"zh-CN": ["富勒格", "法耳科斯", "富勒格的鬼点子"], "en": ["Fulq", "Falcos"]},
    "富勒格其人其事与集市说书的“鬼点子”桥段。",
    [
     asrt("assertion.husn-fulq-tales-1", "fact", t71, [A("town_A2", q71), A("town_A2", q72)], [
        expr("expr.husn-fulq-tales-1-summary", "summary", t71, [A("town_A2", q72)], T2()),
     ]),
     asrt("assertion.husn-fulq-tales-2", "rumor", t72, [A("town_A2", q73)], [
        expr("expr.husn-fulq-tales-2-rumor", "rumor", t72, [A("town_A2", q73)], TALES()),
     ]),
    ])

# ============ 8. lycaron-town ============
q81 = "吕卡隆这座天然要塞位于巍峨的奥尼石山向外突出的一处缓坡之上"
q82 = "接踵而至的围城战仅仅是吕卡隆在接下来的几个世纪中屡遭围攻的第一次"
b81 = bref("吕卡隆", 5)   # V5 老兵(soldier)
b82 = bref("吕卡隆", 2)   # V2 南帝国
t81 = "吕卡隆是奥尼石山缓坡上的一座天然要塞。"
t82 = "吕卡隆屡遭围攻，围城战是这座城的常事。"
doc("doc.geography.lycaron-town", "吕卡隆·石山要塞", "geography", "settlements",
    ["entity.settlement.town_es4"],
    {"zh-CN": ["吕卡隆", "奥尼石山"], "en": ["Lycaron", "Ornian rock"]},
    "吕卡隆的地貌与屡遭围攻的现状；本地老兵与南帝国说法并录。",
    [
     asrt("assertion.lycaron-town-1", "fact", t81, [A("town_ES4", q81), b81], [
        expr("expr.lycaron-town-1-rumor", "rumor", t81, [A("town_ES4", q81)], T1()),
        expr("expr.lycaron-town-1-summary", "summary", t81, [A("town_ES4", q81)], T2()),
        expr("expr.lycaron-town-1-b-soldier", "detail",
             VAR["吕卡隆"][5], [b81],
             [g("soldier","national","detail", settlement_ids=["entity.settlement.town_es4"])]),
     ]),
     asrt("assertion.lycaron-town-2", "fact", t82, [A("town_ES4", q82), b82], [
        expr("expr.lycaron-town-2-summary", "summary", t82, [A("town_ES4", q82)], T2()),
        expr("expr.lycaron-town-2-b-empire-s", "summary",
             VAR["吕卡隆"][2], [b82],
             [g("townsfolk","regional","summary", kingdom_ids=["entity.kingdom.empire_s"])]),
     ]),
    ])

# ============ 9. lycaron-mines ============
q91 = "在帝国征服的最后几年，帕拉部落同帝国协商，进行了和平的合邦，结果他们的家园又被叛军首领革图夺走"
q92 = "因为该城周边有着丰富的银矿脉，任何想在内战中攫取权力的皇位觊觎者都会盯上这里"
t91 = "帝国征服末年帕拉部落与帝国和平合邦，家园随后被叛军首领革图夺走——这是吕卡隆沿革的一幕。"
t92 = "吕卡隆周边银矿丰富，内战中任何觊觎皇位的人都盯着这里——矿与兵祸互为因果。"
doc("doc.economy.lycaron-mines", "吕卡隆·银矿", "economy", "land_production",
    ["entity.settlement.town_es4"],
    {"zh-CN": ["吕卡隆", "银矿", "帕拉部落", "革图"], "en": ["Lycaron", "silver mines"]},
    "吕卡隆银矿与兵祸的由来（沿革断言系银矿因果链的一环，承 IMPL §3.1 留痕：economy 档承载一条政治沿革）。",
    [
     asrt("assertion.lycaron-mines-1", "fact", t91, [A("town_ES4", q91)], [
        expr("expr.lycaron-mines-1-summary", "summary", t91, [A("town_ES4", q91)], T2()),
     ]),
     asrt("assertion.lycaron-mines-2", "fact", t92, [A("town_ES4", q92)], [
        expr("expr.lycaron-mines-2-detail", "detail", t92, [A("town_ES4", q92)], T4()),
     ]),
    ])

# ============ 10. lycaron-rock-tales ============
qA10 = "甚至有人传言，居住在石山顶上的秃鹫已经享用过卡拉迪亚大陆上的每一种民族"
b10 = bref("吕卡隆", 6)   # V6 斯特吉亚
t10 = "有人传言，石山顶上的秃鹫已经品尝过卡拉迪亚每一种民族。"
doc("doc.culture.lycaron-rock-tales", "吕卡隆·秃鹫怪谈", "culture", "customs",
    [],
    {"zh-CN": ["秃鹫怪谈", "奥尼石山"], "en": ["Lycaron vultures"]},
    "石山顶上秃鹫的传言：本地说法与异邦版本并录，只陈述“传言”本身。",
    [
     asrt("assertion.lycaron-rock-tales-1", "rumor", t10, [A("town_ES4", qA10), b10], [
        expr("expr.lycaron-rock-tales-1-rumor", "rumor", t10, [A("town_ES4", qA10)], TALES()),
        expr("expr.lycaron-rock-tales-1-b-sturgia", "rumor",
             VAR["吕卡隆"][6], [b10],
             [g("commoner","local","rumor", culture_ids=["entity.culture.sturgia"])]),
     ]),
    ])

# ---- 统一填 B source_content_hash 并落 YAML ----
chron_texts = []
for name in ["沙拉斯", "卡恰尔半岛", "吕卡隆"]:
    chron_texts.extend(VAR[name])
chron_file_hash = H("\n".join(chron_texts) + "\n")

def fix_bref(ref):
    if ref["source_id"] == CHRON_SID:
        ref["source_content_hash"] = chron_file_hash
    return ref

def all_refs(d):
    out = list(d["sources"]) if d.get("sources") else []
    for a in d["assertions"]:
        out += a["sources"]
        for e in a["expressions"]:
            out += e["sources"]
    return out

written = []
for d in DOCS:
    y = {
        "schema_version": "awake.worldbook.authoring.v1",
        "revision": 1,
        "id": d["id"],
        "title": {"zh-CN": d["title"]},
        "status": "needs_review",
        "domain": d["domain"],
        "subdomain": d["subdomain"],
        "universe": "awake_current",
        "era": {"key": "current", "certainty": "bounded"},
        "content_tier": "base",
        "aliases": d["aliases"],
    }
    if d["entity_ids"]:
        y["entity_ids"] = d["entity_ids"]
    y["summary"] = {"zh-CN": d["summary"]}
    y["registry_bindings"] = dict(REG_BIND)
    y["authority"] = {"owner": "awake_canon", "conflict_policy": "canon_wins"}
    # doc-level sources = 各断言 sources 去重（按 quote_hash）
    seen, doc_sources = set(), []
    for a in d["assertions"]:
        for r in a["sources"]:
            fix_bref(r)
            if r["quote_hash"] not in seen:
                seen.add(r["quote_hash"]); doc_sources.append(r)
    y["sources"] = doc_sources
    y["assertions"] = d["assertions"]
    # 表达里也统一填 hash
    for a in d["assertions"]:
        for r in a["sources"]: fix_bref(r)
        for e in a["expressions"]:
            for r in e["sources"]: fix_bref(r)
    fname = d["id"].split(".")[-1] + ".yaml"
    class NoAliasDumper(yaml.SafeDumper):
        def ignore_aliases(self, data):
            return True
    with io.open(os.path.join(AO, fname), "w", encoding="utf-8", newline="\n") as f:
        f.write(yaml.dump(y, Dumper=NoAliasDumper, allow_unicode=True, sort_keys=False, width=4096, default_flow_style=False))
    written.append(fname)

# ---- 来源登记 + 源文件（全量编译工作区用）——按与哈希一致的同一文本写文件 ----
def write_bytes(path, text):
    with io.open(path, "wb") as f:
        f.write(text.encode("utf-8"))

write_bytes(os.path.join(WS, "sources", GAME_FILE),
            "\n".join(MOTHER[k] for k in ["town_V7","town_S1","town_A2","town_ES4"]) + "\n")
write_bytes(os.path.join(WS, "sources", CHRON_FILE), "\n".join(chron_texts) + "\n")
with io.open(os.path.join(WS, "sources", "source-game-settlements-geo1.yaml"), "w", encoding="utf-8", newline="\n") as f:
    f.write("""source_id: %s
source_version: %s
source_nature: game_snapshot
universe: awake_current
era: current
locator_root: %s
source_content_hash: %s
content_tier: base
license_status: permitted
use_status: active
valid_until: null
imported_at: 2026-09-12T00:00:00Z
normalization_version: utf8-lf-no-bom-v1
""" % (GAME_SID, GAME_VER, GAME_FILE, game_file_hash))

with io.open(os.path.join(WS, "sources", "source-chronicle-animusforge-geo1.yaml"), "w", encoding="utf-8", newline="\n") as f:
    f.write("""source_id: %s
source_version: %s
source_nature: chronicle
universe: awake_current
era: historical
locator_root: %s
source_content_hash: %s
content_tier: base
license_status: permitted
use_status: active
valid_until: null
imported_at: 2026-09-12T00:00:00Z
normalization_version: utf8-lf-no-bom-v1
""" % (CHRON_SID, CHRON_VER, CHRON_FILE, chron_file_hash))

print("\n".join(written))
print("game_file_hash:", game_file_hash)
print("chron_file_hash:", chron_file_hash)
print("GEN-DONE")
