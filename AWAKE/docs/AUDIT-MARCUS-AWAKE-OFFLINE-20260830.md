# Marcus-AWAKE 内置迁移离线机制审查报告

- 审查日期：2026-08-30（Asia/Shanghai）
- 审查类型：离线静态查漏补缺
- 审查范围：AWAKE 游戏侧、内置 Marcus Framework、Runtime Service、IPC、Provider、Storage、事件、NPC 对话、记忆、生命周期
- 审查结论：发现多项需要在下一候选修复或明确决策的问题；本轮没有修改实现

## 1. 证据边界

本轮只读检查了源码、现有离线测试结果、构建/同步记录和当前候选的哈希。没有启动 Bannerlord，没有访问真实云 Provider，没有读取真实 API Key，也没有把任何源码风险直接归因成当前候选已经出现的玩家可见故障。

当前候选：

- BuildId：`awake-20260829-marcus-embedded-002`
- 工程版本：`v0.2.0`
- 最高证据：`E3`
- 候选状态：已同步游戏目录，等待用户执行 E4/E5

四地静态哈希复核结果：

| 文件 | `_build_out` | `dist` | release staging | 游戏目录 |
|---|---|---|---|---|
| `Awake.dll` | `D00478D7B796974B4FFF6EDF385B6E31A7B3F712BECE89E5C013F7438957FEBE` | 一致 | 一致 | 一致 |
| `MarcusAwakeFramework.dll` | `00B34BF79DEFADBE4A946612244EA8631CE777495C2B99ADD11F2B10CE8C7F55` | 一致 | 一致 | 一致 |
| `MarcusAwakeTransport.dll` | `DE81809AC08EA86BE90FD6F0AB77FA348B24B744588B0AF970DFFF2080E6DF48` | 一致 | 一致 | 一致 |
| Runtime `manifest.json` | 不适用 | `58A601B48B32F03B6650AEA4B8694A22F262465ABB94A9D688B8DE11886122AC` | 一致 | 一致 |

说明：以下“confirmed”指源码控制流或数据结构已经可以静态确认；它不等于当前游戏目录已经有匹配 BuildId 的 E4/E5 故障日志。

严重度含义：

- `P1`：会造成状态丢失、串档、错误结算、线程违规或运行时不稳定，建议在继续扩大实机测试前处理。
- `P2`：在压力、快速操作、异常关闭或特定降级路径下可能出现，不能忽略，但不一定阻断基础对话。
- `P3`：主要是可观测性、维护性或契约清晰度问题。

状态含义：

- `confirmed`：静态证据直接证明代码存在该行为。
- `suspected`：触发链存在，但还需要生产调用链或实机条件确认。
- `design_decision`：当前实现可能是有意选择，但必须明确后果并固定契约。
- `unknown_non_actionable`：现有离线证据不足以形成可执行缺陷结论。

## 2. 需要优先处理的确认问题

### F-01：持久化写入 API 把“已入队”返回成“已保存”

- 严重度：`P1`
- 状态：`confirmed`
- 证据位置：
  - `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\WorldStateStore.cs:413-452`，`FlushMemoryFactsAsync`
  - 同文件 `:455-480`，`PatchMemorySummaryAsync`
  - 同文件 `:483-516`，`AppendEventMemoryAsync`
  - 同文件的事件元数据、NPC 主动、信使、引导、待处理对话、交互和承诺写入方法
- 触发条件：`TryEnqueue` 成功，但后续 `DrainAsync` 得到硬失败、延期重试或提交未知。
- 静态证据：多处代码等待 `DrainAsync(...)` 后直接 `return true`，没有检查 `WorldDrainSummary.OwnerApplied`、`OwnerDuplicate`、`OwnerRetryable`、`OwnerCommitUnknown` 或 `HardFailureCount`。相比之下，Transcript 和部分周报/世界事件写入已经检查了摘要，说明这不是统一设计，而是实现不一致。
- 实际影响：上层会认为记忆、关系辅助状态、事件元数据或队列已经保存，随后可能清除 UI 状态、消耗重试机会或继续推进流程；真实存储失败时数据可能静默丢失。
- 最小修复建议：建立唯一的 `PersistCommandAsync`/`AwaitPersistedAsync` 结果转换，所有写入口统一返回“成功、重复、可重试、提交未知、硬失败”；禁止普通布尔值掩盖提交未知。
- 新 BuildId：需要。
- E4/E5：需要匹配新 BuildId 的 E5 存档、失败重试和读档验证；E4 只能证明入口可用，不能证明持久化正确。

