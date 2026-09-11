# Plan: AWAKE Worldbook Studio 第一版
_由多轮需求拷问锁定；经第一轮只读对抗审查后修订，待复审与用户最终签收_

## Goal
建立一个独立、本地运行、不依赖 Bannerlord 启动的 AWAKE 世界书开发工具 MVP。第一版只完成四条可验证垂直切片：参考资料登记、单一规范源格式的正典档案编辑、确定性校验/编译、NPC 知识预览与权限解释。工具面向其他世界书作者和程序开发者，界面默认中文并支持多语言显示；AI 只保留可关闭的建议接口，不作为 MVP 的编译输入或运行依赖。第一版生成独立候选包和报告，不替换当前运行时、不调用游戏同步脚本。

## MVP vertical slices

### Slice A — 工作区与来源登记

- 本地 Web 工具只访问显式白名单根目录：工作区、只读参考目录和导出目录。
- 参考资料以登记记录进入工作区，不直接复制成正典。
- 每个来源保存不可变元数据：`SourceId`、`SourceVersion`、`SourceNature`、`Universe`、`Era`、`Locator`、`QuoteHash`、原始文件路径和导入时间。
- `SourceNature` 至少区分：游戏数据快照、卡拉迪亚编年史、卡拉迪亚之王资料、成人化扩展、模组二创、未来时代、待考据、开发者原创。
- 导入禁止绝对路径逃逸、符号链接、超限文件、YAML 外部标签、任意对象构造和未经登记的外部引用。
- MVP 只支持已冻结的少量样本目录，不导入全部数万条文本；规模上限写入配置并在超限时硬失败。

### Slice B — 正典档案编辑

- 选择一个规范源格式：YAML 单文件档案，正文使用 YAML 多行字符串；不建立独立 Markdown 正文源，避免双文件写入权冲突。
- JSON 只作为编译产物，不作为开发者主编辑源。
- 每条正典档案必须有机器可校验的 `schema_version`、单调 `revision`、不可变 `id`、`title`、`status`、`domain`、`universe`、`era`、`sources`、`summary` 和至少一个知识表达层。
- `status` 枚举固定为：`canon`、`accepted_variant`、`rumor`、`reference_only`、`future`、`needs_review`、`rejected`。
- `domain` 固定为：`politics`、`economy`、`culture`、`war`。
- `universe`、`era`、`SourceNature` 为强制维度；当前霸主层、战团未来层、CK3 二创和待考据内容默认隔离，跨层引用默认阻止编译。
- 每个正典事实必须至少引用一个 `(SourceId, SourceVersion, SourceContentHash)`，或明确标记 `author_created: true` 并填写开发者判断；原创 assertion/expression 还必须有批准审计事件；无来源的内容不能静默进入 `canon`。
- 显示名称、中文标签和多语言文本与内部 ID 分离；内部 ID 不得从文件名、数组位置或显示名称推导。
- 允许 `aliases` 和 `redirects`，禁止直接删除已被引用的 ID；合并、拆分和迁移必须有显式迁移记录。

### Slice C — Schema、校验、编译

- 在实现编辑界面前，先提交 `awake.worldbook.authoring.v1.schema.json`、`source-fixture-demo.yaml`、`fixture-valid-minimal.yaml`、`fixture-valid-accepted-variant.yaml`、`fixture-invalid-source-version.yaml`、`fixture-invalid-canon-unapproved.yaml` 和迁移/拒绝规则。
- Schema 明确必填/可空字段、枚举、未知字段策略、数组顺序语义、引用格式和错误级别。
- 校验器至少检查：格式、重复 ID、缺失引用、来源定位、时间线、跨宇宙引用、状态合法性、权限引用、关键词/别名、变体可达性、动态占位符和内容层级。
- 错误分为 `error`（阻止编译）、`warning`（可编译但必须确认）和 `info`。
- 编译器只生成 `awake.worldbook.v2` 候选包、索引、校验报告、来源清单和内容哈希；第一版不宣称该包已经被当前 AWAKE 运行时消费。
- 同时生成 `runtime_mapping_report`：逐字段说明未来如何映射到现有 `WorldbookRule`、`Variants`、`TextMappings`、`WorldbookPersona`；当前无法表达的字段必须标为 `unsupported_for_v1`，不得静默丢失。
- 文件名和数组顺序不得产生稳定身份；缺失 ID、重复 ID、引用旧 ID 且没有 redirect 一律阻止编译。
- 内容大小使用固定 UTF-8 字节上限和字符上限；Provider Token 估算延后到 P1，不把不同模型 tokenizer 的估算当作 MVP 正确性标准。
- 编译写入临时目录，完成清单、哈希和完整校验后原子替换候选包；磁盘满、中断和校验失败不得部分覆盖上一候选。

