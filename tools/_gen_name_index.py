# -*- coding: utf-8 -*-
"""AWAKE 世界书条目名录生成器（Excel 版）

用法（venv python）:
    C:/Users/26811/.workbuddy-ai/binaries/python/envs/default/Scripts/python.exe tools/_gen_name_index.py

机制：扫描**正典目录**全部 *.yaml 档案 → 整表重建 docs/worldbook-migration/WORLDBOOK-NAME-INDEX.xlsx（原地覆盖）。
每批档案增改后重跑一次即"刷新"；无 LibreOffice 依赖，汇总为生成时静态值（脚本重算保证一致）。

分类口径（**两层，不另建清单**）：主分类 domain（5，封闭）＞ 二级主题 subdomain（43）。
两列的定义与中文名一律**从权威分类目录 knowledge-taxonomy.v1.json 现场读取**，本脚本不自带字面表。
另有「tags」一列：由 doc_id 第三段的文件名前缀**自动派生**（tag，不是分类层；不进 schema、不进包）。
  —— 设计意图（2026-10-01）：tag 是 subdomain 事实上的下属分类，但**不注册进受控词表**，故不死板。

工作簿（有扁平表时 6 张）：名录 / 分类总览 / 分类定义 / 域汇总 / 交叉覆盖 / 刷新说明。

沿革：
  2026-09-13 初版，位于 studio workspace 的 projection/authoring-out/，数据源即同目录投影副本。
  2026-10-01 挪到仓库根 tools/、数据源改指正典 authoring/（原投影副本滞后 245 档，已弃用）；
             「文件名前缀」列改称「tags」并明确其为 tag（非分类层）；同步更新说明口径。
"""
import io
import os
import glob
import sys
import json
import datetime
from collections import Counter

try:
    import yaml
except ImportError:
    sys.exit("need pyyaml in venv")
try:
    import openpyxl
except ImportError:
    import subprocess
    subprocess.check_call([sys.executable, "-m", "pip", "install", "--quiet", "openpyxl>=3.1.0"])
    import openpyxl
from openpyxl.styles import Font, PatternFill, Alignment, Border, Side
from openpyxl.formatting.rule import CellIsRule
from openpyxl.utils import get_column_letter

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, ".."))          # 仓库根 D:/AWAKE-Dev

# 数据源＝正典 authoring（不是投影副本）
SRC = os.path.join(REPO, "AWAKE", "tools", "worldbook-studio",
                   "workspace", "full-geo1", "authoring")
# 输出＝原位置不动
OUT = os.path.join(REPO, "AWAKE", "docs", "worldbook-migration", "WORLDBOOK-NAME-INDEX.xlsx")
# 权威分类目录（唯一；分类定义与顺序都从它读）
TAX_IN = os.path.join(REPO, "AWAKE", "docs", "worldbook-studio-plan",
                      "knowledge-taxonomy.v1.json")
# 定义表的数据源（与脚本同目录；由 _gen_classification_registry_20260914.py 产出，缺失则不加该表）
FLAT_IN = os.path.join(HERE, "_classification-flat.v1.json")


def tag_of(doc_id):
    """doc_id 第三段（首个 '-' 前的 token）＝文件名前缀，用作 tag（不是分类层）。"""
    return doc_id.split(".", 2)[2].split("-", 1)[0]


# ---------- 读权威分类目录（类别顺序＝文件顺序，UI 同序） ----------
_tax = json.load(io.open(TAX_IN, encoding="utf-8"))
DOMAIN_ORDER = [d["id"] for d in _tax["domains"]]
CAT_ZH = {d["id"]: d["label"]["zh-CN"] for d in _tax["domains"]}
SUB_ZH = {s["id"]: s["label"]["zh-CN"] for d in _tax["domains"] for s in d["subdomains"]}

