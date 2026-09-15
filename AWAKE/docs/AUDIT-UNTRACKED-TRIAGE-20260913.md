# 未入库资产处置清单（tools + docs）

- 日期：2026-09-13
- 范围：本轮脏树收口后剩下的全部未入库改动
- 结论：**564 项，绝大部分该进，只有 4 项建议不进**

> **✅ 执行结果（2026-09-13 晚，已按本清单执行完毕）**
>
> 四笔提交：**F** `8736281`（persona-workbench 整目录 · 204 文件）· **G** `85b55f9`（tools 其余 · 49）· **H** `194e13a`（docs 整块 · 309）· **I** `32053f6`（AWAKE.Tests 三源 · 3）。
> 四笔后 smoke **27/27 全绿**（HEAD 可编可过）。
>
> **未纳入 4 项**：`AUDIT-COMPILE-VERIFY-latest.json`、`OBSERVE-RUNTIME-DEFINITIONS-latest.txt`（每重跑即变）·`control-plane/CURRENT.json`（坏指针）·`runtime-sim/qa-out.json`（运行输出）。
>
> 另：`docs/artifact-retention/` 2 项为他人既有 staged 内容，本批以 pathspec `:(exclude)` 保住、未纳入亦未改动。

---

## 一、总量

| 区域 | 未跟踪（新文件） | 已改未存（旧文件改动） |
|---|---|---|
| `AWAKE/tools` | 222 | 32 |
| `AWAKE/docs` | 266 | 44 |
| **合计** | **488** | **76** |

体积：tools 503 KB，docs 2.8 MB。**很小，入库没有压力。**

---

## 二、先讲一条老规矩（它改变判断）

这个仓库自己的惯例是——**工作过程也一并留档**：

- 库里已经有 **36 个 `_` 前缀的过程脚本**（`_audit40_*.py`、`_fix2_dbprobe_*.py`、`_gen_batch1_*.py`……）
- `tools/worldbook-runtime-sim/` 连 `_build_catalog.py`、`_cmp_ask.py` 这种一次性脚本都入库了

所以**不能因为"看着像临时脚本"就丢掉**。这一批同类的，要么一起进，要么一起不进——不该由我单独判死。

---

## 三、分档处置

### 第 1 档 · 非进不可（有硬理由，不进会留病根）

| 类别 | 数量 | 为什么必须进 |
|---|---|---|
| `tools/persona-workbench/characters/` 源卡 | 152 | **成品已入库 78 个（`persona_definitions/*.definition.json`），源稿没入** ⇒ 成品成了"没有底稿的东西"，改不了、也重新生成不了 |
| `docs/mappings/*.tsv` 译名表 | 3 | 你的第 0 条硬规矩就是译名纪律。**`settlement-names-zh-en.tsv` 已入库，这三张（人物/王国/角色）漏了** —— 同一套表 4 缺 3 |
| `tools/persona-workbench/` 规范·契约·工具脚本 | ~41 | 卡入库了，**量卡的尺子没入**（编写准则、表达质量规范、contracts schema、20+ 个 audit 脚本、红队套件、规范快照） |
| `tools/worldbook-contract/v2/` | 7 | **和刚提交的笔 B 是同一条线**：`WeeklyReportService` 已入库，它的契约 schema 与 6 个测试夹具却留在库外 |
| `docs/review-state/*.review.json` | 16 | 你的流程明确是"审查结论落 `docs/review-state/`" |
| `docs/` 根目录 `PLAN-/REVIEW-/AUDIT-` 文档 | ~95 | 项目工作留痕 |
| `docs/worldbook-migration/projection/authoring-out/` 成品 yaml | 69 | 世界书成品档（49 城镇 + 20 物品） |
| `docs/checkpoints/` | 6 | 检查点留痕 |
| `tools/persona-awake-joint/verify-*.ps1` | 3 | 门禁脚本 |

### 第 2 档 · 按老规矩也该进（留痕一致性）

- `authoring-out/` 的 27 个 `_` 过程脚本
- `persona-workbench/redteam/analysis/_*.py` 7 个
- `docs/` 根目录 9 个一次性 `ps1`（`convert*.ps1`、`count-cards.ps1`、`lint-*.ps1`……）
- `tools/worldbook-runtime-sim/` 的 `_caladog_cmp/`、`_dsl/`、`qa-out.json`
- `docs/fixtures/`、`docs/sync-reports/`

### 第 3 档 · 建议不进（4 项）

| 文件 | 理由 |
|---|---|
| `docs/AUDIT-COMPILE-VERIFY-latest.json` | `-latest` 是"当前快照"，**每次重跑就变** ⇒ 进库等于天天制造假改动 |
| `docs/OBSERVE-RUNTIME-DEFINITIONS-latest.txt` | 同上 |
| `tools/worldbook-runtime-sim/qa-out.json` | 模拟器的运行输出（问答结果 dump），是产物不是源 |
| `docs/control-plane/CURRENT.json` | **它的指针是坏的**：里面写 `evidence_path: docs/evidence/...`，而 `docs/evidence/` 正好在 `.gitignore` 里 ⇒ 引用一个永远不存在的文件；且内容停在 09-11 |

### 第 4 档 · 要你拍一句

- `control-plane/CURRENT.json` 是**修好再入**（改指针，或把 `docs/evidence/` 放行），还是**直接不入**？

---

## 四、推荐切笔（待批准后执行）

| 笔 | 内容 | 注意 |
|---|---|---|
| **F** | `tools/persona-workbench/` 整目录 | ⚠️ 新工程 `PersonaWorkbench.Verify/` 只被 `compile-verify.ps1` 引用，**两者必须同笔**，否则是新一份"引用不存在文件"的死提交 |
| **G** | `tools/` 其余（`worldbook-contract/v2` + `persona-awake-joint` + `worldbook-runtime-sim` + 根 `*.ps1`） | 含 32 项已改未存的旧文件 |
| **H** | `docs/` 整块（根文档 + `review-state` + `authoring-out` + `mappings` + `fixtures` + `checkpoints` + `sync-reports` + `worldbook-migration`） | 含 44 项已改未存的旧文件 |
| **I** | `.gitignore` 补第 3 档 4 项（若你同意） | 与 F/G/H 无关，可随时插入 |

提交纪律照旧：`git commit -F <msg> -- <精确路径>`，每笔前查 `git diff --cached --name-only`，index 里常驻的 2 项（`artifact-retention` 两份 md）不带走。

---

## 五、顺带记一笔风险

本仓有公开镜像 `Nobody-zz/AWAKE`。这批内容包括 76 张角色卡源稿与世界书成品档；**若将来同步镜像，它们会公开**。目前镜像落后、未推送，不紧急，但决定前要知道这件事。
