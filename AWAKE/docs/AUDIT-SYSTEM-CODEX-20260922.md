# AWAKE 作业单复核与现有代码首轮评分

## 读数时点与范围

- 采样时间：2026-09-22 13:58–14:07，Asia/Shanghai。起始 `git rev-parse HEAD`：`ee40e8dfb7a31bc85f103d3cd079bcccf1671d43`。工作区有并行未提交修改，以下源码结论针对采样工作树，不等于该 commit 单独的结果。
- 用户本轮要求：先检查审计作业单，再到工作区检查现有代码并打分。本报告是首轮风险抽查和评分，**不是原作业单 Q1–Q8 全量验收完成声明**。
- `AWAKE/tools/build.ps1 -Configuration Debug`：本轮退出 0，主模组与测试工程构建成功；测试工程 4 个 CS4014 警告，位于 `AWAKE.Tests/Program.cs:464,2629,2695,3783`。
- `AWAKE.Tests/bin/Debug/net472/Awake.SdkSmoke.exe`：本轮退出 0，注册的 64 个用例全部通过。没有运行游戏或同步游戏目录。
- `rg --files AWAKE/src -g '*.cs'` 后逐文件统计：164 个 C# 文件、43,863 行（含空白和注释）。最大文件 `WorldStateStore.cs` 4,400 行；`NpcDialogueService.cs` 1,767 行；`AwakeTerminalBehavior.cs` 1,403 行；`AwakeRuntime.cs` 1,356 行。
- `git status --porcelain=v1` 在 14:02 返回 37 条状态记录；包括未跟踪项，不能解释成 37 个缺陷或 37 个本轮改动。
- 本轮没有改生产源码、测试源码、GUI、内容包、路线图。临时故障注入只实例化已编译的真实文件存储类，使用不可写临时目标；没有读写玩家存档。

## 一、先审作业单：方向正确，预设结论需要修订

作业单适合当调查目录，但不能把“事实层”“必须非空”等要求当成事实裁决。

1. **“四个真机问题全部零离线覆盖”不成立。** `WorldFactJournal.cs:128-135` 已将空串解释为 Missing；`WorldStateStore.cs:3671-3678` 已增加损坏账本隔离后继续写入；测试 `Program.cs:2170,2203,2264` 分别覆盖 codec、首次写读、坏 root 恢复。这证明相关子问题已做离线修复，不证明 09-14 的全部现场成因已清除。只检查 `AwakeRuntime.cs` 和 `NpcDialogueVM.cs` 的修改日期无法判断存储缺陷是否修复。
2. **单程序集不阻止分笔提交。** `AWAKE.csproj:23` 通配编译会扩大编译范围，但 Git 按文件和差异块提交，与程序集数量无直接因果。拆程序集也不会自动解决共用索引和宽 pathspec 夹带。
3. **“本机只有 PS5.1”需限定进程环境。** 本轮 `Get-Command pwsh,python,node` 实测 Codex PATH 中存在缓存运行时的 `pwsh.exe` 和 `node.exe`；Python 命中 WindowsApps alias，尚未验证可执行。角色卡脚本硬调 `pwsh` 的事实成立，但“当前环境必然跑不起来”不成立。其他 Agent 的 PATH 可能不同。
4. **“没有 NOTICE”过时。** `rg --files -g '*LICENSE*' -g '*NOTICE*' -g '!**/obj/**' -g '!**/bin/**'` 命中 `AWAKE/THIRD-PARTY-NOTICES.txt`，文首记载 09-17 生成。该文件存在不等于许可证归属和发布包分发已审完；这次不作法律合规结论。
5. **用强制配额约束发现会诱发凑问题。** Q8 应要求独立探索和有效证据，不应以“不在已有清单的一半”代替严重度。找不到新问题也是可接受结果。
6. **“所有未执行契约”“全框架调用点数”需要可计算口径。** 原始文本扫描不能证明语义调用、反射注册、XML 绑定全部可达。应明确项目范围、符号范围、是否包含测试和废弃链；先报候选，再人工核实零调用。
7. **只读与变异检验并非无法并存，但需明确隔离。** 变异应在临时副本或隔离测试产物进行，禁止修改共享生产树。本轮未做三个代码变异实验，不以负向样本测试冒充 mutation testing。
8. **先约定损坏恢复的产品语义。** 把坏 root 重建成空账本不是天然正确答案；要分别定义可用性恢复、历史可检索性和原始数据保留。

