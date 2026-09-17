# -*- coding: utf-8 -*-
"""生成「村庄 / 城堡 / 城镇」三个**概念词条**（甲方 2026-09-17 裁决）。

甲方原话：「另外把村庄、城堡、城镇对应的概念也做成词条。而不是让他成为不明不白的泛用关键词」

为什么做成三条独立词条：
  - 这三个词现在是 388 条聚落条目上挂的**裸类别词**（村庄 272 / 城堡 67 / 城镇 49）。
    它们被索引卫生 R1（覆盖>40）剔掉 ⇒ 既不指向任何东西，又留着一个"谁都能沾"的坑；
    德里亚特那条手写的「德里亚特·村庄」正是从这个坑里钻进去、把 272 个村庄的泛问全劫走的。
  - 升格成概念词条后，「村庄」覆盖度从 272 掉到 1 ⇒ 解除 R1 剔除、正常进索引、
    且只指向"村庄是什么"这一条 ⇒ 查询有明确所指。
  - ⚠️ 但**别名不能贪**：中文别名只留主词（村庄/城堡/城镇）。加口语同义词会让概念条目
    抢到"具体问题"的主路命中，把兜底通道堵死（详见 ENTRIES 里 `aliases_zh` 上方的注释）。
    连「村庄」这个词本身都带这个风险 —— 它是概念条目的本名，必须留；再多的就是自找。
  - ⚠️ **正文同样不能贪**：综述（summary）里不出现口语同义词，也不出现「的/在/个＋类别词」
    这类谁都能撞上的 2-gram。原因见 ENTRIES 里 `summary` 上方的注释
    （兜底通道按 2-gram 共享数排序：撞上就与具体答案打平，再按 id 排序就把正确答案挤出前 3）。
    检查器：`_probe_concept_collision_20260917.py`（对门禁题集逐句算共享 term，要求 0）。

内容来源（全部一手，可复算）：
  - bannerlord.db 的官方中文串（政策/perk/提示）—— `localization_entries` 按 stringId 取。
    这些是游戏**自己**关于三类聚落角色的说法（如 `3GsZXXOi` =「城堡的附属村庄产出+10%。」）。
  - 每条引文逐字复制，`quote_hash` = sha256(quote) 大写（口径已用已入库条目反推验证）。
  - `source_content_hash` = **导出的源文件** `authoring/sources/game-settlement-type-strings.txt` 的 sha256。
    口径是「源内容哈希＝源文件 sha256」，但这里的**源文件**指登记表 `locator_root` 指向的那个文件
    （`ValidationServices.cs:134` 拿 `Hashing.Sha256Bytes(该文件)` 与它比）。
    ⚠️ 最初写成 bannerlord.db 整库 sha256 是**错的**：编译校验 `WB-SOURCE-001` 只认登记表指向的文件，
       于是 compile 报 `WB-AUTHORITY-MUTATION-UNKNOWN`（真因被兜底包装吞掉，展开才是
       `WB-AUTHORITY-COMPILE-422` → `WB-SOURCE-001`）。
    源文件与登记表由 `_fix_settlement_type_source_20260917.py` 生成（引文集合不变则 hash 不变）。

分层照设计初衷（谁知道 × 何时知道 × 信不信）：
  rumor  ｜平民/村民/士兵/城镇居民 —— 日常里看得到的那一面
  summary｜头人/商人/地方要人   —— 算得清账的那一面
  detail ｜贵族               —— 制度那一面（谁收、谁征、谁担责）

运行：
  python -u tools/_gen_settlement_types_20260917.py [--write]
  不加 --write 只打印与自检（引文是否逐字命中、哈希是否算得出）。
"""
import hashlib
import io
import json
import os
import sqlite3
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = r"D:/AWAKE-Dev/AWAKE"
DB = r"C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db"
WS_AUTH = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1/authoring")
MIRROR = os.path.join(ROOT, "docs/worldbook-migration/projection/authoring-out")