# ---------- 采集 ----------
rows = []
for path in sorted(glob.glob(os.path.join(SRC, "*.yaml"))):
    d = yaml.safe_load(io.open(path, encoding="utf-8"))
    if not isinstance(d, dict) or not str(d.get("id", "")).startswith("doc."):
        continue
    exprs = [e for a in d.get("assertions", []) for e in a.get("expressions", [])]
    grants = [g for e in exprs for g in e.get("grants", [])]
    al = d.get("aliases") or {}
    rows.append({
        "doc_id": d["id"],
        "file": os.path.basename(path),
        "zh": (d.get("title") or {}).get("zh-CN", ""),
        "en": (d.get("title") or {}).get("en", ""),
        "domain": d.get("domain", ""),
        "sub": d.get("subdomain", ""),
        "sub_zh": SUB_ZH.get(d.get("subdomain", ""), ""),
        "tag": tag_of(d["id"]),
        "status": d.get("status", ""),
        "rev": d.get("revision", 0),
        "n_expr": len(exprs),
        "n_grant": len(grants),
        "n_culture_gate": sum(1 for g in grants if g.get("culture_ids")),
        "alias_zh": "、".join(al.get("zh-CN") or []),
        "alias_en": ", ".join(al.get("en") or []),
        "summary": (d.get("summary") or {}).get("zh-CN", "") if isinstance(d.get("summary"), dict) else str(d.get("summary") or ""),
    })
rows.sort(key=lambda r: (DOMAIN_ORDER.index(r["domain"]) if r["domain"] in DOMAIN_ORDER else 99, r["doc_id"]))

# ---------- 交叉覆盖扫描：同一 quote_hash 出现在 >=2 个档 ----------
# 档内去重（doc/assertion/expr 三级继承同源不计），只看跨档重复；合法多域 vs 违规双投由人工判定。
cross = {}
for path in sorted(glob.glob(os.path.join(SRC, "*.yaml"))):
    d = yaml.safe_load(io.open(path, encoding="utf-8"))
    if not isinstance(d, dict) or not str(d.get("id", "")).startswith("doc."):
        continue
    per_doc = {}
    def walk(o, trail):
        if isinstance(o, dict):
            if "quote_hash" in o:
                per_doc.setdefault(o["quote_hash"], []).append("/".join(trail) or d["id"])
            for k, v in o.items():
                walk(v, trail + [k])
        elif isinstance(o, list):
            for i, v in enumerate(o):
                walk(v, trail + [f"[{i}]"])
    walk(d, [])
    for h, locs in per_doc.items():
        cross.setdefault(h, {})[d["id"]] = locs
cross_dup = {h: docs for h, docs in cross.items() if len(docs) >= 2}

# ---------- 分类统计（两层） ----------
cat_cnt = Counter(r["domain"] for r in rows)
cat_sub = {}                      # 主分类 → Counter(二级主题)
for r in rows:
    cat_sub.setdefault(r["domain"], Counter())[r["sub"]] += 1
cat_order = sorted(cat_cnt, key=lambda c: (-cat_cnt[c], c))
sub_total = sum(len(v) for v in cat_sub.values())
N = len(rows)

# ---------- tag 统计（派生自文件名前缀，非分类层） ----------
tag_cnt = Counter(r["tag"] for r in rows)
sub_tag = {}                      # (domain, sub) → Counter(tag)
for r in rows:
    sub_tag.setdefault((r["domain"], r["sub"]), Counter())[r["tag"]] += 1

# ---------- 样式常量 ----------
def xl_color(css):
    v = css.removeprefix("#").upper()
    assert len(v) == 6
    return "FF" + v

XL_HEAD_BG = xl_color("#4472C4")   # 表头蓝底
XL_HEAD_FG = xl_color("#FFFFFF")
XL_LIGHT   = xl_color("#D9E2F3")   # 隔行浅底
XL_WARN_BG = xl_color("#FFEB9C")   # needs_review 黄底
XL_WARN_FG = xl_color("#9C6500")
XL_BAD_BG  = xl_color("#FFC7CE")   # 缺失红底
XL_BAD_FG  = xl_color("#9C0006")
XL_SUM_BG  = xl_color("#2F5597")   # 合计深蓝
XL_SEC_BG  = xl_color("#8EA9DB")   # 小节标题底

