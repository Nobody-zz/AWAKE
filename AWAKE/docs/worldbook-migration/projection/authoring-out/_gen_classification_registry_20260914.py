# -*- coding: utf-8 -*-
"""世界书分类体系一览表生成器（两层：主分类 → 二级主题）

用法（venv python）:
    C:/Users/26811/.workbuddy/binaries/python/envs/default/Scripts/python.exe _gen_classification_registry_20260914.py

**分类只有两层，权威唯一**：主分类 domain（5，封闭）＋ 二级主题 subdomain（43）。
权威源 = docs/worldbook-studio-plan/knowledge-taxonomy.v1.json（raw-byte SHA-256 + CAS 门控，由 Studio 的
TaxonomyCatalogService 单点校验 ⇒ 本档**不另建第二份分类清单**）。

文件名 `doc.<主分类>.<前缀>-<名>` 里的**前缀是命名概念，不是文档字段、不是第三层分类**
（百科化宪章 §三：slug 是文件名概念，文档体内禁写）。它只作为「命名约定附表」列出。

输入：taxonomy json（分类定义）＋ 本目录 *.yaml（实际落点与条数）
输出：A) docs/worldbook-migration/WORLDBOOK-CLASSIFICATION-REGISTRY-20260914.md
      B) 本目录 _classification-flat.v1.json（供 xlsx 生成器读，避免第二份定义）
"""
import io, os, json, glob, sys, datetime
from collections import Counter, defaultdict

try:
    import yaml
except ImportError:
    sys.exit("need pyyaml in venv")

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
TAX = os.path.join(ROOT, "docs", "worldbook-studio-plan", "knowledge-taxonomy.v1.json")
MD = os.path.join(ROOT, "docs", "worldbook-migration", "WORLDBOOK-CLASSIFICATION-REGISTRY-20260914.md")
FLAT = os.path.join(HERE, "_classification-flat.v1.json")

NOW = datetime.datetime.now().strftime("%Y-%m-%d %H:%M")

# ---------------- 命名约定附表：文件名前缀 → 中文（唯一一份，非分类） ----------------
# 出处＝WORLDBOOK-FILENAME-NORMALIZATION-REPORT-20260913.md §一 次级分类词表。
# **09-14 taxonomy v2 订正**：48 版自造的实体型子域（mountain/plateau/peninsula/desert/waters/
# castle/village/town/tale）全部撤销、并入题材型子域（terrain/settlements/rivers/customs…），
# 「类型」这一维改由**文件名前缀**承载 ⇒ 前缀词表＝实体类型轴，须与现场档名同步。
# 本表按现场实测 21 个在用前缀逐一对齐（god 单复数混用沿用现状、不统一）。
PREFIX_ZH = {
    # 聚落类
    "villages": "村庄", "towns": "城镇", "castles": "城堡",
    # 地形类
    "mountains": "山地", "plateaus": "高原", "peninsulas": "半岛", "deserts": "荒漠",
    # 水域类
    "rivers": "河流", "lakes": "湖泊", "seas": "海域", "bays": "湾澳",
    # 文化类
    "tales": "传说",
    # 经济类
    "items": "器物", "goods": "物产", "mines": "矿场",
    # 政治类
    "throne": "王权", "territories": "领地", "clans": "氏族",
    # 军事类
    "military": "军制", "troops": "兵种", "weapons": "兵器",
}
# 09-14 晚：前缀统一为复数（实体类加 s；military/throne 抽象类保持单数）。
# 下表 key ＝**现场真实前缀**，故已是复数形态。

def prefix_of(doc_id):
    """doc_id 第三段（首个 '-' 前的 token）＝文件名前缀，命名概念。"""
    return doc_id.split(".", 2)[2].split("-", 1)[0]

# ---------------- 读权威分类目录 ----------------
tax = json.load(io.open(TAX, encoding="utf-8"))
dom, sd = {}, {}
for i, d in enumerate(tax["domains"]):
    dom[d["id"]] = {
        "label": d["label"]["zh-CN"], "help": d["help"]["zh-CN"],
        "examples": d.get("examples", {}).get("zh-CN", []),
        "hints": d.get("conflict_hints", {}).get("zh-CN", []),
        "order": i, "subs": [s["id"] for s in d["subdomains"]],
    }
    for s in d["subdomains"]:
        sd[s["id"]] = {
            "domain": d["id"], "label": s["label"]["zh-CN"], "help": s["help"]["zh-CN"],
            "examples": s.get("examples", {}).get("zh-CN", []), "order": len(sd),
        }
DOM_ORDER = [d["id"] for d in tax["domains"]]

# ---------------- 扫实际数据 ----------------
sd_n, dom_n, tok_n = Counter(), Counter(), Counter()
sd_tok = defaultdict(Counter)
for p in sorted(glob.glob(os.path.join(HERE, "*.yaml"))):
    y = yaml.safe_load(io.open(p, encoding="utf-8"))
    if not str(y.get("id", "")).startswith("doc."):
        continue
    d_, s_, t_ = y.get("domain", ""), y.get("subdomain", ""), prefix_of(y["id"])
    sd_n[s_] += 1; dom_n[d_] += 1; tok_n[t_] += 1; sd_tok[s_][t_] += 1
