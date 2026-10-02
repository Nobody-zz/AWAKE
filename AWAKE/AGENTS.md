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
- 游戏环境固定为 `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord`，Bannerlord API **`v1.4.8`**（2026-10-02 实测；1.3.15 已于 09 月被淘汰，不要再按 1.3.15 建参考）。

## 当前基线与版本政策

- **版本目标与当前状态都读 `docs/AWAKE-ROADMAP.md`**——该文件的「现状」一节是**唯一的现状权威**。
  ⚠️ **2026-09-14 状态控制面归一化**：`docs/AWAKE-CURRENT.md` 已退役并改名为 `AWAKE-STATE-HISTORY.md`（它是 08-24～09-11 的历史流水，不是当前状态）；`docs/AWAKE-VALIDATION.md` 与 `docs/control-plane/CURRENT.json` 同为 09-11 停更件。**三者均不得据以判断当前状态。**
- 版号只按可玩性验收提版，不按功能数量提版；普通修复和架构批次不得擅自提版。
- 路线：**一律以 `docs/AWAKE-ROADMAP.md` 的「版本阶梯」为准**（v0.1 点亮 → v0.2 记得住 → v0.3 活起来 → v0.4 有脸 → v0.5 撑得住 → v1.0，2026-09-14 重排）。下面「路线图」节那张 `0.1.x~0.9.x` 串行表已随之退役，仅作历史参考，**不得据以讨论进度**。
- ⚠️ **本阶段不要用版本号讨论进度**：上一条路线是**串行**设计，而项目实际按**六条会话并行**推进（见「工作流程」节），已跨过其中多个版本目标。进度一律按「离游戏内生效还差什么」讨论。
- **推进方式（2026-09-14 起）**：主干 · 世界书 · 角色卡 · UI 编辑层 · 美术资产 ＝ **五条专线**，各一条会话；另有**一条全局主控**（只盘点状态与方向、不写代码）。**不再是"单 agent 单批次串行"模型。**
  ⚠️ **美术资产线的现役会话＝「本地生图」**（工作台 `C:/Users/26811/WorkBuddy/LocalAIPictureGeneration`，接本机 ComfyUI `127.0.0.1:8188`）；
  原「美术资产」会话已于 **09-15 退役**（先想用 agent 接工具生图，后另起本地 ComfyUI），职能已交接（含 09-15 交付的 4 件 UI 控件资产）。
  其交付产物落在 `tools/awake-art-lab/out/`，**该目录被 `.gitignore` 排除** ⇒ **只看 git 会误判"美术线没产物"**。

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
- 未启用服务：Tools / UiRegistry / CapabilityBroker；相关功能必须等框架接入或使用自建降级，不得写成“已有现成能力”。
- **`Assets` 与 `Media` 已于 2026-10-02 接入**（片 2/4，`docs/PLAN-IMAGE-PORT-TO-FRAMEWORK-20260915.md`）：
  `HostApi` 把 `media`/`assets` 从运行时端口注入（`runtime as IXxxService ?? 空壳`，默认组合与显式注入可区分）；
  生图走 `provider.image.v1`（逻辑 Route，运行时反射调 Provider，出图字节入内容寻址资产库、回帧只带 `AssetHandle`），
  资产读回走 `asset.read` 分块（单块 64 KiB，卡住协议 256 KiB 单帧上限）；
  立绘调用方经 `AwakePortraitGenerator` 走 `Host.Media` + `Host.Assets`，`AwakeImage*.cs` 保留为回退路径。
  ⚠️ 该批次最高证据等级：**框架侧 E3**（离线判据 + 同步哈希一致），**mod 侧只有 E1**——真机 E4 尚未取。
  ⚠️ 替换口仍然只有 5 个（`Permissions/Prompts/Storage/Rag/GameData`）；`Events`/`Models`/`Log` 仍是写死的空壳，属"改框架活"不是"接线活"。