thin = Side(style="thin", color="FFBFBFBF")
BORDER = Border(left=thin, right=thin, top=thin, bottom=thin)
F_HEAD = Font(bold=True, color=XL_HEAD_FG)
FILL_HEAD = PatternFill("solid", fgColor=XL_HEAD_BG)
FILL_BAND = PatternFill("solid", fgColor=XL_LIGHT)
FILL_WARN = PatternFill("solid", fgColor=XL_WARN_BG)
FILL_BAD = PatternFill("solid", fgColor=XL_BAD_BG)
FILL_SUM = PatternFill("solid", fgColor=XL_SUM_BG)
FILL_SEC = PatternFill("solid", fgColor=XL_SEC_BG)
F_SUM = Font(bold=True, color=XL_HEAD_FG)
AL_C = Alignment(horizontal="center", vertical="center", wrap_text=True)
AL_L = Alignment(horizontal="left", vertical="center", wrap_text=True)

wb = openpyxl.Workbook()
wb.properties.title = "AWAKE 世界书条目名录"

# ---------- Sheet1 名录 ----------
ws = wb.active
ws.title = "名录"
HEAD = ["序号", "类别", "子域", "tags", "doc_id", "文件名", "中文名", "英文名",
        "状态", "版本", "表达数", "授权数", "文化门数", "别名（中文）", "别名（英文）", "摘要", "title校验"]
for c, name in enumerate(HEAD, 1):
    cell = ws.cell(row=1, column=c, value=name)
    cell.font = F_HEAD
    cell.fill = FILL_HEAD
    cell.alignment = AL_C
    cell.border = BORDER
for i, r in enumerate(rows, 1):
    vals = [i, r["domain"], r["sub"], r["tag"], r["doc_id"], r["file"], r["zh"], r["en"],
            r["status"], r["rev"], r["n_expr"], r["n_grant"], r["n_culture_gate"],
            r["alias_zh"], r["alias_en"], r["summary"], ("缺失" if not r["zh"] else "通过")]
    for c, v in enumerate(vals, 1):
        cell = ws.cell(row=i + 1, column=c, value=v)
        cell.border = BORDER
        cell.alignment = AL_C if c in (1, 2, 3, 4, 10, 11, 12, 13, 17) else AL_L
        if i % 2 == 0:
            cell.fill = FILL_BAND
END = len(rows) + 1
for col, w in zip("ABCDEFGHIJKLMNOPQ",
                  [6, 11, 18, 14, 34, 24, 22, 22, 13, 7, 8, 8, 9, 26, 20, 60, 10]):
    ws.column_dimensions[col].width = w
ws.freeze_panes = "A2"
ws.auto_filter.ref = f"A1:Q{END}"
# 条件格式：状态 needs_review 黄底（I 列）；title校验 缺失 红底（Q 列）
ws.conditional_formatting.add(f"I2:I{END}",
    CellIsRule(operator="equal", formula=['"needs_review"'], fill=FILL_WARN,
               font=Font(color=XL_WARN_FG)))
ws.conditional_formatting.add(f"Q2:Q{END}",
    CellIsRule(operator="equal", formula=['"缺失"'], fill=FILL_BAD,
               font=Font(color=XL_BAD_FG)))

def put(ws_, row, col, val, font=None, fill=None, align=AL_L, fmt=None):
    cell = ws_.cell(row=row, column=col, value=val)
    cell.alignment = align
    if font:
        cell.font = font
    if fill:
        cell.fill = fill
    cell.border = BORDER
    if fmt:
        cell.number_format = fmt
    return cell

def header_row(ws_, row, names):
    for c, name in enumerate(names, 1):
        put(ws_, row, c, name, font=F_HEAD, fill=FILL_HEAD, align=AL_C)

def strip_md(ws_):
    """Excel 不认 markdown ⇒ 去掉正文里的 ** 与反引号（表格样式已用字体/底色表达）。"""
    for row_ in ws_.iter_rows():
        for cell in row_:
            if isinstance(cell.value, str) and ("**" in cell.value or "`" in cell.value):
                cell.value = cell.value.replace("**", "").replace("`", "")