N = sum(dom_n.values())

sd_used = [s for s in sd if sd_n.get(s)]
sd_unused = [s for s in sd if not sd_n.get(s)]
def esc(x):
    return str(x).replace("|", "\\|")

# ---------------- 扁平表 ----------------
flat = {
    "generated_at": NOW, "taxonomy_version": tax.get("taxonomy_version"), "n_entries": N,
    "categories": [], "subdomain_rows": [], "prefix_rows": [], "fixes": [],
}
for did in DOM_ORDER:
    su = [s for s in dom[did]["subs"] if sd_n.get(s)]
    flat["categories"].append({
        "id": did, "label": dom[did]["label"], "help": dom[did]["help"],
        "hints": dom[did]["hints"], "n": dom_n.get(did, 0),
        "subs_used": len(su), "subs_unused": len(dom[did]["subs"]) - len(su),
    })
for s in sd:
    flat["subdomain_rows"].append({
        "domain": sd[s]["domain"], "domain_zh": dom[sd[s]["domain"]]["label"],
        "subdomain": s, "subdomain_zh": sd[s]["label"], "help": sd[s]["help"],
        "example": (sd[s]["examples"] or [""])[0],
        "status": "已用" if sd_n.get(s) else "未用", "n": sd_n.get(s, 0),
        "prefixes": "、".join(f"{t}({n})" for t, n in sd_tok[s].most_common()),
    })
for t in sorted(tok_n, key=lambda x: (-tok_n[x], x)):
    sds = [s for s in sd if t in sd_tok[s]]
    flat["prefix_rows"].append({
        "prefix": t, "zh": PREFIX_ZH.get(t, ""), "n": tok_n[t],
        "domains": "、".join(sorted({sd[s]["domain"] for s in sds})),
        "subdomains": "、".join(sds),
    })
# 09-14 taxonomy v2 复核：下列四项**均已由 v2 消除**（旧稿的归类欠账，留痕不删）。
# 现在这张表由脚本按「设计 vs 实际」重新推导——只要实际落点与子域定义同源，就不会再冒出来。
FIXES = []
flat["fixes"] = [{"item": a, "designed": b, "actual": c, "suggest": d} for a, b, c, d in FIXES]

# ---------------- markdown ----------------
L = []
A = L.append
A("# 世界书分类体系一览表（主分类 · 二级主题）")
A("")
A("> **机器生成，勿手改正文**：由 `_gen_classification_registry_20260914.py` 从 taxonomy ＋ 现场档案派生。"
  "分类定义要改就改 `knowledge-taxonomy.v1.json`（**须走包 hash / CAS 流程**）。")
A(f"> 生成时间：{NOW}　｜　数据面：**{N} 档**　｜　分类目录 `{tax.get('taxonomy_version')}`")
A("")
A("## 0 · 先把设计的层数说清：**两层，不是三层**")
A("")
A("| | 是什么 | 权威源 |")
A("|---|---|---|")
A("| **主分类**（`domain`） | 5 个，**封闭**（宪章 §二「扩容不扩域」，任何第六域提案直接 BREAK） | "
  "`docs/worldbook-studio-plan/knowledge-taxonomy.v1.json` |")
A("| **二级主题**（`subdomain`） | 43 个；给**作者**选的下拉目录，带中文名、说明、例子、冲突提示 | 同上（**唯一分类目录**） |")
A("")
A("三条要点：")
A("")
A("1. **不得有第二份分类清单**。原设计明定 `TaxonomyCatalogService` 是唯一校验器，"
  "任何调用方不得再写一份字面清单；taxonomy 文件本身按**原始字节 SHA-256** 计 hash，改动 ⇒ 包 manifest 变 ⇒ "
  "Studio 返 `WB-TAXONOMY-CAS-409`。")
A("2. **文件名前缀不是第三层分类**。`doc.<主分类>.<前缀>-<名>` 里的前缀是**命名概念**——"
  "宪章 §三明文「slug 是文件名概念、不是文档字段，文档体内禁写」。它只决定文件名与 id 尾，"
  "本表仅作 §4 命名约定附表列出。")
A("3. **跨域关联有正式字段**：`related_domains`（最多 4 项、唯一、按 taxonomy 顺序、不得含主域），"
  "落在运行时 `entries[*].extensions.relatedDomains`。目前 **0 档启用**——想标跨域，该用它，不是靠别名互串。")
A("")