## 二、评分

评分基准：100 为核心路径与失败语义均有可靠验证、适合持续维护；80 为主要边界闭合；60 为有可复用实现但关键可靠性仍待补齐；40 为基础设施故障足以影响正常使用。不是工作量百分比，也不是发布进度。

| 维度 | 分数 | 权重 | 依据 |
| --- | ---: | ---: | --- |
| 架构与边界 | 70 | 20% | 有服务契约、内容投影、命令 Preflight；但存储实现与上层保证不一致，静态运行时依赖集中 |
| 状态正确性与故障恢复 | 40 | 25% | 已实证失败写入仍内存可见；历史隔离恢复及跨记录提交存在缺口 |
| 测试可信度 | 65 | 20% | 64/64 重跑通过，含有价值负向断言；主 smoke 未覆盖完整对话 UI/信件服务，真实适配器失败路径漏测 |
| 可维护性 | 70 | 15% | 服务和模型已拆文件，错误码较清楚；4,400 行多业务 Store 与共享静态状态增加修改成本 |
| 工具与状态记录 | 55 | 10% | 构建入口有效；测试显式源清单、跨进程 PATH 差异、过时结论与共享提交风险仍在 |
| 功能接线与可观察性 | 70 | 10% | 对话确认、正式/预览周报、信件账本都有真实调用路径；UI、跨会话和保存恢复仍有证据缺口 |
| **工程综合** | **60/100** | **100%** | 加权结果；基于首轮抽查，保留约 ±10 分的判断不确定性 |

另列而不混算：**玩法闭环证据约 55/100；发布准备度约 35/100。** 这是证据成熟度评价，不表示“45% 功能没写”或已量出故障概率。角色卡文笔、美术效果与世界书内容品质未独立评分；本轮只检查它们与运行时连接的工程边界。

值得保留：`WorldCommandBridge.cs:115` 的 Preflight、`NpcDialogueService.cs:1371` 的确认入口、包 hash 校验、正式/预览/不可用状态区分，以及事实日志的正反例。建议先补现有设计的失败语义，不以全面重写或拆程序集作为首要行动。

## 三、问题清单

分级：P0 表示已证明的普遍阻断或严重不可逆损害；P1 表示正常可达失败条件下会错误结算、丢失可查询状态或阻断核心功能；P2 表示维护、证据或工程防护缺口。本轮未确认 P0；“未确认”不表示全体系没有 P0。

### P1-01：写盘失败后读回仍像成功，突破上层持久化判据

现象：`AwakeFileStorageService.cs:163-171` 先改 `Load()` 返回的缓存，再调用 `Save()`；失败不恢复缓存。`Load():211` 直接返回缓存。删除同样先 `Remove` 再保存（`:187`）。

影响：`WorldStateStore.cs:3713-3722,3737-3740` 用失败后的读回判定是否已提交；真实适配器可能回显未落盘值，使上层返回 `awake.world_fact.persisted`。退出后数据不存在。

判定：**真缺陷；真实适配器故障注入已复现**，不只是推断。

证据：本轮 Windows PowerShell 5.1 反射实例化 SDK smoke 中编译的 `AwakeFileStorageService+JsonFileKeyValueStore`，给它一个父路径为现有普通文件的目标，依次 Set/Get/新实例 Get。输出：`WRITE_SUCCESS=False READ_SUCCESS=True READ_VALUE=not-on-disk`；新实例 `REOPEN_VALUE=`。主用例套件在同一源码下仍 64/64 通过。

建议：归主干存储线；候选字典写成功才发布，或失败后从磁盘恢复缓存；对不确定提交采用真正持久化读回；增加 Set/Delete 失败及重开对照。具体实现需区分“替换前失败”和“替换后结果未知”。

### P1-02：坏分片会触发整账本逻辑清空，隔离副本还会被覆盖

现象：读取任一分片缺失/损坏都会返回 Corrupt（`WorldStateStore.cs:919-945`）；写侧不区分 root 坏还是分片坏，统一把 `existing` 变成 Missing（`:3671-3678`），再仅从新事实生成 root（`:3690-3707`）。隔离键固定为 `rootKey + ".quarantine"`（`:3771`）。