# ---------- Sheet2 分类总览（两层 + tag） ----------
w2 = wb.create_sheet("分类总览", 1)
r = 1
put(w2, r, 1, "AWAKE 世界书条目 · 分类总览", font=Font(bold=True, size=13), align=AL_L); r += 1
put(w2, r, 1, f"分类层级：主分类（5，封闭）＞ 二级主题（{len(SUB_ZH)}）｜tag（{len(tag_cnt)} 个，派生自文件名前缀，非分类层）"
              f"｜排序：主分类按分类目录顺序，二级主题按条数降序｜数据源：正典 authoring/ {N} 条｜"
              f"{datetime.datetime.now().strftime('%Y-%m-%d')}",
    align=AL_L); r += 2

put(w2, r, 1, "① 主分类汇总", font=Font(bold=True), align=AL_L); r += 1
header_row(w2, r, ["主分类", "中文", "条数", "二级主题数", "占比"]); r += 1
for c in cat_order:
    put(w2, r, 1, c, align=AL_L); put(w2, r, 2, CAT_ZH.get(c, ""), align=AL_L)
    put(w2, r, 3, cat_cnt[c], align=AL_C); put(w2, r, 4, len(cat_sub[c]), align=AL_C)
    put(w2, r, 5, cat_cnt[c] / N, align=AL_C, fmt="0.0%"); r += 1
put(w2, r, 1, "合计", font=F_SUM, fill=FILL_SUM, align=AL_L)
put(w2, r, 2, "", font=F_SUM, fill=FILL_SUM, align=AL_L)
put(w2, r, 3, N, font=F_SUM, fill=FILL_SUM, align=AL_C)
put(w2, r, 4, sub_total, font=F_SUM, fill=FILL_SUM, align=AL_C)
put(w2, r, 5, 1.0, font=F_SUM, fill=FILL_SUM, align=AL_C, fmt="0.0%")
r += 2

put(w2, r, 1, "② 主分类 × 二级主题 明细（筛这两列即可定位词条）", font=Font(bold=True), align=AL_L); r += 1
header_row(w2, r, ["主分类", "二级主题", "中文", "条数", "tags（下属分类）"]); r += 1
for c in cat_order:
    for sc, cnt in sorted(cat_sub[c].items(), key=lambda kv: (-kv[1], kv[0])):
        tags = "、".join(f"{t}({n})" for t, n in sorted(sub_tag[(c, sc)].items(), key=lambda kv: (-kv[1], kv[0])))
        put(w2, r, 1, c, align=AL_L); put(w2, r, 2, sc, align=AL_L)
        put(w2, r, 3, SUB_ZH.get(sc, ""), align=AL_L); put(w2, r, 4, cnt, align=AL_C)
        put(w2, r, 5, tags, align=AL_L); r += 1
put(w2, r, 1, "合计", font=F_SUM, fill=FILL_SUM, align=AL_L)
put(w2, r, 2, "", font=F_SUM, fill=FILL_SUM, align=AL_L)
put(w2, r, 3, "", font=F_SUM, fill=FILL_SUM, align=AL_L)
put(w2, r, 4, N, font=F_SUM, fill=FILL_SUM, align=AL_C)
put(w2, r, 5, "", font=F_SUM, fill=FILL_SUM, align=AL_L)
r += 2

put(w2, r, 1, "③ 口径与待办", font=Font(bold=True), align=AL_L); r += 1
for t in [
    f"• 分类只有两层：主分类 domain（5，封闭）＋ 二级主题 subdomain（43）。权威源 knowledge-taxonomy.v1.json，"
    f"本表两列的定义与中文名均从它现场读取。",
    "• tag（原「文件名前缀」，doc_id 第三段）＝subdomain 事实上的下属分类，但**不注册进受控词表**、不进 schema、"
    "不进包——由生成器从档名自动派生，故不显死板。",
    f"• 未用二级主题 {len(SUB_ZH) - sub_total} 个：目录已登记、作者可选、尚无条目。",
    "• 定义、举例、归类冲突提示与纠错清单见「分类定义」表；全文见 "
    "docs/worldbook-migration/WORLDBOOK-CLASSIFICATION-REGISTRY-20260914.md。",
]:
    put(w2, r, 1, t, align=AL_L); r += 1
strip_md(w2)
for col, w in zip("ABCDE", [12, 12, 10, 12, 46]):
    w2.column_dimensions[col].width = w
w2.freeze_panes = "A2"

