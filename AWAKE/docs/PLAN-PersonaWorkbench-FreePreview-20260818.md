# Plan: Persona Workbench Free Preview
_Locked via grill — by user + Codex, 2026-08-18_

## Goal

交付一个可独立试用的 Windows 本地 Persona Workbench：用 AI 辅助用户把自由角色描述制作成可校验、可编辑、可导出的结构化 Persona 与 Persona DSL；在同一稳定核心上，后续提供 AWAKE 创作适配器。首发不依赖 Bannerlord，不修改游戏目录，不将 AI 输出自动投入运行时。

## Delivery order

1. **Batch P — Free Preview（首发）**
   - 交付本机 `127.0.0.1` 网页工作台和 Windows 启动入口；首次选择独立工作区，不扫描或修改 AWAKE/游戏目录。启动时生成高熵 bootstrap 令牌，仅放入本机启动 URL fragment；前端一次性交换为绑定 server-instance、短期失效的会话令牌，令牌仅驻留页面 origin-scoped 内存/session storage，随后用 `history.replaceState` 清除 fragment。每个写请求都必须在自定义 header 中携带该令牌和 CSRF 值；服务端不接受 cookie-only 认证，发送 `Referrer-Policy: no-referrer`，拒绝非 loopback 对等端、非精确 Host/Origin 或无效令牌的请求，并禁用 CORS。
   - 提供自由描述输入、通用标签注册表、结构化 Persona 编辑、Persona DSL 实时预览、预算/冲突/schema 校验、本地历史与 `.persona.json` / DSL / Markdown 导出。
   - 提供四个显式 AI 动作：生成初稿、局部改写、结构审稿、DSL 精简。每次仅产生可编辑候选 `draft`，不自动批准、不后台调用、不无限重试。
   - 支持独立 OpenAI-compatible Provider 配置；Key 只由后端经当前 Windows 用户 DPAPI 解密，配置仅保存密文或引用，前端、命令行、导出、历史、异常与 HTTP 日志均不得接触明文，并统一经 secret redactor 脱敏。DPAPI 不可用时拒绝持久化 Provider Key（可要求当次手动输入且仅驻留内存，或禁用 AI 请求），不得退回明文配置。Provider 仅允许 HTTPS，或严格解析后的 loopback IP/localhost；禁用重定向和环境代理，连接前后复核解析地址。每个实际非 loopback 目标首次外发均需明确确认；本地端点不触发云端提示。
   - 对 429/超时/格式错误保存工作状态、单请求队列、有限格式修复与明确冷却提示；README 只在故障排查简要说明，不将限流机制作为卖点。每个 AI 动作都有字段白名单、对象深度/数组/字符串/总字节上限、未知字段拒绝、控制字符拒绝与逐字段差异预览；失败内容只保留为隔离候选。所有用户输入、导入 JSON、legacy 内容、历史和 AI 输出一律视为不可信纯文本，禁止 raw HTML/危险 DOM sink，并以 restrictive CSP 与全渲染路径测试保证转义。

2. **Batch A — AWAKE authoring adapter（Free Preview 反馈后）**
   - 读取 AWAKE manifest 与 persona 标签注册表；选择/填写稳定 `CharacterId`，展示旧人格为只读参考。
   - 先扩展 AWAKE worldbook manifest/loader，增加版本化 `overrideDirectory`、根目录围栏以及 definitions/overrides 的固定加载优先级；围栏必须以 canonical resolved path 验证仍处于选定 Worldbook root 内，拒绝 adapter 控制树中的 reparse point，并测试 manifest 路径穿越和 junction/symlink 逃逸。在此契约和测试落地前，Workbench 不得写入 `persona_definitions/overrides/`。
   - 按“一角色一文件”的独立 `persona_definitions/overrides/` 目录写入 AWAKE draft；批准时按 `(CharacterId, IdentityId, Role, Scope)` 计算规范 ownership key，拒绝任何重叠 approved 覆盖，不能依赖运行时 priority 静默遮蔽。
   - 只有人工批准后的 AWAKE Persona 才能成为 `approved`；编辑 approved 文件自动退回 draft，撤销批准/禁用后按既有回退链处理。Loader 必须硬拒绝未知 major schema、未知 status 和未迁移定义，不能只记录警告后继续参与选择。
   - 不自动同步 dist/游戏目录，不做游戏热重载；构建、同步和游戏验收仍走 AWAKE 既有流程。

3. **Batch G — 后续游戏内适配（不在本计划实现）**
   - 游戏内只读命中/DSL/诊断与玩家覆盖；不直接编辑已安装内容包文件。

## Data contracts

