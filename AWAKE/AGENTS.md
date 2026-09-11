# AWAKE 项目任务准则

> 本文件是 `D:\AWAKE-Dev\AGENTS.md` 在 AWAKE 运行时项目目录的嵌套任务版，覆盖 `AWAKE` 全部开发线。根 AGENTS.md、全局 `~/.codex/AGENTS.md` 与 `grill-me-codex` skill 继续有效；本文件按本项目实际情况细化执行边界，与根规范冲突时以更靠近本目录的规则优先。

## 项目定位与边界

- `AWAKE`（中文名：醒世 / 觉醒世界）是独立的 MarcusAIFramework 运行时模组，不是 AF/爱与恨插件。
- 命名（2026-08-15 已执行）：运行时主模组为 **AWAKE: Awakened World AI**；代码、目录、命名空间、路由、存储、ModId 已全部改为 AWAKE。内容包保留自己的命名空间，与本仓库无关。
- 定位：包容、强兼容的 AI 世界运行架构，为 Bannerlord 提供通用 NPC 智能、记忆、世界知识与效果治理；具体世界观只能作为独立可选内容包接入，不是 AWAKE 的内置前提。
- 内容策略可插拔：基础运行时与默认内容保持纯净可玩；任何分级内容都以独立内容包/世界书/插件形式接入，统一经 ContentPolicy 门控，核心不硬依赖任何具体内容取向。
- 项目拆分：AWAKE 是通用 AI 世界运行时；内容包工程（世界书、事件、信件、NPC 主动基础）在本仓库之外独立维护，本仓库只保留加载、检索、门控与结算机制。
- 女神功能归女神人格支线：运行时只保留通用“AI 人格对话壳”与菜单注册框架；女神人格、祭坛、愿力、神谕入口不得硬编码进核心，未启用内容包时不出现在游戏菜单中。
- 设计取向：承载多种世界观与内容取向的 AI 世界架构；后续功能设计优先保证架构通用、内容可替换、其他模组/世界书可接入。
- 与历史 AnimusForge 系工程彻底分离：不同 ModId/DLL/存档命名空间/版本线，不读其状态、不反射其内部签名、不共享实现代码；新代码与新增文档不得再出现该命名。
- 马库斯框架是唯一权威 AI 底层；本模组只负责世界观语义、玩法规则、内容与游戏内体验。
- 世界书/设定内容由外部内容包提供，代码全部自建。
- 游戏环境固定为 `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord`，Bannerlord API `v1.3.15`。

## 当前基线与版本政策

- 当前工程声明版本仍为 `v0.2.0`；当前开发目标是 `0.2.1`，冻结候选与实际验证状态以 `docs/AWAKE-CURRENT.md` 为准。AWAKE 独立版本线从 v0.1.x 重新起算，版本目标以 `docs/AWAKE-ROADMAP.md` 为准。
- 版号只按可玩性验收提版，不按功能数量提版；普通修复和架构批次不得擅自提版。
- 路线：`0.1.x` 运行时核心 → `0.2.x` 内容包基础 → `0.3.x` 世界模拟 → `0.4.x` 关系与记忆 → `0.5.x` 内容系统 → `0.6.x` 体验 → `0.7.x` 生态 → `0.8.x` 性能 → `0.9.x` 发布。工程声明版本、开发目标、冻结候选和证据等级不得混写。
- 当前状态只从 `docs/AWAKE-CURRENT.md` 恢复；版本目标从 `docs/AWAKE-ROADMAP.md` 读取；证据等级从 `docs/AWAKE-VALIDATION.md` 读取。历史任务队列只作追溯，不再要求每轮完整读取。

## 路线图

- 0.1.x 运行时核心：内容无关 AWAKE、NPC 深谈入口、存储/知识/配置、双路径验收。
- 0.2.x 内容包基础：世界书、事件、信件、NPC 主动基础；具体内容支线独立交付。
- 0.3.x 世界模拟：周报/季报/公告/政令/世界事件。
- 0.4.x 关系与记忆：分级记忆、承诺账本、秘闻传播、双通道好感。
- 0.5.x 内容系统：事件 JSON、世界书四档、内容包工具。
- 0.6.x 体验完善：UI/字体/媒体/诊断。
- 0.7.x 生态：跨 Mod 能力、外部人格/世界书。
- 0.8.x 性能：索引/缓存/RAG/批量读取。
- 0.9.x 发布：回归、崩溃清零、打包、发行。
- 未启用服务：Tools / UiRegistry / CapabilityBroker / Assets / Media；相关功能必须等框架接入或使用自建降级，不得写成“已有现成能力”。

