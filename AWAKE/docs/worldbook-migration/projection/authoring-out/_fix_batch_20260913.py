# -*- coding: utf-8 -*-
"""修1+修2 联合补丁（2026-09-13）：
A) 12 档补 A 级官方引文（quote 子串策略，避免与既有档同 hash 双投）
B) 20 档补 en title
文本级插入，改后 yaml.safe_load 复验。
"""
import os, re, json, hashlib
import yaml

DIR = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out"
CNJ = json.load(open(os.path.join(DIR, "_fix2_cn_texts_20260913.json"), encoding="utf-8"))

SETTLE_VER, SETTLE_HASH = "bannerlord-1.3.15.110062", "E8F7E5F0AB96CE249D4108ED663FAFF81706A2647066FF30E9816631F0AC0AD3"
WAR1_VER = "bannerlord-1.3.15.110062"
WAR1_HASH = "41792a7110317feb4b4f8dc8c5ae861fe070bb13adb64771e9455166d81001bb"

def h(qs):
    return hashlib.sha256(qs.encode("utf-8")).hexdigest().upper()

# ---- A 源补挂清单 ----
V7 = "这座如今被称为沙拉斯的城市，其历史最早可以追溯到第一批卡拉德殖民者登上这片大陆的海岸之时。"
T7 = json.load(open(os.path.join(DIR, "_fix2_settle_texts_20260913.json"), encoding="utf-8"))["town_S7"]["cn"]
PATCH = {
    "charas-bay": {"loc": "bannerlord.db#settlements.castle_village_EW1_1.descriptionText",
                   "quote": CNJ["castle_village_EW1_1"], "sid": "settlements.geography2"},
    "charas-origin-tales": {"loc": "bannerlord.db#settlements.town_V7.descriptionText", "quote": V7,
                            "sid": "settlements.geography2"},
    "dawn-mtn": {"loc": "bannerlord.db#settlements.village_K6_2.descriptionText",
                 "quote": CNJ["village_K6_2"], "sid": "settlements.geography2"},
    "dawn-stew": {"loc": "bannerlord.db#settlements.village_K6_1.descriptionText",
                  "quote": "喀拉罕位于柯希·罗希尼——黎明山脉山麓的丘陵地带。当地村民向迁徙的部落购买马匹，自己也饲养一些，库赛特军队的许多马匹都来源于此。",
                  "sid": "settlements.geography2"},
    "der-furs": {"loc": "bannerlord.db#settlements.castle_village_V6_2.descriptionText",
                 "quote": "当地村民会在山上捕获河狸与水貂，有时会在海湾水域捉海豹。", "sid": "settlements.geography2"},
    "der-vill": {"loc": "bannerlord.db#settlements.castle_village_V6_2.descriptionText",
                 "quote": CNJ["castle_village_V6_2"], "sid": "settlements.geography2"},
    "kach-land": {"loc": "bannerlord.db#settlements.castle_village_S1_1.descriptionText",
                  "quote": CNJ["castle_village_S1_1"], "sid": "settlements.geography2"},
    "kach-own": {"loc": "bannerlord.db#settlements.town_S7.descriptionText", "quote": T7,
                 "sid": "settlements.geography2"},
    "kach-tales": {"loc": "bannerlord.db#settlements.town_S7.descriptionText",
                   "quote": "根据传说，巴尔加德公主阿基娜的丈夫被雷维尔人杀死，但她接受了敌国国王的提议，与其结为连理以解决这一争端。然而，在新婚之夜，她堵住了厅堂的大门，将国王和他的一百名族人与勇士活活烧死在里面。",
                   "sid": "settlements.geography2"},
    "lac-lake": {"loc": "bannerlord.db#settlements.castle_village_EN7_1.descriptionText",
                 "quote": CNJ["castle_village_EN7_1"], "sid": "settlements.geography2"},
    "royal-guard": {"loc": "bannerlord.db#localization.DHbF9JvO", "quote": "皇家侍卫", "sid": "lore.war1"},
    "sturgia-military": {"loc": "bannerlord.db#localization.k1Xr4rKn", "quote": "亲卫骑兵", "sid": "lore.war1"},
}
# geo2 子源的 version/hash：从 devseg-plateau 现有条目动态取
_ref = yaml.safe_load(open(os.path.join(DIR, "devseg-plateau.yaml"), encoding="utf-8"))
GEO2_VER = GEO2_HASH = None
for s in _ref.get("sources", []):
    if s["source_id"].endswith(".geography2"):
        GEO2_VER, GEO2_HASH = s["source_version"], s["source_content_hash"]
        break
print("geography2 源:", GEO2_VER, (GEO2_HASH or "")[:16])

