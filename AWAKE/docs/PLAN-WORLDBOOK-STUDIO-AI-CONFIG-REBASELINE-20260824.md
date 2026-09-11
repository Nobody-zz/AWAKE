# Plan: Worldbook Studio AI 配置重基线
_Locked via grill — by Codex + 用户（2026-08-24）_

## Goal

只对 Worldbook Studio 的云端 Provider 与本机 Local Worker 配置层做一次离线重基线，使 DPAPI 本机配置、环境变量、Provider 状态、请求/结果契约、Consent/CAS、失败处理和日志使用同一条可验证路径。普通编辑继续离线运行；只有编辑者明确选择 Provider、预览发送范围并确认后，才允许执行单次 AI 请求。配置变化、凭据轮换或档案变化不得让旧请求和旧建议继续生效。本批次不触碰 MarcusAIFramework、不启动 Bannerlord、不调用真实 Provider、不使用真实 API Key、不同步游戏目录。

## Approach

1. **建立唯一配置解析快照**
   - 以 `ProviderConfiguration.FromEnvironment()` 为唯一归一化入口。
   - `environment` 快照模式只从传入字典读取命名凭据，不回读进程环境；真实运行模式才读取当前进程环境，避免测试和运行中状态漂移。
   - 按 Provider 分组做原子解析：云端的 DPAPI 配置与云端环境变量是一个配置组；Local Worker 环境变量是独立配置组。云端组不得字段级混用，Cloud/Local 两组可以各自报告来源和有效性。
   - DPAPI 云端组有效时整体优先；整体损坏、无法解密或字段不完整时，只有完整有效的云端环境变量组才能整体回退。每种失败都返回结构化、非敏感的 `LoadState`/警告。
   - 解析结果拆成不含秘密的 `ProviderResolutionSnapshot` 和仅由 Provider transport 使用的内部凭据句柄；公共状态 DTO、请求哈希、日志和建议文件不得接触明文密钥。
   - 快照同时提供每个 Provider 的来源、状态、警告、配置代次、凭据代次和非敏感配置指纹，供 CLI、Web、状态页和实际调用共同使用。
   - timeout、max tokens、模型名、环境变量名和端点都做显式字段校验；缺失、非法、默认化和有效必须可区分，禁止静默把损坏值伪装成有效配置。
   - `ProviderGenerationStore` 不是第二个独立权威，而是 `ProviderStateStore` 的唯一访问抽象；每个 Provider 的配置代次和凭据代次只在一个版本化状态记录中存在。状态记录包含 schema、cloud/local generation、DPAPI 云端设置（可选）及安全元数据，采用当前用户范围命名互斥锁、临时文件写入、flush、replace 和读回校验；旧文件或完整新文件二选一，残留临时文件启动时清理。
   - 所有保存、删除、来源切换和显式轮换都在同一互斥区内完成：先读取并校验旧记录，计算下一代次，写入完整新记录并原子替换；替换失败时旧记录和旧代次保持不变。配置代次对任何解析结果变化递增；凭据代次对 API Key/Worker Secret 来源或轮换动作递增。

2. **统一 Provider 状态与设置界面**
   - 修正 Web `ProviderSettingsView()` 直接读取存储层造成的状态漂移，使其只使用统一解析快照。
   - `configured` 只代表本地配置完整且通过安全策略，不代表远端可达；状态 DTO 同时返回安全来源、警告、配置代次和“未做联网验证”的明确标记。
   - 启动、打开设置、刷新状态不得自动联网；状态检查只做本地格式、凭据来源、协议和端点安全检查。
   - 增加独立的协议级 `health/handshake` 测试连接契约：仅在编辑者主动点击后执行一次有界探测/Worker 握手，不发送世界书正文、不调用模型、不消耗 Token；离线模式和 `--no-ai` 模式下由核心出站门直接拒绝。
   - 把本地状态接口与 Provider 出站接口分离；`loadProviders()`、`loadProviderSettings()` 只能执行本地解析，测试中必须证明不会触发出站请求。
   - 保持现有本地绑定边界，并明确区分接口类型：只读本地 Provider 状态 GET 允许无 `Origin`，但必须有有效本地会话/CSRF 和受限本地 Host；所有设置写入、Consent、分析和测试连接 POST/PUT 必须校验精确 `Origin`、会话和 CSRF。F53 作为只读豁免的回归契约，不引入全站 Origin 政策。
   - F53 的精确前置条件固定为：Host 仅允许 `127.0.0.1:5077`，绑定地址仅允许 `http://127.0.0.1:5077`，必须有 bootstrap 后的 session cookie 和 CSRF header；无 `Origin` 只对该只读 GET 豁免。`localhost`、`[::1]`、任意 `X-Forwarded-Host` 和其他 Host 在本批次拒绝或不作为受支持入口。
   - UI 显示安全的来源和警告，不显示 API Key、Worker Secret 或敏感端点细节。