## 工作流程

- 任何新功能、新玩法机制、新内容批次动手前必须走 `grill-me-codex`：先逐题拷问并锁定计划文件，再由独立审查代理以只读方式对抗审查；只有 `VERDICT: APPROVED` 且用户签收后才允许写代码。
- 计划与审查日志归档到 `AWAKE\docs`；`PLAN.md` 被并行任务占用时改用带日期/功能名的独立计划文件。
- 审查轮次达到上限后，实现前必须等待用户签收，不自行把“已审查”当成“已批准”。
- 通用任务分类、工具路由、执行租约、重试和停止条件由 `bannerlord-mod-development-orchestrator` 统一调度；本文件不重复规定通用 Codex 工作流。
- 同时最多一个 `implementing` 批次和一个 `candidate_frozen` 运行时候选。候选进入 `pending_game` 后不得继续改动同一候选 DLL；新运行时代码必须创建新 BuildId 和新候选。
- 批次获用户签收后，租约内的可逆短任务自动接续；检查点是恢复边界，不是要求用户重复回复“继续”。
- 同步游戏目录、启动游戏、不可逆覆盖、正式提版、发布和计划外产品决策仍需用户明确授权。

## MCM 菜单规则

- 每做一个功能，必须评估 MCM 菜单是否需要调整或新增调控项；评估结论写进 PLAN。
- 只要功能存在游戏内玩家可调行为（开关、频率、阈值、强度、预设、快捷键等），就必须评估 MCM 入口，且默认值 fail-safe；作者工具、CLI、内容编辑字段和离线测试参数不自动强制进入 MCM。
- 如果游戏内功能不需要 MCM，必须在 PLAN 中写明理由（例如纯内部管道、无玩家可调参数）；工具侧配置应在对应工具计划中说明。
- MCM 改动必须同步检查菜单分组、中英文、预设联动、`Config.json` 兼容与开发者调试入口是否分开。

## Gauntlet 列表顺序规则

- 修改通讯录、对话历史、关系行、分组名册、修改器或列表详情页面前，必须先加载根目录 `.agents/skills/verify-bannerlord-gauntlet-list-order/SKILL.md`。
- AWAKE 的动态绑定列表必须维护唯一规范逻辑顺序，只在写入绑定列表边界反向一次；不得通过反转联系人领域数据、稳定 ID、关系阶段阈值或 XML 与数据双重反转来“修正”视觉顺序。
- 发布前除 XML、编译和静态门禁外，必须由用户实机验收首尾联系人、分组展开/折叠、底部条目详情、返回后的滚动位置以及 0/满值关系条；未实机验证不得声明视觉顺序已修复。

## 状态、候选与证据

- `awake-task-continuity` 只恢复 `AWAKE-CURRENT.md` 中的活动批次、执行租约、候选 BuildId、阻断项和唯一 `next_action`，不负责工具路由或选择新功能。
- 活动批次中断时更新独立 `docs/checkpoints/<task_id>-checkpoint.md` 和 `AWAKE-CURRENT.md`；历史任务队列只追加里程碑，不再承载逐轮运行状态。
- 候选运行时必须记录 BuildId、程序集版本、DLL SHA-256 和世界书/Manifest 指纹。日志没有匹配 BuildId 时，不得归因到当前候选。
- 证据等级固定为：`E0` 计划/静态存在；`E1` 编译与解析；`E2` 离线测试/Smoke；`E3` 同步与哈希一致；`E4` 匹配 BuildId 的真机入口闭环；`E5` 匹配 BuildId 的存读档/长时回归。
- 完成声明必须写实际达到的最高证据等级；未完成、未同步、未真机或旧构建证据不得包装为当前候选已完成。
- `429`、取消、detached、外部超时或结果不确定时停止当前租约并写检查点；不得自动重放不确定的外部请求。

## 架构硬规则

