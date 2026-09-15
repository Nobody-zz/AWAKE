# 世界事实底座：统一事实、选择性读取

- 批次：`world-fact-foundation-20260912`
- 状态：`approved_pending_user_signoff`
- 审查预算：高风险批次，最多 3 轮；已完成基线审查与一次针对修订差异的复审。由于 Round 2 只发现同一 P1 的字节级编码遗漏，允许一次最终定向复审；后续非阻断观察进入 backlog，不重开本批次。
- 代码许可：无。必须先得到独立审查 `VERDICT: APPROVED`，再由用户明确签收。
- 影响范围：AWAKE 运行时、其战役存储投影、离线 smoke；不写世界书正文、角色卡或 Persona Storage。

## 1. 决定与背景

### 已确认的现状

1. `WorldEventLedger` 当前是一个最多 50 条的内存队列；`WorldStateStore` 的 `awake.world_events.v1.records` 最多保存 200 条。两者都不是全战役事实档案。
2. 现有 `WorldEventRecord` 将事实、展示文本、可见对象和下游投影混在一起。`eventKey` 内含部分标识，但阵营、人物、地点不是可查询字段；`occurredAt` 还可能退化为“第几天”的 epoch 时间。
3. 当前采集器仅覆盖五类原生战役回调。它已将 TaleWorlds 对象在回调中缩减为字符串，但结果仍被压平为 `day/kind/text/eventKey`。
4. `WeeklyReportService`、`WorldKnowledgeProjectionService` 和未来人物记忆都需要同一段历史，但它们的选择标准不同。让任一消费者在写入时过滤，会令其它消费者永久失去事实。
5. 底层 `IKeyValueStore` 只有按键 `Get/Set/Delete`；在未得到适配器实证前，不能假定跨键事务、CAS 或无限单值容量。

### 本批次的产品决定

**任何通过基本合法性校验的世界事实都记录；是否显示、是否可被某个角色调取、是否参与报告，由读取策略决定，不由采集器决定。**

这不是“所有角色都知道一切”。它只保证事实没有被过早扔掉：人物记忆只能读取其知识策略允许且与其有关的那部分；公开报告只能读取公开、适合报告的那部分；诊断信息不进入任何叙事消费者。

## 2. 事实链路和所有权

```
游戏原生回调 / 已确认的玩家行动结果
          -> WorldFact（不可变原始事实）
          -> WorldFactJournal（按时间保存，完整历史）
          -> WorldFactQuery + SelectionPolicy
             -> 近期动态 / 本周动态
             -> 世界知识投影
             -> 人物可知记忆候选
             -> 后续事件触发条件
```

| 层 | 拥有者 | 只负责 | 不负责 |
| --- | --- | --- | --- |
| 游戏真相 | Bannerlord | 发生、结果、存档中的真实状态 | 文本叙事、角色认知 |
| 原始事实 | AWAKE `WorldFactJournal` | 完整、结构化、可追溯地保存已发生事实 | 判断某 NPC 是否知道、写世界书 |
| 知识分发 | AWAKE 查询策略 | 按公开性、关系、地点、时间和用途选择事实 | 改写原始事实 |
| 人物记忆 | AWAKE 记忆层（后续批次） | 记录角色实际得到/经历/确认过的知识 | 把所有世界事实塞入角色记忆 |
| Persona / 世界书 | 外部内容与运行时投影 | 解释和表达被选出的材料 | 充当事实数据库 |
| 报告 | AWAKE 报告投影 | 将选择出的事实压缩成玩家可读文本 | 保存唯一历史来源 |

## 3. 结构化事实契约（`awake.world_fact.v1`）

每条已落库事实不可就地改写。最小字段如下；字段名、枚举和 canonical JSON 在实现前固定为一个 AWAKE 私有契约文件，并由 fixture 校验。

| 字段 | 含义与限制 |
| --- | --- |
| `schema` | 固定 `awake.world_fact.v1` |
| `factId` | 由稳定 `eventKey` 生成的 canonical SHA-256 标识；同 key 的相同事实幂等，不同事实稳定冲突 |
| `eventKey` | 采集器提供的稳定游戏事件身份；不得用展示文本拼接代替 |
| `occurred` | `{ campaignDay, timeSlot }`；day >= 1，timeSlot 是明确的战役时间槽，不使用 UTC epoch 伪时间 |
| `kind` | 受控的事件类别，如 `war_declared`；未知类别可存为 `native_unknown`，不得丢弃有效原生回调 |
| `origin` | `native_game`、`player_action_confirmed`、`mod_generated`、`legacy_import`、`runtime_diagnostic` 五选一 |
| `authority` | `game_confirmed`、`mod_asserted`、`legacy_unstructured` 三选一；用于避免把推测当游戏真相 |
| `entities` | 排序、去重的 `{ type, id, role }`；type 仅 `hero/faction/clan/settlement/party`，保留 ID，不保留 TaleWorlds 实体 |
| `initialKnowledgeScope` | `public`、`restricted`、`private`；描述默认可分发范围，不等同“具体角色已经知道” |
| `presentation` | 稳定的简短摘要和 domain；仅是展示投影的输入，不能反推实体或权限 |
| `causationId` | 可选；只关联已确认的上游事实/动作，不可用于循环引用 |

