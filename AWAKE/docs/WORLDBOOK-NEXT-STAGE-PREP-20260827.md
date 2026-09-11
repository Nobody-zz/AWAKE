# AWAKE 世界知识下一阶段准备说明

- `document_id`: `AWAKE-WORLDBOOK-NEXT-STAGE-PREP-20260827`
- `status`: `prepared_for_user_signoff`
- `evidence_level`: `E0`（准备与边界文件，不代表下一阶段已实现）
- `current_editor_candidate`: `WorldbookStudio-win-x64.zip`
- `current_editor_candidate_sha256`: `645c8fb301aa906c9c78275638f86c8f0c4eb3daed16efddc2cfd5bb75353208`
- `runtime_smoke`: `PASS`
- `code_changes_started`: `false`
- `game_directory_changed`: `false`

## 1. 本文件的结论

世界书编辑器和作者平台已经可以冻结为 v1。下一阶段不再继续重做编辑器，也不从零重写世界书读取器，而是把已有的 V2 编译产物、运行时读取器和 NPC 对话入口接成第一条真实可验证闭环。

下一阶段暂定名称：

> **Worldbook Runtime V1：世界书激活、身份筛选与知识询问首个闭环**

它不是“完整学习系统”，也不是“全部 NPC 自动学习”。首期只证明：游戏能加载一份经过校验的世界知识包，NPC 能按身份返回允许的知识，并能通过明确的知识询问入口完成有限对话和记忆保存。

## 2. 已确认的现状

### 2.1 编辑器到运行时的格式已经存在

- Worldbook Studio 的 `RuntimePackageCompiler` 已将作者档案编译为 `awake.worldbook.v2` 的 `runtime.json`、`index.json` 和 `manifest.json`。
- AWAKE 的 `WorldbookPackageIntegrity` 已能校验相对路径、Schema、包 ID、版本、索引一致性和三层 SHA-256。
- `WorldbookRuntime` 已能识别 V2 包或 V1 registry，并创建 `WorldKnowledgeQueryService`。
- `WorldKnowledgeLoader` 已能构建条目、表达、身份、转介和关键词索引。
- `WorldKnowledgeQueryService` 已能产生 `known`、`partial`、`referral`、`blocked`、`not_found` 五种结果，并处理身份、范围、详细度、条件和否定规则。

### 2.2 当前仍存在的真实缺口

- 工作区 `ModuleData/Worldbook/manifest.json` 仍是 `awake.worldbook.v1`，不能把它误称为新的 V2 运行时候选。
- `WorldbookRuntime.EnsureCreated()` 的正常战役生命周期接线尚未形成当前 BuildId 的 E4 证据；现有调用主要出现在测试/探针和开发者重载路径。
- NPC 对话代码已经读取 `WorldbookRuntime.Knowledge`，但“联系人卡片 → 询问知识 → 询问专用会话”入口尚未实现并验证。
- B3 询问-only 计划和实施合同已通过独立审查，但 `user_signoff=false`、`implementation_authorized=false`，因此不能据此直接改运行时代码。
- 真实云端 Provider、本机 Worker、游戏目录同步、Bannerlord 实机和存档恢复仍没有当前批次证据。

## 3. 下一阶段分批顺序

### N0：契约与候选准备

目标是锁定输入和输出，不写运行时代码。

- 冻结 Worldbook Studio Authoring Contract v1。
- 以 Studio 的 V2 导出格式作为运行时唯一输入，不让 Runtime 读取 YAML、来源全文或 AI 草稿。
- 明确 V1 旧目录只作为迁移/对照来源；未经新 BuildId 验证，不覆盖现有运行时内容。
- 建立一份最小 V2 fixture，包含普通平民、贵族、士兵、头人和可转介 NPC。

### N1：编译—加载—查询离线闭环

目标是证明编辑器输出可以被 AWAKE 读取器正确消费。

```text
作者档案 fixture
→ Studio compile
→ V2 runtime/index/manifest
→ AWAKE hash verify
→ WorldbookRuntime load
→ identity-aware query
→ structured result
```

