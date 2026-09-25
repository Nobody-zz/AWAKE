# -*- coding: utf-8 -*-
"""hero 档别名补全（21 个普通英雄：keywords<=3）。
身份词从档自身正文抽取：波耶/女贵族/勇士/汗/大那颜/贵族/将领/族长…
另补「文化＋身份」组合问法（如「斯特吉亚波耶」）。

试跑： python _uw_hero_aliases_probe_20260925.py
落盘： 同名脚本加 --apply（带备份）。
"""
import io, json, os, re, sys, shutil, collections

ROOT = r"D:\AWAKE-Dev\AWAKE"
FULL = os.path.join(ROOT, "tools", "worldbook-studio", "workspace", "full-geo1")
AUTH = os.path.join(FULL, "authoring")
PKG = os.path.join(FULL, "compiled", "geo1-v30-settle-alias")
if not os.path.isdir(PKG):
    PKG = os.path.join(FULL, "compiled", "geo1-v29-uw-wide")
ARCH = os.path.join(FULL, "_archive-20260925-hero-aliases")

# 身份词表：正文出现即登记
ROLE = [
    (r"波耶", ["波耶"]),
    (r"女贵族|女领主", ["女贵族"]),
    (r"勇士|战士|猛将", ["勇士"]),
    (r"大汗|可汗|汗国|汗的", ["可汗", "汗"]),
    (r"大那颜|那颜", ["那颜", "大那颜"]),
    (r"苏丹", ["苏丹"]),
    (r"族长|家主", ["族长"]),
    (r"将领|统兵|率军|统帅", ["将领"]),
    (r"贵族|贵胄|世族", ["贵族"]),
    (r"至高王|国王|皇帝|女皇|大公", ["君主"]),
]
# 文化词（由家族 code 前缀推）：从 title 里的"X家的"取家族，再由 registry 补文化 —— 此处直接用 summary 里的地名
# 🚨 护栏：靠近「征服/攻占/攻打/边疆/东部」的"帝国"是**宾语**不是身份（兀儿浑≠帝国可汗）；
#    靠近「抵御/对抗/防守/抵挡/敌/防」的是**敌对对象**不是归属（淮娅＝帝国人，非巴旦尼亚人）。
CULTURE_BAD = re.compile(
    r"(征服|攻占|攻打|击破|边疆|东部|西部|南部|北部|帝国边境|抵御|对抗|防守|抵挡|敌对|防范|戍卫)")


def cul_hit(b, pat):
    for m in re.finditer(pat, b):
        s = max(0, m.start() - 12)
        ctx = b[s:m.end() + 12]
        if CULTURE_BAD.search(ctx):
            continue
        return True
    return False


CULTURE = [
    (r"斯特吉亚", ["斯特吉亚"]),
    (r"阿塞莱", ["阿塞莱"]),
    (r"库赛特", ["库赛特"]),
    (r"帝国", ["帝国"]),
    (r"巴旦尼亚", ["巴旦尼亚"]),
    (r"瓦兰迪亚", ["瓦兰迪亚"]),
]


def zh(t):
    return (t.get("zh-CN") or "") if isinstance(t, dict) else (t or "")


def load_pkg():
    return json.load(io.open(os.path.join(PKG, "runtime.json"), encoding="utf-8")).get("entries") or []


def blob(x):
    parts = [zh(x.get("title")), zh(x.get("summary"))]
    for ex in (x.get("expressions") or []):
        parts.append(zh(ex.get("text")))
    return " ".join(p for p in parts if p)


def gen(x):
    t_zh = zh(x.get("title"))
    b = blob(x)
    zh_al = []
    for pat, names in ROLE:
        if re.search(pat, b):
            for n in names:
                if n not in zh_al:
                    zh_al.append(n)
    culs = []
    for pat, names in CULTURE:
        if cul_hit(b, pat):
            for n in names:
                if n not in culs:
                    culs.append(n)
    # 组合问法：文化＋身份
    rolenames = [n for _, ns in ROLE for n in ns if n in zh_al]
    for c in culs:
        for r in rolenames[:3]:
            v = c + r
            if v not in zh_al:
                zh_al.append(v)
    return t_zh, zh_al


def main():
    apply = "--apply" in sys.argv
    ents = load_pkg()
    rows = []
    for x in ents:
        i = str(x.get("id", ""))
        if "hero-" not in i:
            continue
        k = x.get("keywords") or []
        if len(k) > 3:
            continue
        slug = i.split("entry:")[-1].split(".", 1)[-1]
        if "probe" in slug:          # 旧探针档，不补
            continue
        t, al = gen(x)
        rows.append((slug, t, k, al))
    print("hero 待补 =", len(rows))
    for slug, t, k, al in rows:
        print("--- %-22s %s" % (t, al))
    if not apply:
        return
    if os.path.isdir(ARCH):
        shutil.rmtree(ARCH)
    shutil.copytree(AUTH, ARCH)
    print("已备份 ->", ARCH)
    n = 0
    for slug, t, k, al in rows:
        p = os.path.join(AUTH, slug + ".yaml")
        if not os.path.exists(p):
            print("!! 缺档", slug); continue
        txt = io.open(p, encoding="utf-8").read()
        m = re.search(r"^aliases:\n((?:[ \t].*\n)*)", txt, re.M)
        if m:
            old = re.findall(r"^  - (.*)$", m.group(0), re.M)
            merged = []
            for a in al + old:
                if a and a not in merged:
                    merged.append(a)
            blk = "aliases:\n  zh-CN:\n" + "".join("  - %s\n" % a for a in merged)
            txt2 = txt[:m.start()] + blk + txt[m.end():]
        else:
            cm = re.search(r"^content_tier:.*\n", txt, re.M)
            if not cm:
                print("!! 无 content_tier", slug); continue
            blk = "aliases:\n  zh-CN:\n" + "".join("  - %s\n" % a for a in al)
            txt2 = txt[:cm.end()] + blk + txt[cm.end():]
        if txt2 == txt:
            continue
        rm = re.search(r"^revision:\s*(\d+)\s*$", txt2, re.M)
        if rm:
            txt2 = txt2[:rm.start(1)] + str(int(rm.group(1)) + 1) + txt2[rm.end(1):]
        io.open(p, "w", encoding="utf-8", newline="\n").write(txt2)
        n += 1
    print("已改 =", n)


if __name__ == "__main__":
    main()
