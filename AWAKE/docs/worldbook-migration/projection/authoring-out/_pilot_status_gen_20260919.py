# -*- coding: utf-8 -*-
"""试点：给 3 档聚落按「原文自己的口气」重判可信度（assertions[].kind），并补上对应的口吻表达。

判据（可核，全部落在来源原文的字面上）：
  ① `fact`          —— 原文平铺直叙，无存疑标记。
  ② `rumor`         —— 原文写「据传说 / 有数种……没有任意两种对得上 / 人们相信」，即原文自认无凭据。
  ③ `interpretation`—— 原文写「名声 / 主流的宣传说法 / 只是出于……敬畏」，即这是某方的解释或立场。

不改现役档：读现役档 → 只换 `assertions` 段 → 写到 pilot 目录。其余字段逐字保留。
每个新 quote 必须能在来源文件里逐字定位（构建期硬断言，定位不到即抛错）。
"""
import glob
import hashlib
import io
import json
import os

import yaml

AUTH = r"D:\AWAKE-Dev\AWAKE\docs\worldbook-migration\projection\authoring-out"
SRC = r"D:\AWAKE-Dev\AWAKE\tools\worldbook-studio\workspace\full-geo1\authoring\sources"
OUT = r"D:\AWAKE-Dev\AWAKE\docs\worldbook-migration\pilot-status-20260919"

# ---- 来源登记：source_id -> (locator_root, 全文) ----
REG = {}
for f in glob.glob(os.path.join(SRC, "source-*.yaml")):
    d = yaml.safe_load(io.open(f, encoding="utf-8"))
    root = d.get("locator_root")
    if not root:
        continue
    p = os.path.join(SRC, root)
    if os.path.exists(p):
        REG[d["source_id"]] = (root, io.open(p, encoding="utf-8").read())


def qh(text):
    return hashlib.sha256(text.encode("utf-8")).hexdigest().upper()


class NoAlias(yaml.dumper.Dumper):
    def ignore_aliases(self, data):
        return True


# ============ 手写判层 ============
# 每档：locator 用现役档里的第一条（同来源同文件），quotes 逐句取自官方原文。
# expr 的 grants 由现役档里同 layer 的那条原样复制，不手抄。

PILOT = {}

PILOT["villages-diantogmail.yaml"] = {
    "why": "原文第二句自带「有数种本地传说……没有任意两种版本的细节能对得上」——说法不一，是原文自己承认的。",
    "asserts": [
        {
            "id": "assertion.village-diantogmail-1",
            "kind": "fact",
            "text": "迪安托格麦尔在“林·泰瓦尔”，也就是暗湖之畔；村民自湖岸取黏土，供附近的工匠制器。",
            "quotes": [
                "迪安托格麦尔位于“林·泰瓦尔”，即“暗湖”之畔。",
                "这里的村民会从湖边收集黏土，供附近的工匠使用。",
            ],
            "exprs": [
                {"id": "expr.village-diantogmail-1-rumor", "layer": "rumor", "grants": "low",
                 "text": "暗湖边上就是我们村，走一段滩就到。滩上有挖泥的坑，谁家要烧罐子就下来挑。"},
                {"id": "expr.village-diantogmail-detail", "layer": "detail", "grants": "high",
                 "text": "迪安托格麦尔以湖泥为业——岸滩淤土、细而耐烧；泥按细粗分等，供附近匠人制器，一年到头有外村人来挑。"},
            ],
        },
        {
            "id": "assertion.village-diantogmail-2",
            "kind": "rumor",
            "text": "“白鼬之翔”是村名的通行解，但本地说法有好几种，没有两种的细节能彼此对上。",
            "quotes": [
                "有数种本地传说解释该地名字的由来——意为“白鼬之翔”，但没有任意两种版本的细节能对得上。",
            ],
            "exprs": [
                {"id": "expr.village-diantogmail-2-rumor", "layer": "rumor", "grants": "low",
                 "text": "迪安托格麦尔的人自己也说不清村名。村里人说祖宗留下的名字，意思记不全了——反正泥是真的，拿来有用。"},
                {"id": "expr.village-diantogmail-2-detail", "layer": "detail", "grants": "high",
                 "text": "村名解作“白鼬之翔”，说法却不止一种，讲的人各执一词。外人来打听，本地人也乐意各讲各的。"},
            ],
        },
    ],
}