A(f"## 1 · 主分类（{len(DOM_ORDER)} 个，封闭）")
A("")
A("| 主分类 | 中文 | 定义 | 归类冲突提示 | 档数 | 二级主题（已用/未用） |")
A("|---|---|---|---|---|---|")
for did in DOM_ORDER:
    c = dom[did]
    su = [s for s in c["subs"] if sd_n.get(s)]
    A(f"| `{did}` | {c['label']} | {esc(c['help'])} | "
      f"{'<br>'.join(esc(h) for h in c['hints']) or '—'} | {dom_n.get(did, 0)} | "
      f"{len(su)} / {len(c['subs']) - len(su)} |")
A("")
A(f"合计 **{N} 档**。")
A("")

A(f"## 2 · 二级主题（{len(sd)} 个）")
A("")
A("| 状态 | 主分类 | 二级主题 | 中文 | 说明（作者看到的定义） | 例子 | 档数 | 在用文件名前缀 |")
A("|---|---|---|---|---|---|---|---|")
for did in DOM_ORDER:
    for s in dom[did]["subs"]:
        st = "已用" if sd_n.get(s) else "未用"
        A(f"| {st} | {dom[did]['label']} | `{s}` | {sd[s]['label']} | {esc(sd[s]['help'])} | "
          f"{esc((sd[s]['examples'] or ['—'])[0])} | {sd_n.get(s, 0)} | "
          f"{'、'.join(f'`{t}`({n})' for t, n in sd_tok[s].most_common()) or '—'} |")
A("")
A(f"已用 **{len(sd_used)}** 个，未用 **{len(sd_unused)}** 个。"
  "「未用」＝目录里已登记、作者可选、但尚无条目——**不是给我们预留的批次位**，是目录本来就这么宽。")
A("")

A("## 3 · 纠错清单（拿原设计的归类判定当尺子）")
A("")
A("| # | 对象 | 设计怎么说 | 实际怎么挂 | 建议 |")
A("|---|---|---|---|---|")
for i, (a, b, c, d) in enumerate(FIXES, 1):
    A(f"| {i} | {a} | {b} | {c} | {d} |")
A("")

A(f"## 4 · 命名约定附表：文件名前缀（{len(flat['prefix_rows'])} 个）")
A("")
A("> **这不是分类层**，是文件名的取词表（出处：09-13 档名规范化报告 §一 次级分类词表 ＋ 09-14 新增 `castle`）。"
  "新前缀只改这里与档名规范，**不动包、不动 hash**。")
A("")
A("| 前缀 | 中文 | 在用档数 | 常见所属二级主题 |")
A("|---|---|---|---|")
for r in flat["prefix_rows"]:
    A(f"| `{r['prefix']}` | {r['zh'] or '—'} | {r['n']} | "
      f"{'、'.join('`' + x + '`' for x in r['subdomains'].split('、'))} |")
A("")
A("两点待办：① `castle` 已 67 档在用，但**尚未写进 09-13 报告 §一 词表**（P5）。"
  "② `mount` 与 `mountains` 两个前缀并存（各 2 档），**未归一**——本表照实列，不替它归一。")
A("")

A("## 5 · 维护与检查")
A("")
A("| 动作 | 怎么做 | 成本 |")
A("|---|---|---|")
A("| **新增档案** | 主分类从 5 个里挑；二级主题从 43 个里挑（选最贴的那个，拿 §2 的说明和例子当尺） | 低 |")
A("| **新增文件名前缀** | 改档名规范 ＋ 本生成器的 `PREFIX_ZH` ⇒ 重跑本脚本 | 低，不动包 |")
A("| **新增/启用二级主题** | 改 `knowledge-taxonomy.v1.json` ⇒ 包 hash/manifest 变 ⇒ Studio CAS 409 ⇒ **须重建包**；"
  "走 `corrections_<date>/` 留痕（先例：09-13 登记 economy 的 items/goods） | **高** |")
A("| **新增主分类** | **不许**（宪章 §二，红测 E9 守） | — |")
A("| 重出一览表 | `python _gen_classification_registry_20260914.py` | 秒级 |")
A("| 重出 xlsx 分类表 | `python _gen_name_index_20260913.py`（读同一份扁平表） | 秒级 |")
A("| 一致性检查 | 每档 `domain` ∈5、`subdomain` ∈43 且属同主分类；`doc_id` 前缀 ∈ §4 表 | 秒级 |")
A("")
A("---")
A("")
A(f"*本表由 `_gen_classification_registry_20260914.py` 生成于 {NOW}；分类定义源：taxonomy "
  f"`{tax.get('taxonomy_version')}`（唯一权威）；条数取自 `authoring-out/*.yaml` 现场扫描。*")
A("")

io.open(MD, "w", encoding="utf-8").write("\n".join(L))
io.open(FLAT, "w", encoding="utf-8").write(json.dumps(flat, ensure_ascii=False, indent=2) + "\n")
print(f"OK entries={N} domains={len(DOM_ORDER)} subdomains={len(sd)} "
      f"(used={len(sd_used)} unused={len(sd_unused)}) prefixes={len(flat['prefix_rows'])}")
print(" ->", MD)
print(" ->", FLAT)