- Free Preview 默认文件：`persona-workbench.character.v1`；使用稳定版本字段、通用五类标签 `trait`、`expression`、`behavior`、`trigger`、`boundary`。
- AWAKE 模式使用既有 `awake.persona.definition.v1` 与 AWAKE 标签注册表；自由自定义标签只能用于 Free Preview，导出 AWAKE 前必须映射为已注册标签。
- 通用模式与 AWAKE 模式共用分类、字段含义、DSL 顺序、确定性生成与校验原则，但分别加载其注册表。
- 人格文件只保存长期塑造、经历和作者信息；王国、家族、职位、婚姻等当前身份只作为预览样本或 AWAKE 运行时上下文，不写死为 Persona 真相。
- Workbench 与 AWAKE 共用版本化 JSON 契约、标签定义与黄金测试样例；首发不强行共享运行时 DLL。

## UX and safety decisions

- 固定五步流：选择模式 → 输入资料 → AI 工作区 → 结构化编辑与预览 → 保存/导出。
- 界面提供基础结构化编辑、只读 DSL 预览、高级 JSON 兜底、旧人格只读对照；不做富文本、账号、云同步、多人协作、无限聊天、批量自动生成或 AI 自动批准。
- 每次保存前备份单个目标文件到 `.history/`；以工作区锁和写入会话 ID 串行化，临时文件在目标同卷创建并 flush，解析/校验后再原子替换。打开时保存内容哈希/版本，且在 backup/replace 前立即复核；不一致时中止覆盖，并把当前编辑结果保存为单独命名的 conflict draft。可校验提交 journal 记录 backup → replace → complete，用于崩溃恢复；第二实例只读，检测 OneDrive/外部修改和未完成提交。
- 恢复旧版本只生成新的 draft，不静默恢复 approved。
- AWAKE authoring 写入只发生在项目源码世界书目录；禁止直接写 dist、游戏安装目录或玩家存档。

## Verification

- 为 schema、通用/AWAKE 标签、冲突、draft/approved 门禁、JSON 导入导出、历史恢复和并发锁定编写自动测试。
- AI adapter 用本地模拟响应覆盖：有效 JSON、格式修复、未注册标签、429、超时、取消、Key 脱敏和不自动重试。
- 新增安全集成测试：loopback 之外的对等端拒绝、Host/Origin/CSRF/bootstrap token 的一次性交换与 URL fragment 清除、无 CORS/no-referrer、会话令牌必须在自定义 header 中携带且 cookie-only/跨实例令牌拒绝、endpoint 重定向/代理/DNS 变化拒绝、DPAPI 不可用 fail-closed、Key 不出现在前端/进程参数/日志/备份、恶意深层或超大 JSON 隔离，以及用户/导入/历史/AI 四类文本均不能成为 DOM-XSS。
- 新增恢复与发布门禁：断电式 journal 恢复、OneDrive 外部改写的写前哈希冲突与 conflict draft、同卷原子替换、manifest traversal/junction escape，以及 package manifest 白名单；发布包不得包含 Key、`.env`、日志、`.history`、测试 fixture、源码映射或开发配置。
- DSL 用黄金样例覆盖确定性生成、字段级局部改写、预算裁剪、自由标签与 AWAKE 导出映射。
- AWAKE adapter 以真实 Loader 解析导出的 draft，并验证 approved 文件只在既有优先级下生效。
- 首发前验证：全新机器工作区、无 API 手工模式、云端确认、本地端点模式、导出复读、失败后草稿仍存在。
- 不启动 Bannerlord；Batch A 后的游戏内验收单列，且仅由用户提供运行时日志确认。

## MCM evaluation

Free Preview 是独立本地工具，没有 Bannerlord 运行时或玩家可调游戏行为，因此不新增 AWAKE MCM 项。Batch A 也只写作者侧文件，不新增 MCM；未来 Batch G 若出现游戏内玩家覆盖开关，再单独评估 MCM。

## Risks / open questions

- API Key 的 Windows 安全存储实现必须以所选运行时和依赖可用性验证；若 DPAPI 不可用，拒绝持久化 Key，不提供任何明文回退。
- OpenAI-compatible 端点的 JSON Schema 能力不一致，客户端必须支持受限 JSON-only 提示和本地严格校验，不能假设所有 Provider 都支持 response_format。
- Free Preview 的试用反馈可能要求调整通用标签名称/分类；必须通过 schemaVersion 与迁移策略演进，不能原地改变既有语义。
- Windows 自包含打包须在 Batch P 中先固定运行时、资源白名单、启动端口/会话令牌传递和升级策略；未通过发布门禁不得分发试用包。

## Out of scope

- Bannerlord 游戏内 Persona 编辑、玩家人格模式、存档/时间线提交、热重载。
- 自动批准、自动同步、在线账号、云端协作、遥测上传、批量无人值守生成。
- 直接解决上游 Provider 的账户额度、429 或 CC Switch/YJ 服务稳定性；工具只做本地可恢复的失败处理。