### F-02：NPC 记忆 reservation 在真正入队前被删除

- 严重度：`P1`
- 状态：`confirmed`
- 证据位置：`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\WorldStateStore.cs:413-450`，`FlushMemoryFactsAsync`
- 触发条件：方法取出 `_memoryReservations[conversationId]` 后，在 `TryEnqueue(command)` 前发生线程切换、会话结束或入队失败。
- 静态证据：第 429-433 行先读取序号并移除 reservation，第 450 行才尝试入队；最终 drain 只会补写仍在 `_memoryReservations` 中的 reservation。
- 实际影响：会话结束补写无法再找回这条 reservation；若入队失败或在两步之间触发最终 drain，记忆可能完全消失。即使内容没有丢，reservation 与真实写入状态也会失去对应关系。
- 最小修复建议：把 reservation 改成显式状态机，至少保留 `Reserved → Enqueued → Applied/Retryable/Abandoned`；只有 owner 结果被确认后才移除，最终 drain 处理 `Reserved` 与 `Enqueued` 两类未完成项。
- 新 BuildId：需要。
- E4/E5：需要 E5 的对话结束、快速关窗、切档和最终 drain 验证。

### F-03：战役恢复读取早于 Runtime/Storage 就绪，且没有可靠补载

- 严重度：`P1`
- 状态：`confirmed`（玩家可见后果仍待 E4/E5）
- 证据位置：
  - `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\ProbeExtension.cs:269-273`
  - `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeHostComposition.cs:169-172`
  - `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\EventDialogueQueue.cs:81-124`
  - `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeOnboardingService.cs:83-108`
- 触发条件：`CampaignSessionReady` 期间启动 onboarding/对话队列读取时，`AwakeRuntime.WorldStateStore` 仍为空；或读取任务与 Runtime 启动发生竞态。
- 静态证据：`ProbeExtension` 以 fire-and-forget 方式读取；`AwakeHostComposition` 在生命周期回调之后才调用 `StartRuntimeService`。`EventDialogueQueue.LoadFromStoreAsync` 遇到空 Store 直接返回，且只在成功路径设置 `_loaded`；`AwakeOnboardingService` 遇到空 Store 也直接返回。源码扫描没有找到可靠的“Runtime ready 后补载这两项”的生产调用。
- 实际影响：旧存档中的待处理对话、引导进度或提醒可能在本次战役启动时没有恢复；对话队列还可能在后续被当作未加载而产生重复竞态。
- 最小修复建议：把恢复拆成 `RuntimeReady` 阶段的幂等加载任务；启动顺序改为“Runtime/Storage ready → 读取存档状态 → 发布 CampaignSessionReady 可用信号”，并给每个恢复器记录 session generation，禁止重复加载。
- 新 BuildId：需要。
- E4/E5：E4 检查 Runtime ready 后首次读取；E5 检查同一存档重启、切档和重复进入。

### F-04：旧战役最终 drain 失败可能污染新战役

- 严重度：`P1`
- 状态：`confirmed`
- 证据位置：
  - `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeRuntime.cs:851-889`
  - 同文件 `:900-1065`
- 触发条件：旧 Store 的最终 drain 失败，随后开始新战役并调用 `EnsureWorldStateReadyAsync`。
- 静态证据：`RetiredWorldStateDrains` 保存失败的旧 drain；`ObserveWorldStateDrainAsync` 对旧 boundary 的失败项保留 `Failed` 项。新战役的 `ResetSessionStateForCampaignCore` 清除 `_worldStateDrainFailed`，但不清除旧的 `RetiredWorldStateDrains`；之后 `AwaitRetiredWorldStateDrainsAsync` 的返回值仍检查 `RetiredWorldStateDrains.Any(value => value.Failed)`。因此旧 boundary 的失败可能让新战役拒绝重新打开 Store。
- 实际影响：某次切档或退出时的存储失败可能让后续新存档无法初始化世界状态，即使新战役自身没有错误。
- 最小修复建议：将 drain 结果完全绑定到 boundary generation；新战役只等待/判断属于当前边界的 drain。旧失败项应转为独立诊断记录并从新战役的启动门中隔离，不能用全局列表作为当前可用性条件。
- 新 BuildId：需要。
- E4/E5：必须做故障注入后的切档、重启和第二战役启动测试。