### Slice D — NPC 预览与权限解释

- MVP 先实现确定性、无 Provider 的知识可见性模型，不实现动态传播和 NPC 逐个学习。
- 每条知识使用显式 `KnowledgeGrant`、`KnowledgeDeny`、`profile`、`scope`、`min_detail` 和 `layer`。
- 权限优先级固定为：硬禁止 > 明确允许 > 身份默认画像 > 地域/文化默认；无匹配时返回未知。
- 预览样本至少覆盖：普通村民、头人、商人、酒馆老板、普通士兵、贵族、高 Steward 贵族，以及匿名/缺失身份。
- `npc_preview` 只显示可见摘要、可见细节、传闻、未知、推荐中介和通用不可见状态；拒绝原因、规则 ID、来源和解释链只由 `author_diagnostics` 返回。
- `author_diagnostics` 提供解释链：档案 ID、来源 ID、知识状态、权限规则、身份快照、编译版本和最终表达层；`npc_preview` 不携带这些内部字段。
- MVP 不写入真实 NPC 记忆，不依赖游戏当前存档，不执行周报传播；动态 `KnowledgePatch` 留到后续批次。

## AI boundary

- MVP 只实现 Provider-agnostic 的建议接口和隔离存储结构，不要求安装或配置 AI。
- AI 输出进入不可编译的 `suggestions/` 区域，必须绑定 `SourceId`、来源快照哈希、提示词版本、Provider/模型、输入哈希、输出哈希、时间和操作者。
- 只有开发者显式逐项确认后，才可生成正典源文件变更和审计事件；未确认建议不能影响编译结果。
- 无 AI 流程必须能独立完成导入、编辑、校验、预览、编译和导出。

## Localization boundary

- 内部代码、稳定 ID、规范关键词和运行时枚举保持稳定；界面显示名、字段说明、身份名称、错误信息和帮助文本通过语言包显示。
- 关键词和别名按语言单独存储，不因界面翻译自动改写规范关键词；Unicode、全半角、繁简和大小写归一化规则在索引器中固定并测试。
- 正文语言和标签语言分开，源文引用不被自动翻译后当作原始来源。

## Source and authority model

- 游戏数据快照对当前运行状态字段负责：存活、当前所属、当前地点、当前战争/领地状态和真实存在的 NPC/地点。
- AWAKE 正典对历史、文化、宗教、人物动机、关系因果和官方/传闻解释负责。
- 每个正典档案在 Schema 中记录 `authority`；游戏快照与 AWAKE 正典的字段归属、覆盖方向、冲突状态和快照版本由语义报告记录，不伪装成单个正文字段。
- 角色死亡、国家灭亡、改名、分裂和存档回滚只改变运行状态或生成冲突报告，不自动删除历史正典。

## Workspace ownership and export safety

- `authoring/`：开发者源文件、来源登记、正典和建议。
- `compiled/`：可重建候选包、索引、报告和哈希；可删除重建。
- `fixtures/`：固定离线样本和预览断言。
- `export/`：明确生成的独立发布包。
- 运行时状态目录（债务、对话历史、压缩记忆、语音映射等）默认不由 Studio 编辑或导出；目录 ownership 表必须在实现前锁定。
- 第一版不调用 `tools\sync_module.ps1`，不写游戏目录，不修改 MCM 镜像；未来同步必须单独授权、确认绝对路径、检测游戏进程并默认采用跳过游戏目录策略。
- 成人扩展必须有 `content_tier`、默认隐藏、预览遮罩和导出确认；默认导出为纯净内容，不能误导出成人层。

## Validation and completion

- 固定 fixture 矩阵至少包含：有效正典、缺失来源、冲突实体、跨时代引用、权限正例、权限反例、未知身份、传闻、动态占位符、别名迁移、多语言显示、超限文件、磁盘/中断失败和成人层导出门禁。
- 无 AI 端到端验证必须断言：无 Provider 配置、无网络请求、相同输入产生相同编译哈希、错误码稳定、失败不覆盖上一候选。
- 工具自身需要独立的 build、package、release contract；可执行文件、静态资源、Schema、语言包、fixtures 和编译产物清单纳入哈希。
- 复用 Persona Workbench 时只复用明确允许的本地 Web/launcher 约定；不复制 Provider 授权、会话、CSRF、代次令牌等业务契约，除非另有计划和测试。
- 第一版完成定义：在不启动游戏、无 AI Provider 的条件下，其他开发者可以用中文界面导入一份登记过的样本来源、编辑一条正典、通过/触发校验、预览至少两种身份的可见差异、查看解释链、编译 `awake.worldbook.v2` 候选包并生成报告和哈希；不修改当前游戏目录。