## 工作流程

> ⚠️ **本节 2026-09-14 重写。** 原文为 codex 单会话时代所写：要求走 `grill-me-codex` 审查流程、
> 由 `bannerlord-mod-development-orchestrator` 统一调度、并规定"同时最多一个 `implementing` 批次"。
> **codex 已于 2026-09-13 停手**，那两个 skill **当前无人执行**（仍留在 `~/.codex/skills/`），单批次约束**不再适用**。

- **多线并行**：每条线自己排批次、自己定节奏；涉及跨线的事（共享文件、接口、投送）**先报备再动**。
- **提交纪律**：每个小任务**只暂存自己拥有的路径**，提交前必查 `git diff --cached --name-only`；
  **禁 `git add -A` / `git add .` / `git reset --hard` / `git clean`**；不回退别人未提交的改动。
  混编文件（多人同时改的）按「**谁认领谁交**」处理。
- 计划与审查日志归档到 `AWAKE\docs`；`PLAN.md` 被并行任务占用时改用带日期/功能名的独立计划文件。
- **构建成功 ≠ 游戏内验证**：本地 smoke 不得称游戏实测；证据分级见下「状态、候选与证据」节。
- 同步游戏目录、启动游戏、不可逆覆盖、正式提版、发布和计划外产品决策仍需用户明确授权。

## 跨 agent 共享知识

> **2026-10-01 新增。** 背景：本工作区同时有**多个 agent 系统**在跑，各家的 skill 目录互不可见——
> DSH（`<dshHome>/skills`）、WorkBuddy（`~/.workbuddy/skills`，56 个）、Codex（`~/.codex/skills`，37 个，09-13 停手）、
> 以及 zcode / Trae 等。同一个仓库被 4 套 agent 碰，而每套只看得见自己那份 skill。

- **凡跨 agent 的共享知识一律进仓库**，不得只写在某个 harness 的私有 skill 目录里。
  否则每个 harness 各维护一份私有副本，然后各自漂移——这正是本仓库已经受够的病
  （同族问题见 `docs/REVIEW-DESIGN-20261001.md`「多个权威源互相打架」）。
- **harness 专属的东西才留在各家的 skill 目录**：例如「本机没有 bash 工具」「用 `py -3`」
  「该 harness 的工具名」「它的沙箱模式」。这类内容对别的 harness 是**错的**，不能进仓库。
- **仓库工具放 `AWAKE/tools/`**，任何 agent 都能跑；各家的 skill 只做薄壳与指针。
  现有跨 agent 工具：
  - `tools/check-ps1.ps1` —— `.ps1` 的 BOM / 非 ASCII / 行尾 / 语法自检（纯 ASCII，自带不需 BOM）
  - `tools/assert-gate.ps1` —— 断言一道门的退出码与输出模式（"红在对的理由上"的可执行形式）
  - `tools/gen_skill_refs.py` —— 把发现提取的 JSON 渲染成参考文档
- **共享参考文档放 `AWAKE/docs/reference/`**，由 `gen_skill_refs.py` 生成，**不要手改**（会被重跑覆盖）。
  现有：`bannerlord-facts.md`、`bannerlord-gauntlet-ui.md`、`worldbook-pipeline.md`、`reverify-queue.md`。
- 参考文档里的 `valid_for` 是**测量版本**，不是当前版本。本机游戏已从 `v1.3.15` 升到 `v1.4.8`，
  **引用任何数字或否定式断言前先核当前版本**；否定式断言（"原版做不到 X"）风险最高。
- **同一条纪律对文档也适用**：新知识写进仓库文档，不要只写进某个 agent 的会话记忆或私有目录。

## MCM 菜单规则

