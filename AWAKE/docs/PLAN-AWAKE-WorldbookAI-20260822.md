# Plan: AWAKE Worldbook Studio AI Assistance
_Locked via grill — by Codex + user, 2026-08-22_

## Goal

在不修改 AWAKE 冻结运行时候选、不写入游戏目录和不让 AI 直接取得正典裁定权的前提下，为 `Worldbook Studio` 增加首批 AI 辅助闭环：用户主动选择当前档案和 Provider，使用本机 Worker 或 OpenAI-compatible 云端 Provider 生成结构化审查建议与候选补丁；建议保存到 `authoring/suggestions/`，经过差异预览和人工选择后只应用到编辑器缓冲区，用户手动保存后再次执行现有校验/编译链。

## Approach

1. 先落地严格版本化契约：`assistance.request.v1`、`assistance.result.v1`、`suggestion-envelope.v1`、`knowledge-patch.v1`、`ai-consent.v1` 和 `provider-status.v1`；全部 `additionalProperties=false`，固定 discriminator、错误码、大小/深度/数组数量上限和 `request_hash`/`source_document_hash`/nonce/integrity 字段。分析类型固定为权限越界、领域一致性、元数据/关键词/referral 建议和候选文本润色，所有结果明确为非正典建议。
2. 增加统一 `IAssistanceProvider` 抽象和 Provider 目录；内置 `LocalWorkerProvider`（只允许显式配置的 loopback 地址和受限端口）与 `OpenAICompatibleCloudProvider`（只允许 HTTPS、拒绝私有/link-local/metadata 目标、关闭自动重定向）。两者都通过同一结构化结果契约返回，不写工作区。
3. 在 Core 增加 `AssistanceService` 和固定请求投影：只读取已保存的当前档案，拒绝脏编辑器直接发送；附带有限的 profile/referral registry 摘要，不发送来源正文、其他档案、完整工作区或 API Key。请求正文用 golden fixture 固定序列化，Provider 状态只做本地配置检查，不自动联网探测。
4. 实现两个明确隔离的人工确认上下文。Web：页面加载后显式调用 `POST /api/ai/session/bootstrap`，只接受单值精确 Origin `http://127.0.0.1:5077`，不要求已有 CSRF；该调用只创建内存 `web_session_id`、设置 HttpOnly、`SameSite=Strict`、非 Secure 的 loopback cookie 并返回一次性 CSRF 值，不接触工作区或 Provider。之后所有 Web 状态变更必须同时满足 cookie、CSRF header 和精确 Origin，缺失、多值、大小写/端口不匹配的 Origin 一律拒绝，GET 不产生业务副作用。CLI：`ai-consent-preview` 在当前用户专属本地应用数据目录创建短期 `cli_session_id`/consent record，设置 Windows ACL、禁用继承并验证仅当前用户可读；ACL 设置/验证失败或已有宽权限时 fail-closed，输出脱敏预览和 token。`ai-analyze --consent <token>` 只能消费该 CLI record，不要求也不接受浏览器 Origin/CSRF，拒绝裸 `--confirm-cloud`。两类 token 不互换，5 分钟内单次使用并绑定精确文档 hash、Provider、字段集和 request hash。失败、超时、限流或格式错误只返回稳定错误码，不自动无限重试。
5. 增加专用 `RequireSuggestions` 写入边界和原子建议存储：同目录临时文件、唯一 request/nonce 派生文件名、独占创建、`Flush(true)`、完成标记、同卷 rename、启动时清理未完成临时文件；按 request/provider/nonce hash 去重，重复请求返回既有建议而不制造逻辑重复。建议只保存脱敏请求清单、hash、字节数、同意记录和结构化结果，不保存原始 prompt/response、header、URL、环境变量名或秘密。建议目录设单文件/总容量上限，且对解码后的总字节数、对象深度、字段长度、建议数、补丁数和每个补丁值单独限额，超限拒绝而不自动删除。建议读取统一要求 `document_id + source_hash + saved_revision + suggestion_id`，session binding 由 Web HttpOnly cookie 或 CLI 受保护 consent record 在服务端派生，客户端不得提交可授权的明文 session ID；缺任一项或不匹配都返回不可枚举的 `WB-AI-SUGGESTION-404`，不把错误字段当查询过滤器。
6. 实现安全补丁和统一 CAS 服务：仅支持 `add`/`replace`/`remove` 的受限子集，禁止 root、move/copy/test、通配符、`-` 数组索引和重复/歧义操作；首批可应用路径仅限已存在或明确允许的本地化文本叶节点，权限规则、来源、registry、authority、审核状态、ID、schema_version 和 referral 列表只能作为 review-only 建议。补丁必须匹配源档案 hash、建议 hash、schema 版本、session/buffer ID 和一次性 apply nonce；应用只返回编辑器缓冲区，并先运行完整 overlay 校验，控制字段或语义错误使建议不可应用。新增统一 `DocumentCas`：save、validate-buffer、compile、apply 和 CLI/Web 多 writer 都必须提交 expected saved hash + revision，成功时以同一锁/原子替换递增 revision，过期一律返回 `WB-CAS-409`；`ai-apply` CLI 还必须提交 `--expected-hash`、`--expected-revision`、`--buffer-id`、`--suggestion-hash` 和 `--apply-nonce`，不得从用户可控路径推断。compile 只读当前匹配 revision，不再允许无 expected revision 的 AI 路径。
7. 增加 Web 入口和状态机：`GET /api/ai/providers` 只返回 `configured/missing/invalid/blocked` 等枚举和本地化标签，不返回 host、模型路径、环境变量名或原始诊断；`POST /api/ai/consent-preview`、`POST /api/ai/analyze`、`GET /api/ai/suggestions`、`GET /api/ai/suggestion`、`POST /api/ai/apply` 和 `POST /api/ai/validate-buffer` 共用 Core 与统一 session/CAS。前端实现保存前置、取消请求、过期/脏状态/旧 hash 拒绝、递增 buffer revision、逐条应用/撤销、确定性 undo 记录、差异预览和应用后校验；不提供 API Key 输入框，浏览器错误只显示稳定错误码。集中式 `SafeResponseMapper` 负责所有成功 DTO、异常、ValidationReport、workspace 信息、Provider 错误和日志投影，禁止绝对路径、host、header、环境变量名、秘密和原始 body；现有 `/api/workspace`、`/api/confirmation-token`、`/api/compile`、`/api/export` 也迁移到该 mapper，并加入全 endpoint 脱敏回归。
8. 保留四个明确 CLI 入口：`ai-providers`（仅本地配置检查）、`ai-consent-preview --document --provider --analysis`（输出脱敏预览与一次性 token）、`ai-analyze --document --provider --analysis --consent <token>`（建议写入受限 suggestions 目录）和 `ai-apply --document --suggestion --patch`（只向 stdout 输出新缓冲区，首批无覆盖文件选项）；固定退出码和 JSON 输出，不通过 CLI 绕过 consent/CAS/补丁校验。
9. 增加离线契约与安全测试并作为阻断门禁：伪 Provider、云端/Worker 假 Handler、请求投影 golden fixture、无确认不发送、HTTPS/SSRF/重定向拒绝、DNS resolve/connect 重绑定和连接池复用、Worker 伪装/HMAC 编码、CLI consent ACL、API Key 不进入异常/日志、超大/深 JSON、content-part/markdown JSON 兼容、固定 HTTP 状态/响应路径/空内容/body 上限/超时/取消错误码、一次性 consent/replay、session bootstrap/CSRF/精确 Origin、跨文档建议访问、旧 confirmation/compile/export/workspace endpoint 脱敏、非法补丁/stale hash/控制字段拒绝、连续 buffer revision/undo、save/compile/validate-buffer 多 writer CAS、CLI apply 参数完整性、overlay 全量校验、并发写入、OneDrive rename 故障、reparse race。每个门禁必须实际运行、不得 skip；未运行或不确定也失败。再执行 Release build、F01–F33 回归、AI 浏览器闭环、package/release-check。