### F-05：UI 入口同步等待异步存储

- 严重度：`P1`
- 状态：`confirmed`
- 证据位置：
  - `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\WorldEventInboxOverlay.cs:28`
  - `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeTerminalBehavior.cs:1232-1240`
- 触发条件：玩家打开世界事件收件箱或周报，而存储读取尚未完成、Runtime 响应慢或发生异常。
- 静态证据：UI 入口直接调用 `LoadAsync(...).GetAwaiter().GetResult()`，随后才创建 overlay/周报文本。
- 实际影响：游戏/UI 线程会被同步阻塞；慢盘、Runtime 启动或数据库锁等待时可能出现明显卡顿。若底层未来需要回到游戏线程，存在形成死锁的风险。
- 最小修复建议：入口只创建“加载中”界面；后台完成读取后经统一 UI dispatcher 更新；失败显示可理解的降级状态，不在 UI/campaign tick 同步等待文件、数据库或 IPC。
- 新 BuildId：需要。
- E4/E5：E4 打开入口并用慢读取/Runtime 未就绪场景验证；不要求马上证明死锁，先证明主线程不阻塞。

### F-06：后台 continuation 继续访问 Bannerlord 实时对象

- 严重度：`P1`
- 状态：`confirmed`（具体崩溃或竞态待 E4）
- 证据位置：
  - `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeEventEngine.cs:114-170, 174-199, 402-435`
  - `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\NpcProactiveService.cs:79-110, 280-350`
  - `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\NpcDialogueService.cs:171-258, 400-430, 925-1040`
- 触发条件：异步操作在 `ConfigureAwait(false)` 后完成，继续执行条件判断、弹窗、Hero/Settlement/Skill 读取或命令准备。
- 静态证据：事件、NPC 主动和 NPC 对话流程在 await 后继续使用 `Campaign.Current`、`Settlement.CurrentSettlement`、`MobileParty.MainParty`、`Hero`、实时角色映射和 UI 相关对象。`NpcProactiveService` 还把从 hook 得到的 `Hero` 对象跨越异步 I/O 保存到后续逻辑中。
- 实际影响：在 Bannerlord 非主线程访问实时对象会产生竞态、偶发异常、失效引用或不稳定行为；即使大多数机器上暂时不崩，也违反当前项目的线程边界。
- 最小修复建议：把流程明确拆成“游戏线程快照 → 后台纯数据/AI/存储 → 游戏线程结算与 UI”；后台只携带稳定 ID 和 DTO，不携带 Hero、Settlement、Campaign 等实时对象。所有弹窗和效果命令入口统一经过已观察到的游戏线程。
- 新 BuildId：需要。
- E4/E5：E4 需检查事件、主动 NPC、NPC 对话三条入口；E5 需长时运行和切换场景。

### F-07：信使历史回调可能把旧联系人历史显示到新联系人

- 严重度：`P1`
- 状态：`confirmed`
- 证据位置：`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeMessengerVM.cs:216-249, 297-320`
- 触发条件：先打开联系人 A 的历史，再快速切换到联系人 B，A 的异步读取在 B 已成为当前联系人后完成。
- 静态证据：`LoadHistoryAsync` 接收了 `contactKey`，但回调只调用 `PopulateHistory(lines)`；`PopulateHistory` 在回调执行时读取 `_activeContactKey`，没有比较“这批 lines 属于哪个 contactKey”。因此 A 的 lines 可能在 B 当前活动时通过检查并显示。
- 实际影响：历史对话串档，可能让玩家看到错误人物的对话内容；这也是一个数据边界问题，不只是视觉刷新问题。
- 最小修复建议：将 `contactKey` 或递增加载 generation 一起传入 UI 回调，只接受仍匹配当前联系人和当前 VM generation 的结果；切换联系人时使旧加载任务失效。
- 新 BuildId：需要。
- E4/E5：E4 做快速联系人切换和慢读取；E5 做退出/重进后的历史恢复。

