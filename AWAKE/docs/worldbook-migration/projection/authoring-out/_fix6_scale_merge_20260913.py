# -*- coding: utf-8 -*-
"""规模收敛批：按裁定合并超限断言。
原则：只动断言结构与断言级文本（逐字取自原断言，不造新事实）；表达文本一律不改
（paravenos 表达全部原样保留，其余档被并组的同构表达合并文本、并集引文）。
矩阵命中面（I1-I5=paravenos 表达文本）零改动。
"""
import io, yaml, copy, json

BASE = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out/"

def uniq_sources(srcs):
    out, seen = [], set()
    for s in srcs:
        k = s.get("quote_hash") or s.get("quote")
        if k in seen:
            continue
        seen.add(k)
        out.append(s)
    return out

def merge_group(doc, idxs, new_text, merge_exprs, expr_joiner=""):
    """idxs: 该档 assertions 列表内下标组；返回合并后的单断言。"""
    group = [doc["assertions"][i] for i in idxs]
    head = copy.deepcopy(group[0])
    head["text"]["zh-CN"] = new_text
    # 断言级引文并集
    all_srcs = []
    for a in group:
        all_srcs += a.get("sources", [])
    head["sources"] = uniq_sources(all_srcs)
    if merge_exprs:
        exprs = []
        for a in group:
            exprs += a.get("expressions", [])
        # 按 (layer, grants指纹) 分组，组内合并文本与引文
        def fp(e):
            return (e["layer"], json.dumps(sorted(
                (g["profile_id"], g["scope"], g["min_detail"], json.dumps(g.get("culture_ids") or []))
                for g in e.get("grants", [])), ensure_ascii=False))
        buckets, order = {}, []
        for e in exprs:
            k = fp(e)
            if k not in buckets:
                buckets[k] = copy.deepcopy(e)
                order.append(k)
            else:
                b = buckets[k]
                if b["text"]["zh-CN"] != e["text"]["zh-CN"]:
                    b["text"]["zh-CN"] = b["text"]["zh-CN"] + expr_joiner + e["text"]["zh-CN"]
                b["sources"] = uniq_sources(b.get("sources", []) + e.get("sources", []))
        head["expressions"] = [buckets[k] for k in order]
    else:
        kept = []
        for a in group:
            kept += a.get("expressions", [])
        head["expressions"] = kept
    return head

# ---- 合并 spec：file -> (组下标列表, 每组新断言文本 or None=取组内第一条, 是否并表达) ----
def T(fn, *texts):
    return "".join(dict.fromkeys(texts))  # 去重保序拼接（同句只留一次）