- 每做一个功能，必须评估 MCM 菜单是否需要调整或新增调控项；评估结论写进 PLAN。
- 只要功能存在游戏内玩家可调行为（开关、频率、阈值、强度、预设、快捷键等），就必须评估 MCM 入口，且默认值 fail-safe；作者工具、CLI、内容编辑字段和离线测试参数不自动强制进入 MCM。
- 如果游戏内功能不需要 MCM，必须在 PLAN 中写明理由（例如纯内部管道、无玩家可调参数）；工具侧配置应在对应工具计划中说明。
- MCM 改动必须同步检查菜单分组、中英文、预设联动、`Config.json` 兼容与开发者调试入口是否分开。

## Gauntlet 列表顺序规则

- 修改通讯录、对话历史、关系行、分组名册、修改器或列表详情页面前，必须先确认列表的规范逻辑顺序口径。
  ⚠️ 原文要求的加载入口「根目录 `.agents/skills/verify-bannerlord-gauntlet-list-order/SKILL.md`」**该路径不存在**（2026-09-14 核实）；
  现用技能体系为 `~/.workbuddy/skills/`，其中**没有**对应的列表顺序技能 ⇒ 这条目前**无工具可依**，按下两条实质要求自行守。
- AWAKE 的动态绑定列表必须维护唯一规范逻辑顺序，只在写入绑定列表边界反向一次；不得通过反转联系人领域数据、稳定 ID、关系阶段阈值或 XML 与数据双重反转来“修正”视觉顺序。
- 发布前除 XML、编译和静态门禁外，必须由用户实机验收首尾联系人、分组展开/折叠、底部条目详情、返回后的滚动位置以及 0/满值关系条；未实机验证不得声明视觉顺序已修复。

## 状态、候选与证据

- `awake-task-continuity`（codex 时代的恢复技能，**现无人执行**）原本只恢复 `AWAKE-CURRENT.md` 中的活动批次、执行租约、候选 BuildId、阻断项和唯一 `next_action`。**该文件已于 2026-09-14 退役**（→ `AWAKE-STATE-HISTORY.md`）⇒ 此条**失去依据**；当前状态一律读 `AWAKE-ROADMAP.md` 的「现状」节。
- 活动批次中断时更新独立 `docs/checkpoints/<task_id>-checkpoint.md`；历史任务队列只追加里程碑，不再承载逐轮运行状态。
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

- 主工程：`dotnet build -c Release -p:BannerlordApi=1.4.8` 0 warnings / 0 errors。
- **`AWAKE.Tests` 也必须编得过**（`dotnet build ..\AWAKE.Tests\AWAKE.Tests.csproj -c Release` 0 errors）。
  ⚠️ `AWAKE.Tests.csproj` 是**显式 `<Compile Include>` 清单**，而 `AWAKE.csproj` 那边是 `src\**\*.cs` 通配 ⇒
  **往 `src\` 新增任何源文件都必须同步加进这份清单**，否则主工程编得过、测试工程炸 CS0246。
  这个坑已踩过四次（清单里留了四条同款补记）。2026-10-02 就是它被 `build.ps1` 第二步抓出来的。
- **本机 `dotnet` 必须带 `-m:1`**：默认并行（worker nodes）会让 `restore` / `build` **静默假失败**
  （打印「生成失败 / 0 个警告 / 0 个错误」，约 1.5~2s 返回 exit 1，**零诊断**）。
  逐开关实测只有 `-m:1` 管用（`--disable-parallel`、`-p:RestoreDisableParallel=true`、`-nodeReuse:false` 都不行）。
  `tools\package_embedded_runtime.ps1` 已在 `Invoke-Dotnet` 收口点统一补上；`build.ps1` 内部的 `dotnet build` **没有补**，
  所以 `build.ps1` 在本机跑不到 `TESTS_OK`——要拿真结果就按上面两步手工跑（MSBuild + `dotnet build … -m:1`），别把它的假红当成代码问题。
- SdkSmoke：`Awake.SdkSmoke.exe` `PASS ALL`（2026-10-02 实测 `RESULT total=70 passed=70 failed=0`）。
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
