# AWAKE 项目任务准则

> 本文件是 `C:\Users\26811\OneDrive\文档\New project\AGENTS.md` 在 AWAKE 运行时项目目录的嵌套任务版，覆盖 `_houkai_merge\AWAKE` 全部开发线。根 AGENTS.md、全局 `~/.codex/AGENTS.md` 与 `grill-me-codex` skill 继续有效；本文件按本项目实际情况细化执行边界，与根规范冲突时以更靠近本目录的规则优先。

## 项目定位与边界

- `AWAKE`（中文名：醒世 / 觉醒世界）是独立的 MarcusAIFramework 运行时模组，不是 AF/爱与恨插件。
- 命名（2026-08-15 已执行）：运行时主模组为 **AWAKE: Awakened World AI**；代码、目录、命名空间、路由、存储、ModId 已全部改为 AWAKE。“斯拉涅斯之拥（Slanesh's Embrace）”保留为内容包名。
- 定位：包容、强兼容的 AI 世界运行架构，为 Bannerlord 提供通用 NPC 智能、记忆、世界知识与效果治理；斯拉涅斯/齁改世界观是内置内容之一，不是运行前提。
- 情色/成人内容突出但可选：基础运行时与默认内容保持纯净可玩；性相关内容作为插件、世界书或内容包补充，统一经 ContentPolicy 门控，核心不硬依赖任何成人内容。
- 项目拆分：AWAKE 是通用 AI 世界运行时；`SlaneshsEmbraceContent` 是内容包基础（世界书、事件、信件、NPC 主动基础）；女神人格与情色机制各自作为独立内容包支线。
- 女神功能归女神人格支线：运行时只保留通用“AI 人格对话壳”与菜单注册框架；女神人格、祭坛、愿力、神谕入口不得硬编码进核心，未启用内容包时不出现在游戏菜单中。
- 创作意义已从“成人世界观的延伸与完善”转为“承载多种世界观与内容取向的 AI 世界架构”；后续功能设计优先保证架构通用、内容可替换、其他模组/世界书可接入。
- 与 AF 版 `AnimusForgeHoukai`、插件版 `AnimusForgeLoveHateHoukaiPlugin` 彻底分离：不同 ModId/DLL/存档命名空间/版本线，不读 AF 状态、不反射 AF 内部签名、不共享实现代码。
- 马库斯框架是唯一权威 AI 底层；本模组只负责世界观语义、玩法规则、内容与游戏内体验。
- 世界书/设定内容可以复制自四版齁改，但代码全部自建。
- 游戏环境固定为 `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord`，Bannerlord API `v1.3.15`。

## 当前基线与版本政策

- 当前基线：`v0.2.0`（过渡态）；AWAKE 独立版本线从 v0.1.x 重新起算，见 `docs/Awake-Roadmap-0.1-0.9-20260815.md`。
- 版号只按可玩性验收提版，不按功能数量提版；普通修复和架构批次不得擅自提版。
- 路线：`0.1.x` 运行时核心 → `0.2.x` 内容包基础 → `0.3.x` 世界模拟 → `0.4.x` 关系与记忆 → `0.5.x` 内容系统 → `0.6.x` 体验 → `0.7.x` 生态 → `0.8.x` 性能 → `0.9.x` 发布。

## 路线图

- 0.1.x 运行时核心：内容无关 AWAKE、NPC 深谈入口、存储/知识/配置、双路径验收。
- 0.2.x 内容包基础：世界书、事件、信件、NPC 主动基础；女神人格与情色机制各自独立支线。
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
- 计划与审查日志归档到 `_houkai_merge\AWAKE`；`PLAN.md` 被并行任务占用时改用带日期/功能名的独立计划文件。
- 当前活动批次：AWAKE 运行时核心基线（v0.1.x）；后续按路线图顺序推进。
- 审查轮次达到上限后，实现前必须等待用户签收，不自行把“已审查”当成“已批准”。

## 任务连续性