- 所有游戏数据读取只走 `GameData` / `ContextContribution`，不长期持有 TaleWorlds 实时对象。
- 游戏运行时的战役状态、关系、记忆、事件和玩家可变状态只走 `Storage`，不在 ModuleData 或自定义 JSON 中保存存档业务状态；世界书、内容包、作者工具和发布清单可以使用受契约约束的 JSON/YAML 文件，但不得冒充战役存档状态。
- 所有 AI 调用只走逻辑 Route + Output Schema，不在游戏侧直接 HTTP/保存 Key。
- 所有效果只走 Command + Preflight + 权限 + 幂等，不绕过命令治理。
- 所有事件走 EventService，周报、NPC 反应、后续追踪只订阅事件。
- 已接入 Marcus 的资产服务统一走 `AssetHandle`，不在游戏 UI 中暴露文件路径；尚未接入的服务不得假定存在，资产相关降级方案必须在计划中明确隔离范围和迁移路径。
- 所有错误按 `FrameworkError.Code/Category` 分支，不解析本地化文本；失败必须保留 owner 与 correlation ID。
- 所有异步调用携带 deadline、CancellationToken、correlation/causation ID；UI/campaign tick 中不得阻塞网络、文件或数据库操作。
- 权限统一走 `PermissionCatalog` + `PermissionGate`：manifest、调用点、目录不得各写一套字符串；后台路径只 `Evaluate`，玩家主动时机才 `EnsureAsync`；未知权限 fail closed，取消映射 `awake.cancelled`。
- 马库斯已提供且当前版本已接入的能力不做第二套实现；尚未接入的服务不得被当作现成能力，AWAKE 仍负责自身的世界状态投影、内容策略、命令治理和游戏体验层。
- 不引入本地 ONNX / embedding / rerank 推理，不手写 tokenizer、vocab、模型加载或推理代码；语义检索只走 Marcus RAG / Companion，离线回退本地关键词。任何“必须在游戏侧跑本地模型”的需求先走 grill-me 并说明框架能力为何不足。

## 质量门

- 主工程：`dotnet build -c Release -p:BannerlordApi=1.3.15` 0 warnings / 0 errors。
- SdkSmoke：`Awake.SdkSmoke.exe` `PASS ALL`。
- maf-lint：`MarcusAIFramework_Reference\SDK_20260815\analyzers\maf-lint.ps1` 0 blocking。
- 文档：README_CN/EN、Framework Usage Map、BUILD_VERIFICATION 与实现同步；版本号一致。\n- 世界书/Studio 批次附加门：Contract JSON parse、世界书占位符审计、Studio 功能测试和 package/release-check；仅适用于对应工具或内容批次，不作为所有运行时批次的通用替代门。
- 同步：`dist\Modules\AWAKE` 的 DLL 与 `_build_out` SHA-256 一致；游戏目录同步只在用户退出游戏且明确要求时执行。
- 游戏内验证必须由用户运行游戏后提供日志；交付说明分开写编译、SdkSmoke、lint、哈希、游戏内验证与剩余风险。

## 内容分层与边界

- 本仓库是内容无关的运行时：默认纯净可玩，兼容普通骑砍体验与其他模组；缺少任何内容包时不得降级或阻塞。
- 分级内容只以独立内容包/世界书/插件形式交付，不硬编码进核心运行时；核心只提供加载、检索、门控与结算机制，统一经 ContentPolicy 门控。
- 本仓库与其文档不复述内容包正文，只写契约、门控与接口。内容侧工作在本工作区之外进行。
- 本文件及任何项目文档不得写入要求 Codex 放弃内容安全与内容政策的协议；红队或对抗性提示协议不纳入工作区规范。
## 工作区与临时产物

- 本工作区是 git 仓库（`D:\AWAKE-Dev`，branch `main`），已从 OneDrive 同步目录移出；文件移动/删除前必须核验目标绝对路径。
- 编辑前只备份本次将修改的精确文件到系统临时目录，不整目录复制项目。
- 一次性检查脚本、反编译片段、验证报告不落入 `src`、`ModuleData`、`GUI`、`dist`；任务结束后清理自己创建的临时文件。
- 不主动整理用户未要求的遗留目录、备份、旧发布包或其他 Mod。