事实的 canonical 顺序固定为 `campaignDay -> timeSlot -> factId`。同一事实的 entities 先按 `type/id/role` 排序；所有字符串使用 NFC、UTF-8 JSON 字符串转义（不做大小写、空白或标点归一化）。

`factId` 的身份 preimage 固定为 `"awake.world_fact.v1\\n" + eventKey`，取 UTF-8 SHA-256 的 64 位小写十六进制。事实内容 fingerprint 的 preimage 是字段顺序严格为 `schema,eventKey,occurred,kind,origin,authority,entities,initialKnowledgeScope,presentation,causationId` 的 canonical JSON，其中缺失可选值写 JSON `null`，数组顺序即上述排序；同一 `eventKey` 的不同 fingerprint 是 `awake.world_fact.event_key_conflict`，不得覆盖已存事实。`factId` 和 fingerprint 都由 fixture 的固定字节串断言。

`campaignDay` 取 `AwakeRuntime.CurrentGameDay()`，必须大于零；`timeSlot = floor(CampaignTime.Now.ToDays * 144)`，每游戏日 144 个槽。有效事实还必须满足 `campaignDay * 144 <= timeSlot < (campaignDay + 1) * 144`；若宿主的 day 编号与该关系不符，采集失败并记录技术日志，绝不写出不一致事实。合法 `role` 仅为 `subject,party_a,party_b,previous_owner,new_owner,killer,capturer,prisoner,affected`。

### eventKey 的字节级编码

每种原生事实先形成 `awake.world_fact.event_key.v1` canonical JSON preimage，字段按下表的书写顺序，数值以不带指数的十进制整数写出，字符串使用本节既定 NFC/UTF-8 JSON 转义，缺失可选 ID 写 JSON `null`。不得使用分隔符拼接、显示名称、`ToString()` 的本地化输出或任意对象序列化。

`eventKey = "wf1-" + lowercaseHex(SHA-256(UTF8(preimage)))`。`factId` 再按第 3 节计算。数组内 ID 以 ordinal 升序排序；重复 ID 是非法输入。每个 kind 的 fixture 固定 preimage 原文、eventKey 和 factId 三者。

| kind | canonical JSON preimage（字段顺序即规范） |
| --- | --- |
| `war_declared` | `{"schema":"awake.world_fact.event_key.v1","kind":"war_declared","timeSlot":<integer>,"factionIds":[<id-a>,<id-b>]}` |
| `peace_made` | `{"schema":"awake.world_fact.event_key.v1","kind":"peace_made","timeSlot":<integer>,"factionIds":[<id-a>,<id-b>]}` |
| `settlement_owner_changed` | `{"schema":"awake.world_fact.event_key.v1","kind":"settlement_owner_changed","timeSlot":<integer>,"settlementId":<id>,"previousOwnerHeroId":<id-or-null>,"newOwnerHeroId":<id>}` |
| `hero_killed` | `{"schema":"awake.world_fact.event_key.v1","kind":"hero_killed","timeSlot":<integer>,"victimHeroId":<id>,"killerHeroId":<id-or-null>}` |
| `hero_prisoner_released` | `{"schema":"awake.world_fact.event_key.v1","kind":"hero_prisoner_released","timeSlot":<integer>,"prisonerHeroId":<id>,"capturerFactionId":<id-or-null>,"releaseDetail":<enum-name>,"releaseDetailCode":<integer>}` |

`releaseDetail` 为 Bannerlord 回调提供的枚举成员名，使用 invariant enum name；未知值写 `unknown`，但仍保留稳定的原始数值作为额外 `releaseDetailCode` integer 字段，顺序紧随 `releaseDetail`。`native_unknown` 不使用本表：其 key preimage 必须另有被测试的 kind-specific canonicalizer，否则不写入。

### 现有五种原生回调的固定映射