影响：一次分片损坏可使仍完好的历史事实退出当前查询视图；二次损坏又覆盖第一次隔离的 root。物理旧分片可能仍在，但这不等于游戏可继续查询历史，也不等于永久保留恢复索引。

判定：**真缺陷，静态路径证明**；本轮未模拟多分片双次损坏。

证据：`Program.cs:2264-2305` 只验“坏 root → 隔离键存在 → 新事实 1 条”，未断言旧有效事实仍可查或多次隔离可追溯。用例名“recovery”比它实际保证更宽。

建议：归主干事实存储线；区分损坏层次，保留独立隔离版本和有效分片，返回明确降级状态；测试坏一个分片、二次恢复、恢复失败。

### P1-03：每次追加重写全历史为新 revision，旧分片累计

现象：`WorldStateStore.cs:3690` 取全历史事实，`:3706-3713` 每次重新构建并写入全部分片；`:3845` 分片 key 含本次 revision。文件适配器每次 Set 又序列化整个 namespace（`AwakeFileStorageService.cs:238-248`）。

影响：N 次追加形成累计历史副本，容量和写放大随历史增长；512 KiB 单值限制不能限制承载所有值的 JSON 文件。大型战役会承担越来越重的磁盘和内存成本。这里是复杂度风险，不冒充已测出的帧率下降。

判定：**设计缺口**，达到 P1 是因为常规长期记录持续扩大问题。

证据：`rg -n 'DeleteAsync|Journal.*Delete|Prune|Compact' AWAKE/src/WorldStateStore.cs` 本轮零匹配：在本 Store 当前追加路径未见旧 journal revision 回收；不据此宣称全仓绝无其他清理工具。

建议：归主干存储线；复用未变化分片、仅提交新增部分，明确旧 revision 回收和恢复保留策略；用真实文件适配器量测追加次数与文件字节数。

### P1-04：Overlay 导入失败会留下部分生效的修改

现象：`WorldKnowledgeQueryService.cs:231-237` 逐条调用会原地改对象并递增 revision 的 `TryApplyOverlay`（`:175-200`）；后续操作失败立即返回 false，已成功的前序操作未回滚。

影响：读档/导入被报告失败，但标题、摘要或表达文本可能已经变化；重新导入还可能因第一条旧 revision 而再次失败。调用方 `AwakeTerminalBehavior.cs:74` 只记录失败日志。

判定：**真缺陷，静态路径证明**；不是 entry ID 改名问题。

证据：用“第一条合法、第二条目标不存在”的 operations 可沿上述路径得到部分写入。尚未执行该专门夹具；该文件属于世界书线在途文件，本轮未改动。

建议：归世界书运行时线；在副本上预验并应用全批操作，成功后整体发布；增加失败前后 snapshot/revision 相等断言。

### P1-05：信件正文与投递账本分别提交，失败时留下半封信

现象：`AwakeLetterService.cs:423-451` 先 `AppendLetterAsync` 成功，再 `RecordAsync` 登记投递；回复路径 `:302-335` 也是两次提交。第二次失败直接返回失败。

影响：正文历史可能已经可读，但投递账本没有对应项；重试、过期、未读统计可能与历史视图不一致。不能将其直接称为“重复扣款”，当前证据没有证明那一层。

判定：**跨记录一致性缺口**；未执行崩溃窗口实验。

证据：上述顺序；`AWAKE.Tests.csproj:89-92` 明确不纳入 `AwakeLetterService.cs`，只编入信件账本与输入验证。64 例中的 messenger-history 并不能替代这一生产服务失败场景。

建议：归主干信件线；持久化操作意图并支持重放补齐，或定义单一权威记录及可重建投影；验证第二次写失败/退出/重开。

### P1-06：未绑定 campaign 仍打开共享 unbound 存储

现象：`AwakeFileStorageService.cs:99-103` 将空 campaign ID 映射到 `unbound`；`:67-80` 用此路径创建并静态缓存 store，打开时不拒绝未知 campaign。

影响：若调用时机早于 campaign 绑定，句柄会持有 unbound 路径，后续 campaign 变更不会改变该实例的 path。多场战役早期开启可能共享该位置。