### F-08：对话、信使和 onboarding 的取消没有完全绑定会话生命周期

- 严重度：`P1`
- 状态：`confirmed`（越过边界的可见后果待 E4/E5）
- 证据位置：
  - `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\NpcDialogueService.cs:400-430, 1300-1362`
  - `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeMessengerVM.cs:358-382, 463-489, 622-632`
  - `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\ProbeExtension.cs:271-273`
- 触发条件：切换联系人、关闭窗口、结束战役或 Runtime drain 时，仍有使用 `CancellationToken.None` 的后台加载、AI、记忆、信使、信件或保存任务。
- 静态证据：`NpcDialogueService.CancelActiveAsync` 增加 generation 并调用 route cancel，但初始化和部分记忆/命令/Transcript 写入仍显式使用 `CancellationToken.None`；信使历史、信件和发送也使用 `CancellationToken.None`。这些任务没有统一链接到 session generation 的取消源。
- 实际影响：旧会话的迟到结果可能写入新会话状态、继续访问已失效对象，或者关闭窗口后仍持续消耗 Provider/Storage 资源。
- 最小修复建议：为每个战役和每个 UI 会话建立 linked CTS；所有后台 I/O、AI、保存和回调都必须携带它。取消只阻止新结果提交，不能依赖单独的 route cancel；回调还要检查 session generation、contact generation 和 disposed 状态。
- 新 BuildId：需要。
- E4/E5：重点验证切档、关窗、快速切换联系人、Runtime 重启和长响应取消。

### F-09：世界事件缺少来源/认识论状态的保留，入事件总线时统一提升为事实

- 严重度：`P1`
- 状态：`confirmed`
- 证据位置：
  - `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\WorldStateStore.cs:1995-2048`
  - 同文件 `:98-123`
- 触发条件：任何带有事件 payload 的世界状态写入成功，并进入 `_pendingEvents`。
- 静态证据：`WorldPendingEvent` 只保存事件 ID、命令、payload、关联 ID、事件 kind/schema，没有保存来源、访问范围或 epistemic status；发布时 `EventEnvelope` 固定使用 `DataAccessScope.PlayerKnown`、`SourceClass.ExtensionProvider` 和 `EpistemicStatus.Fact`。
- 实际影响：NPC 传闻、玩家转述、AI 候选事件或尚未核实的世界变化可能被事件系统当成玩家已知的客观事实，污染周报、知识投影和后续 NPC 提示词。
- 最小修复建议：把 `source`、`access scope`、`epistemic status` 作为 pending event 的必需字段；发布前做白名单校验。区分 `Fact`、`Rumor`、`Claim`、`Opinion`，默认不允许未经代码结算的 AI 文本进入 `Fact`。
- 新 BuildId：需要。
- E4/E5：E4 检查事件→周报→NPC 知识链路；E5 检查事件重启和知识投影恢复。

### F-10：AI/命令写入世界事件只做长度检查，没有客观事实边界

- 严重度：`P1`
- 状态：`confirmed`
- 证据位置：
  - `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeWorldCommandAdapters.cs:189-272`
  - `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\NpcDialogueConstants.cs:24-30`
  - `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\NpcPromptTemplate.cs:55-84`
- 触发条件：对话 AI 返回 `awake.world.effect.record`，或其他受允许的调用方提交世界事件记录。
- 静态证据：`AwakeWorldEffectRecordAdapter.Validate` 主要检查 `text` 非空/不超过 2000、`kind` 长度和 `day` 是否为整数；没有事件类型白名单、日期范围、真实游戏来源、事实/传闻/观点区分、代码状态核验或“AI 只能提交候选”的约束。
- 实际影响：模型可以把虚构或错误叙述写成世界事件，随后经过 F-09 的固定 envelope 进入事实/玩家已知链路，形成世界知识污染。
- 最小修复建议：将 AI 事件改为 `CandidateEvent`；由代码校验实体、日期、事件类型、来源和当前游戏状态，再由明确的结算动作转为 `Fact`。未知类型、越界日期和缺少来源一律拒绝或进入隔离审查队列。
- 新 BuildId：需要。
- E4/E5：E4 需要验证错误事件不会进入事实知识；E5 需要验证隔离候选不会在重启后自动升级。