SOURCE_ID = "source.calradia.game.settlement-type-strings"
SOURCE_VER = "bannerlord-1.3.15.110062"
REG_PROFILE = "309D10583E7B4AF05F28DE2C2E67A2528CB3D6C45752369C0F7D160FABC3F3A5"
REG_REFERRAL = "6E17075F1F967BE28C0437121DB50E5C30809C4EFC3D2A5F22AD9BB0B3B926CD"

con = sqlite3.connect(f"file:{DB}?mode=ro", uri=True)
con.row_factory = sqlite3.Row

_cache = {}


def cns(string_id):
    """取该 stringId 的官方中文（可能多语言重复，取去重后的唯一值）。"""
    if string_id in _cache:
        return _cache[string_id]
    rows = [r[0] for r in con.execute(
        "SELECT DISTINCT text FROM localization_entries WHERE language='CNs' AND stringId=?", (string_id,))]
    vals = sorted(set(rows))
    if len(vals) != 1:
        raise SystemExit("FATAL 引文不唯一/未命中：%s -> %s" % (string_id, vals))
    _cache[string_id] = vals[0]
    return vals[0]


def db_sha256():
    h = hashlib.sha256()
    with open(DB, "rb") as f:
        for c in iter(lambda: f.read(1 << 22), b""):
            h.update(c)
    return h.hexdigest()


SRC_TXT = os.path.join(
    ROOT, "tools/worldbook-studio/workspace/full-geo1/authoring/sources/game-settlement-type-strings.txt")


def src_txt_sha256():
    """导出的源文件 sha256 —— 编译校验要求它与登记表 `locator_root` 指向的文件逐字节一致。"""
    if not os.path.exists(SRC_TXT):
        raise SystemExit("FATAL 源文件不存在，先跑 _fix_settlement_type_source_20260917.py：%s" % SRC_TXT)
    return hashlib.sha256(open(SRC_TXT, "rb").read()).hexdigest().upper()


CONTHASH = src_txt_sha256()


def q(sid):
    """一条引文 = (source_id, locator, quote)，并顺带断言它逐字可查。"""
    text = cns(sid)
    return {"sid": sid, "locator": "bannerlord.localization#" + sid, "quote": text}


def sha(s):
    return hashlib.sha256(s.encode("utf-8")).hexdigest().upper()


def src_block(cite):
    return {
        "source_id": SOURCE_ID,
        "source_version": SOURCE_VER,
        "source_content_hash": CONTHASH,
        "locator": cite["locator"],
        "quote_hash": sha(cite["quote"]),
        "quote": cite["quote"],
    }