判定：**代码层风险成立；09-14 现场是否确由它造成仍未证**。这次未复核该场日志、session 交替及实际目录内容。

建议：归主干生命周期/存储线；未绑定时返回 typed unavailable，绑定后重开；用两场 campaign 的早开/晚绑定测试确认隔离。

### P2-01：事件元数据加载失败可能被缓存成“已加载”

现象：`AwakeEventEngine.cs:281` 对 `_metaLoaded` 早退；host 为空（`:289`）或读出的 doc 为 null（`:301-303`）时也将其置 true。

影响：若这些失败分支在生产运行中进入，该 session 后续会跳过再次读取，冷却/执行历史可能无法恢复。

判定：**待验证缺陷候选**；需要进一步验证 Tick 外层是否保证 host/store 可用以及 Reset 时机，因此先 P2。

建议：归主干事件线；将“成功空记录”和“失败未加载”区分，做服务晚就绪测试。

### P2-02：证据记录和工程环境没有统一的可复核输出

现象：ROADMAP 同时保留多次补注；作业单把曾经正确的缺陷状态继续写成当前断言。实际本轮 Debug 测试工程有 4 个警告，不能概括为全链零警告。

影响：重复修已修问题、错误选择下一批；宽范围提交又会模糊修改归属。

判定：**记录腐烂/纪律漏洞**。`git config --get core.hooksPath` 无输出，`.git/hooks` 排除 sample 后为空；仅说明本地未见自定义 hook，不能排除 CI 或服务器保护。

建议：归主控/工具线；每次输出 source revision、dirty hashes、命令、时间、退出码、证据层；提交前生成待交文件清单并提示跨线路径。Hook 是辅助，不是安全隔离；不同 agent 独立 worktree 更直接。

## 四、四条玩法链：有代码不等于已经完成游戏验收

表格是静态可达入口定位，不把每个格子的代码存在判作通过。

| 路径 | 入口 | 调用 | 结算 | 玩家可观察结果 | 存档/恢复 | 首要缺口 |
| --- | --- | --- | --- | --- | --- | --- |
| NPC 对话 | NpcDialogueLauncher.cs:39 | NpcDialogueService.cs:222 | :1371 确认 → :1567 CommandBridge | NpcDialogueOverlay.cs:47-54 创建面板；实际渲染本轮未验 | AwakeTranscriptService.cs:92 写历史 | 实机打开/取消与真实持久化可靠性 |
| 写信 | AwakeLetterService.cs:119 | :410 WriteOutboundAsync | :423 正文、:437 账本 | :392 未读统计 | :379 GetLettersAsync | 两次提交中间失败的一致性 |
| 事件 | AwakeContentApi.cs:125 注册；AwakeEventEngine.cs:83 装载 | :153/:244 恢复元数据和判定 | :550 WorldCommandBridge | :462 Popup | :299 GetEventMetaAsync | 失败恢复状态、真实触发与窗口可见性 |
| 周报 | AwakeTerminalBehavior.cs:1255 | :1272 EnsureFormalReportsReadyAsync | WorldEventContracts.cs:369 正式报告流程 | AwakeTerminalBehavior.cs:1280/1295/1298 正式/预览/不可用 | WorldEventContracts.cs:381 读取已存报告；ProbeExtension.cs:374 恢复 | 实际存储适配器与损坏历史保留 |

## 五、64 例到底保证什么

本轮完整重跑全部 64 例，但深读是抽样，不宣称逐个完成 mutation 和路径覆盖分析。

| 组别/样本 | 可以让测试失败的回归 | 该证据不能证明 |
| --- | --- | --- |
| build-identity | 固定 SHA fixture 不一致、身份字段空 | 当前 DLL 已投送游戏 |
| b1-native-state | 单 session single-flight/代际行为错误 | 真 `Hero.MainHero` 访问不发生原生空引用；Program.cs:417 注入了探针 |
| world-fact-journal | 空串又判 Corrupt、错误 schema 被接受 | 实际写盘可靠性 |
| world-fact-journal-roundtrip | 首条事实无法通过 Store 写读 | 文件系统失败/进程重启；Program.cs:2207 用 FakeKeyValueStore |
| world-fact-journal-recovery | 隔离键未写、恢复后无新事实 | 多分片历史保留、第二次隔离、多进程恢复 |
| messenger-history | 输入验证和内存历史基本语义错误 | AwakeLetterService 两次提交的一致性 |
| prompt/knowledge-gate | 固定模板、预算和知识门规则回归 | 模型实际答复质量、真实人格扮演 |
| dialogue-chain-redtest | 用例实际调用/检查的文本、绑定或包契约改变 | Gauntlet 成功打开并完成整轮玩家交互 |