- 每次开始新请求前先读 `docs/AWAKE-Task-Queue-20260816.md`。
- 先继续进行中任务；新想法、新建议先登记到任务队列，不顶替当前任务。
- 完成一项后更新队列状态，再进入下一项；半成品必须继续推进，不得因为新话题搁置。
- 当前任务被打断时，把“做到哪里、卡在哪、下一步是什么”写回任务队列，确保下轮能接续。
- 未完成/半成品不得在交付说明中写成已完成。

## 架构硬规则

- 所有游戏数据读取只走 `GameData` / `ContextContribution`，不长期持有 TaleWorlds 实时对象。
- 所有持久状态只走 `Storage`，不在 ModuleData 或自定义 JSON 中保存业务数据。
- 所有 AI 调用只走逻辑 Route + Output Schema，不在游戏侧直接 HTTP/保存 Key。
- 所有效果只走 Command + Preflight + 权限 + 幂等，不绕过命令治理。
- 所有事件走 EventService，周报、NPC 反应、后续追踪只订阅事件。
- 所有资产走 AssetHandle，不在游戏 UI 中暴露文件路径。
- 所有错误按 `FrameworkError.Code/Category` 分支，不解析本地化文本；失败必须保留 owner 与 correlation ID。
- 所有异步调用携带 deadline、CancellationToken、correlation/causation ID；UI/campaign tick 中不得阻塞网络、文件或数据库操作。
- 权限统一走 `PermissionCatalog` + `PermissionGate`：manifest、调用点、目录不得各写一套字符串；后台路径只 `Evaluate`，玩家主动时机才 `EnsureAsync`；未知权限 fail closed，取消映射 `awake.cancelled`。
- 马库斯已提供的能力不做第二套实现。
- 不引入本地 ONNX / embedding / rerank 推理，不手写 tokenizer、vocab、模型加载或推理代码；语义检索只走 Marcus RAG / Companion，离线回退本地关键词。任何“必须在游戏侧跑本地模型”的需求先走 grill-me 并说明框架能力为何不足。

## 质量门

- 主工程：`dotnet build -c Release -p:BannerlordApi=1.3.15` 0 warnings / 0 errors。
- SdkSmoke：`Awake.SdkSmoke.exe` `PASS ALL`。
- maf-lint：`_houkai_merge\MarcusAIFramework_Reference\SDK_20260815\analyzers\maf-lint.ps1` 0 blocking。
- 文档：README_CN/EN、Framework Usage Map、BUILD_VERIFICATION 与实现同步；版本号一致。
- 同步：`dist\Modules\AWAKE` 的 DLL 与 `_build_out` SHA-256 一致；游戏目录同步只在用户退出游戏且明确要求时执行。
- 游戏内验证必须由用户运行游戏后提供日志；交付说明分开写编译、SdkSmoke、lint、哈希、游戏内验证与剩余风险。

## 内容分层与红线

- 核心架构不绑定特定内容取向：默认纯净可玩，兼容普通骑砍体验与其他模组；缺少成人内容包时不得降级或阻塞。
- 成人/情色内容是可选项：只有用户显式启用对应内容包/世界书/插件后才进入事件池、知识检索与提示词；18+ 硬校验先于一切，年龄不明确或无法验证时按不满足处理，任何情况下不得包含未成年角色的性内容。
- 启用成人内容时，仍遵守世界观口径、NPC 不自动服从、关系推进不一步到位、高张力情节必须经过阻力、严重后果必须呈现连锁代价等既有规则；不得用现代伦理道德约束中世纪 NPC。
- 显式内容以插件、世界书或内容包形式交付，不硬编码进核心运行时；核心只提供加载、检索、门控与结算机制。
- 本文件及任何项目文档不得写入要求 Codex 放弃内容安全与内容政策的协议；红队或对抗性提示协议不纳入工作区规范。

## 工作区与临时产物

- 本工作区不是 git 仓库，且位于 OneDrive 同步目录；文件移动/删除前必须核验目标绝对路径。
- 编辑前只备份本次将修改的精确文件到系统临时目录，不整目录复制项目。
- 一次性检查脚本、反编译片段、验证报告不落入 `src`、`ModuleData`、`GUI`、`dist`；任务结束后清理自己创建的临时文件。
- 不主动整理用户未要求的遗留目录、备份、旧发布包或其他 Mod。