3. **加入非敏感配置代次与请求指纹**
   - Consent、请求哈希和建议记录绑定 Provider 的非敏感配置指纹。
   - 指纹包含 Provider ID、规范化端点的安全摘要、模型、协议版本、超时、Token 限制、凭据来源和配置代次；不包含 API Key、Worker Secret 或任何由密钥派生的持久摘要。
   - `configuration_generation` 与 `credential_generation` 分开定义。DPAPI 保存、修改、删除时在同一原子设置记录中递增；临时文件、替换和读回校验保证崩溃时旧配置仍可读。
   - 共享的本机非敏感 `ProviderGenerationStore` 为 cloud/local 各维护代次。环境变量凭据按 Studio 启动快照读取；运行中更换环境变量需要重启 Studio，并通过设置界面的“凭据已轮换/使旧 AI 授权失效”或 CLI 明确命令递增对应代次。绝不用密钥派生摘要检测变化。
   - Provider 地址、模型、协议、限制、来源、代次或选择变化时，旧 Consent 和待应用建议立即失效；历史记录可以保留，但不得调用、应用或导出。
   - 提供共享的 `ProviderCas`/Consent 验证契约，Web 内存令牌和 CLI Windows 文件令牌可以物理隔离，但必须验证相同的 Provider、配置代次/指纹、凭据代次、请求哈希、来源哈希、revision、buffer 和过期字段。
   - 环境变量凭据的值变化本身不可安全检测；因此环境变量来源必须配套显式非敏感轮换动作 `rotate-credential --provider cloud|local`（Web 设置页和 CLI 共用同一 Core 操作），该动作只递增对应凭据代次，不读取、比较或持久化密钥。未执行轮换时，UI/CLI 必须提示“环境变量凭据变化无法自动检测，需先执行轮换”，不得静默宣称旧授权已失效。
   - `CredentialHandle` 只能由包含同一 Provider、配置代次、凭据代次和指纹的解析快照工厂创建；句柄绑定快照、仅传给对应 transport、一次请求短生命周期内有效，禁止公共 DTO、日志和持久层序列化，调用结束后释放其内部秘密载体。

4. **处理并发请求和失效结果**
   - 每次请求在 Consent 阶段捕获不可变的 Provider 配置快照、档案哈希、保存 revision 和请求哈希。
   - Web 引入按 session/request 绑定的请求注册表和 linked cancellation source；配置代次变化时取消同一 Studio 会话的进行中请求。
   - Consent 生命周期显式记录 `issued → running → completed/failed/cancelled`；一次性令牌不可重复消费，失败、取消或失效只允许重新预览，不允许复用旧令牌。
   - CLI 使用原子状态文件/重命名表达相同生命周期；保留现有 Windows-only ACL 契约，本批次不扩展 CLI ACL 参数化和跨平台权限重构。
   - 配置修改时尽可能取消进行中的请求；无法及时取消时允许请求结束，但结果必须在保存前再次核对配置代次和全部 CAS 条件。
   - 任何不一致都丢弃结果并返回“Provider 配置已变化，请重新分析”，不自动重试、不自动切换 Provider。
   - `SuggestionStore.Save` 接收并原子验证完整 CAS envelope；配置在 Provider 返回与建议落盘之间变化时，结果不得保存为有效建议。
   - 唯一 `ProviderCas` envelope 字段固定为：`provider_id`、`configuration_generation`、`credential_generation`、`provider_fingerprint`、`request_id`、`consent_token_hash`、`document_id`、`source_document_hash`、`saved_revision`、`request_hash`、`buffer_id`、`expires_at`。分析前、响应保存前、建议应用/拒绝前三处都必须逐字段校验；应用阶段还必须校验建议状态为 pending 和一次性 apply nonce。
   - 状态转换表固定为：`issued → running → completed|failed|cancelled|expired`；只有 `issued→running`、`running→completed`、`running→failed`、`running→cancelled` 和超时清理允许，终态不可复用。Web 在进程内锁下执行 CAS；CLI 用同一字段的原子文件重命名/状态写回。重复消费、崩溃恢复不确定或状态文件损坏一律 fail closed，重新预览。
   - generation 变化的线性化点是 `ProviderStateStore` 在互斥区内成功替换完整状态记录的时刻；随后按 Provider 标记旧请求失效并取消 linked CTS。所有出站 gate、结果保存和应用校验都必须读取不早于该线性化点的当前状态；请求注册必须先读取并绑定当前 generation，再进入可运行状态。