四个真机现象对应结论：root_corrupt 有明确子问题覆盖；native readiness 有状态机覆盖但替换了实际探针；面板打不开未取得等价 GUI 执行证据；unbound 路径选择未取得现有用例能检出的证据。不能把这四项统一算作“0%”，也不能报虚假的精确覆盖百分比。

毒性检查：抽到的事实日志三个测试都有具体值断言，不是只打印 PASS。**三个生产代码变异实验本轮未做**；真实文件适配器故障注入不是 mutation testing。完整 Q3 仍待补，评分已考虑这一限制。

## 六、框架接线与单程序集

以下是可重跑的**成员访问文本命中行数**，不是精确调用次数：对 `AWAKE/src` 的 `.cs` 用 `rg -n '\.<成员>\s*[?.]'`。不计 framework 内部调用；可能包含非 host 同名成员，漏掉局部接口变量、反射和分行访问。因此不能凭零命中直接删除服务。

| 成员 | 命中行数 | 成员 | 命中行数 |
| --- | ---: | --- | ---: |
| Capabilities | 0 | Tools | 0 |
| GameData | 3 | Context | 3 |
| Rag | 3 | Events | 1 |
| Commands | 2 | Ai | 1 |
| Models | 3 | Media | 0 |
| Prompts | 3 | Storage | 1 |
| Assets | 0 | Permissions | 2 |
| Diagnostics | 4 | Log | 0 |

这 16 个兼容 host 服务成员中，有 6 个本轮文本口径零命中。可用 Roslyn SemanticModel 生成符号调用表，再附 XML/反射入口清单；本轮没有生成全 framework 所有接口的完整语义图，Q4 仅完成入口筛查。

源码首轮文件名分类：对话/提示词 39、知识 18、Persona 10、UI/美术相关 25、其余运行时/世界状态 72，共 164。此分类用于定位，不冒充跨 Agent 所有权。直接跨域引用确定存在，例如 `NpcDialogueService.cs:1037-1043,1146-1151` 使用 Worldbook/Persona，`:407` 使用 Memory；这是必要业务组合的一部分。

最小合理切口：先明确 Knowledge/Persona/Memory 的只读输入与结果接口，把无 TaleWorlds 依赖的验证、模型和 codec 作为候选独立编译单元；不要为 Git 分笔提交而迁移 164 个文件。测试项目显式 Compile 清单与主项目通配之间的漂移，比“只有一个 DLL”更直接影响维护。尚未做完整跨线符号计数和拆分成本测量。

## 七、内容契约、环境与发布检查

- Overlay schema 与实现确有偏差：`tools/worldbook-contract/v1/overlay.schema.json:11,24` 最小 revision/baseRevision 为 1，首条操作可能从 0 开始。对当前 `WorldKnowledgeQueryService.cs` 用 `rg -n 'Schema|Validate'` 本轮未见 schema 校验入口；导入直接读 operations。不能扩大成“全项目没有校验器”。
- `WorldKnowledgeQueryService.cs:214` activation 仍是常量；`AwakeTerminalBehavior.cs:66-78` 同步 overlay 与 activation，但本轮沿该 overlay 导入路径未见 package hash 对照。加载包本身则确有三哈希校验（`WorldbookPackageIntegrity.cs:66-75`），两者不能混为一谈。
- `RuntimePackageCompiler.cs:111,504` 由 source ID 派生 entry ID；改 source ID 会改变 entry ID。对该编译器 `rg -n 'previous|baseline|migration|removed'` 本轮未见旧包比较；编译外围是否已有比较工具未完整追踪，不能宣称全链绝无 ID 兼容门禁。
- `rg --files AWAKE/tools -g '*.ps1' -g '*.py' -g '*.js'` 本轮列出 639 个脚本文件；`rg -l '\bpwsh\b' AWAKE/tools -g '*.ps1' -g '*.py'` 命中 25 个文件，包含文档字符串，不能当 25 个失败入口。关键审批链 `verify-e2-matrix.ps1:59,84` 确有实际调用。
- 发布“不崩”：本轮只构建/离线测试；新档、读档、退出仍须匹配 BuildId 的证据。
- 发布“存档兼容”：P1-01/02/04/06 是实际阻碍，不能通过全绿抹去。
- 发布“共存”：本轮未做多模组组合测试，不评分为通过。
- 发布“上手”：配置和说明存在；未做干净环境从安装到首轮对话验收。
- 发布“分级”：在 `AWAKE/src/Prompts` 和 `framework/MarcusAwakeFramework/src` 执行 `rg -n 'adult|nsfw|成人' -g '*.cs'` 本轮零命中；这只是一小段代码的词面检查，不证明完整发布包无分级内容，也不扫描/复述内容正文。
- 发布“文档/截图”：本轮未核工坊页面和正式发布截图；THIRD-PARTY-NOTICES 存在，自有 LICENSE 与上游署名覆盖仍需专门发布盘点，不作法律判定。