### F-11：事件触发先消耗冷却/次数，后尝试显示弹窗

- 严重度：`P1`
- 状态：`confirmed`
- 证据位置：
  - `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeEventEngine.cs:125-170`
  - 同文件 `:304-375`
  - `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeEventBehavior.cs:26-38`
- 触发条件：规则符合条件，但当前有对话、信使、Inquiry 或其他 UI 状态，导致弹窗不能实际打开；或者事件元数据保存失败。
- 静态证据：`RecordTriggerAsync` 在 `ShowRule` 前更新 `_cooldowns`、`_dailyCounts` 和 `_metaVersions`；`ShowRule` 只是把显示动作排入 dispatcher，实际执行时才检查 `CanShowPopup`。`RecordTriggerAsync` 对存储异常只记录日志，不回滚内存计数。小时 tick 和链式事件还依赖普通 `_busy` 与 `CancellationToken.None`。
- 实际影响：玩家没有看到事件，事件却已经消耗次数和冷却；存储失败后内存与落盘状态可能不一致；链式事件和长时间异步任务还可能出现重复或卡 busy。
- 最小修复建议：采用 `selected → pending_display → displayed → choice_settled → trigger_committed` 状态机；只有确认弹窗打开或明确进入可恢复队列后才提交消耗。元数据写失败保留待提交状态，不静默吞掉。
- 新 BuildId：需要。
- E4/E5：E4 检查 UI 被占用、关闭窗口和链式事件；E5 检查冷却/次数跨日和跨存档恢复。

### F-12：Runtime 普通请求账本没有统一容量/TTL，拒绝请求也可能留下幽灵条目

- 严重度：`P1`
- 状态：`confirmed`
- 证据位置：
  - `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeRuntimeService\src\RuntimeServiceHost.cs:59-69`
  - 同文件 `:1217-1271`，`HandleEchoAsync`
  - 同文件 `:1484-1550`，`HandleStorageBusinessAsync`
  - 同文件 `:2680-2737`，账本创建与 Provider 幂等预留
- 触发条件：持续提交唯一 `message_id/task_id` 的请求，或在 `MaximumConcurrentFrames` 已满时提交请求。
- 静态证据：`messageLedger`、`taskLedger`、`providerIdempotency` 和 `suppressedMarkers` 是普通字典；当前明确的 TTL/容量清理主要针对 committed stream replay 和 pending cancellation。Echo/Storage 在注册 active task 前先 `GetOrCreateTaskLedger`，达到并发上限后直接返回而没有回滚；Provider 也在部分 admission 失败路径上已经建立账本后返回 busy。
- 实际影响：异常或高频请求可不断占用 Runtime 内存；并发满时的失败请求会留下没有终态的条目，后续重试可能看到 `task_in_progress`，也会放大账本泄漏。
- 最小修复建议：把“容量预留”和“账本提交”做成一个 admission 事务；失败必须回滚。为普通 terminal/error/suppressed ledger 设定按状态区分的上限、TTL 和指标；in-progress 条目必须有超时回收。
- 新 BuildId：需要。
- E4/E5：先做离线压力/故障注入，再做 E4 长时运行；E5 检查 Runtime 重启后的残留策略。

## 3. 需要明确的 Runtime/IPC 契约问题

### F-13：Provider 幂等账本只在内存中，Runtime 重启后丢失

- 严重度：`P1`
- 状态：`design_decision`
- 证据位置：
  - `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeRuntimeService\src\RuntimeServiceHost.cs:62-64`
  - 同文件 `:1203-1214`，health/diagnostic 明确报告 `ledger=non_durable`
  - 同文件 `:2706-2737`，`ReserveProviderIdempotency`