5. **固定 Provider 选择、离线和失败策略**
   - 云端和 Local Worker 必须由编辑者逐次明确选择，互不自动回退，不共享失败重试状态、凭据或会话。
   - 保存、校验、编译、导出等普通 Studio 操作保持离线。
   - 在 Core 建立不可绕过的出站授权门；Web、CLI、分析请求、测试连接和未来 Provider 入口都必须调用同一门。只有选择 Provider、预览发送范围并确认后，才允许该次 AI 请求；`--no-ai` 和离线审查模式始终 fail closed。
   - 在 transport 层显式声明零重试策略；分析、测试连接、Worker 握手和 HTTP handler 对 408、429、认证错误、网络错误均只允许一次出站尝试。超时、网络错误、认证错误、格式错误和 429 返回一次明确错误，用户重新操作时重新生成 Consent。
   - 请求必须带固定超时和取消能力，不得阻塞编辑器 UI。
   - Provider 工厂和路由测试必须证明选定 cloud 失败绝不创建 local，选定 local 失败绝不创建 cloud，未知 Provider fail closed。
   - Core 出站授权输入固定为 `OutboundAuthorization(operation, provider_id, request_id, provider_cas, consent_proof, offline_mode, no_ai)`；`operation` 只允许 `analyze` 或 `health_handshake`。授权由 Consent/测试连接入口一次性签发，Provider transport 不接受缺失或不匹配的授权；`offline_mode`/`no_ai` 在最前面拒绝并返回固定错误码，不能被 Provider 配置覆盖。
   - `health_handshake` 使用独立的一次性授权，不复用分析 Consent；请求只允许固定健康路径和最小协议字段，禁止世界书投影、模型字段和提示正文。

6. **固定秘密和 AI 结果的安全边界**
   - 云端 API Key 继续使用 Windows DPAPI 本机保存；环境变量仅作为既定回退来源；Local Worker Secret 继续按环境变量读取。
   - 任何错误、警告、日志、哈希、Consent 和建议记录都不得包含密钥、Authorization Header、完整敏感查询参数、原始异常消息或可逆凭据内容。
   - Provider/HTTP 异常统一经过脱敏映射器，只输出固定错误码、Provider、操作、耗时、关联 ID 和非敏感哈希；禁止直接记录 URI、请求头、请求正文和原始响应。
   - 所有 Provider 结果强制 `review_only=true`；AI 只能返回建议、候选文本和受限结构化补丁。
   - 应用前重新检查档案版本、来源哈希、请求哈希、Provider 配置代次、建议绑定和一次性 nonce；非法字段、越权路径或未知操作时整批拒绝。

7. **建立离线验证和交付证据**
   - 为配置优先级、DPAPI 损坏回退、字段不混用、非敏感警告、环境快照隔离、字段损坏分类、配置/凭据代次、旧 Consent/建议失效、共享 CAS、请求生命周期、并发结果丢弃、出站门、Provider 不自动切换、零重试、日志脱敏和 CLI/Web 一致性补充独立离线测试。
   - 测试连接使用固定 mock health/handshake contract，验证请求方法、路径、无正文/无模型调用、超时和出站次数；真实上游是否正确计费列为后续有界演示，不由离线证据宣称。
   - 测试明确记录每个场景的输入、出站请求计数、状态转换、错误码和断言结果；现有 F01–F70 证据与新增测试分开列出。
   - 零重试矩阵固定覆盖 `cloud/local × analyze/health_handshake × DNS/连接失败、408/超时、429、401/403、5xx、非法 JSON、响应体读取取消`；每个格子都断言 `SendAsync` 最多 1 次、无备用 Provider 调用、无后台延迟重放；取消后也不得出现第二次请求。
   - 验证 Release 编译、Contract JSON 解析、现有 Worldbook Studio F01–F70、Launcher smoke、Web DOM/脚本和 package/release-check 不回退。
   - 本批次只产生离线证据；真实云端请求、真实 Local Worker、真实 API Key、Bannerlord 实机、游戏目录同步和游戏内验证均列为后续明确动作。
   - 更新本批次检查点，记录实现文件、测试命令、哈希和未验证项；不把离线通过包装为真实 Provider 或游戏内闭环。