SPEC = {
    # charas-bay 4→3：群岛挡浪+海峡要道 归"湾澳形胜"
    "charas-bay.yaml": {
        "groups": [
            ([0], None, False),
            ([1, 2], "南侧群岛罗列，替湾子挡住外海涌浪，湾里水面平缓，泊船与近岸行船都安稳；海峡内通湾水、外连大洋，是这片水进出的要道。", True),
            ([3], None, False),
        ],
        "keep_ids": ["assertion.sara-bay-1", "assertion.sara-bay-2", "assertion.sara-bay-4"],
    },
    # der-furs 6→3：生计+海豹油 / 皮货去向 / 道路
    "der-furs.yaml": {
        "groups": [
            ([0, 1, 4], "村民上山下海讨生活：山里捕河狸、水貂，海豹则要到湾里捉；毛皮和油脂是村里的主要出产，海豹油点灯，烟少火亮。", True),
            ([2, 3], "海豹的皮货在瓦尔切格湾那片水面上有名，进的是湾内各处的交易；做成斗篷顶得住水汽，卡琉斯堡守军过冬的皮袄，有些料子就出自德里亚特。", True),
            ([5], None, False),
        ],
        "keep_ids": ["assertion.der-furs-1", "assertion.der-furs-3", "assertion.der-furs-6"],
    },
    # kach-land 5→3：地貌港位（消重复）/ 战略 / 骑兵判断
    "kach-land.yaml": {
        "groups": [
            ([0, 1, 2], "卡恰尔半岛从伊卡拉荒原探进海里，是一条狭长的石地，恰好横在南北两片海域中间。地面多是石头和陡崖，种不出庄稼，能泊大船的港位没有几处。", True),
            ([3], None, False),
            ([4], None, False),
        ],
        "keep_ids": ["assertion.kach-land-1", "assertion.kach-land-4", "assertion.kach-land-5"],
    },
    # kach-tales 4→2：三方讲法归一组（3 表达原样保留）/ 互不对账
    "kach-tales.yaml": {
        "groups": [
            ([0, 1, 2], "巴旦尼亚、诺德、斯特吉亚三方对半岛易主各有讲法：巴旦尼亚叙述归因于借斯特吉亚人清除诺德人后反失其地；诺德叙述归因于巴旦尼亚人设局、雇军反噬；斯特吉亚叙述归因于两方相争而坐收其成。", False),
            ([3], None, False),
        ],
        "keep_ids": ["assertion.kach-tales-1", "assertion.kach-tales-4"],
    },
    # lac-lake 6→3：水陆与生计 / 冬季活水与商货 / 兵志
    "lac-lake.yaml": {
        "groups": [
            ([0, 1, 3], "拉科尼斯湖在卡拉迪亚北部，四面为山地与森林环抱，是一大片封闭的内陆淡水湖；弥戎河与喀拉卡兹河从不同方向注入，把它接进北方的内河航运。湖岸的营生三样：打鱼、行船、码头扛活。", True),
            ([2, 4], "湖水终年不冻。入冬后四邻的河道封冻、道路被大雪隔断，湖上的船却照常往来，是北方冬季少有的活水路；帝国出产的麦子由水路北运，斯特吉亚的毛皮顺同一条水路南回，两头商货都在湖上转运。", True),
            ([5], None, False),
        ],
        "keep_ids": ["assertion.lac-lake-1", "assertion.lac-lake-3", "assertion.lac-lake-6"],
    },
    # paravenos：断言1+2 归沿革（4 表达原样）；断言3 拆 民间三方(3 rumor) / 治理与深层(2 detail)
    "paravenos.yaml": {
        "groups": [
            ([0, 1],
             "巴拉维诺斯（Paravenos）是卡拉狄乌斯大帝亲手建立的第二座重要殖民地，曾取代沙拉斯成为卡拉德人的首都；后来帝国统治重心东移。瓦兰迪亚人「铁臂」奥斯里克入侵时，认定此城作为权力宝座贵于作为掠获财源，与城中元老协商献城；城归奥斯里克旁支戴·提尔家，改今名帕拉汶德。",
             False),
            ([2],
             "如今的帕拉汶德是多重记忆叠在一座城里：瓦兰迪亚人当它是自己的西部王冠；帝国遗民记得它的旧名与沦陷；巴旦尼亚山民只当它是砍圣林、铺石路的异族据点。",
             False),
            ([2],
             "城中的治理与深层评价：市场、法庭、竞技场归瓦兰迪亚贵族掌管；帝国贵族私下仍以旧名相称，把易帜视作帝国走向衰落的又一块界碑。",
             False),
        ],
        "keep_ids": ["assertion.paravenos-1", "assertion.paravenos-3", "assertion.paravenos-4"],
        "split_group2": True,  # 第2组(断言3)按下标拆：组内 [0,1,2] rumor 表达给前档，[3,4] detail 给新档
    },
}

for fn, spec in SPEC.items():
    path = BASE + fn
    doc = yaml.safe_load(io.open(path, encoding="utf-8"))
    old_n = len(doc["assertions"])
    asserts = []
    for gi, (idxs, new_text, merge_e) in enumerate(spec["groups"]):
        if spec.get("split_group2") and gi == 2:
            # paravenos 拆组：断言3 的表达 0-2(rumor) 归"民间"，3-4(detail) 归"深层"
            a3 = doc["assertions"][2]
            rumor_exprs = [e for e in a3["expressions"] if e["layer"] == "rumor"]
            detail_exprs = [e for e in a3["expressions"] if e["layer"] != "rumor"]
            a_deep = {
                "id": spec["keep_ids"][2],
                "revision": 1,
                "kind": "fact",
                "text": {"zh-CN": spec["groups"][2][1]},
                "sources": uniq_sources(sum([e.get("sources", []) for e in detail_exprs], [])),
                "expressions": detail_exprs,
            }
            # 前一档(民间)补 rumor 表达
            asserts[-1]["expressions"] = rumor_exprs
            asserts[-1]["sources"] = uniq_sources(asserts[-1].get("sources", []) + a3.get("sources", []))
            asserts.append(a_deep)
            continue
        g = merge_group(doc, idxs, new_text, merge_e)
        if new_text is None:
            g["text"]["zh-CN"] = doc["assertions"][idxs[0]]["text"]["zh-CN"]
        g["id"] = spec["keep_ids"][gi]
        asserts.append(g)
    doc["assertions"] = asserts
    doc["revision"] = int(doc.get("revision", 1)) + 1
    io.open(path, "w", encoding="utf-8", newline="\n").write(
        yaml.dump(doc, allow_unicode=True, sort_keys=False, width=120))
    print(f"{fn}: 断言 {old_n} -> {len(asserts)}  rev={doc['revision']}")
    for a in doc["assertions"]:
        print(f"    {a['id']}  expr={len(a.get('expressions', []))}")
print("SCALE-MERGE-DONE")
