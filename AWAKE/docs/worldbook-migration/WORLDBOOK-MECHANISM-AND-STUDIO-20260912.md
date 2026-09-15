# 世界书机制 + 世界书编辑器 · 全景梳理 — 2026-09-12

> 目的：把「世界书是什么机制」和「编辑器怎么用」一次讲清楚，供后续批次参考。
> 本文是**现状快照**（含实测数据），不是计划书。所有结论均带文件路径 + 行号。
>
> 阅读顺序建议：第零节（一句话）→ 第一节（三层）→ 第六节（现状与断点）。其余按需查。

---

## 零、一句话

**世界书是一套"NPC 知道什么、肯说多少"的知识系统。**

人负责写内容（中世纪味的客观事实 + 不同身份的说法），机器负责判定（这个 NPC 该不该知道、能说到多细）。两者之间隔着**一套权限契约**——这才是世界书的机制本体。

---

## 一、三层结构：内容 → 生产 → 运行

| 层 | 干什么 | 谁负责 | 产物在哪 |
|---|---|---|---|
| **内容层** | 作者写词条：客观事实 + 分层说法 + 谁能看 | 内容编辑者（人 / AI 草稿 + 人审） | Studio 工作区 `authoring/*.yaml` |
| **生产层** | 校验 → 编译 → 导出，把 YAML 变成机器能吃的包 | Worldbook Studio | `compiled/` → `export/WorldbookV2/` |
| **运行层** | 游戏加载包，NPC 被问时按身份查出该说的那段 | AWAKE 运行时（主干侧） | 游戏 `Modules/AWAKE/ModuleData/Worldbook/` |

**关键点：三层之间都是"文件交接 + 哈希校验"，没有活的接口。**
内容改一个字 → 哈希变 → 编译证明失效 → 必须重新走一遍生产层。这是刻意的防呆设计（见 4.4）。

---

## 二、内容模型：一个词条长什么样

### 2.1 四层嵌套

```
档案 document（一份词条，如「德里亚特·村庄」）
 └─ 断言 assertion（一条客观事实，中立内核）
     └─ 表达 expression（同一事实的某种说法，按层级分层）
         ├─ grants  授权：谁能看到这层说法
         └─ denies  否决：谁绝对不能看
```

**为什么这么设计**（Max 定的硬规矩）：
- **断言是共用的客观内核**——不带任何个人色彩，谁看到都一样。
- **表达是按身份分层的说法**——村民说得土、商人讲行情、贵族论体面；知道得多的人拿到更细的一层。
- 所以"同一个事实"可以有好几段"说法"，机器按问话人的身份挑一段给他。

### 2.2 真实例子（`authoring/der-vill.yaml`）

```yaml
assertions:
- id: assertion.der-vill-1
  kind: fact
  text: 德里亚特是卡琉斯堡左近的一座村庄，夹在瓦尔切格湾和埃博半岛之间的山脊上。   # ← 中立内核
  expressions:
  - id: expr.der-vill-1-summary
    layer: summary                                                              # ← 层级
    text: 德里亚特是卡琉斯堡左近的一座村庄，夹在瓦尔切格湾和埃博半岛之间的山脊上。   # ← 这一层的说法
    grants:
    - profile_id: profile.commoner    # 平民
      scope: regional                 # 在本地区域内
      min_detail: summary             # 至少知道到 summary 这一层
    denies: []
```

### 2.3 三个旋钮（决定"谁能知道多少"）

| 旋钮 | 取值 | 回答的问题 |
|---|---|---|
| **layer**（层级） | `unknown` → `rumor` → `summary` → `detail` → `secret` | **知道多细**（由浅到深五档） |
| **scope**（范围） | `local` → `regional` → `national` → `faction` → `elite` → `private` | **知道多广**（六档阶梯） |
| **conditions**（条件） | `culture_ids` / `kingdom_ids` / `settlement_ids` / `role_ids` / `is_female` / `is_clan_leader` / `min-max_age` / `min_management` / `min_steward` / `min_skill` | **具体是谁**（文化/王国/聚落/角色/性别/年龄/管理等级/技能） |