PILOT["towns-pen-cannoc.yaml"] = {
    "why": "原文三句分三种性质：位置是事实；女神一句自带「据传说」；末句自带「这些渔人口里的传言只是出于……敬畏」，是原文在解释传言的来由。",
    "asserts": [
        {
            "id": "assertion.town-pen-cannoc-1",
            "kind": "fact",
            "text": "彭·坎诺克坐落于高山之巅，俯瞰特朗河自乌卡利翁高原直奔瓦兰迪亚的低地。",
            "quotes": [
                "彭·坎诺克坐落于高山之巅，注视着特朗河自乌卡利翁高原奔腾涌向瓦兰迪亚的低地。",
            ],
            "exprs": [
                {"id": "expr.town-pen-cannoc-1-rumor", "layer": "rumor", "grants": "low",
                 "text": "彭·坎诺克在顶上，往下看就是特朗河。河打高原上下来，水急，翻白花。"},
                {"id": "expr.town-pen-cannoc-1-detail", "layer": "detail", "grants": "high",
                 "text": "彭·坎诺克扼特朗河上游——山地水路的龙头，放排下行的货在此集结；滩口窄，枯水期是抢运的关口。"},
            ],
        },
        {
            "id": "assertion.town-pen-cannoc-2",
            "kind": "rumor",
            "text": "据传说，山岩下的暗池里住着水之女神，她把旅人与闯入者诱入死地；极少数时候她与某个英雄相恋，赐他胜利与福分。",
            "quotes": [
                "据传说，水之女神生活在山岩之下的暗池里面，诱惑着旅行者和闯入者踏入死地，只有很少的情况下女神会和一位英雄坠入爱河，赠予他胜利和赐福。",
            ],
            "exprs": [
                {"id": "expr.town-pen-cannoc-2-rumor", "layer": "rumor", "grants": "low",
                 "text": "山底下暗池里住着水娘娘，贪心的旅人会被拖下去。渔夫们说那不是神怪，是那条河真吃人。"},
                {"id": "expr.town-pen-cannoc-2-detail", "layer": "detail", "grants": "high",
                 "text": "女神的故事有讲究：落水的、失足的，都记在她名下。行船的人敬她，说敬过河的人少翻船。"},
            ],
        },
        {
            "id": "assertion.town-pen-cannoc-3",
            "kind": "interpretation",
            "text": "渔人之间这套传言，出自对特朗河上游的敬畏——石面滑腻，一步踩错便落入急流。",
            "quotes": [
                "当然，这些渔人口里的传言只是出于对特朗河上游的敬畏，因为在滑腻的石子上着错一步脚，人就会掉落到湍急的水里。",
            ],
            "exprs": [
                {"id": "expr.town-pen-cannoc-3-detail", "layer": "detail", "grants": "high",
                 "text": "彭·坎诺克的女神传说，是行船人的规矩来源——雇船先敬河，纠纷少一半。这层说法的底子是：石面滑，人下去就上不来。"},
            ],
        },
    ],
}

PILOT["towns-amitatys.yaml"] = {
    "why": "原文自己写明两处不是事实：开头说暴君之名是「极为糟糕的名声」；末句说归咎天怒是「帝国在该地区站稳脚跟的主流宣传说法」。",
    "asserts": [
        {
            "id": "assertion.town-amitatys-1",
            "kind": "fact",
            "text": "阿弥塔堤斯原为帕拉酋长所据；他与帝国军队起冲突，围城时地震震塌城墙，城破后遭烧杀抢掠，死者多于该酋长亲手所杀。",
            "quotes": [
                "他不可避免地和帝国的军队发生了冲突，在围城期间，一场地震震塌了城墙，帝国士兵得以进入城内。",
                "在接下来的烧杀抢掠中，死去的民众比这位暴君亲手杀的还要多，但这就是战争。",
            ],
            "exprs": [
                {"id": "expr.town-amitatys-1-rumor", "layer": "rumor", "grants": "low",
                 "text": "阿弥塔堤斯的城墙是叫地震震塌的，兵从缺口进去。这一场下来，死的人比那酋长生前杀的还多。"},
                {"id": "expr.town-amitatys-1-detail", "layer": "detail", "grants": "high",
                 "text": "阿弥塔堤斯城防重建百年后，市面以农牧与零工为主；做买卖不碰旧账，提地震与围城都算犯忌。"},
            ],
        },
        {
            "id": "assertion.town-amitatys-2",
            "kind": "interpretation",
            "text": "该城帕拉酋长以暴君之名载于记述——说他为求永生而屠戮百姓，以取悦冥界诸神。",
            "quotes": [
                "阿弥塔堤斯的帕拉酋长有着极为糟糕的名声，他是一名暴君，为了追求永生，他不惜屠戮无辜的百姓，只为取悦冥界诸神。",
            ],
            "exprs": [
                {"id": "expr.town-amitatys-2-rumor", "layer": "rumor", "grants": "low",
                 "text": "都说这城的旧主是个狠人，杀人不眨眼，为的是求长生。这名声传了几代，谁也不细问。"},
                {"id": "expr.town-amitatys-2-detail", "layer": "detail", "grants": "high",
                 "text": "阿弥塔堤斯的旧主在记述里是暴君——求永生、屠百姓、祭冥神。这三样在纸上连成一串，读的人自己掂量。"},
            ],
        },
        {
            "id": "assertion.town-amitatys-3",
            "kind": "interpretation",
            "text": "有人把这场屠掠归咎于上天降下的怒火；这也是帝国在当地站稳脚跟的主流宣传说法。",
            "quotes": [
                "有人将此结果归咎于上天降下的怒火，而这也是帝国在该地区站稳脚跟的主流宣传说法。",
            ],
            "exprs": [
                {"id": "expr.town-amitatys-3-detail", "layer": "detail", "grants": "high",
                 "text": "阿弥塔堤斯有一说叫“天罚说”：屠掠是上天降的怒火。这说法在帝国站脚的当口传得最顺。"},
            ],
        },
    ],
}