## 八、独立发现与未完成边界

独立探索得到：故障后缓存假成功、恢复后历史不再可查询、全历史 revision 写放大、Overlay 部分导入、信件半提交、事件元数据失败被缓存。它们不靠简单复述“未接线”得出；其中若干属于更早历史审查讨论过的风险，不声称首次发现。

没查到/不确定：

- 09-14 原始日志和原 DLL 未在本轮重新归因；native 空引用不能仅凭 Failed 后又 Ready 就定案为竞态。
- 没有测 Gauntlet 真渲染、存读档、多模组共存、长时帧率和发布包实际内容。
- 未做三个代码变异实验、64 个测试逐条深读、全接口语义调用图、完整脚本依赖解析、全部 schema 到执行器的覆盖普查。
- 世界书和角色卡仍有在途修改；本报告不锁定内容产物，不改变他线状态。
- 评分是有依据的工程首评，不能替代上线准入裁决；后续修复是否生效须有对应失败条件的回归证据。

建议优先级：先修存储假成功与历史恢复边界，再补信件/Overlay 半提交及真实适配器测试；日常功能仍由本体负责人推进。难点分析可继续交给本对话，不对第三阶段排期。

## 附：P1-01 复现方法

本轮 Debug Awake.dll SHA-256：`7C4AF44F59BAA63D747C8F98452F0CB36AD346D2633FA7BBD598832A1EFA947B`。故障注入执行的是同轮测试程序集内编译的生产存储类；测试不是游戏 DLL 宿主集成证明。

Windows PowerShell 5.1 中可用以下步骤重跑核心实验。让路径的父级是现有普通文件，目录创建就必然失败；不会覆盖该文件。先按正文命令构建测试程序集。

```powershell
$testExe = 'D:\AWAKE-Dev\AWAKE.Tests\bin\Debug\net472\Awake.SdkSmoke.exe'
$assembly = [Reflection.Assembly]::LoadFrom($testExe)
$flags = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$assembly.GetType('Awake.AwakeLog').GetProperty('Enabled', $flags).SetValue($null, $false, $null)
$type = $assembly.GetType('Awake.AwakeFileStorageService+JsonFileKeyValueStore', $true)
$ctor = $type.GetConstructor($flags, $null, [type[]]@([string],[string]), $null)
$store = $ctor.Invoke(@('audit', ($testExe + '\impossible.json')))
$task = $type.GetMethod('SetAsync').Invoke($store, @('key','not-on-disk',$null,[Threading.CancellationToken]::None))
$task.GetAwaiter().GetResult().IsSuccess # False
$task = $type.GetMethod('GetAsync').Invoke($store, @('key',$null,[Threading.CancellationToken]::None))
$task.GetAwaiter().GetResult().Value # not-on-disk
$reopened = $ctor.Invoke(@('audit', ($testExe + '\impossible.json')))
$task = $type.GetMethod('GetAsync').Invoke($reopened, @('key',$null,[Threading.CancellationToken]::None))
$task.GetAwaiter().GetResult().Value # empty
```

本轮实际使用临时脚本文件作为不可建目录的父路径，输出已记录于 P1-01；脚本已清理。上面以现有 exe 文件替换该父路径，便于无需创建额外输入文件复现。