# ---------- Sheet3 分类定义（读 _classification-flat.v1.json，与一览表同源） ----------
flat = None
if os.path.exists(FLAT_IN):
    flat = json.load(io.open(FLAT_IN, encoding="utf-8"))
if flat:
    wd = wb.create_sheet("分类定义", 2)
    r = 1
    put(wd, r, 1, "AWAKE 世界书分类体系 · 定义表（主分类 / 二级主题）",
        font=Font(bold=True, size=13), align=AL_L); r += 1
    put(wd, r, 1, f"权威源：knowledge-taxonomy.v1.json（唯一分类目录，raw-byte SHA-256 + CAS 门控）"
                  f"｜数据面 {flat['n_entries']} 档｜生成 {flat['generated_at']}", align=AL_L); r += 2

    put(wd, r, 1, "① 主分类（5，封闭 · 宪章 §二「扩容不扩域」，第六域提案 BREAK）",
        font=Font(bold=True), align=AL_L); r += 1
    header_row(wd, r, ["主分类", "中文", "定义", "归类冲突提示", "档数", "二级主题 已用/未用"]); r += 1
    for c in flat["categories"]:
        put(wd, r, 1, c["id"], align=AL_L); put(wd, r, 2, c["label"], align=AL_L)
        put(wd, r, 3, c["help"], align=AL_L)
        put(wd, r, 4, "\n".join(c["hints"]), align=AL_L)
        put(wd, r, 5, c["n"], align=AL_C)
        put(wd, r, 6, f"{c['subs_used']} / {c['subs_unused']}", align=AL_C)
        r += 1
    r += 1

    put(wd, r, 1, f"② 二级主题（{len(flat['subdomain_rows'])} ＝ 已用 {sum(1 for x in flat['subdomain_rows'] if x['status'] == '已用')}"
                  f" ＋ 未用 {sum(1 for x in flat['subdomain_rows'] if x['status'] == '未用')}）——作者选的下拉目录",
        font=Font(bold=True), align=AL_L); r += 1
    header_row(wd, r, ["状态", "主分类", "二级主题", "中文", "说明", "例子", "档数", "在用 tags"]); r += 1
    for row_ in sorted(flat["subdomain_rows"],
                       key=lambda x: (0 if x["status"] == "已用" else 1, x["domain"], x["subdomain"])):
        put(wd, r, 1, row_["status"], align=AL_C); put(wd, r, 2, row_["domain_zh"], align=AL_L)
        put(wd, r, 3, row_["subdomain"], align=AL_L); put(wd, r, 4, row_["subdomain_zh"], align=AL_L)
        put(wd, r, 5, row_["help"], align=AL_L); put(wd, r, 6, row_["example"], align=AL_L)
        put(wd, r, 7, row_["n"], align=AL_C); put(wd, r, 8, row_["prefixes"], align=AL_L)
        if row_["status"] == "未用":
            for cc in range(1, 9):
                wd.cell(row=r, column=cc).fill = FILL_BAND
        r += 1
    r += 1

    put(wd, r, 1, f"③ 纠错清单（{len(flat['fixes'])} 条，拿原设计的归类判定当尺子）",
        font=Font(bold=True), align=AL_L); r += 1
    header_row(wd, r, ["#", "对象", "设计怎么说", "实际怎么挂", "建议"]); r += 1
    for i, f in enumerate(flat["fixes"], 1):
        put(wd, r, 1, i, align=AL_C); put(wd, r, 2, f["item"], align=AL_L)
        put(wd, r, 3, f["designed"], align=AL_L); put(wd, r, 4, f["actual"], align=AL_L)
        put(wd, r, 5, f["suggest"], align=AL_L); r += 1
    r += 1

    put(wd, r, 1, f"④ 命名约定附表：tags（{len(flat['prefix_rows'])} 个）——非分类层，是 subdomain 事实上的下属分类",
        font=Font(bold=True), align=AL_L); r += 1
    header_row(wd, r, ["tag", "中文", "在用档数", "常见所属二级主题"]); r += 1
    for row_ in flat["prefix_rows"]:
        put(wd, r, 1, row_["prefix"], align=AL_L); put(wd, r, 2, row_["zh"] or "—", align=AL_L)
        put(wd, r, 3, row_["n"], align=AL_C); put(wd, r, 4, row_["subdomains"], align=AL_L); r += 1
    r += 1

    put(wd, r, 1, "⑤ 维护口径（全文见 docs/worldbook-migration/WORLDBOOK-CLASSIFICATION-REGISTRY-20260914.md）",
        font=Font(bold=True), align=AL_L); r += 1
    for t in [
        "• 不得有第二份分类清单：taxonomy 是唯一目录，Studio 由 TaxonomyCatalogService 单点校验。",
        "• 新增档案：主分类从 5 个里挑，二级主题从 43 个里挑（拿 §② 的说明与例子当尺）。成本低。",
        "• 新增 tag：改档名取词即可，不动包、不动 hash、不动 taxonomy（tag 不进受控词表）。",
        "• 新增/启用二级主题：改 taxonomy ⇒ 包 hash/manifest 变 ⇒ Studio CAS 409 ⇒ 须重建包；走 corrections_<日期> 留痕。",
        "• 新增主分类：不许（宪章 §二，红测 E9 守）。",
        "• 本表由 tools/_gen_name_index.py 读 _classification-flat.v1.json 生成（重跑即刷新，勿手改）。",
    ]:
        put(wd, r, 1, t, align=AL_L); r += 1
    strip_md(wd)
    for col, w in zip("ABCDEFGH", [9, 11, 20, 11, 42, 22, 8, 30]):
        wd.column_dimensions[col].width = w
    wd.freeze_panes = "A4"