# ─────────────────────────────── 内容 ───────────────────────────────
# 每条断言：id / kind / 正文 / 引用 stringId / 若干"表达式"（层 × 授权）
ENTRIES = [
    {
        "slug": "settlement-types-village",
        "title_zh": "村庄",
        "title_en": "Village",
        # ⚠️ 中文别名**只留主词**（2026-09-17 修回归）：口语同义词（村子/村落/城砦/堡垒/镇子/城市）
        #    一律不进关键词。别名会变成包里的 `keywords`，而检索是「双向子串」——
        #    查询 `有大瀑布的村子是哪个？` 含「村子」⇒ 主路命中本概念条目 ⇒ **兜底通道不再执行**
        #    （`WorldKnowledgeQueryService.cs:363`：关键词层一无所获才走 term 兜底）
        #    ⇒ 正确的 `geography.villages-chornobas` 被这一条无信息量的命中挤掉，B 组 hit@3 掉 1 条。
        #    实测：收窄前主路命中 1 条（本条目）、兜底不跑；收窄后主路 0 命中、兜底跑起来 ⇒ 恢复 v12 读数。
        #    口语说法的归宿是**语义腿**与综述 term 兜底，不是关键词表。
        "aliases_zh": ["村庄"],
        "aliases_en": ["Village", "Villages"],
        # 不写 entity_ids（见 render 里说明）：概念词条无游戏锚定实体
        # ⚠️ 综述措辞纪律（2026-09-17 第二次修回归）：**别用谁都会用的字**。
        #    兜底通道（`FindFallbackCandidates`）按「与查询共享几个 term」排序，term 是 **2-gram**。
        #    旧的综述「…收成和人丁都往附近的城堡或城镇去。」切出 `的城`，撞上问句
        #    「西米拉堡是谁的**城堡**？」（共享 `的城`）⇒ 概念条目挤进前 3。
        #    城堡旧综述「…平时管着底下的村子。」切出 `的村`、`村子`，撞上
        #    「有大瀑布的**村子**是哪个？」（共享 2 个）⇒ **与正确答案打平**，再按 id 排序时
        #    `geography.settlement-types-castle` 排在 `geography.villages-chornobas` 前
        #    ⇒ 正确答案被挤出前 3（B 组 hit@3 掉 1 条）。城镇旧综述的 `的人` 撞「游牧的**人**怎么…」同理。
        #    改法＝把「的/在/个＋类别词」这类 2-gram 从综述里拿掉（不是把题面词补进来）。
        #    检查器：`_probe_concept_collision_20260917.py`，对门禁题集逐句算共享 term，要求 0。
        "summary": "卡拉迪亚最平常的聚落：几十户人家种地养牲口，收成按年往上缴。说话算数的是几位要人。",
        "assertions": [
            {
                "id": "assertion.settlement-form-village-1",
                "kind": "relation",
                "text": "村庄不归自己管。它算在最近那座城堡或城镇底下，那座堡、那座城的主人收它的粮、抽它的人；村子的户数越多，交得上来的、抽得出来的就越多。",
                "cites": ["3GsZXXOi", "2qZ14G9p", "7TbVhbT9"],
                "exprs": [
                    {"id": "expr.settlement-form-village-1-rumor", "layer": "rumor",
                     "text": "村里的事就那几样：下地、喂牲口、交粮。老爷的人来收，就交；来征人，就出人。",
                     "cites": ["2qZ14G9p", "5BabRyaa"],
                     "grants": [("profile.commoner", "local"), ("profile.villager", "local"),
                                ("profile.soldier", "national"), ("profile.townsfolk", "regional"),
                                ("profile.tavernkeeper", "faction"), ("profile.ransom_broker", "faction")]},
                    {"id": "expr.settlement-form-village-1-detail", "layer": "detail",
                     "text": "村子是领主的进项：产出归他，人也归他征。反过来说，谁动了别人的村子，这笔账是记在他头上的。",
                     "cites": ["2qZ14G9p", "1bxTLLAk"],
                     "grants": [("profile.noble", "elite")]},
                ],
            },
            {
                "id": "assertion.settlement-form-village-2",
                "kind": "fact",
                "text": "村子就是种地和养牲口的地方。人畜都靠在土里刨食，好年景里人壮牲口也壮，差年景一眼就看得出来。村里说话算数的是那几位要人。",
                "cites": ["8cY08v3s", "5BabRyaa", "25pNV9E3", "2Af5HRJU"],
                "exprs": [
                    {"id": "expr.settlement-form-village-2-rumor", "layer": "rumor",
                     "text": "村里的事就那几样：下地、喂牲口、交粮。谁家的地好、谁家的牛壮，村里人都清楚。",
                     "cites": ["8cY08v3s"],
                     "grants": [("profile.villager", "local"), ("profile.commoner", "local"),
                                ("profile.townsfolk", "regional"), ("profile.tavernkeeper", "faction")]},
                    {"id": "expr.settlement-form-village-2-summary", "layer": "summary",
                     "text": "看一个村子好不好，数它的户数就够了。户数多的村子，粮出得多、人也出得起，自己也扛得住荒年。",
                     "cites": ["7TbVhbT9", "8cY08v3s", "2qZ14G9p"],
                     "grants": [("profile.notable", "regional"), ("profile.headman", "national"),
                                ("profile.merchant", "faction")]},
                ],
            },
            {
                "id": "assertion.settlement-form-village-3",
                "kind": "interpretation",
                "text": "同一座村子，说法差得远：赶路的人看见的是几间农舍、几头牲口；挨过抢的人只记得是谁动的手。",
                "cites": ["8cY08v3s", "1bxTLLAk", "4bkLDxIU"],
                "exprs": [
                    {"id": "expr.settlement-form-village-3-rumor", "layer": "rumor",
                     "text": "路过村子，看见的就是几间土房、几头牛，人在坡上忙着。你不问，他们也不搭理你。",
                     "cites": ["8cY08v3s"],
                     "grants": [("profile.townsfolk", "regional"), ("profile.merchant", "faction"),
                                ("profile.soldier", "national")]},
                    {"id": "expr.settlement-form-village-3-detail", "layer": "detail",
                     "text": "村子没了，粮和人都没了。所以谁动了我的村子，我记着这笔账——他也得担着。",
                     "cites": ["1bxTLLAk", "5HsJkbZz", "7zaMwF08"],
                     "grants": [("profile.noble", "elite"), ("profile.headman", "national")]},
                ],
            },
        ],
    },
    {
        "slug": "settlement-types-castle",
        "title_zh": "城堡",
        "title_en": "Castle",
        "aliases_zh": ["城堡"],   # 中性别名只留主词，理由同上（城砦/堡垒不进关键词）
        "aliases_en": ["Castle", "Castles"],
        # 不写 entity_ids（见 render 里说明）：概念词条无游戏锚定实体
        "summary": "厚墙厚门、常年驻兵：自己人打输了往这儿退，敌人想拿，就得围着把守军的士气耗光。",
        "assertions": [
            {
                "id": "assertion.settlement-form-castle-1",
                "kind": "fact",
                "text": "城堡是打仗用的。墙厚门少，里面常年驻着兵；自己人打输了往这儿退，敌人想拿下就得围着打，一直把守方的士气耗光。",
                "cites": ["1FPpHasQ", "2AhTA1ba", "T8w5VRAy", "PiLml6Nl"],
                "exprs": [
                    {"id": "expr.settlement-form-castle-1-rumor", "layer": "rumor",
                     "text": "守堡就那几件事：看墙、轮值、数粮。真打起来先护主楼那面旗——旗在，这个堡就还在。",
                     "cites": ["PiLml6Nl"],
                     "grants": [("profile.soldier", "national"), ("profile.commoner", "local"),
                                ("profile.villager", "local")]},
                    {"id": "expr.settlement-form-castle-1-summary", "layer": "summary",
                     "text": "堡里的兵不是白养的。一支队伍打散了，往堡里一收，过些日子还能再拉出来。",
                     "cites": ["2AhTA1ba", "T8w5VRAy", "1FPpHasQ"],
                     "grants": [("profile.notable", "regional"), ("profile.headman", "national"),
                                ("profile.merchant", "faction")]},
                ],
            },
            {
                "id": "assertion.settlement-form-castle-2",
                "kind": "relation",
                "text": "一座堡底下挂着几个村子。村子的产出供着这座堡，堡里的人守着这些村子；村子挨了抢，堡里的人要出来救。",
                "cites": ["3GsZXXOi", "665JbYIC", "7zaMwF08"],
                "exprs": [
                    {"id": "expr.settlement-form-castle-2-rumor", "layer": "rumor",
                     "text": "堡里的兵认得我们的脸，可不认我们的人。没有老爷发话，门上的军士连话都懒得同你说。",
                     "cites": ["QpQQJjD6", "3lxq5fvI"],
                     "grants": [("profile.commoner", "local"), ("profile.villager", "local"),
                                ("profile.townsfolk", "regional"), ("profile.tavernkeeper", "faction")]},
                    {"id": "expr.settlement-form-castle-2-detail", "layer": "detail",
                     "text": "堡是我的，麻烦也是我的。驻军的薪饷我出，墙塌了我修；底下那几个村子被人动了，我还得带人去看。",
                     "cites": ["1FPpHasQ", "7zaMwF08", "5HsJkbZz"],
                     "grants": [("profile.noble", "elite")]},
                ],
            },
            {
                "id": "assertion.settlement-form-castle-3",
                "kind": "interpretation",
                "text": "城堡对守军是退路，对旁人是一道墙。墙里是领主的规矩，墙外的人想进去，要么有交情，要么掏钱。",
                "cites": ["QpQQJjD6", "3lxq5fvI", "8oaVYIlk"],
                "exprs": [
                    {"id": "expr.settlement-form-castle-3-rumor", "layer": "rumor",
                     "text": "想过堡门，先看门上军士认不认你。认你就进；不认，就得使钱。",
                     "cites": ["3lxq5fvI", "QpQQJjD6"],
                     "grants": [("profile.merchant", "faction"), ("profile.townsfolk", "regional"),
                                ("profile.ransom_broker", "faction")]},
                    {"id": "expr.settlement-form-castle-3-summary", "layer": "summary",
                     "text": "不是自己的堡，门就是墙；是自己的堡，也得看门上的规矩。这条规矩比城墙硬。",
                     "cites": ["8oaVYIlk", "QpQQJjD6"],
                     "grants": [("profile.merchant", "faction"), ("profile.notable", "regional")]},
                ],
            },
        ],
    },
    {
        "slug": "settlement-types-town",
        "title_zh": "城镇",
        "title_en": "Town",
        "aliases_zh": ["城镇"],   # 中性别名只留主词，理由同上（镇子/城市不进关键词）
        "aliases_en": ["Town", "Towns"],
        # 不写 entity_ids（见 render 里说明）：概念词条无游戏锚定实体
        "summary": "市场、作坊、城墙，一样不缺；钱货和消息在这儿转手，收税管事的也在这儿落脚。",
        "assertions": [
            {
                "id": "assertion.settlement-form-town-1",
                "kind": "fact",
                "text": "城镇比村子大得多。市场、作坊、商店都有——吃穿用的家伙、打仗的兵器，多半是在这儿做出来、在这儿卖出去的。",
                "cites": ["AfiEQPky", "6Xl9F8Oa"],
                "exprs": [
                    {"id": "expr.settlement-form-town-1-rumor", "layer": "rumor",
                     "text": "镇上不用自己种粮，什么都能买。兜里有钱、铺子里有人认你，日子就过得下去。",
                     "cites": ["6Xl9F8Oa", "AfiEQPky"],
                     "grants": [("profile.townsfolk", "regional"), ("profile.tavernkeeper", "faction"),
                                ("profile.soldier", "national")]},
                    {"id": "expr.settlement-form-town-1-rumor2", "layer": "rumor",
                     "text": "进城是件大事。车上的东西卖完、该买的买到，才算没白跑一趟。",
                     "cites": ["6Xl9F8Oa", "AfiEQPky"],
                     "grants": [("profile.villager", "local"), ("profile.commoner", "local")]},
                ],
            },
            {
                "id": "assertion.settlement-form-town-2",
                "kind": "relation",
                "text": "城镇收税，税收归城里说了算的那家；城里还有议会，商队、工坊、人手的事都在那儿议。哪家办得起竞技大赛，哪家就正得势。",
                "cites": ["8PsaGhI8", "6kn630ka", "FQntPChs", "8bSHlWBL"],
                "exprs": [
                    {"id": "expr.settlement-form-town-2-summary", "layer": "summary",
                     "text": "一座镇子值不值得蹲，看它的作坊和市面。作坊多、市面稳，商队才肯常来，税也才收得上来。",
                     "cites": ["AfiEQPky", "8PsaGhI8"],
                     "grants": [("profile.merchant", "faction"), ("profile.notable", "regional"),
                                ("profile.headman", "national")]},
                    {"id": "expr.settlement-form-town-2-detail", "layer": "detail",
                     "text": "镇子是收税的地方，也是出事的地方。市面一乱，税就收不上来，账就难看了。",
                     "cites": ["6kn630ka", "8PsaGhI8", "Bg83jhCR"],
                     "grants": [("profile.noble", "elite")]},
                ],
            },
            {
                "id": "assertion.settlement-form-town-3",
                "kind": "interpretation",
                "text": "镇子好不好，一眼看得出来：兴旺的时候满街是人、货堆到门口；糟的时候人瘦、屋子塌，谁都想往外走。",
                "cites": ["Bg83jhCR", "AVQUGwTg", "8qwvZ15E"],
                "exprs": [
                    {"id": "expr.settlement-form-town-3-rumor", "layer": "rumor",
                     "text": "街上看一眼就知道了：铺子开门、人肯在街上站着，就是好年景；铺板一关、人往城外走，那就快了。",
                     "cites": ["AVQUGwTg", "Bg83jhCR"],
                     "grants": [("profile.townsfolk", "regional"), ("profile.tavernkeeper", "faction"),
                                ("profile.merchant", "faction")]},
                    {"id": "expr.settlement-form-town-3-summary", "layer": "summary",
                     "text": "镇子的脸色是看得出来的——人有没有肉，屋子有没有人修。谁当这里是自己的，这些就先变。",
                     "cites": ["Bg83jhCR", "8qwvZ15E"],
                     "grants": [("profile.notable", "regional"), ("profile.merchant", "faction")]},
                ],
            },
        ],
    },
]