## Key decisions & tradeoffs

- **范围收窄：** 只处理 Worldbook Studio Provider/Local Worker；MarcusAIFramework 已迁移到独立任务，完全排除。
- **配置权威：** 所有界面和调用都使用同一份归一化配置快照，禁止 Web、CLI、状态页各自解析。
- **配置优先级：** 云端配置组内有效 DPAPI 优先，整体失效才整体回退云端环境变量；Local Worker 是独立配置组。组内禁止字段级混用，组间分别报告来源。
- **Provider 路由：** 云端和 Local Worker 都是编辑者主动选择的独立路线，失败不自动切换。
- **离线优先：** 普通编辑不联网；AI 请求是一次性、明确确认的例外；`--no-ai` 永远禁止出站。
- **状态语义：** `configured` 只表示本地配置可用，不承诺远端连通；无自动健康探测。
- **失效策略：** Provider 配置、凭据代次、档案哈希、revision 或请求范围变化都会使旧授权/建议失效；历史可追溯但不可应用。Web/CLI 使用同一 CAS 字段集合。
- **凭据处理：** 密钥不进入指纹、日志和持久请求记录；配置快照与 transport 凭据句柄分离；DPAPI 配置变更使用原子本机代次，环境变量变更以重启并通过共享本机代次显式失效旧授权。
- **重试策略：** 无自动重试，避免重复扣费、重复 Token 消耗和限流升级。
- **AI 权限：** AI 永远是审稿助手，不得自动写入、发布或导出世界书。
- **日志边界：** 只记录 Provider、操作、状态、错误码、耗时、关联 ID 和非敏感哈希；不保存完整提示词和原始响应。
- **一致性边界：** 状态查询是纯本地操作，测试连接是独立出站操作；Core 出站门是 Web/CLI 的共同硬门。

## Risks / controlled limitations

- 现有 `ProviderSettingsStore.TryLoad()` 的布尔结果是待替换实现，不是设计分歧；实现目标是迁移到唯一 `ProviderStateStore` 记录并提供不泄露原因的解析状态，区分“文件不存在”“DPAPI 不可用”“字段不完整”和“已安全回退”。
- 环境变量凭据在 Studio 运行中变更时按设计不会热更新，且密钥值变化不可自动检测；UI 和启动器必须要求重启并执行共享本机 `rotate-credential` 动作，未轮换时不得宣称旧授权已失效。
- 本地 Web 与 CLI 的 Consent 存储目前是分开的；本批次保持隔离，不允许跨界面消费令牌，需在测试中明确验证。
- Web 请求取消、配置代次递增和建议原子保存需要共同的请求注册表/状态转换；若某 Provider 仅能在 HTTP 层取消，CAS 仍是最终安全门。
- 真实上游 Provider 的响应差异、限流策略、模型质量和真实 Local Worker 版本兼容不能由离线测试证明。
- 测试连接不调用模型只能由协议级 mock 证明“本地发送的请求不含模型正文”；真实上游计费/路由行为仍需后续有界演示。
- 交付按一条最小闭环验收：解析快照 → 单一代次状态 → Consent/CAS → Core 出站门 → 单 Provider transport → 二次保存/应用校验；不追加与这条失效链路无关的 UI 或通用安全改造。

## Out of scope

- MarcusAIFramework 的源码、配置、依赖、打包和迁移。
- AWAKE 游戏运行时、NPC 知识查询、世界事件、周报、Persona DSL 和存档逻辑。
- Bannerlord 启动、实机测试、游戏目录 `Modules\\AWAKE` 同步、PlayerExports 和冻结运行候选。
- 真实云端 Provider 请求、真实 API Key、真实 Local Worker 调用和网络质量验证。
- 重做世界书编辑器的普通表单功能、世界书内容创作和旧世界书迁移。
- 本批次允许对 Provider 设置记录、共享本机代次记录、Web/CLI Consent 状态和 Suggestion CAS 做定向存储契约扩展；不进行无关的通用存储重构。
- CLI `icacls` 参数化、跨平台 ACL 重构、代理/IPv6 Host 扩展和一般 Web 安全加固另立后续批次；本批次只保留 F53 及现有本地来源边界的回归证明。
