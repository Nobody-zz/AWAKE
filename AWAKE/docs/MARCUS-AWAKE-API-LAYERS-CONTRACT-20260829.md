# Marcus-AWAKE 分层 API E1 证据契约

## 目的

本契约固定 `tools/verify_marcus_awake_api_layers.ps1` 的离线证据输出结构。机器校验使用 `docs/evidence/schemas/MARCUS-AWAKE-API-LAYERS-E1.schema.json`；本文件补充 schema 不适合表达的字段关系、分层归类顺序和证据边界。

E1 只代表离线静态证据：构建、程序集反射 API 面、基线对照、源码/程序集禁用项扫描和既有 JSON schema 解析。E1 不代表游戏启动、游戏内入口闭环、游戏目录同步、真实 Provider、存读档或长时回归。

## 输出范围

审计产物固定为 `docs/evidence/MARCUS-AWAKE-API-LAYERS-E1-20260829.json`，顶层字段如下：

| 字段 | 规则 |
|---|---|
| `schema_version` | 固定为 `marcus-awake/api-layers-evidence/v1`。 |
| `evidence_level` | 固定为 `E1`。 |
| `batch_id` | 当前脚本固定为 `MARCUS-AWAKE-API-LAYERS-20260829`。 |
| `commands` | 记录离线构建命令、退出码和输出；成功证据要求退出码为 `0`。 |
| `legacy_baseline` | 记录 2026-08-26 legacy API 基线路径、类型数、缺失类型和是否完整保留。 |
| `current_baseline` | 记录 2026-08-29 current API 基线路径、当前类型数、新增类型数和反射结果是否完全匹配。 |
| `assembly` | 记录被审计程序集的相对路径、SHA-256 和程序集版本；构建/程序集缺失时允许使用空哈希或空版本表达失败证据。 |
| `phase_counts` | 记录各 API 分层的非负类型数；键只允许 `P3A-approved`、`P3B-approved`、`P3C-approved`、`P3D-approved`、`P5-approved`、`unclassified`。 |
| `api_phase_records` | 为当前程序集每个导出类型记录类型名、源文件、分层和是否存在于 legacy 基线。 |
| `unclassified_types` | 必须等于 `api_phase_records` 中 `phase=unclassified` 的类型名集合；通过时为空数组。 |
| `parse_results` | 记录脚本当前解析的 `MARCUS-AWAKE-P3A-E1.schema.json` 和 `MARCUS-AWAKE-P3A-E2.schema.json`，以及解析错误。 |
| `forbidden_scan` | 记录源码命中、允许的架构例外、未允许源码命中、程序集引用命中和公共 API 类型命中。 |
| `pass` | 只有审计脚本的全部静态门通过时才为 `true`。 |

## API 分层

`api_phase_records` 使用 `Get-ApiPhase` 的首个匹配规则；规则顺序是契约的一部分：

1. legacy 基线中已有的类型归为 `P3A-approved`。
2. `RuntimeServiceClient`、`RuntimeServiceClientOptions` 归为 `P3B-approved`。
3. `ProviderRuntimeApi.cs` 中的类型，以及 `AiTaskHandle`、`ProviderErrorMapping`，归为 `P3D-approved`。
4. `Compat/Embedding`、`Compat/Rerank`、`Compat/Sql`、`Compat/Timeline` 路径中的类型，以及 `ProviderTaskRequest`，归为 `P3C-approved`。
5. 其余 `Compat/`、`FullApiTypes.cs`、`ApiCompatibilityTypes.cs`、`ApiCompatibilityExtensions.cs` 类型归为 `P5-approved`。
6. 没有命中上述规则的类型归为 `unclassified`，不得在契约外静默归类。

`phase_counts` 应与 `api_phase_records` 按 `phase` 聚合后的结果一致；`api_phase_records` 应与程序集导出类型一一对应。schema 约束允许的键和值类型，跨字段的数量闭包由离线审计复核。

## 禁用项扫描

`forbidden_scan.passed` 的判定是：`unexpected_source_findings`、`assembly_findings` 和 `public_api_findings` 三个数组均为空。`source_findings` 可以包含已在 `allowed_architecture_findings` 中逐项说明的架构例外；例外不等于删除原始命中，也不等于放宽公共 API 禁止项。

`allowed_architecture_findings` 必须保留脚本声明的七项固定例外及其理由；schema 对文件、规则和理由做精确约束。

扫描规则名称固定为：`http_client`、`sockets`、`named_pipe`、`sqlite`、`process`、`file_io`、`environment_secret`、`blocking_wait`、`wall_clock`、`bannerlord_reference`。

## 通过条件

脚本的 `pass=true` 必须同时满足：

- 构建退出码为 `0`，程序集存在；
- legacy 基线非空、没有缺失类型；
- current 基线与当前反射 API 面完全匹配；
- 没有 `unclassified_types`；
- 脚本列出的既有 schema 全部解析成功；
- `forbidden_scan.passed=true`。

schema 负责机器可解析性、字段闭合、枚举、基本数值边界和成功结果的局部一致性；它不替代脚本的反射、源码扫描或基线比较逻辑。

## 明确不代表

本证据不声明以下事项：

- Bannerlord 已启动或游戏内功能已进入可达入口；
- AWAKE 模组目录、测试包或发布归档已同步；
- 当前 DLL 已通过 E2 离线 smoke、E3 同步哈希、E4 真机入口或 E5 存读档/长时回归；
- 真实 Provider、网络、API key、Runtime Service 或游戏存档链路已验证。

本批只新增证据 schema 与文档，不修改审计脚本、源码、世界书候选、游戏目录、版本号或同步目录。