| 回调 / kind | eventKey 身份 | entities（`type:id:role`） | origin / authority / 默认知识 |
| --- | --- | --- | --- |
| `WarDeclared` / `war_declared` | 上表 `war_declared` preimage | 两个 faction：`party_a`、`party_b` | `native_game / game_confirmed / public` |
| `MakePeace` / `peace_made` | 上表 `peace_made` preimage | 两个 faction：`party_a`、`party_b` | `native_game / game_confirmed / public` |
| `OnSettlementOwnerChangedEvent` / `settlement_owner_changed` | 上表 `settlement_owner_changed` preimage | settlement：`affected`；旧/新 owner hero：`previous_owner/new_owner`；可得时两者 faction：同角色 | `native_game / game_confirmed / public` |
| `HeroKilledEvent` / `hero_killed` | 上表 `hero_killed` preimage | victim hero：`subject`；killer hero：`killer`（存在才写） | `native_game / game_confirmed / public` |
| `HeroPrisonerReleased` / `hero_prisoner_released` | 上表 `hero_prisoner_released` preimage | prisoner hero：`prisoner`；capturer faction：`capturer`（存在才写） | `native_game / game_confirmed / public` |

回调中的展示名称只进入 `presentation.summary`，不进入 eventKey；无法取得稳定 ID 的回调不得用名称替代 ID。`native_unknown` 可保存，但必须同样具备稳定 eventKey、时间和至少一个实体。

### 来源隔离规则

- `native_game + game_confirmed`：可作为报告、世界知识和人物候选的来源，但仍需读取策略允许。
- `player_action_confirmed`：只能在游戏动作已完成后的回调中生成，不能用玩家菜单点击本身当作事实。
- `mod_generated + mod_asserted`：可供明确选择它的模组功能使用；默认不进入正式报告和人物记忆。
- `runtime_diagnostic`：仍可记录到技术诊断通道，但**不写入 WorldFactJournal**，更不能进入报告、知识或记忆。
- `legacy_import + legacy_unstructured`：保留既有 v1 报告可读性；由于没有可靠实体结构，不能被人物实体查询误用。

## 4. 保存与兼容策略

### 4.1 新存储布局

当前 `awake.world_events.v1` 继续只作为旧报告和短期兼容读模型，**不能扩容伪装成长档案**。新事实使用独立、私有的 journal 命名空间：

- 一个小型 campaign journal root：仅保存 schema、已提交时间分段清单、版本和最后可用清单指纹；
- 固定七日的不可变事实段：键由 `campaign/day-start/day-end/revision/hash` 唯一确定，payload 为 canonical facts；
- 已提交 root 只引用已经 hash 校验过的段；读取只信任最后一个完整 root；
- 写入顺序是“写段并重读校验 -> 单键替换 root -> 重读确认”。若最后一步结果未知，重读同一 root：匹配目标即成功，保留旧 root 即 retryable，任何其它值 fail closed；绝不删除已提交的旧段。

此布局将“记录全部合法事实”与单一键的大小限制分开。事实段有明确 UTF-8 上限；单条事实超过上限返回稳定 `awake.world_fact.too_large`，不会截断或伪造成功。root 也有固定上限：到达上限时分层为不可变 index shard，而非无限增长单键。

**唯一写入者规则：** 一个 campaign generation 只有一个 `WorldFactJournalWriter`，由 `CampaignSessionReady` 创建、由 session cancellation 终止。所有采集回调只向它投递不可变 `WorldFactInput`；writer 以单一 FIFO 队列逐条处理，并且 root 的读—校验—替换全过程在同一个 writer 内串行。没有当前 writer、generation 不一致、writer 已关闭时，输入不被宣布已保存，返回 retryable `awake.world_fact.writer_unavailable`。因此本批次不把“并发 Get/Set 后重读”误当作避免丢写的办法；跨进程/多模组 writer 不在此私有 AWAKE journal 的支持范围内。

### 4.2 先证实、后依赖

本批次仍是一个 AWAKE 运行时批次，**不改变 Marcus Framework 公共 API**；但它包含 AWAKE 私有文件适配器与生命周期路径为 journal 提供能力证明/最小修复，不能把此项转嫁为一个未定义的外部前置批次。实现前必须先在 AWAKE 实际注入的文件存储路径上证明：

1. 单键 Set 的并发观察者只能看到完整旧值或完整新值；
2. 写入返回失败/不确定时，重开存储后的值与进程内缓存不矛盾；
3. 同一 campaign/session/generation 的存储上下文可重建，跨代或无效 scope 被拒绝；
4. 取消、截止时间和权限错误有稳定映射；后台初始化只走 `PermissionGate.Evaluate`。