最低验收覆盖：

- 普通平民只能得到摘要或传闻；
- 贵族年龄/管理能力条件生效；
- 士兵得到战争知识；
- 头人得到本国知识而非外国知识；
- 不知道时只返回转介，不泄漏受限正文；
- 包损坏、哈希不一致或内容门禁失败时 fail closed；
- 读取结果包含来源包版本、命中 ID、状态和错误码。

### N2：安全战役生命周期激活

目标是把读取器从测试路径接入安全的战役会话生命周期。

约束：

- `OnSubModuleLoad` 只做轻量初始化，不访问 `Campaign.Current`。
- 在当前版本已核验的战役就绪点执行一次激活，并保证单次会话不重复加载。
- 激活失败时保留原游戏行为，NPC 查询返回明确的不可用状态，不调用 AI 伪造知识。
- 激活、包 ID、版本、哈希、条目数和警告写入 AWAKE 日志。
- 本批不覆盖游戏目录，不修改旧 V1 内容，不修改 Marcus 框架。

### N3：B3 知识询问-only 纵向闭环

复用已经审查通过的 B3 实施合同，不另造聊天系统：

```text
联系人卡片“询问知识”
→ KnowledgeInquiry 模式
→ B2 世界知识查询
→ known/partial 最多一次 AI 生成
→ 其他状态代码直接回答
→ 一条短期询问记忆
→ Storage 保存
→ 重开 NPC 后有限连续性
```

硬边界：

- 询问模式不得触发关系、承诺、金币、世界效果或通用 Transcript；
- AI 只负责角色化表达，不能改变客观知识状态；
- Provider 不自动重试；
- 不实现玩家传授、NPC 自主学习、跨 NPC 传播、周报和完整事件联动；
- 所有写入必须沿用现有 Storage 和 `inquiryId` 幂等规则。

### N4：离线证据与候选生成

- Release build：0 warnings / 0 errors。
- 编译器—运行时 fixture 全部通过。
- B3 focused tests、负向副作用测试、Storage round-trip 通过。
- 生成新的 BuildId，并分别记录 E1、E2 证据。
- 只有明确授权后，才进入 E3 同步；不自动同步游戏目录。

### N5：用户运行验证

由用户运行匹配 BuildId 的包后提供证据：

- E3：root、dist、游戏目录和测试包文件及哈希一致；
- E4：真实打开联系人、询问 NPC、看到正确知识状态和代码/AI回答；
- E5：保存、退出、重载后询问记忆和激活状态正确恢复。

## 4. 本阶段不做的事

- 不继续扩大 Worldbook Studio UI，除非作者试用发现阻塞性问题。
- 不重写五大分类、二级分类或作者字段语义。
- 不制作完整卡拉迪亚知识库内容批次。
- 不把 1084、1086、1090 等年份直接当作 NPC 当前可知性的硬过滤器。
- 不让所有 NPC 自动学习或逐个调用 AI；后续知识更新使用事件、周报和统一状态投影。
- 不接入真实云端 Provider/Worker 作为本阶段完成条件。
- 不改 Marcus 正在同步的框架线，不启动 Bannerlord，不结束其他进程。

## 5. 启动门槛

进入 N1/N2/N3 代码实现前，必须同时满足：

```text
现有编辑器契约保持冻结
→ 本文件边界无新增未审查决策
→ B3 实施合同获得用户签收
→ implementation_authorized=true
→ 实施前 Bannerlord/AI/存档边界复核通过
```

如果实现中发现必须修改世界书 V2 格式、保存字段、Provider 合同、普通对话副作用边界或游戏目录迁移方式，应停止当前写集，修订计划并重新审查，不能在代码里临时扩大范围。

## 6. 当前唯一推荐动作

先将编辑器独立修复包交给世界书作者做离线试用，同时保留 B3 合同等待签收。作者试用反馈不阻塞 N1 的离线 fixture 准备，但在反馈收回前不重新设计编辑器底层格式。

本文件只完成下一阶段准备，不代表 N1、N2 或 N3 已实现。