另有一个独立维度 **`profile`**（身份，共 12 个，见 2.4）：村民 / 市民 / 平民 / 头人 / 乡绅 / 商人 / 酒馆老板 / 赎金经纪人 / 士兵 / 贵族 / 贵族总管 / 身份未知。

### 2.4 判定顺序（机器怎么用这些旋钮）

**`Deny` > `Grant` > 继承 > 未知**，实现在 `AWAKE/src/WorldKnowledgeQueryService.cs`：

1. **先查否决**（`:52` → `HasMatchingDeny` `:218-226`）：命中就直接出局，没有商量。
2. **再查授权**（`:57` → `SelectExpression` `:228-268`）：必须有 grant 命中才会被选中；`public` 授权可绕过一切能力检查（`:240`）。
3. **身份继承**：`WorldbookIdentityEvaluator.AddIdentity`（`:173-187`）沿 `parents` 递归，每远一层 `score-1`。
4. **能力比对**：`:241-245` 是否有能力；`:246-251` 比 Scope / Detail 的秩；`:253` 详细度过高则降级。身份不在集合里 → `:64` 直接 false。

### 2.5 转介（referral）

NPC 不知道时**不是沉默，而是指路**："这方面我不清楚。你可以去问：……"
实现：`NpcDialogueService.cs:61/:249` 收集 referral id（过滤 `PubliclyAskable`）、`:79-84` 拼指路句。
登记表现有 4 条：`referral.notary_merchant` / `ransom_broker` / `tavernkeeper` / `headman`。

---

## 三、作者要填什么（authoring.v1 schema）

schema 不在 Studio 目录里，在 **`AWAKE/docs/worldbook-studio-plan/awake.worldbook.authoring.v1.schema.json`**（顶层 `additionalProperties:false`，多一个字段就报错）。

| | 字段 | 说明 |
|---|---|---|
| **必填 13 项** | `schema_version` / `revision` / `id` / `title` / `status` / `domain` / `universe` / `era` / `content_tier` / `summary` / `registry_bindings` / `assertions` / `authority` | `id` 必须匹配 `^doc\.(politics\|economy\|culture\|war\|geography)\.…$`；`domain` 五选一；`era` = key + certainty（exact/bounded/approximate/unknown） |
| **可选 7 项** | `subdomain` / `related_domains` / `entity_ids` / `aliases` / `redirects` / `lifecycle` / `sources` / `author_created` | `entity_ids` = 锚点绑定；`sources` = 来源引文（我们 12 档全带） |

**三条容易踩的交叉约束**（schema `:142-242`、`:711-749`、`:893-917`）：
1. **`sources` 与 `author_created` 互斥**——要么引别人资料，要么声明是自己写的，不能又引又原创。文档级/断言级/表达级都一样。
2. **`status=canon` 时**，`author_created` 必须是 `approved_author_created`，且 `approved` 必带 `review_event_id`。
3. **`lifecycle` 里出现** `supersedes` / `split_from` / `merged_into` / `valid_from` / `valid_to` 任一个，就必须同时补 `event_id` + `revision`。

---

## 四、编辑器：Worldbook Studio

### 4.1 三个入口

| 入口 | 是什么 | 谁用 |
|---|---|---|
| **Launcher**（`Awake.WorldbookStudio.Launcher.exe`） | **双击启动**。开窗口 → 抢互斥锁防双开 → 起 Web 子进程 → 自动开浏览器 | **内容编辑者，日常就用这个** |
| **Web** | 真正的操作界面，`http://127.0.0.1:5077`（只绑 loopback，可用 `AWAKE_WB_PORT` 改） | 人在这个页面里干活 |
| **CLI** | `init / validate / compile / preview / export / authoring-register\|select\|approve\|proof / publish-staging / ai-* / doctor` | 开发者 / 自动化脚本 |

> 关启动器窗口 = 安全关掉 Web。不要去任务管理器杀进程。

### 4.2 工作区目录地图

默认位置 `Windows 文档\AWAKE\WorldbookStudio`（我们的开发用 `tools/worldbook-studio/workspace`，**该目录被 gitignore**）。