## Key decisions & tradeoffs
- 独立本地 Web 工具，而不是游戏内编辑器；默认离线、无游戏依赖。
- 第一版先做四条垂直切片，延后完整资料迁移、动态传播、实际运行时替换和在线协作。
- YAML 单一规范源 + JSON 编译产物；不允许 YAML 与 Markdown 双源互相覆盖。
- 游戏数据只负责当前运行状态；AWAKE 正典负责世界观解释；参考资料必须经来源登记和开发者裁定。
- 中文/多语言界面与稳定内部 ID 分离；普通作者不需要理解英文字段。
- AI 可插拔但不进入 MVP 编译路径；所有 AI 建议隔离、可追溯、逐项确认。
- 编译、导出、同步分离；第一版完全不调用游戏同步脚本。
- 默认纯净导出；成人内容作为显式受控层。

## Review revision 2026-08-21 (third revision)

- 来源 Schema 将 `valid_until` 设为必填；`null` 表示永久有效，非 null 必须是未过期的 ISO 8601 时间。
- 生命周期变更在 Schema 中强制 `event_id`，语义校验绑定 `event_type=migration`；新增 audit-event、ID ledger、current pointer 机器 Schema。
- Profile/referral 机器注册表成为唯一权威；SourceNature 增加卡拉迪亚之王资料枚举；NPC 预览与作者诊断彻底分 DTO。
- F15 fixture 增加路径探针和输出 guard 证据字段；内容图固定节点、边、tier 推导和 unknown 阻断。
## Risks / open questions
- 现有运行时 `awake.worldbook.v1` 无法直接表达全部 v2 字段；必须先完成 `runtime_mapping_report`，再决定何时建立 v2 运行时适配器。
- 卡拉迪亚编年史、卡拉迪亚之王资料和成人化扩展混合不同时代/宇宙，导入样本必须人工选定并带来源元数据。
- 需要在实现前冻结最小 Schema、来源登记、审计事件、profile/referral 注册表绑定、字段目标映射、内容图、ID ledger、目录 ownership 和 fixture 清单。
- 需要确定工具项目路径、具体 .NET/前端版本和与 Persona Workbench 只读复用的最小边界。

## Out of scope
- 不删除或重写现有四版成人化世界书。
- 不把任何参考目录整体自动正典化。
- 不实现 NPC 逐个主动学习、周报传播、动态 KnowledgePatch 或 Provider 驱动的运行时学习。
- 不启动 Bannerlord，不修改存档、不自动启用 Mod、不同步游戏目录。
- 不替换当前 AWAKE v1 读取器，不生成新运行时 DLL。
- 不做在线多人协作、账号系统、云资料库或强制联网 AI。
- 不让 AI 自动裁定冲突、删除资料或直接写入正式正典。
- 不在 MVP 引入 embedding、训练模型或新推理引擎。

## Completion definition
MVP 以四条垂直切片的离线闭环为完成标准：来源登记 → YAML 正典编辑 → Schema/引用/权限/时间线校验 → 确定性编译 → NPC 视角预览和解释链 → 独立候选包、报告、清单和哈希。全流程不启动游戏、不需要 AI、不写游戏目录，且失败不会部分覆盖上一候选。

## Frozen pre-implementation attachments

实现前置契约已冻结在以下文件；实现批次不得在没有单独计划和用户签收的情况下扩大其语义：