- 触发条件：Provider 请求在 Runtime 进程重启、父进程异常退出或断电前后被重试。
- 静态证据：Provider 幂等 key、请求 hash、状态和 replay 关系只保存在 `providerIdempotency` 与普通 task ledger 中，没有持久化恢复路径。
- 实际影响：重启后相同幂等 key 会被当作新请求；对于模型请求通常只是重复消耗，但对 Provider 配置、外部副作用或“提交已发生但响应丢失”的请求，可能重复执行或无法区分未知状态。
- 最小修复建议：二选一并写入协议：一是持久化不含密钥的 request hash、状态、终态摘要和 TTL，重启后可 replay/reconcile；二是明确 `unknown_after_runtime_restart`，要求调用方先查询/人工确认，禁止自动重放有副作用的操作。Provider secret 不能因为做幂等而直接写入普通账本。
- 新 BuildId：如果改变协议或实现，需要；仅补文档不需要。
- E4/E5：必须有 Runtime 重启、响应丢失和重复提交测试；当前没有 E5 证据。

### F-14：Runtime drain 对连接 dispatch 只等待 1500ms，超时后仍可能有任务运行

- 严重度：`P1`
- 状态：`confirmed`
- 证据位置：
  - `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeRuntimeService\src\RuntimeServiceHost.cs:276-307`
  - 同文件 `:353-427`
  - 同文件 `:2565-2571`
  - 同文件 `:2573-2595`
- 触发条件：连接关闭、父进程退出或 Runtime shutdown 时，某个 dispatch 不响应取消或 Provider 调用超过 1500ms。
- 静态证据：`DrainDispatchesAsync` 使用 `Task.WhenAny(all, timeout)`，超时后直接返回；连接随后释放 session，Runtime 继续清理 Provider/Storage。虽然 `BeginDrain` 会发出取消，但没有保证所有任务已经结束，也没有把超时任务纳入后续关闭屏障。
- 实际影响：后台任务可能在依赖对象已处置后继续运行；响应可能丢失，Provider 任务状态可能停在中间态，Storage 可能收到迟到写入。
- 最小修复建议：区分“停止接收新请求”和“依赖安全关闭”；超时后记录 active task，并在服务退出前完成硬停止/安全弃置。对不可取消的 Provider 调用要有独立的 abandoned 状态和重启恢复策略，不能仅靠等待 1500ms。
- 新 BuildId：需要。
- E4/E5：E4 做关闭/断开/Provider 慢响应；E5 做重启与重复提交。

### F-15：请求序号在业务校验前被消费，失败重试的回复不稳定

- 严重度：`P2`
- 状态：`suspected`
- 证据位置：
  - `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeRuntimeService\src\RuntimeServiceHost.cs:593-600`
  - 同文件 `:696-710`
  - 同文件 `:1174-1186`
- 触发条件：帧通过传输层 sequence window 后，在业务 header/schema 校验阶段失败，调用方随后尝试使用同一 message/sequence 重放。
- 静态证据：`ReceiveWindow.Evaluate` 接受后立即 `RecordAcceptedRequest`，随后才做 business header 检查；错误路径没有总是保存稳定的 terminal replay template。后续重复可能从原业务错误变成 `duplicate_unknown`。
- 实际影响：调用方难以根据错误分类决定“同一请求重试”还是“新请求重试”，可能导致错误诊断不稳定。
- 最小修复建议：固定协议语义：要么所有已消费 sequence 的业务错误都保存可重放错误模板，要么明确要求错误后必须使用新的 message/sequence，并在 SDK 客户端统一实现。不要让调用方靠错误文本猜测。
- 新 BuildId：需要修改实现时需要；仅补协议文档不需要。
- E4/E5：需要 IPC 故障注入和客户端重试测试；当前离线传输单测通过，不能据此排除这一跨层契约问题。

### F-16：Framework 兼容命令路径没有统一保证游戏线程

- 严重度：`P2`
- 状态：`suspected`
- 证据位置：`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeFramework\src\HostApi.cs:809-855`
- 触发条件：外部调用方从非游戏线程直接调用兼容层的 `PreflightAsync`/`SubmitAsync`。
- 静态证据：兼容层直接执行 `registration.Adapter.Preflight` 与 `registration.Adapter.Execute`，自身没有统一 marshal；当前主要生产路径是否始终由游戏线程调用，离线代码扫描不能完全证明。
- 实际影响：如果未来 Provider/Runtime 或其他扩展从后台线程使用这条兼容路径，命令适配器可能在错误线程读取/修改游戏状态。
- 最小修复建议：在 API 契约中明确线程前置条件并增加断言/诊断；若兼容层必须支持后台调用，统一转到宿主的游戏线程调度器，不能让每个扩展自行猜测。
- 新 BuildId：改变实现时需要。
- E4/E5：需要真实生产 caller 路径或专门的跨线程测试后才能升级为 confirmed。