若这四项有一项不成立，journal 写入保持关闭，并输出 `awake.world_fact.storage_unavailable`；采集器不得把内存队列当成已保存成功。其修复限于 `AwakeFileStorageService` 的 AWAKE 注入路径和 `CampaignSessionReady` 的 AWAKE 调用链：不更改 `IKeyValueStore` 公开签名、不改变 FrameworkHost 默认 `UnavailableStorageService`、不改变 SQLite/Runtime Service 默认组合。文件适配器需要保存并验证 journal lease（owner/campaign/timeline/session/generation）；每次操作以该 lease 创建新 context，不能复用已过期 deadline 的 RequestContext。后台恢复消费同一个 Evaluate-only storage task/result，不能再调用 `EnsureWorldStateReadyAsync` 或导致 `RequestAsync`。

### 4.3 迁移与回滚

- 旧 `awake.world_events.v1.records` 和既有周报完全只读保留，不修改、重算或删除。
- 首次新 journal 可用时，只对之后产生的 v1 facts 使用新路径；不从扁平旧记录虚构 entities。
- 旧周报继续能展示；新查询将旧记录标为 `legacy_import`，只允许时间/类别/文本兼容读取，不进入基于人物、阵营、地点的精确查询。
- 若 journal 根或任何被引用段损坏，查询返回 `corrupt/unavailable`，不把它解释为“本周没有事”；最后完整 root 仍可读。
- 回滚仅关闭新 journal 写入与投影，旧存档和旧报告不受影响；不可删除任何已写 journal 段。

## 5. 选择性查询，而非选择性写入

新增纯读取 `WorldFactQuery` 和具名 `WorldFactSelectionPolicy`。它们返回 facts 与每一条的选择理由；不修改 journal。新 journal 的每一种叙事读取只能通过这些 query 进入消费者，不能直接读取 chunk/root。

| 用途 | 默认可读集合 | 明确排除 | 结果 |
| --- | --- | --- | --- |
| `RecentDynamics`（近期动态） | 近期 `public`、`native_game`、游戏确认事实 | 诊断、仅 mod 断言、角色私密事实 | 时间倒序的短列表 |
| `WeeklyDynamics`（本周动态） | 七日内公开、游戏确认、适合报告的事实 | 诊断、未确认、重复 factId | 固定窗口快照，含 `sourceFactIds` |
| `WorldKnowledge` | 内容策略允许的公开事实 | 任何 private/restricted 事实，除非明确授权 | 可索引的知识投影 |
| `CharacterMemoryCandidate` | 角色已知范围内，且与其自身/所属阵营/地点/直接互动相关的事实 | “公开”但与角色无关联的全部历史，不可证明可知的 restricted/private | 候选，不等同已写入角色记忆 |
| `EventTriggerCandidate` | 规则明确订阅的结构化事实 | 展示文本匹配、诊断事实 | 可解释的规则输入 |

`CharacterMemoryCandidate` 只是一条交接 seam：本批次不写 Persona prompt、不改变对话、不产生“角色已经记住”的存档记录。那需要单独的记忆分发批次和用户签收。

### 消费者唯一入口和兼容旁路

| 消费者 | 新事实唯一入口 | 旧 v1 兼容入口 | 禁止的旁路 |
| --- | --- | --- | --- |
| `近期动态` | `WorldFactQuery.Execute(RecentDynamics)` | 只在 journal 尚不可用时由 legacy adapter 生成并标记不可正式保存 | 直接枚举 `WorldEventLedger` |
| `本周动态` | `WorldFactQuery.Execute(WeeklyDynamics)` | 既有完成报告只读展示 | `WeeklyReportService.BuildWindow(records, ...)` 直接接收 live ledger |
| `WorldKnowledgeProjectionService` | `WorldFactQuery.Execute(WorldKnowledge)` | legacy adapter 仅投影 `legacy_import` 且不带实体权限 | 直接将 ledger snapshot 中每条 record 变为知识条目 |
| 人物记忆候选 | `WorldFactQuery.Execute(CharacterMemoryCandidate)` | 无；旧记录不支持精确实体候选 | 文本搜索、世界知识投影反查 |

所有入口在 wiring 完成后均有一项调用链测试和一项静态门禁：生产报告/知识路径不得引用 `WorldEventLedger.SnapshotAll`、`CaptureSnapshot` 或直接遍历 `WorldEventRecord`。测试允许 legacy adapter 独占这些引用。

## 6. 报告和当前状态的边界