| 目录 | 装什么 |
|---|---|
| `authoring/` | **人编辑的 YAML 正典**；下含 `sources/`（资料）、`suggestions/`（AI 建议）、`audit/events`、`identity/`、`draft-state/`、`session-state/`、`handoff-inbox/` |
| `authoring-v1/` | **权限链 sidecar**：document-revisions / selections / approval-proofs / compile-proofs / publish-proofs / operations / commit-markers。**只有 AuthorityGate 写，不是给人手改的** |
| `compiled/` | 编译产物 + `reports/`；`quarantine/` 是崩溃/冲突产物的隔离区 |
| `export/WorldbookV2/` | staging 包 + `current.json` 发布指针 |
| `prebatches/` / `batches/` | 批量任务的扫描阶段 / 正式阶段 |
| `cache/v1/`、`fixtures/` | 批次缓存 / 只读测试样例 |

### 4.3 两条创作路径

**A. 单份（Quick Authoring）** — 适合一份资料出一份档案：
导入资料 → 填"我想整理什么" → 选内容范围 → 生成草稿 → 看来源和警告 → **逐条人工"采纳"** → 创建待审核档案（`needs_review`）→ 在编辑器里补二级主题/权限/时代 → 保存。

**B. 批量** — 适合一次导入多份资料：
`/scan` 扫资料 → 写 `prebatches/` → `/create` 提升为 `batches/` → `/consent` 勾选读取范围（**不勾就不读**）→ `/start` 提取事实 → 逐条 `/review` 人工审 → `/create-documents` 生成草稿 → 回主编辑器补字段保存。

额度（`BatchPromotionRepository.cs:394-408`）：**最多 200 文件 / 总 64 MiB / 单文件 16 MiB / 单单元 8 万字符**；并发本地 1、云端默认 2（上限 4）；单尝试 120s、最多 3 次；扫描 TTL 24 小时。有租约（lease）+ fence token 防并发打架，结果不确定的项记 `unknown_result` 需人工确认。

**共同的铁律**：
- **AI 草稿必须逐条人工采纳**，改了字要重新采纳，**绝不自动写入正典**（README:43）。
- AI 建档后一律 `needs_review`，**不会因为你点了生成就自动发布、自动同步游戏**（新手指引:791）。
- 不配 API Key 也能用「先做本地检查」查空字段、占位、权限问题（README:45）。

### 4.4 权限链 Authority Gate（为什么要点"申请授权"）

把"编译/导出/发布"从"扫目录就干"改成**必须持证**：

```
文档登记(document-revisions) → 固化集合(selections) → 批准证明(approval-proofs)
→ 编译许可(compile-proofs) → 导出暂存(export_staging) → 发布指针(publish_pointer)
```

- 每一步都有 proof_hash 自洽校验，**改一个字证明就失效**（`AuthorityGate.cs:421-449`；前端 `studio-editor-safety.js:352` 监听输入直接失效证明）。
- 为什么要这么重：**防止未审核/已改动的内容混进游戏包**，且崩溃重启能 fail-closed、不产生半成品指针。
- 编辑者会不会撞到：**会，但 UI 已包成一步**——点「申请编译授权」自动串起 register→select→approve→proof 四连，成功后编译/导出按钮才亮。
- 旧的直连路由（`/api/compile` `/api/save-authoring` `/api/document/new` `/api/export`）已全部返 **410**（`Web/Program.cs:208-213`）。

### 4.5 校验体系（诊断码）

Studio 有 30+ 个诊断码，常用的：

| 码 | 含义 |
|---|---|
| `WB-DOC-001/002/003` | 文档 id 非法 / domain 无效 / **entity_ids 不是 `entity.<kind>.<code>` 或 kind 不在 hero·clan·settlement 内** |
| `WB-REGISTRY-001` | `registry_bindings` 的版本或哈希与登记表当前值不符 → **必须重绑** |
| `WB-PROFILE-001` | 权限规则引用了不存在的 profile，或继承链成环 |
| `WB-REFERRAL-001/002` | 转介目标不存在 / 未开放公开询问 |
| `WB-CANON-001` | 正典未批准，或批准审计事件缺失/对象哈希不符 |
| `WB-SOURCE-001` | 来源未登记、哈希不符、引文定位失败 |
| `WB-AUDIT-001` / `WB-LEDGER-001` | 审计链断裂 / 账本记录哈希不符 |
| `WB-YAML-001..008` | YAML 解析失败、嵌套过深、锚点别名、重复键等 |
| `WB-INDEX-AMBIGUOUS` | 一个关键词命中多份词条（警告，不是错误） |

