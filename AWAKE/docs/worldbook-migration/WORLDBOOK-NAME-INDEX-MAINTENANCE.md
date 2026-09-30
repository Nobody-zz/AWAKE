# 世界书条目名录 · 维护纪律（2026-10-01 立）

> 适用对象：`AWAKE/docs/worldbook-migration/WORLDBOOK-NAME-INDEX.xlsx`
> 生成器：`tools/_gen_name_index.py`（**仓库根**）
> 本文是纪律条文，不是说明；说明见 xlsx 内「刷新说明」表。

---

## 一、这条纪律是什么

**名录是当前全库的读数快照，必须与正典同步。**

> **凡增档 / 改档 / 删档，收工前必重跑一次生成器；提交前名录必须是新的。**

违者后果（实测过）：名录曾滞后 245 档、politics 域读数差 **20 倍**（7 vs 142），
并残留 3 个正典里根本不存在的孤儿档（`mines-lycaron` / `tales-husn-fulq` / `war_history-kuyug`）。

**⇒ 名录不准 = 后续所有基于它的判断都不准。**

---

## 二、怎么刷

```bash
# 在仓库根 D:/AWAKE-Dev 下执行
C:/Users/26811/.workbuddy-ai/binaries/python/envs/default/Scripts/python.exe tools/_gen_name_index.py
```

产出：`AWAKE/docs/worldbook-migration/WORLDBOOK-NAME-INDEX.xlsx`（**原地覆盖**，6 张表整体重建）。

**刷新前先备份旧件**（沿用既有惯例）：

```
AWAKE/docs/worldbook-migration/_archive-WORLDBOOK-NAME-INDEX-<日期>-<因由>.xlsx
```

---

## 三、数据源（★ 这条最容易搞错）

| 项 | 正确位置 |
|---|---|
| **数据源（正典）** | `AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring/*.yaml` |
| ~~旧数据源（已弃用）~~ | ~~`AWAKE/docs/worldbook-migration/projection/authoring-out/*.yaml`~~ |
| 权威分类目录 | `AWAKE/docs/worldbook-studio-plan/knowledge-taxonomy.v1.json` |
| tag 定义表数据源 | `tools/_classification-flat.v1.json`（与脚本同目录） |

⚠️ **旧数据源为什么弃用**：那是给迁移期用的**投影副本**，不是正典。它有两个毛病——
① **滞后**（只到 558 档，缺后来 245 档）；② **脏**（残留正典已不存在的孤儿档）。
**旧脚本与旧 flat 已归档至** `projection/authoring-out/_archive-name-index-src-20261001/`，别再拿它跑。

---

## 四、三层结构（分类 + tag）

```
主分类 domain（5，封闭）      —— 权威源 knowledge-taxonomy.v1.json，改它 = 重编译
 └─ 二级主题 subdomain（44）  —— 同上，进受控词表
     └─ tag（34 个）          —— **不进受控词表**、不进 schema、不进包
                                由 doc_id 第三段的文件名前缀自动派生
```

**tag 的定位（2026-10-01 甲方裁定）**：

> tag 是 **subdomain 事实上的下属分类**，但**不注册进词表**——因为一注册就死板
> （44 子域 × 数类 = 数百个受控值，且加一类要改 schema + 重编译）。

**⇒ 处置规则**：

| 动作 | 要改什么 | 代价 |
|---|---|---|
| 新增 tag | 只改档名取词 | **零**（不动包、不动 hash、不动 taxonomy） |
| 新增/启用 subdomain | 改 taxonomy | 包 hash/manifest 变 ⇒ CAS 409 ⇒ **须重建包** |
| 新增 domain | —— | **不许**（宪章 §二） |

---

## 五、已知待办（下次刷新前记着）

1. **`_classification-flat.v1.json` 仍是 09-14 的旧件**（分类定义表第 ②③④ 节的档数、在用 tag 是旧读数）。
   **口径未错，数字旧。** 需重新生成一份（生成器 `_gen_classification_registry_20260914.py`）。
2. **tag 派生仍沿用旧命名**——后续"筛一遍、检查一遍、做一次梳理派生"时处理。
3. **两个"万能 tag"待拆**：`tales-`（体裁，不是依据）、`concept-`（性质，不是依据），散落在多个 subdomain。

---

## 六、给后来者的一句话

> **名录是仪表盘。仪表盘不准，不等于车没动——但你会照着它开沟里。**
