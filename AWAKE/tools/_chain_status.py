# -*- coding: utf-8 -*-
"""链路现状读数：一眼看清「仓库上线包 vs 游戏目录包」差多少。可复用，不带日期。

为什么需要：回答"现在什么状况"时，**最大的坑是拿旧数字当现状**——
包是从 Studio 工作区编的、游戏目录那份可能停在几天前，两份都不是同一个东西。
所以任何现状汇报前，先跑这个，按**实测**说话。

用法：python tools/_chain_status.py
"""
import io, json, os, datetime, collections

REPO_PKG = r"D:\AWAKE-Dev\AWAKE\ModuleData\Worldbook\packages\calradia\runtime.json"
REPO_IDX = r"D:\AWAKE-Dev\AWAKE\ModuleData\Worldbook\packages\calradia\index.json"
GAME_ROOT = r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AWAKE\ModuleData\Worldbook"
WORKSPACE = r"D:\AWAKE-Dev\AWAKE\tools\worldbook-studio\workspace"


def walk(o):
    if isinstance(o, dict):
        oid = o.get("id")
        if isinstance(oid, str) and oid.startswith("awake:entry:"):
            yield o
        for v in o.values():
            yield from walk(v)
    elif isinstance(o, list):
        for v in o:
            yield from walk(v)


def stamp(p):
    if not os.path.exists(p):
        return "缺失"
    t = datetime.datetime.fromtimestamp(os.path.getmtime(p))
    return "%s（%s）" % (t.strftime("%m-%d %H:%M"), "%.1f MB" % (os.path.getsize(p) / 1048576.0))


def survey(label, path):
    print("=== %s ===" % label)
    print("  路径 %s" % path)
    print("  时间 %s" % stamp(path))
    if not os.path.exists(path):
        print("  —— 不存在 ——")
        return None
    ents = list(walk(json.load(io.open(path, encoding="utf-8"))))
    ids = set(e["id"] for e in ents)
    withref = [e for e in ents if ((e.get("extensions") or {}).get("entityRefs"))]
    dom = collections.Counter(e["id"].split(":")[-1].split(".")[0] for e in ents)
    print("  条目 %d   带 entityRefs %d（%.0f%%）" % (len(ents), len(withref), 100.0 * len(withref) / max(1, len(ents))))
    print("  顶层域 %s" % dict(dom.most_common(8)))
    return ids


repo = survey("仓库上线包 runtime.json", REPO_PKG)
survey("仓库 index.json", REPO_IDX)
game_pkg = os.path.join(GAME_ROOT, "packages", "calradia", "runtime.json")
game = survey("游戏目录包", game_pkg)

print()
print("=== Studio 工作区（包是从这里编的，未入库）===")
if os.path.isdir(WORKSPACE):
    subs = sorted(os.listdir(WORKSPACE))[:8]
    print("  存在；子项 %s%s" % (subs, " …" if len(os.listdir(WORKSPACE)) > 8 else ""))
else:
    print("  不存在")

# ---- K1 核查：keywords 里是否还混着「内部文档 id」 ----
# 背景：编译器有一处未提交改动（K1 修正 2026-09-16），把 sourceId 从 keywords 里去掉
#       —— id 不是玩家/NPC 会用的说法，且运行时只按「匹配串长度」排序，id 会凭串长压过真关键词。
#       若包里仍带着 id，说明**这份包不是现行编译器编的**，重编会变。
print()
print("=== K1 核查：包内 keywords 是否混入内部 id ===")


def k1_check(label, path):
    if not os.path.exists(path):
        print("  %s：包不存在" % label)
        return
    raw = json.load(io.open(path, encoding="utf-8"))
    ents = list(walk(raw))
    withid = []
    for e in ents:
        kws = e.get("keywords") or []
        hit = [k for k in kws if isinstance(k, str) and (k.startswith("awake:") or ".entry." in k or k == e["id"].split(":")[-1])]
        if hit:
            withid.append((e["id"].split(":")[-1], hit[:3]))
    print("  %s：%d/%d 条的关键词里带内部 id%s" % (label, len(withid), len(ents),
                                              ("；例 %s" % withid[:3]) if withid else " ⇒ **已应用 K1**"))


k1_check("仓库包", REPO_PKG)
k1_check("游戏目录包", game_pkg)

print()
print("=== 差异 ===")
if repo and game:
    print("  仓库 %d 条 ；游戏目录 %d 条 ；差 %+d" % (len(repo), len(game), len(repo) - len(game)))
    onlyrepo = sorted(repo - game)
    onlygame = sorted(game - repo)
    print("  只在仓库有 %d 条%s" % (len(onlyrepo), ("：%s" % ", ".join(x.split(":")[-1] for x in onlyrepo[:10])) if onlyrepo else ""))
    print("  只在游戏目录有 %d 条%s" % (len(onlygame), ("：%s" % ", ".join(x.split(":")[-1] for x in onlygame[:10])) if onlygame else ""))
else:
    print("  有一侧缺失，无法对比")