## Key decisions & tradeoffs

- 云端首批只实现 OpenAI-compatible HTTP，不绑定厂商 SDK；兼容 OpenAI、兼容网关和其他实现，默认请求 `/chat/completions`，不发送 `response_format`，只允许一次有明确原因的 `max_tokens`/`max_completion_tokens` 兼容回退和一次 markdown JSON 解包。
- 云端配置只从环境变量读取：`WORLD_BOOK_CLOUD_BASE_URL`、`WORLD_BOOK_CLOUD_MODEL`、`WORLD_BOOK_CLOUD_API_KEY_ENV`、`WORLD_BOOK_CLOUD_TIMEOUT_SECONDS`、`WORLD_BOOK_CLOUD_MAX_TOKENS`；实际 API Key 只从被引用的环境变量读取，绝不写入工作区。
- 本机 Worker 只访问显式的 `WORLD_BOOK_LOCAL_WORKER_URL`，只允许 `127.0.0.1`/`::1` 和显式受限端口，不默认扫描端口、不直接接管 Ollama、不绕过既有 Worker 权限边界；Worker 返回结构必须遵守同一建议契约。
- 本机 Worker 需要本机受保护的安装密钥或签名 challenge-response；密钥只从本机安全配置/环境读取，伪装 Worker、错误 challenge 和错误签名一律拒绝。
- 云端端点在每次连接前解析并固定允许地址，连接层再次校验所有解析地址，关闭自动重定向；配置检查与实际连接不能共享可被 DNS 重绑定的未固定解析结果。
- AI 首批只分析已保存的当前档案，并发送有限 registry 摘要；不发送来源正文、其他档案、未保存缓冲区或完整工作区。跨档案比较和来源内容上传属于后续批次。
- `KnowledgePatch` 采用受限 JSON Pointer 操作，补丁是候选数据而不是命令；首批只允许本地化文本叶节点，任何补丁都必须经过 hash/CAS、路径白名单和完整 overlay 重新校验。
- AI 可以建议权限规则和文本，但不能自动批准 `canon`、改变来源/权威边界或写入运行时包；人工应用后仍需作者保存、审查和编译。
- Core 是唯一权威调用链；Web/CLI 只做入口和展示，不各自实现 Provider、提示词、补丁或文件写入逻辑。
- Provider 状态不联网探测；所有 Web/CLI/日志 DTO 都不暴露绝对路径、BaseUrl、API Key 环境变量名、请求头、异常正文或响应原文。
- AI 同意令牌与成人导出确认令牌完全分离；AI 使用一次性、短期、会话绑定的 consent，不复用现有 `/api/confirmation-token`。
- Web AI 的状态变更只接受 `Origin: http://127.0.0.1:5077` 的单值请求，并同时要求 HttpOnly session cookie 与 `X-AWAKE-CSRF`；缺失、重复、端口/大小写不匹配或其他 Origin 一律 `WB-AI-CSRF-403`。CLI 不走 Web cookie/Origin，而是消费当前用户应用数据目录中的短期 CLI consent record；Web token 与 CLI token 不能互换。
- `POST /api/ai/session/bootstrap` 是唯一 Web session 创建入口：它只验证精确 Origin，不要求尚不存在的 CSRF，不访问工作区，不调用 Provider；后续状态变更才同时要求 cookie、CSRF 和 Origin。页面静态 GET 不创建业务 session。
- Local Worker 握手契约固定为 `POST /awake/handshake`：Studio 发送 32-byte random `client_nonce`、协议版本 `awake.worker.v1`、Unix 秒时间戳和 request hash；Worker 返回同 nonce、符合 `^[A-Za-z0-9._-]{1,64}$` 的 `worker_id`、协议版本、时间戳和 `Base64Url(HMAC-SHA256(secret, length_prefixed_utf8(protocol|client_nonce|worker_id|timestamp|request_hash)))`。secret 只从 `WORLD_BOOK_LOCAL_WORKER_SECRET_ENV` 指向的环境变量读取；nonce 单次、时钟偏差超过 60 秒、签名错误、协议错误或响应超过 64 KiB 均拒绝并返回稳定错误码。
- Cloud HTTP 每次 AI 请求创建并销毁独立、不使用代理、不自动重定向的 `HttpClient`/`SocketsHttpHandler`；`ConnectCallback` 连接前解析 host，过滤未允许地址并固定一个合法 IP，socket 只连接该 IP，TLS SNI/Host 仍使用原 host；设置 `PooledConnectionLifetime=TimeSpan.Zero`，不复用跨请求连接，IPv4/IPv6 多地址逐个验证，代理配置被拒绝。Cloud 仅允许 HTTPS；Worker 仅允许 loopback 与显式端口。
- OpenAI-compatible 适配矩阵固定为：`chat_completions_v1` 发送 `model/messages/temperature/max_tokens`；2xx 只提取 `choices[0].message.content` 的字符串或 text content parts；空/缺失内容为 `WB-AI-FORMAT-EMPTY`；明确参数不支持的 400/422 才允许一次改用 `max_completion_tokens`，不再发送其他未声明字段；401/403=`WB-AI-AUTH-401`，408/超时=`WB-AI-TIMEOUT-408`，429=`WB-AI-RATE-429`，5xx=`WB-AI-UPSTREAM-5XX`，其他非 2xx=`WB-AI-HTTP-ERROR`；响应 body 上限 2 MiB，解析/markdown 单层解包失败返回 `WB-AI-FORMAT-JSON`，取消返回 `WB-AI-CANCELLED`。
- 每份 suggestion 同时绑定文档 ID、source hash、revision、session/buffer ID、suggestion hash 和一次性 apply nonce；每次应用都从上一次 buffer revision 继续，应用或撤销后旧 nonce 失效。
- OpenAI-compatible 适配矩阵固定为：`chat_completions_v1` 使用 string content + `max_tokens`；遇到明确的参数不支持错误只允许一次 `max_completion_tokens` 回退；`content_parts_v1` 只接受 text 部分；`markdown_json_v1` 只去除一层 fenced block；其余响应返回稳定的 `WB-AI-FORMAT-*` 错误，不发送未声明字段。