### F-17：UI dispatcher 无每帧处理预算，且尚未观察到游戏线程时会直接执行

- 严重度：`P2`
- 状态：`confirmed`（具体卡顿程度待实测）
- 证据位置：`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeUiDispatcher.cs:25-33, 85-112`
- 触发条件：后台回调在短时间内大量入队，或 Permission 请求在游戏线程尚未被观察时触发。
- 静态证据：`Drain` 每帧循环直到队列为空，没有动作数/耗时上限；`RunOnGameThreadAsync` 在 `_gameThreadId == 0` 时直接执行 factory，注释明确这是为了测试/早期初始化，但生产调用点 `PermissionGate` 也能走到这一分支。
- 实际影响：回调堆积时单帧可能执行大量工作；早期初始化时可能在未知线程访问宿主/UI相关功能。
- 最小修复建议：生产路径在未观察到游戏线程时应排队或返回“等待主线程”；测试路径单独提供 direct helper。dispatcher 每帧设置动作数和时间预算，并对可丢弃/过期 UI 更新做合并。
- 新 BuildId：需要改变运行时行为时需要。
- E4/E5：E4 检查早期打开 MCM/权限请求；离线压力测试检查 dispatcher backlog。

## 4. 已检查但不应误报的项目

1. `MarcusAwakeTransport` 的 `SequenceWindow`、`PipeFrameIO`、`ProtocolCodec` 已有独立测试；本轮没有新反证，因此不把底层实现直接列为确认漏洞。
2. `StorageAndRagApi.cs` 中的 unavailable 仅是 Framework 降级 API；当前 Runtime Service 已有 SQLite/RAG 后端，不能据此断言生产 Runtime 没有后端。
3. P3A forbidden scan 中的 Runtime/IPC 路径属于本次“内置迁移”的预期实现，不沿用旧的“禁止进程/IPC”宽泛规则把它误报成架构违规。
4. 当前候选没有匹配 BuildId 的 E4/E5 日志；本报告不宣称已经发生游戏崩溃、真实云 Provider 失败或存档损坏。
5. Worldbook Studio、角色编辑器和 v1 世界知识内容不在本轮修复范围；本轮只审查它们被运行时调用时所经过的底层边界。

## 5. 推荐修复顺序

### 第一批：先堵状态和线程边界

1. 统一 `WorldStateStore` 写入结果，消除 F-01 的假成功。
2. 修复 F-02 reservation 状态机和 F-03 Runtime ready 后恢复顺序。
3. 修复 F-04 旧 drain 与新战役隔离。
4. 去除 F-05 的 UI 同步等待。
5. 把 F-06、F-08 合并为“会话快照/取消/游戏线程”批次。

### 第二批：再堵知识和事件污染

1. 修复 F-09 pending event 的 provenance/epistemic/access 字段。
2. 修复 F-10 AI 事件候选与代码事实结算边界。
3. 修复 F-11 事件触发的显示/提交顺序。
4. 对 F-07 增加联系人 generation 检查。

### 第三批：Runtime/IPC 账本治理

1. 修复 F-12 admission 事务、幽灵账本和统一 TTL/容量。
2. 对 F-13 做“持久幂等”或“重启后未知”二选一的正式设计决策。
3. 修复 F-14 drain 关闭屏障。
4. 用真实 caller 复核 F-15/F-16，再决定是否进入实现批次。
5. 给 F-17 增加 dispatcher 时间预算和早期线程安全门。

## 6. 本轮交付与状态更新

- 新增报告：`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\AUDIT-MARCUS-AWAKE-OFFLINE-20260830.md`
- 修改源码：无
- 修改冻结候选：无
- 修改游戏目录：无
- 启动 Bannerlord：无
- 访问真实云 Provider/API Key：无
- 当前候选仍为 `awake-20260829-marcus-embedded-002`，最高证据仍为 `E3`
- 任何实现修复都必须创建新的 BuildId，并重新走 E1/E2/E3，再由用户执行匹配的 E4/E5