### 4.6 编译 / 导出 / 三个哈希

`Compile()`（`Application.cs:530/539`）做七件事：建快照（重建两次做输入指纹校验）→ 校验 tier/闭包/token → 算 ReportHash → 出 documents/index + contentHash → 建内容图谱 → `RuntimePackageCompiler.Build` → 产出 9 份报告（validation / source-report / audit-report / id-report / runtime_mapping_report 等）。

`Export()` = CompileCore + 原子发布（lease + 临时目录 → `complete.marker` → 原子 Move → 切 `current.json` 指针）。

三个哈希（`ContractHashing.cs`）：
- `contentHash` = 规范化哈希{runtime.json, index.json}
- `manifestHash` = 去掉 `hashes` 字段后的 manifest 规范化哈希
- `packageHash` = SHA256(hex(manifestHash) ‖ hex(contentHash))

---

## 五、运行层：包在游戏里怎么用

### 5.1 加载

`WorldbookRuntime.LocateManifest()`（`src/WorldbookRuntime.cs:196-209`）从 DLL 目录向上找 6 层，依次试 `<dir>/ModuleData/Worldbook/manifest.json`、` <dir>/Worldbook/manifest.json`。
校验三重 SHA256（`WorldbookPackageIntegrity.cs:26-89`），加载后静态缓存，**无过期、无热重载**（除 Overlay）。
Dev 命令：`worldbook_status / search / reload / edit / export_overlay`。

### 5.2 一次问答的完整链路（`src/NpcDialogueService.cs`）

```
:968 取 WorldbookRuntime.Knowledge
:969-986 构造 WorldbookQuery（身份/文化/王国/聚落/角色/详细度/上限 4096 字节）
:987   BannerlordWorldbookIdentityAdapter.Apply（补 IdentityId / KnowledgeScope / EffectiveDetail）
:1005  WorldKnowledgeQueryService.Query()   ← 权限判定在这里（见 2.4）
:1035  WorldKnowledgeDecisionPolicy.Create
:1050  AllowsAi 门禁
:1054  BuildPromptBlock（拼进 LLM 提示词）
:1069  BuildPersonaProjection
```

### 5.3 死字段（写了但运行时根本不读）

查证结论，做内容时不必为这些字段纠结：

| 字段 | 状态 |
|---|---|
| `indexes`（runtime 包内） | 只用于完整性校验，**loader 完全不读**；查询用的是内存里重建的关键词索引 |
| `index.json` 的 `keywordToEntryIds` | 同上 |
| `WorldKnowledgeReferral.Reason` | 全仓无读 |
| `WorldKnowledgeIdentity.ReferralTargetIds` | 全仓无读 |
| `WorldbookQuery.SceneKeywords` / `ContextModes` | v2 路径无读（只有 legacy 路径用） |

---

## 六、现状与断点（哪里通、哪里断）

### 6.1 通的

| 环节 | 状态 |
|---|---|
| 12 档内容建档 | ✅ 已落库，40 断言 / 40 表达 / 40 授权，全带来源引文 |
| 身份 / 转介登记表 | ✅ 12 profile + 4 referral |
| 权限契约 | ✅ 机器完整实现（Deny>Grant>继承>未知） |
| 运行时消费 | ✅ 游戏里跑着 pilot 小样包，链路打通并已验收（010） |
| 闭合复验工具 | ✅ `tools-r3/validate-authoring-closure.ps1`，16 项检查 |

### 6.2 断的（按严重度）

**① C16 — 12 档里 10 档编译不过（最严重，且是新发现的）**