- `本周动态` 是 materialized view：保存 `sourceFactIds`、window、policy version、内容 fingerprint；不是事实所有者。
- `近期动态` 是当前查询结果，不把未封窗事实伪装成正式报告。
- 晚到事实不会悄悄改写已经完成的 `本周动态`；后续报告修订策略另行设计。当前报告照旧保持 v1 兼容读取。
- “某城归谁”“两国是否交战”属于可重建的状态投影，未来独立 `WorldStateProjection` 从 facts 更新；本批次不允许报告文本反向成为游戏状态。

## 7. 交付切片、入口与验收

这是一个批次，按实现依赖分四步交付；任一步失败停止在该步，不改变下一步语义。

1. **事实模型和兼容适配**：新增私有 `WorldFact`、canonicalizer、来源枚举；现有五个采集回调按第 3 节映射生成结构化输入；`WorldEventRecord` 仅作为旧读模型。  
   验证：五类输入各有 fixture；同事实幂等、同 key 不同 payload 冲突、实体排序、真实 timeSlot、未知 native kind。
2. **Journal 和恢复**：先完成 4.2 的文件适配器能力证明和私有 lease/context 校验；在 `CampaignSessionReady` 建立 Evaluate-only 的 storage task，后台回调只消费该结果；随后以唯一 writer 实现段/root/recovery，不经 Native knowledge readiness。  
   验证：保存-读档、chunk rollover、断电式未知 root 写、损坏段、跨 generation、无权限；`RequestAsync` 次数为 0；同一 generation 的并发回调保留所有事实。
3. **查询与报告桥接**：实现四个 policy 的纯读取 API；新 `近期动态` 与后续 `本周动态` 从 query 取得 facts；旧报告仍可打开。  
   验证：全部合法事实均可在宽查询找到；每个 policy 的排除理由可断言；报告含 sourceFactIds；legacy 不能伪装实体匹配。
4. **可观测性与运行时验收**：记录 `factId/origin/authority/window/queryPolicy/result/errorCode/campaignGeneration`，但不记录正文或角色私密细节。  
   验证：focused smoke、Release build、SdkSmoke、maf-lint；用户授权同步后验证 DLL hash（E3）；用户提供同 BuildId 真机新建/读档日志（E4/E5）。

### 不在本批次

- 不新增战役事件类别；先迁移已有五类采集器。
- 不写或导入世界书正文，不导入角色卡。
- 不直接接入人物对话、Persona、提示词或角色记忆写入。
- 不做谣言传播、NPC 知识模拟、语义检索或游戏内设置菜单。没有玩家可调规则，因此本批次 MCM 结论为“不新增”。
- 不改 Marcus Framework 公共 API；若适配器不能提供前述存储证明，另立 Framework 合同批次。

## 8. 接受标准与证据

| 编号 | 可观察结果 | 最低证据 |
| --- | --- | --- |
| A1 | 合法原生事实无论重要性均进入 journal；诊断不会混入 | focused fixture + journal inspector |
| A2 | 同 key 同内容幂等；同 key 异内容 fail closed，旧事实仍在 | focused fixture |
| A3 | 人物/阵营/地点能通过 ID 查询；旧 v1 记录不会伪匹配 | focused fixture |
| A4 | 一条事实从写段到 root 提交后，重开/读档可读；未知写不冒充成功 | fault fixture + restart fixture |
| A5 | `RecentDynamics`、`WeeklyDynamics`、知识、人物候选读取的是同一 factId 集合的不同子集 | policy matrix test |
| A6 | Native Knowledge 不可用、后台权限未授予、损坏文档都不会生成“空世界”或触发权限 UI | lifecycle/call-chain test |
| A7 | 既有 v1 周报和旧 records 可读且字节不改 | compatibility fixture |
| A8 | 新候选的 E1/E2/E3/E4/E5 按项目准则分别记录，未获得的证据明确标未验证 | build/smoke/hash/user log |

## 9. 选择依据与遗留风险

拒绝“继续把 records 上限从 200 调高”：它既不能保存完整历史，也不能支持不同消费者的正确读取。拒绝“按报告重要性写入”：它会永久造成角色记忆和后续事件的资料偏差。拒绝“本批次直接做人物记忆”：记忆分发需要另行定义角色如何得知，不能以 public 字段偷渡。

遗留风险是底层实际存储适配器的原子替换和失败可见性。它不是可以靠文档掩盖的问题，故第 4.2 节将它放在 Journal 写入之前的硬门。该门未通过时，最多可实现结构化的内存/兼容适配与离线测试，不能宣布“全战役已保存”。

## 10. 签收后的第一个动作

先建立事实模型、canonical fixture 和**实际注入文件存储路径**的四项能力证明；不先改报告、知识或人物对话。通过后才写 journal，避免再出现“有界面/有报告，但没有可靠历史”的假闭环。
