# -*- coding: utf-8 -*-
"""链路缺口红测 · 对抗样本生成（2026-09-17）

靶：**今天的链路**（448 条包 `ModuleData/Worldbook/packages/calradia`），走真实对话路径
`NpcDialogueService` → `WorldKnowledgeQueryService.FindCandidates`（真代码零替身，探针 = `worldbook-runtime-sim probe`）。

与 09-16 那轮的区别（**这轮真正新增的交叉面**）：
  · 09-16 量的是**两条臂各自**（字面 48 条、语义 top3），**没有量过合并后的上线形态**；
  · 也**没量过脏输入 × 身份/档位**这一交叉（权限闸是今天才补上判据的）。
⇒ 本轮出两档：`不挂语义`（＝改动前）与 `挂语义`（＝今天上线的合并形态），同一批样本逐条对照。

样本 100% 按目标条目的**真实 keywords**造（不照抄卡名），分组照抄 09-16 模板并保留阳性对照。
"""
import io
import json

T_HELMET = "awake:entry:economy.items-sturgian_helmet_closed"     # 闭面军阀盔
T_LAMESA = "awake:entry:geography.villages-lamesa"                # 拉迈萨
T_FUR = "awake:entry:economy.goods-fur"                           # 毛皮
T_STURGIA = "awake:entry:war.military-sturgia"                    # 斯特吉亚军事力量
T_SECRET = "awake:entry:politics.clans-charas-cortain-secret"     # 沙拉斯·科尔坦家的账（平民无权）

ROWS = []


def add(no, group, cat, text, targets, note, identity="profile.noble"):
    ROWS.append({
        "no": no, "group": group, "cat": cat, "text": text,
        "targets": targets, "note": note, "identity": identity,
    })


# ── A 错别字（形近/同音/联想）──
add("KWA1", "错别字", "MISS-RISK", "闭面军伐盔", [T_HELMET], "伐/阀 同音")
add("KWA2", "错别字", "MISS-RISK", "闭面军阀魁", [T_HELMET], "魁/盔 形近")
add("KWA3", "错别字", "MISS-RISK", "拉迈莎", [T_LAMESA], "莎/萨 同音")
add("KWA4", "错别字", "MISS-RISK", "毛匹", [T_FUR], "匹/皮 同音")
add("KWA5", "错别字", "MISS-RISK", "斯特基亚", [T_STURGIA], "基/吉 同音")
# ── B 同音 ──
add("KWB1", "同音", "MISS-RISK", "闭面军阀亏", [T_HELMET], "亏/盔 同音 kuī")
add("KWB2", "同音", "MISS-RISK", "拉买萨", [T_LAMESA], "买/迈 同音")
# ── C 繁体（含简体阳性对照）──
add("KWC1", "繁体", "MISS-RISK", "閉面軍閥盔", [T_HELMET], "繁体")
add("KWC2", "繁体", "MISS-RISK", "拉邁薩", [T_LAMESA], "繁体")
add("KWC3", "繁体", "MISS-RISK", "斯特吉亞軍事力量", [T_STURGIA], "繁体")
add("KWC4", "繁体", "MISS-RISK", "毛皮", [T_FUR], "简体对照（该词繁简同形）")
# ── D 空格 ──
add("KWD1", "空格", "MISS-RISK", "闭面 军阀盔", [T_HELMET], "词中空格")
add("KWD2", "空格", "MISS-RISK", "闭 面军 阀盔", [T_HELMET], "多处空格")
add("KWD3", "空格", "MISS-RISK", "闭面\t军阀盔", [T_HELMET], "词中 TAB")
add("KWD4", "空格", "MISS-RISK", "拉 迈 萨", [T_LAMESA], "逐字空格")
# ── E 标点（词中＝致命；词后＝阳性对照）──
add("KWE1", "标点", "MISS-RISK", "闭面，军阀盔", [T_HELMET], "词中逗号")
add("KWE2", "标点", "MISS-RISK", "闭面-军阀盔", [T_HELMET], "词中连字符")
add("KWE3", "标点", "MISS-RISK", "拉，迈萨", [T_LAMESA], "词中逗号")
add("KWE4", "标点", "MISS-RISK", "毛，皮", [T_FUR], "词中逗号")
add("KWE5", "标点", "MISS-RISK", "闭面军阀盔。", [T_HELMET], "词后句号（阳性对照）")
add("KWE6", "标点", "MISS-RISK", "，拉迈萨", [T_LAMESA], "词前逗号（阳性对照）")
add("KWE7", "标点", "MISS-RISK", "毛皮？！", [T_FUR], "词后疑问+叹（阳性对照）")
# ── F 简写（截断/倒装/首字母/单字）──
add("KWF1", "简写", "MISS-RISK", "闭面", [T_HELMET], "截断前二字")
add("KWF2", "简写", "MISS-RISK", "盔阀军面闭", [T_HELMET], "倒装")
add("KWF3", "简写", "MISS-RISK", "bmjfk", [T_HELMET], "拼音首字母")
add("KWF4", "简写", "MISS-RISK", "拉迈", [T_LAMESA], "截断")
add("KWF5", "简写", "MISS-RISK", "皮子", [T_FUR], "别称")
add("KWF6", "简写", "MISS-RISK", "斯特吉亚", [T_STURGIA], "势力名截断")
# ── G 大小写 / 全半角（阳性对照）──
add("KWG1", "大小写", "MISS-RISK", "closed warlord helmet", [T_HELMET], "全小写")
add("KWG2", "大小写", "MISS-RISK", "CLOSED WARLORD HELMET", [T_HELMET], "全大写")
add("KWG3", "大小写", "MISS-RISK", "lamesa", [T_LAMESA], "全小写")
add("KWG4", "大小写", "MISS-RISK", "FUR", [T_FUR], "全大写")
add("KWG5", "全半角", "MISS-RISK", "Ｃｌｏｓｅｄ Ｗａｒｌｏｒｄ Ｈｅｌｍｅｔ", [T_HELMET], "全角字母")
# ── H 整句（带名字 / 不带名字的描述型）──
add("KWH1", "整句", "MISS-RISK", "闭面军阀盔是啥玩意？", [T_HELMET], "带名字的整句")
add("KWH2", "整句", "MISS-RISK", "北方头领戴的那种连脸都罩住的盔是啥？", [T_HELMET], "不带名字的描述型")
add("KWH3", "整句", "MISS-RISK", "阿塞莱最好的马在哪儿吃草？", [T_LAMESA], "不带名字的描述型")
add("KWH4", "整句", "MISS-RISK", "北方有什么又暖又贵的皮货？", [T_FUR], "＝今天题集里的真题")
add("KWH5", "整句", "MISS-RISK", "盾墙加飞斧是哪个国家的打法？", [T_STURGIA], "＝今天题集里的真题")
# ── I 过匹配（期望：不该命中任何条目）──
for idx, token in enumerate(["盔", "军阀", "head", "HeadArmor", "sturgian", "doc",
                             "村", "村庄", "castle_village", "皮", "货", "Goods",
                             "军事", "力量", "war", "economy", "geography", "entry"], start=1):
    add("KWI%d" % idx, "过匹配", "OVER-RISK", token, [], "短词/泛词/内部 id")