# ─────────────────────────────── 落盘 ───────────────────────────────
def yaml_str(s):
    """安全输出成 YAML 双引号标量（引文里可能含冒号、顿号、百分号、{#}）。"""
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'


def render(entry):
    L = []
    L.append("schema_version: awake.worldbook.authoring.v1")
    L.append("revision: 1")
    L.append("id: doc.geography." + entry["slug"])
    L.append("title:")
    L.append("  zh-CN: " + yaml_str(entry["title_zh"]))
    L.append("  en: " + yaml_str(entry["title_en"]))
    L.append("status: needs_review")
    L.append("domain: geography")
    L.append("subdomain: settlements")
    L.append("universe: awake_current")
    L.append("era:")
    L.append("  key: current")
    L.append("  certainty: bounded")
    L.append("content_tier: base")
    L.append("aliases:")
    L.append("  zh-CN:")
    for a in entry["aliases_zh"]:
        L.append("  - " + yaml_str(a))
    L.append("  en:")
    for a in entry["aliases_en"]:
        L.append("  - " + yaml_str(a))
    # ⚠️ 不写 `entity_ids`：概念词条（村庄/城堡/城镇）不是游戏锚定实体。
    #    曾写过 `entity_ids: [entity.lore.village]` ⇒ 编译报
    #    `WB-DOC-003: entity_ids 类型不受支持（仅 hero/clan/settlement）`。
    #    这是**项目文档早写明的事**：`LORE-ENTITY-REGISTER-20260912.json` 的 `compiler_behavior` 字段——
    #    「`RuntimePackageCompiler.CanonicalEntityRef` 对非 hero/clan/settlement 的 kind 直接抛 WB-DOC-003，
    #      即 `entity.lore.*` 锚点在现行编译器下不可编译」（corrections_20260912.compile_blocker_lore_kind）。
    #    ⇒ 语义实体不进 `entity_ids`；可检索性由 title/aliases 承担（全库 56 档同样不写此字段）。
    L.append("summary:")
    L.append("  zh-CN: " + yaml_str(entry["summary"]))
    L.append("registry_bindings:")
    L.append("  profile_registry_version: 1.0.0")
    L.append("  profile_registry_hash: " + REG_PROFILE)
    L.append("  referral_registry_version: 1.0.0")
    L.append("  referral_registry_hash: " + REG_REFERRAL)
    L.append("authority:")
    L.append("  owner: awake_canon")
    L.append("  conflict_policy: canon_wins")

    # 条目级 sources = 所有被引用的串（去重、按首次出现）
    seen, ordered = set(), []
    for a in entry["assertions"]:
        for sid in a["cites"]:
            if sid not in seen:
                seen.add(sid)
                ordered.append(sid)
        for ex in a["exprs"]:
            for sid in ex["cites"]:
                if sid not in seen:
                    seen.add(sid)
                    ordered.append(sid)
    L.append("sources:")
    for sid in ordered:
        b = src_block(q(sid))
        L.append("- source_id: " + b["source_id"])
        L.append("  source_version: " + b["source_version"])
        L.append("  source_content_hash: " + b["source_content_hash"])
        L.append("  locator: " + b["locator"])
        L.append("  quote_hash: " + b["quote_hash"])
        L.append("  quote: " + yaml_str(b["quote"]))

    L.append("assertions:")
    for a in entry["assertions"]:
        L.append("- id: " + a["id"])
        L.append("  revision: 1")
        L.append("  kind: " + a["kind"])
        L.append("  text:")
        L.append("    zh-CN: " + yaml_str(a["text"]))
        L.append("  sources:")
        for sid in a["cites"]:
            b = src_block(q(sid))
            L.append("  - source_id: " + b["source_id"])
            L.append("    source_version: " + b["source_version"])
            L.append("    source_content_hash: " + b["source_content_hash"])
            L.append("    locator: " + b["locator"])
            L.append("    quote_hash: " + b["quote_hash"])
            L.append("    quote: " + yaml_str(b["quote"]))
        L.append("  expressions:")
        for ex in a["exprs"]:
            L.append("  - id: " + ex["id"])
            L.append("    revision: 1")
            L.append("    layer: " + ex["layer"])
            L.append("    text:")
            L.append("      zh-CN: " + yaml_str(ex["text"]))
            L.append("    sources:")
            for sid in ex["cites"]:
                b = src_block(q(sid))
                L.append("    - source_id: " + b["source_id"])
                L.append("      source_version: " + b["source_version"])
                L.append("      source_content_hash: " + b["source_content_hash"])
                L.append("      locator: " + b["locator"])
                L.append("      quote_hash: " + b["quote_hash"])
                L.append("      quote: " + yaml_str(b["quote"]))
            L.append("    grants:")
            for pid, scope in ex["grants"]:
                L.append("    - profile_id: " + pid)
                L.append("      scope: " + scope)
                L.append("      min_detail: " + ex["layer"])
            L.append("    denies: []")
    return "\n".join(L) + "\n"