N_SHEETS = 6 if flat else 5

# ---------- Sheet4 域汇总 ----------
w3 = wb.create_sheet("域汇总", 3 if flat else 2)
for c, name in enumerate(["主分类", "中文", "档数", "表达合计", "授权合计", "文化门合计"], 1):
    cell = w3.cell(row=1, column=c, value=name)
    cell.font = F_HEAD
    cell.fill = FILL_HEAD
    cell.alignment = AL_C
    cell.border = BORDER
by_dom = {}
for r_ in rows:
    by_dom.setdefault(r_["domain"], []).append(r_)
rr = 2
for dom in DOMAIN_ORDER + [d for d in by_dom if d not in DOMAIN_ORDER]:
    if dom not in by_dom:
        continue
    g = by_dom[dom]
    vals = [dom, CAT_ZH.get(dom, ""), len(g), sum(x["n_expr"] for x in g),
            sum(x["n_grant"] for x in g), sum(x["n_culture_gate"] for x in g)]
    for c, v in enumerate(vals, 1):
        cell = w3.cell(row=rr, column=c, value=v)
        cell.border = BORDER
        cell.alignment = AL_C if c > 2 else AL_L
        if rr % 2 == 0:
            cell.fill = FILL_BAND
    rr += 1
tot = ["合计", "", len(rows), sum(r_["n_expr"] for r_ in rows),
       sum(r_["n_grant"] for r_ in rows), sum(r_["n_culture_gate"] for r_ in rows)]
for c, v in enumerate(tot, 1):
    cell = w3.cell(row=rr, column=c, value=v)
    cell.font = F_SUM
    cell.fill = FILL_SUM
    cell.border = BORDER
    cell.alignment = AL_C if c > 2 else AL_L
for col, w in zip("ABCDEF", [14, 10, 10, 10, 10, 12]):
    w3.column_dimensions[col].width = w

# ---------- Sheet5 交叉覆盖 ----------
w4 = wb.create_sheet("交叉覆盖", 4 if flat else 3)
for c, name in enumerate(["引文指纹（前16）", "跨档数", "档清单", "定位明细", "人工判定"], 1):
    cell = w4.cell(row=1, column=c, value=name)
    cell.font = F_HEAD
    cell.fill = FILL_HEAD
    cell.alignment = AL_C
    cell.border = BORDER
if cross_dup:
    rr = 2
    for h, docs in sorted(cross_dup.items(), key=lambda kv: (-len(kv[1]), kv[0])):
        vals = [h[:16], len(docs), "、".join(docs),
                "\n".join(f"{dd}: {'; '.join(locs)}" for dd, locs in docs.items()), ""]
        for c, v in enumerate(vals, 1):
            cell = w4.cell(row=rr, column=c, value=v)
            cell.border = BORDER
            cell.alignment = AL_C if c in (2,) else AL_L
            if rr % 2 == 0:
                cell.fill = FILL_BAND
        rr += 1