# ── J 边界（期望：不该命中任何条目）──
add("KWJ1", "边界", "OVER-RISK", "", [], "空串")
add("KWJ2", "边界", "OVER-RISK", "   ", [], "纯空格")
add("KWJ3", "边界", "OVER-RISK", "？？？", [], "纯标点")
add("KWJ4", "边界", "OVER-RISK", "zzzzzz", [], "拉丁乱码")
add("KWJ5", "边界", "OVER-RISK", "doc.economy.goods-fur", [], "内部文档 id 直问")
# ── K 权限 / 档位 × 脏输入（今天才有的交叉面）──
add("KWK1", "权限档位", "MISS-RISK", "科尔坦家的账", [T_SECRET], "干净问法（阳性对照）", "profile.noble")
add("KWK2", "权限档位", "MISS-RISK", "戴·科尔坦家靠什么发财？", [T_SECRET], "真题问法（阳性对照）", "profile.noble")
add("KWK3", "权限档位", "MISS-RISK", "科尔坦家的帳", [T_SECRET], "繁体＋贵族（脏输入阳性对照）", "profile.noble")
add("KWK4", "权限档位", "OVER-RISK", "科尔坦家的账", [T_SECRET], "★ 平民问同一句：整条不该出现", "profile.commoner")
add("KWK5", "权限档位", "OVER-RISK", "科尔坦家的帳", [T_SECRET], "★ 平民＋繁体", "profile.commoner")
add("KWK6", "权限档位", "OVER-RISK", "戴·科尔坦家靠什么发财？", [T_SECRET], "★ 平民＋真题问法", "profile.commoner")
add("KWK7", "权限档位", "OVER-RISK", "沙拉斯", [T_SECRET], "★ 平民＋泛词（过匹配 × 权限）", "profile.commoner")
add("KWK8", "权限档位", "MISS-RISK", "毛皮值钱吗？", [T_FUR], "平民该拿到 rumor 档", "profile.commoner")

spec = {"queries": [
    {"name": "KW%s" % r["no"][2:] + "-" + r["group"] + "-" + r["text"][:16],
     "identity": r["identity"],
     "text": r["text"],
     "requested_detail": "secret"}
    for r in ROWS
]}

expect = [{"no": r["no"], "group": r["group"], "cat": r["cat"], "text": r["text"],
           "targets": r["targets"], "note": r["note"], "identity": r["identity"]}
          for r in ROWS]

with io.open("tools/_redtest_chain_spec_20260917.json", "w", encoding="utf-8") as fh:
    json.dump(spec, fh, ensure_ascii=False, indent=2)
    fh.write("\n")
with io.open("tools/_redtest_chain_expect_20260917.json", "w", encoding="utf-8") as fh:
    json.dump(expect, fh, ensure_ascii=False, indent=2)
    fh.write("\n")

groups = {}
for r in ROWS:
    groups[r["group"]] = groups.get(r["group"], 0) + 1
print("样本 %d 条：" % len(ROWS), "，".join("%s %d" % kv for kv in groups.items()))
print("MISS-RISK", sum(1 for r in ROWS if r["cat"] == "MISS-RISK"),
      "／ OVER-RISK", sum(1 for r in ROWS if r["cat"] == "OVER-RISK"))