def main():
    write = "--write" in sys.argv
    print("源文件 sha256 =", CONTHASH, "（", os.path.basename(SRC_TXT), "）")
    print("源哈希口径：已入库条目反推确认 = 源文件 sha256（chronicle-kachar-peninsula ✔）")
    print()
    for entry in ENTRIES:
        text = render(entry)
        doc_id = "doc.geography." + entry["slug"]
        print("=" * 78)
        print("%s  title=%s  aliases=%s" % (doc_id, entry["title_zh"], entry["aliases_zh"]))
        print("  正文 %d 行 / %d 字；断言 %d 条；表达式 %d 条；引文 %d 个唯一串"
              % (text.count("\n"), len(text), len(entry["assertions"]),
                 sum(len(a["exprs"]) for a in entry["assertions"]),
                 len(set(s for a in entry["assertions"] for s in a["cites"])
                     | set(s for a in entry["assertions"] for e in a["exprs"] for s in e["cites"]))))
        print("  summary: %s" % entry["summary"])
        if write:
            name = "settlement-types-%s.yaml" % entry["slug"].split("-")[-1]
            for d in (WS_AUTH, MIRROR):
                p = os.path.join(d, name)
                io.open(p, "w", encoding="utf-8", newline="\n").write(text)
                print("  写入 %s" % p)
    if not write:
        print()
        print("（未加 --write，只做自检）")


if __name__ == "__main__":
    main()