## Risks / open questions

- 不同 OpenAI-compatible 服务对 token 参数、JSON mode、内容分段和错误格式支持不完全一致；首批只做固定适配矩阵和有界回退，无法解析就报告，不做无限兼容分支。
- 云端内容可能包含未发布世界观文本；人工确认只保证用户明确看到数据范围，不等同于隐私或合规审查，界面需明确提示。
- 本机 Worker 的具体部署端点由本机环境决定；Studio 只提供协议适配和状态诊断，不负责注册、启动或修复 Worker。
- YAML 的格式化可能在补丁应用后发生变化；语义 JSON 不变即可，原始排版保持不属于首批保证。
- AI 建议质量不能由编译通过证明；本批验证 Provider 调用链、结构和隔离，不把内容判断包装成事实正确。
- OneDrive 的 rename/锁定语义可能影响建议原子写入；临时文件不进入读取索引，故障后由清理逻辑处理，仍需记录未验证的持久化风险。
- 现有非 AI `/api/workspace` 的绝对路径响应需要同时改成隐私安全 DTO，避免新 AI 状态接口与旧接口形成相反口径。

## Out of scope

- 不启动 Bannerlord，不修改 `Modules\AWAKE`、`PlayerExports`、`ModuleData`、`dist` 或冻结候选 `awake-20260820-syncpack-001`。
- 不实现 NPC 游戏内学习、周报传播、运行时动态知识更新或存档迁移。
- 不实现后台自动调用、定时批量扫描、自动接受建议、自动发布或自动同步。
- 不保存 API Key、完整云端请求正文、完整响应原文、BaseUrl、环境变量名或未脱敏 Provider 日志；只保存脱敏请求清单和结构化建议。
- 不引入本地 ONNX、embedding、rerank、tokenizer 或第二套世界书运行时读取器。