else:
    cell = w4.cell(row=2, column=1, value="全库无跨档引文（同一 quote_hash 均只出现在单档）")
    cell.alignment = AL_L
for col, w in zip("ABCDE", [20, 8, 42, 70, 12]):
    w4.column_dimensions[col].width = w

# ---------- Sheet6 刷新说明 ----------
w5 = wb.create_sheet("刷新说明", 5 if flat else 4)
CN_NUM = {5: "五", 6: "六"}
notes = [
    ("AWAKE 世界书条目名录（机器生成，请勿手改数据）", True),
    ("", False),
    ("■ 刷新方式（实时更新机制）", True),
    ("数据源：AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring/*.yaml（**正典全库档**，增删档后自动跟随）。", False),
    ("刷新命令：C:/Users/26811/.workbuddy-ai/binaries/python/envs/default/Scripts/python.exe tools/_gen_name_index.py", False),
    (f"每次批档增改 / 提交前重跑一次，{CN_NUM.get(N_SHEETS, N_SHEETS)}张表整体重建并原地覆盖本文件；汇总数字为生成时快照，由脚本保证一致。", False),
    ("分类列与「分类总览」「分类定义」两表均由脚本从 yaml 与分类目录现场派生，重跑不会丢失——故请勿在 Excel 内手工改动。", False),
    ("", False),
    ("■ 字段说明（名录）", True),
    ("类别：主分类（domain），五个封闭，值 geography / politics / culture / economy / war；排序按分类目录顺序。", False),
    ("子域：二级主题（subdomain），共 43 个，作者建档时选的那个；定义与中文名见「分类定义」表。", False),
    ("tags：doc_id 第三段（首个 '-' 前的词），如 village / castle / town。它是 subdomain 事实上的下属分类，"
     "但**不注册进受控词表**、不进 schema、不进包——只由档名派生，故不死板。", False),
    ("表达数/授权数：assertions 内 expressions 总数、grants 总数；文化门数＝带 culture_ids 条件的 grant 数。", False),
    ("title校验：发布纪律验收项——zh-CN 标题非空即\"通过\"，\"缺失\"红底示警。", False),
    ("别名（中文/英文）：编译进运行时包 keywords，供双向子串检索；modder 翻包按 doc_id 对应 entry id：awake:entry:<类别>.<slug>。", False),
    ("", False),
    ("■ 各表用途", True),
    ("分类总览：① 主分类汇总（条数/二级主题数/占比）② 主分类 × 二级主题 明细（含下属 tags）③ 口径与待办。", False),
    ("分类定义：主分类 5 个的定义与归类冲突提示；二级主题 43 个的说明与例子；纠错清单；tags 附表。", False),
    ("域汇总：各主分类的表达/授权/文化门合计。交叉覆盖：同一引文指纹出现在 ≥2 档的清单，服务「同变体不双投」纪律。", False),
    ("", False),
    ("■ 已知包袱", True),
    ("老批 slug 为缩写（如 sara-bay=沙拉斯湾、der-furs=德瑞亚特毛皮、lac-lake=拉科尼斯湖），war 批起全拼；中文名一律看\"中文名\"列（官方译名）。", False),
    ("", False),
    (f"生成时间：{datetime.datetime.now().strftime('%Y-%m-%d %H:%M')}｜生成器：tools/_gen_name_index.py", False),
]
for i, (txt, bold) in enumerate(notes, 1):
    cell = w5.cell(row=i, column=1, value=txt)
    cell.alignment = AL_L
    if bold:
        cell.font = Font(bold=True)
w5.column_dimensions["A"].width = 110

wb.save(OUT)
print(f"OK rows={len(rows)} exprs={tot[3]} grants={tot[4]} cross_dup_hashes={len(cross_dup)} "
      f"cats={len(cat_order)} subdomains_used={sub_total} tags={len(tag_cnt)} -> {OUT}")