EN_TITLE = {
    "charas-town": "Charas", "charas-cortain-secret": "Charas", "charas-reign": "Charas",
    "varcheg-town": "Varcheg", "varcheg-swap": "Varcheg",
    "husn-fulq-town": "Husn Fulq", "husn-fulq-tales": "Husn Fulq",
    "lycaron-town": "Lycaron", "lycaron-mines": "Lycaron", "lycaron-rock-tales": "Lycaron",
    "der-vill": "Deriat", "der-furs": "Deriat",
    "kach-own": "Kachyar Peninsula", "kach-land": "Kachyar Peninsula", "kach-tales": "Kachyar Peninsula",
    "lac-lake": "Lakonis Lake", "lac-tales": "Lakonis Lake",
    "dawn-mtn": "Dawn Mountains", "dawn-stew": "Dawn Mountains", "dawn-taboo": "Dawn Mountains",
}

report = []
for slug, p in PATCH.items():
    fp = os.path.join(DIR, slug + ".yaml")
    txt = open(fp, encoding="utf-8").read()
    # A 源条目
    ver, shash = (WAR1_VER, WAR1_HASH) if p["sid"] == "lore.war1" else (GEO2_VER, GEO2_HASH)
    sid_full = "source.calradia.game." + p["sid"]
    entry = (f"- source_id: {sid_full}\n"
             f"  source_version: {ver}\n"
             f"  source_content_hash: {shash}\n"
             f"  locator: {p['loc']}\n"
             f"  quote_hash: {h(p['quote'])}\n"
             f"  quote: {p['quote']}\n")
    m = re.search(r"\n(sources:\n)((?:[ ]+- .*\n|[ ]{2,}.*\n)*)", txt)
    assert m, slug + ": sources 块未找到"
    # 检测列表项缩进
    tail = m.group(2)
    ind_m = re.match(r"( *)- ", tail)
    ind = ind_m.group(1) if ind_m else ""
    entry_indented = "".join(
        (ind + line) if line.strip() else line
        for line in entry.splitlines(keepends=True))
    # 插到 sources 列表末尾 = sources: 块内最后一个列表项之后、下一个顶格键之前
    sm = re.search(r"(?m)^sources:\s*$", txt)
    assert sm, slug + ": sources: 行未找到"
    pos = sm.end()
    boundary = len(txt)
    for lm in re.finditer(r"(?m)^\S.*$", txt[pos:]):
        boundary = pos + lm.start()
        break
    txt = txt[:boundary] + entry_indented.rstrip("\n") + "\n" + txt[boundary:]
    # revision +1
    txt = re.sub(r"(?m)^revision: (\d+)$", lambda mm: f"revision: {int(mm.group(1)) + 1}", txt, count=1)
    # en title（仅空缺档；已有 en 的跳过）
    en = EN_TITLE.get(slug)
    if en:
        tm = re.search(r"title:\n  zh-CN: ([^\n]+)\n(status:)", txt)
        if tm:  # 多行格式
            txt = txt[:tm.end(1)] + f"\n  en: {en}\n" + txt[tm.end(1):]
        else:   # 单行 {zh-CN: ...}
            tm2 = re.search(r'title: (\{[^}\n]*zh-CN:[^}\n]*)\}', txt)
            assert tm2, slug + ": title 单行未匹配"
            txt = txt[:tm2.end(1)] + f', en: "{en}"}}' + txt[tm2.end(1) + 1:]
    # 复验
    d = yaml.safe_load(txt)
    if en:
        assert d["title"].get("en"), slug + ": en 仍空"
    src_ids = [s["source_id"] for s in d["sources"]]
    assert sid_full in src_ids, slug + ": A 源未入"
    open(fp, "w", encoding="utf-8", newline="\n").write(txt)
    report.append([slug, sid_full, "en=" + (en or "(已有)")])
    print(f"[OK] {slug}: +A源({p['sid']}) en_title={en or '(已有)'}")

# en title 只补不挂 A 的档（dawn-taboo, lac-tales）
for slug in ["dawn-taboo", "lac-tales"]:
    fp = os.path.join(DIR, slug + ".yaml")
    txt = open(fp, encoding="utf-8").read()
    en = EN_TITLE[slug]
    tm = re.search(r"title:\n  zh-CN: ([^\n]+)\n(status:)", txt)
    if tm:
        txt = txt[:tm.end(1)] + f"\n  en: {en}\n" + txt[tm.end(1):]
    else:
        tm2 = re.search(r'title: (\{[^}\n]*zh-CN:[^}\n]*)\}', txt)
        txt = txt[:tm2.end(1)] + f', en: "{en}"}}' + txt[tm2.end(1) + 1:]
    yaml.safe_load(txt)
    open(fp, "w", encoding="utf-8", newline="\n").write(txt)
    report.append([slug, "-", "en=" + en])
    print(f"[OK] {slug}: en_title={en}")

json.dump(report, open(os.path.join(DIR, "_fix_batch_report_20260913.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("\n补丁完成：", len(report), "处档级改动")