| 附件 | 用途 | SHA-256 |
|---|---|---|
| `docs/worldbook-studio-plan/awake.worldbook.authoring.v1.schema.json` | 最小机器可校验源格式 | `DB5566F740FD4043F2719C7BC046F967EA48D4D6637BD021D59AF7862AA1BB29` |
| `docs/worldbook-studio-plan/PROFILE-REGISTRY.md` | profile 中文说明和版本边界 | `48214CF037D9856CE2B22F0C55177D977603020712AEB91A4E14B0284D5601A6` |
| `docs/worldbook-studio-plan/profile-registry.v1.json` | 机器 profile 注册表 | `309D10583E7B4AF05F28DE2C2E67A2528CB3D6C45752369C0F7D160FABC3F3A5` |
| `docs/worldbook-studio-plan/profile-registry.v1.schema.json` | 机器 profile 注册表结构 | `8FEEC96815135DA5BE6E480ECAC98CA076374C4BAFD740472D42E129993E4175` |
| `docs/worldbook-studio-plan/referral-registry.v1.json` | 机器 referral 注册表 | `9579CE74DB13CD16D0921A94747CF2AA35BD040306B25CF91046AE83B3C90141` |
| `docs/worldbook-studio-plan/referral-registry.v1.schema.json` | 机器 referral 注册表结构 | `729B150E9EEF87EAFBC61AE047733372582FF0B20E205976CFFE1BCCE75C2E27` |
| `docs/worldbook-studio-plan/PERMISSION-CONTRACT.md` | grant/deny 组合和预览隔离 | `23FE5E0596D5AD80BEAB057B80CADD421FE177A9678412312CCE1B1CA22D1CC0` |
| `docs/worldbook-studio-plan/npc-preview.v1.schema.json` | NPC 预览 DTO 白名单 | `932BF110C9E6B4087FC21BB34D5F3808E4F99431447D87B7A84CF0954E89E225` |
| `docs/worldbook-studio-plan/author-diagnostics.v1.schema.json` | 作者诊断 DTO 白名单 | `8CFA901FF04DAA9986369E964F96F579FD172F73BEEDEB715EC34FBCC968B271` |
| `docs/worldbook-studio-plan/preview-fixture.v1.schema.json` | 预览 fixture envelope 与注册表 hash 绑定 | `9912670758E8A2C71743B8C0A2B1077F8AF82F63C0A744B0BA2C58D94B099EF7` |
| `docs/worldbook-studio-plan/ID-MIGRATION-CONTRACT.md` | 稳定 ID、redirect 和生命周期 | `7D6C822DE9615F9869C689377E85EEE09BC362A46243B7C3ACFAFAC43E557A08` |
| `docs/worldbook-studio-plan/ID-LEDGER-CONTRACT.md` | ID tombstone、重用阻断和迁移 ledger | `C266356B55E22D8EE0BE11EB011BB052A164619E746B0E4F8ED3E5D1F35510EF` |
| `docs/worldbook-studio-plan/id-ledger.v1.schema.json` | 机器 ID ledger 结构 | `910A0660BF7DF051A46751E3DC116B100483877A5DC92D7CF8DABA429528803D` |
| `docs/worldbook-studio-plan/RUNTIME-MAPPING-CONTRACT.md` | v2 候选与现有 v1 字段映射/阻断规则 | `7207998DF510AEF1F70D1CD7A014EC7C40142498B0AFA8AF4BCE97D8F0CDB66B` |
| `docs/worldbook-studio-plan/DIRECTORY-OWNERSHIP.md` | 工作区、候选、运行时目录所有权 | `50FC25CE87E95906302B9F0FCD94D8FFF99E95B0DC029B2C608670501C28047C` |
| `docs/worldbook-studio-plan/FIXTURE-MATRIX.md` | 最小离线 fixture、CLI 和验收 | `C5EEFE168B7353845666A81D8A4042BAF8130195AB6E9EB4F0AC13828C5E57A0` |
| `docs/worldbook-studio-plan/fixture-F12-atomic-pointer.json` | F12 原子发布故障样本 | `DED297A5DF991F1B7E377A060B198A281373F89583B0A14396CFF6AA76F89D05` |
| `docs/worldbook-studio-plan/fixture-F15-v1-boundary.json` | F15 v1/v2 路径边界样本 | `CB76C4648D6F63DDCD8788480C434B38001666A290AC619F8D7E57DA96CC1E43` |
| `docs/worldbook-studio-plan/fixture-valid-minimal.yaml` | 最小有效正典样本 | `A86BC452B8979C94BC4445C8E42A46A513F190013519AA37D04969FE49F444C9` |
| `docs/worldbook-studio-plan/fixture-valid-accepted-variant.yaml` | 有效 accepted_variant 样本 | `D97067BADB7F13397ABF11BD9F2EBE7452B37E2F233A5A7A0E92BF743710FD5B` |
| `docs/worldbook-studio-plan/fixture-invalid-source-version.yaml` | 来源版本缺失失败样本 | `4A3B106E278A98C136910688336B66AEB62485AF95043CB61D117A77F5AF6776` |
| `docs/worldbook-studio-plan/fixture-invalid-canon-unapproved.yaml` | 未批准正典失败样本 | `D03A90E87F1FDC483DD9984A1752FA8CE36A11BA79511CC21B83BBB578284F1D` |
| `docs/worldbook-studio-plan/TOOLCHAIN-CONTRACT.md` | .NET 10、依赖、离线和原子发布边界 | `463FA8354F9B20A1478670F4E472B8CD3039482F685903FFDCF5C7427D595B80` |
| `docs/worldbook-studio-plan/current-pointer.v1.schema.json` | 机器 current.json 发布指针结构 | `4372E54B6D665CF372FEE781E2D4AADD01578BC1835D0C94306726CE1C7711E0` |
| `docs/worldbook-studio-plan/AUDIT-EVENT-CONTRACT.md` | 审计事件、批准、revision 和 hash 链 | `86EB2B8BFA8810502EC432CCAC018F3CAAEE511B9E74F2F023ED92B1176720FE` |
| `docs/worldbook-studio-plan/audit-event.v1.schema.json` | 机器审计事件结构与 decision 配对 | `6E9611C924B7E280BDFB7500094D61135DC93E56DC37C16994DE106E33F15473` |
| `docs/worldbook-studio-plan/source-demo.txt` | 来源正文最小 fixture | `0D53F08678D8101B0C7E5053B30224FC671C55051770BA18207435EAD7AD37BD` |
| `docs/worldbook-studio-plan/source-fixture-demo.yaml` | 机器来源登记有效 fixture | `39CDB78D5F0A7EAE8D619CCD417F99981C12CE97FBDDF7739663A70DF1DC6894` |
| `docs/worldbook-studio-plan/SOURCE-REGISTRY-CONTRACT.md` | 来源登记、版本、定位、哈希和许可状态 | `5AE70EA867F923E043D94BEDE55BB206AA14E5DD08D156E5A26228CF1ABB5FB7` |
| `docs/worldbook-studio-plan/source.registry.v1.schema.json` | 机器来源登记结构 | `AD03E181F1E600238D56E3B48530BA3682FFFCB34EB2AE98609BB4E143310700` |
| `docs/worldbook-studio-plan/CONTENT-TIER-CONTRACT.md` | 内容图闭包和纯净导出门禁 | `F60F0C93532249D39C5B0668E75FFCF6EB2C896443665ECD6E68FCD2FB1E9C84` |
| `docs/worldbook-studio-plan/content-graph.v1.schema.json` | 机器内容图节点/边结构 | `1FBB31FA6D3F41D6BCAF8EA903DDBD03D550174346A18E16610843C76E5F7187` |
以上附件是 MVP 的前置契约。任一变更都必须更新计划、重算哈希并重新审查；未通过附件校验不得开始实现。