- 现象：这 10 档的锚点是 `entity.lore.*`，而编译器 `RuntimePackageCompiler.CanonicalEntityRef`（`RuntimePackageCompiler.cs:253`）**只认 `hero/clan/settlement` 三种 kind，其余直接抛 `WB-DOC-003`**；`BuildEntry` 对每档每个 `entity_id` **无条件调用**（同文件 147）。
- 一直没暴露的原因：**Studio 至今一次都没编译出过包**（`compiled/` 与 `export/WorldbookV2/` 实测全空），而游戏里跑的是另做的 pilot 小样。
- 归属：**Studio 侧**（要改 C# 编译器 + 测试夹具）。内容侧不实施，已记为 BLOCKED。
- 2026-09-12 已修其中一个（德里亚特重绑为 `entity.settlement.castle_village_v6_2`），其余 5 个语义锚点仍会触发。

**② C13 — 转介接线未做**

`place_cluster → fallback_referral_ids` 未接。 blockers：referral 登记表被 golden 测试钉死哈希，改它会打掉测试套件并触发 12 档重绑。归 Studio 侧联合批次；本侧交付物 `projection/CLUSTER-REFERRAL-MAP-20260912.json` 已备（5 簇 / 12 条命名 + 逐档映射，`prepared_not_wired`）。

**③ 内容侧：门控有三条路径从未被触发**

（09-12 下午实测，见 `STATUS-20260912.md` 第七节）

| 项 | 实测 |
|---|---|
| `secret` / `unknown` 层 | 各 **0 条** |
| `denies` 否决 | **40/40 全空**（而它是必填字段） |
| `scope` 六档 | 只用了 4 档，`faction` / `private` 为 0 |
| 权限三维度（culture/kingdom/settlement + 年龄 + 管理） | **全为 0** |
| 身份 12 个 | 只用了 6 个 |
| 契约 golden case 6 条 | 内容**一条都没覆盖** |

即：**机器造好了，内容一次都没走过。** 这是批次 1（授权结构做真）要解决的。

### 6.3 一张现状图

```
作者/AI ──> authoring/*.yaml (12 档, needs_review)
                  │
                  │  ← 断点 C16：10 档含 entity.lore.*，编译抛 WB-DOC-003
                  ↓
            [ Studio 编译 ]  ← 至今产物为空
                  ↓
            [ Studio 导出 ]  ← 至今产物为空
                  ↓
       游戏 ModuleData/Worldbook/manifest.json
                  │
                  ↓
       实际在跑的是 pilot 小样包（awake:pilot.worldbook，3 条目 / 6 身份 / 0 转介）
```

---

## 七、术语对照（人话 ↔ 代码）

| 人话 | 代码里的词 |
|---|---|
| 一份词条 | document（档案） |
| 一条客观事实 | assertion（断言） |
| 同一事实的某种说法 | expression（表达） |
| 知道多细 | layer（unknown→rumor→summary→detail→secret） |
| 知道多广 | scope（local→regional→national→faction→elite→private） |
| 是谁 | profile（12 个身份）+ conditions（文化/王国/聚落/角色/年龄/技能…） |
| 谁能看 / 谁不能看 | grants / denies |
| 不知道时指路 | referral（转介） |
| 这个地名挂哪个游戏对象 | entity_ids（锚点） |
| 引文出处 | sources（来源） |
| 编译许可证 | compile-proof（Authority Gate 四步之一） |
| 进游戏的包 | runtime.json + index.json + manifest.json（schema `awake.worldbook.v2`） |

---

## 八、做内容时必须记住的硬约束

1. **锚点两类不可混**：游戏里有同名对象的 → `entity.settlement.*`；游戏里没有的 → 才用 `entity.lore.*`。出处 B9 + ID-MIGRATION-CONTRACT。
2. **实体 ID 一律小写**（`entity.settlement.town_v7`），游戏原生写法是 `town_V7`——别照抄。
3. **改任何基线数据前先查是否被 golden 钉死**：`grep -rin <哈希前 8 位> --include=*.cs --include=*.json`。被钉死 → 属跨线批次，别在内容侧硬做。
4. **AI 草稿必须人工逐条采纳**，改字要重采纳。
5. **知识要分层**：内核绝对中立，观感按身份分层；管理等级高 = 更渊博，不是更"科学"；**严禁现当代科学视角**（中世纪背景）。
6. **不为拆分而拆分**——按边界拆，不按数量拆。

---

**文档状态**：现状梳理，未提交。数据截至 2026-09-12 13:10。
