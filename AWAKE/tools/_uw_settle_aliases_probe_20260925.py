# -*- coding: utf-8 -*-
"""聚落别名欠账：从档自身正文机械抽取产业/地形词 -> 合成 aliases。
本脚本只做「试跑打印」，不落盘。落盘在 _uw_settle_aliases_apply_20260925.py。
"""
import io, json, os, re, collections, sys

ROOT = r"D:\AWAKE-Dev\AWAKE"
PKG = os.path.join(ROOT, "tools", "worldbook-studio", "workspace", "full-geo1",
                   "compiled", "geo1-v29-uw-wide")
AUTH = os.path.join(ROOT, "tools", "worldbook-studio", "workspace", "full-geo1", "authoring")

# ---- 产业词表：正则片段 -> 规范别名（正文里出现即登记）----
# 顺序 = 优先级；一条正文常同时命中多个，全收。
# 🚨 收紧原则（09-25 实证）：正则命中 ≠ 语义成立。
#   ① 禁单字词（"边"会命中"旁边"）；② 否定语境须护栏（"别处种粮，这里种葡萄"）。
PROD = [
    (r"葡萄|酿酒|酒村|酒坊|甜酒|白葡萄酒|红酒|陈酿", ["葡萄", "酒"]),
    (r"育马|牧马|马驹|良马|马匹|骑兵马", ["马", "养马"]),
    (r"牧牛|奶牛|牛乳|黄油|养牛", ["牛", "牧牛"]),
    (r"牧羊|羊户|羊毛", ["羊", "牧羊"]),
    (r"粮作|粮村|粮产|粮源|黑麦|大麦|小麦|麦浪|收粮|老粮|产麦|种粮", ["粮", "麦"]),
    (r"盐田|取盐|盐货|盐村|盐价比|盐井|转盐|盐价", ["盐"]),
    (r"铁矿|矿石|矿沟|矿口|矿脉|银矿|浅层矿|矿山|铁价|粗铁|立村.*矿|矿.*立村", ["矿", "铁"]),
    (r"渔场|渔获|潟湖|湖口|网笼|鳟鱼|梭鱼|鲟鱼|比目鱼|沙丁|渔权|捕鱼", ["渔", "鱼"]),
    (r"橄榄", ["橄榄"]),
    (r"椰枣|枣树", ["枣", "椰枣"]),
    (r"奶酪|干酪", ["奶酪"]),
    (r"木料|林材|山毛榉|橡木|伐木", ["木", "木料"]),
    (r"毛皮|山货|狩猎|猎户", ["毛皮", "山货"]),
    (r"生丝|桑", ["丝"]),
    (r"亚麻|棉", ["棉麻"]),
    (r"黏土|陶户|泥坑|制砖", ["黏土", "陶"]),
    (r"木炭|炭窑", ["木炭"]),
    (r"香料|药草", ["香料"]),
    (r"毛毡", ["毛毡"]),
]
# ---- 地形/区位词表（同样禁单字）----
GEO = [
    (r"沙漠|绿洲|旱谷", ["沙漠", "绿洲"]),
    (r"高原|高地|山脊|山裂|山场|丘陵|谷口|山前|梯田", ["高原", "山地"]),
    (r"汇流|水运|湖道|渡口|临河|河坡|河湾|河灌|两河", ["河", "水路"]),
    (r"湾口|半岛|临海|背崖|滨海|浅滩|海岸", ["沿海", "海滨"]),
    (r"边境|边区|国之边|边地", ["边境"]),
]
# ---- 类属后缀（标题追加，形成"X村""X镇""X堡"等问法）----
SUFFIX = {"villages": ["村"], "towns": ["镇", "城"], "castles": ["堡", "城堡"]}


def load_entries():
    r = json.load(io.open(os.path.join(PKG, "runtime.json"), encoding="utf-8"))
    return r.get("entries") or []


def zh(t):
    if isinstance(t, dict):
        return t.get("zh-CN") or ""
    return t or ""


# ---- 城镇专属语汇（城镇正文句式与村庄不同：「是军市」「占X之利」「转运」）----
TOWN_EXTRA = [
    (r"军市|军需|具装|马弓手营|边警|烽火|补给", ["军市", "驻军"]),
    (r"商队|贸易|转运|集散|歇脚|商路|买卖|行当", ["商队", "贸易"]),
    (r"水运交汇|河口|湖口|港口|码头", ["港口", "转运"]),
    (r"行省|首府|省城|治所", ["首府"]),
    (r"城防|城墙|高塔|要塞|堡垒|要塞|围城", ["城防", "要塞"]),
    (r"通婚|同化|风俗|语言", ["风土"]),
]


def blob_of(x):
    parts = [zh(x.get("title")), zh(x.get("summary"))]
    for ex in (x.get("expressions") or []):
        parts.append(zh(ex.get("text")))
    return " ".join(p for p in parts if p)


NEG = re.compile(r"(别处|别的|不种|而非|不是|不像|哪有|哪能|谁种|与[^，。]{0,4}不同)")


def hit(blob, pat):
    """正文命中，但剔除「否定/对比」语境：命中点前后 12 字内有否定词则不算。"""
    for m in re.finditer(pat, blob):
        s = max(0, m.start() - 12)
        ctx = blob[s:m.end() + 12]
        if NEG.search(ctx):
            continue
        return True
    return False


def gen_aliases(x):
    i = str(x.get("id", ""))
    slug = i.split("entry:")[-1].split(".", 1)[-1]
    head = slug.split("-")[0]
    if head not in ("villages", "towns", "castles"):
        return None, None, None
    blob = blob_of(x)
    t_zh = zh(x.get("title"))
    t_en = zh(x.get("title")) if False else None
    # 英文标题：从 title 里取 en
    tt = x.get("title") or {}
    t_en = tt.get("en") or ""
    zh_al, en_al = [], []

    for pat, names in PROD:
        if hit(blob, pat):
            for n in names:
                if n not in zh_al:
                    zh_al.append(n)
    for pat, names in GEO:
        if hit(blob, pat):
            for n in names:
                if n not in zh_al:
                    zh_al.append(n)
    if head == "towns":
        for pat, names in TOWN_EXTRA:
            if hit(blob, pat):
                for n in names:
                    if n not in zh_al:
                        zh_al.append(n)
    # 类属后缀：给村名/镇名加"X村"等（若标题本身不含）
    for suf in SUFFIX[head]:
        v = t_zh + suf
        if suf not in t_zh and v not in zh_al:
            zh_al.append(v)
    return head, zh_al, en_al


def main():
    ents = load_entries()
    rows = []
    for x in ents:
        head, zh_al, en_al = gen_aliases(x)
        if head is None:
            continue
        k = x.get("keywords") or []
        if len(k) > 3:      # 已富裕的档不动
            continue
        rows.append((head, str(x.get("id")), zh(x.get("title")), k, zh_al))
    cnt = collections.Counter(r[0] for r in rows)
    print("待补档（keywords<=3）按类:", dict(cnt), "合计", len(rows))
    print()
    for head, i, t, k, al in rows[:15]:
        print("--- %s [%s]" % (t, head))
        print("    现有:", k)
        print("    建议:", al)


if __name__ == "__main__":
    main()
