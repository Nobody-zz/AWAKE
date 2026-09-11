# AWAKE 服务地址容错立项 — 2026-09-10

## 立项结论

- 目标：MCM「服务地址」接受用户**自然的填法** —— 根地址、带版本前缀、或直接粘贴完整接口地址
  （`…/chat/completions`），都能规整成可用的 API 根，而不是只有一种形状能用。
- 触发证据：用户 2026-09-09/09-10 在 MCM 与该工具链里一贯填
  `https://api.deepseek.com/chat/completions`（完整接口地址）；同一天 `provider-models` 连续三次
  `Status=4 (Failed)`，而 `provider-profile-upsert` 全部 `Status=1 (Applied)`。
- 风险等级：`standard`（单文件逻辑 + 文案 + 测试，可逆，无新存档格式、无公共接口变更）。
  通道：审查状态机，`max_rounds=2`（一次独立审查 + 一次修订）。
- 本文件是**立项**；实现授权来自用户 2026-09-10 的"做"。

## 当前事实（已核实）

| # | 事实 | 证据 |
|---|---|---|
| 1 | Runtime 把 BaseUrl 当 **API 根**，自己拼固定子路径：拉模型拼 `models`、生成拼 `chat/completions` | `F/MarcusAwakeProvider/src/ProviderAdapters.cs:31`、`:56`；拼接规则 `ProviderContracts.cs:325` `BuildEndpointUri` |
| 2 | 因此 `…/chat/completions` 会被拼成 `…/chat/completions/models` → 404 | 同上；`ExactOriginEndpointPolicy`（`ProviderContracts.cs:460`）只校验同源，**不校验路径**，所以错误形状不会被提前拦下 |
| 3 | 用户自己的另一个工具（WorldbookStudio）**本来就容忍三种写法** | `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/AssistanceProviders.cs:454` `BuildCloudEndpoint`：以 `/chat/completions` 结尾→直接用；以 `/v1` 结尾→拼 `/chat/completions`；其他→拼 `/v1/chat/completions` |
| 4 | AWAKE 侧目前**只做合法性校验，不做规整** | `S/AwakeProviderConfiguration.cs:557-591` `TryCaptureSnapshot`：要求绝对 http(s)、无 userinfo/query/fragment，然后 `uri.AbsoluteUri` 原样交给 Runtime |
| 5 | MCM 提示词只说"完整地址，例如 `https://api.openai.com/v1`"，没说明完整接口地址也可 | `S/AwakeConfig.cs:51`；`ModuleData/Languages/awake_strings.xml:76`；`ModuleData/Languages/CNs/awake_strings-zh-HANS.xml:76` |

## 决策锁定

| # | 决策点 | 结论 |
|---|---|---|
| D-1 | 修在哪 | AWAKE 侧入口（`TryCaptureSnapshot`），把用户输入规整成 API 根后再交给 Runtime。**不动** Runtime 的 `BuildEndpointUri` / `ValidateProfilePolicy` / `ExactOriginEndpointPolicy` 契约 |
| D-2 | 规整规则 | 去掉末尾的已知接口后缀 `/chat/completions`、`/completions`、`/models`（忽略大小写，容忍尾斜杠），保留其余路径（如 `/v1`）；**发生改写时写一行** `provider_base_url_normalized from=… to=…`，不静默改写 |
| D-3 | 不自动补版本 | **不**自动追加 `/v1`（DeepSeek 的 `/models` 就在根下；OpenAI 需要 `/v1` 时由用户写进地址，与现有 MCM 提示一致） |
| D-4 | 校验不变 | 非 http(s)、含账号密码、含 query/fragment、空值仍然拒绝，中文错误文案逐字不变 |
| D-5 | 提示词 | 中英 MCM 提示词补充"可直接粘贴完整接口地址"，属**声明的用户可见文案变更**，仅此一处 |
| D-6 | 版本 | 另立 BuildId，不改 `SubModule.xml` / 程序集版本；`v0.2.0` 不变 |

## 契约（可观察结果）

`入口` MCM「服务地址」填 `https://api.deepseek.com/chat/completions`
→ `调用` `TryCaptureSnapshot` 规整为 `https://api.deepseek.com/`
→ `结算` Runtime 收到规整后的 BaseUrl，拉模型请求 `https://api.deepseek.com/models`
→ `可观察结果` 「拉取可用模型」不再因为多拼一层路径而失败；日志里的 provider 请求不再指向 `…/chat/completions/models`。

### 验收标准

1. 下列输入全部规整为同一目标根：`https://api.deepseek.com`、`https://api.deepseek.com/`、
   `https://api.deepseek.com/chat/completions`、`https://api.deepseek.com/v1/chat/completions`
   （→ `…/v1`）、`https://api.deepseek.com/models`、大小写混合与尾斜杠变体。
2. 非法输入仍被拒且文案不变：非绝对地址、非 http(s)、含 userinfo、含 query、含 fragment。
3. MCM 中英提示词说明两种写法都可用。
4. 离线回归全绿：新增 smoke 用例、主 smoke、`--persona-anchor`。
5. 不动 Runtime 契约、不动存档格式、不提版本号、不新增 MCM 设置项。

## 非目标

- 不修 API Key / 凭据问题（`credentials` 目录缺失另案实机确认）。
- 不自动补 `/v1`、不猜 provider 家族、不加 provider 预设模板。
- 不改 Runtime 的 base_url 校验策略；不把错误形状在 Runtime 侧静默吞掉。

## 已知取舍（审查记录）

- `/models` 与 `/completions` 也在剥离清单：若某个自建网关的 API 根**真的以这两段结尾**，会被改写。
  判定为可接受（极罕见），代价是这种情况会多一层 404；但改写不再静默 —— `AwakeLog` 会留下
  `provider_base_url_normalized from=… to=…`，可直接看出发生了什么。
- `AWAKE.Tests` 的 `AssertProviderBaseUrlEndpoint` 复刻了 Runtime `BuildEndpointUri`（`F/MarcusAwakeProvider/src/ProviderContracts.cs:325`）的相对解析规则，**是显式的契约镜像**：`AWAKE.Tests` 目标为 `net472`，无法引用 `net8.0` 的 Provider 程序集。若 Runtime 改拼接规则，该断言不会自动失败 —— 记在此处作为已知漂移风险。

## 待用户签收

- [ ] 用户签收本批次（审查状态机 round 1 → 修订 → round 2）。