# ============ 生成 ============
os.makedirs(OUT, exist_ok=True)
report = []

for fname, spec in PILOT.items():
    src_path = os.path.join(AUTH, fname)
    doc = yaml.safe_load(io.open(src_path, encoding="utf-8"))

    # 取两个 grants 模板：低层（layer=rumor）/ 高层（layer=detail）
    grants_tpl = {}
    for a in doc["assertions"]:
        for e in a.get("expressions") or []:
            grants_tpl.setdefault(str(e.get("layer")), e.get("grants") or [])
    assert "rumor" in grants_tpl and "detail" in grants_tpl, (fname, list(grants_tpl))
    # 档位 -> 按「表达层」复制的授权名单，不手抄
    TPL = {"low": grants_tpl["rumor"], "high": grants_tpl["detail"]}

    # 用第一条 source 的 (source_id, locator, source_version, source_content_hash) 作模板
    s0 = doc["sources"][0]
    sid = s0["source_id"]
    locator = s0["locator"]
    sver = s0["source_version"]
    shash = s0["source_content_hash"]
    root, body = REG[sid]

    asserts = []
    for a in spec["asserts"]:
        srcs = []
        for q in a["quotes"]:
            assert q in body, "quote 定位不到 | %s | %s" % (fname, q[:40])
            srcs.append({
                "source_id": sid,
                "source_version": sver,
                "source_content_hash": shash,
                "locator": locator,
                "quote_hash": qh(q),
                "quote": q,
            })
        exprs = []
        for e in a["exprs"]:
            exprs.append({
                "id": e["id"],
                "revision": 1,
                "layer": e["layer"],
                "text": {"zh-CN": e["text"]},
                "sources": [dict(srcs[0])],
                "grants": [dict(g) for g in TPL[e["grants"]]],
                "denies": [],
            })
        asserts.append({
            "id": a["id"],
            "revision": 1,
            "kind": a["kind"],
            "text": {"zh-CN": a["text"]},
            "sources": srcs,
            "expressions": exprs,
        })

    old_kinds = [a.get("kind") for a in doc["assertions"]]
    doc["assertions"] = asserts
    out_path = os.path.join(OUT, fname)
    text = yaml.dump(doc, Dumper=NoAlias, allow_unicode=True, default_flow_style=False,
                     sort_keys=False, width=100000, indent=2)
    assert "&id" not in text
    io.open(out_path, "w", encoding="utf-8", newline="\n").write(text)
    report.append({
        "file": fname,
        "doc_id": doc["id"],
        "revision": doc["revision"],
        "why": spec["why"],
        "kinds_before": old_kinds,
        "kinds_after": [a["kind"] for a in asserts],
        "asserts": len(asserts),
        "exprs": sum(len(a["expressions"]) for a in asserts),
        "source": root,
    })

print(json.dumps(report, ensure_ascii=False, indent=2))
print()
print("产物目录:", OUT)
for f in sorted(os.listdir(OUT)):
    print("  %-36s %6d B" % (f, os.path.getsize(os.path.join(OUT, f))))