## Additional hard gates

- `author_created` 与 `sources` 使用 XOR；`status=canon` 的原创 document/assertion/expression 必须是 `approved` 且有有效批准审计事件。
- 每个 assertion 和 expression 都必须有独立来源或原创声明，不允许仅在档案顶层挂一个来源覆盖整篇正文；`status=canon` 的原创 assertion/expression 必须是 `approved`，且 `review_event_id` 必须是非空、有效的 `event.*` ID，否则阻止编译。
- `SourceContentHash`、`SourceVersion`、规范化引用文本、稳定 locator 和许可证/使用状态失配时阻止编译。
- profile/referral 必须来自冻结机器注册表；候选绑定的版本/hash 失配、未知 ID 或继承成环阻止编译。
- 权限规则使用显式 grant/deny，deny 优先；作者诊断 DTO 与 NPC 预览 DTO 分离，NPC 预览不得泄漏拒绝正文或隐藏来源摘录。
- redirect 和 lifecycle 必须为 typed 结构并绑定 `event_type=migration`；ID ledger tombstone 阻止循环、过期重用和无人工映射的合并/拆分。
- v2 候选必须放入 `export/WorldbookV2/` 等正常自动探测不会扫描的独立目录；`--out` 先做 realpath/祖先路径硬阻断，marker 仅作诊断字段，不提供手工注入后的“不消费”保证。
- 编译输入白名单只允许已确认的 `authoring/*.yaml`；`suggestions/` 和其间接引用一律阻止编译。
- warning 确认绑定输入 revision、规则 ID 和报告哈希；源文件变化后自动失效。
- 纯净导出执行 content-tier 闭包检查，成人依赖经来源、别名、redirect、缓存或索引间接进入时直接阻止导出。
- CLI、退出码、报告路径、网络拦截和 fixture 预期以 `FIXTURE-MATRIX.md` 为准，不由实现批次自行解释。
- 候选写入同卷版本目录，使用 `complete.marker` 和原子 `current.json` 指针，保留旧版本；跨卷 rename 不作为原子性前提。



