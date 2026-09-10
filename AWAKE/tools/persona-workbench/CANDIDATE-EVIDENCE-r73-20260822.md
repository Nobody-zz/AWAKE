# PersonaWorkbench r73 候选证据

## 范围

- 目标方案：`PLAN-AI-GENERATION-20260821.md`、`PLAN-SEMANTIC-DENSE-DSL-20260821.md`。
- 本轮实现：canonical DSL 严格解析/回渲染语义校验；保护字段按解码后的语义值验证；同一 section 内不同语义字段的重复 `DATA_CN` 不再误判为 DSL 重复；新增跨字段重复证据回归。
- 未调用云端 Provider，未启动 Bannerlord，未修改全局模型配置、游戏文件或历史候选包。

## 源码回归

- `PersonaWorkbench.Core.Tests`：PASS。
- `PersonaWorkbench.Web.Tests`：PASS。
- `PersonaWorkbench.BrowserSmoke`：PASS。
- Core 新增回归：同一 section 中 `PublicDescription` 与 `SelfClaimRules` 使用相同证据时，canonical DSL 保留两条 `DATA_CN`，同时通过 round-trip 语义校验。
- golden DSL：保持原文本输出不变。

## 固定样本与本地模型

| 项目 | 值 |
|---|---|
| Ollama endpoint | `http://127.0.0.1:11434/v1/chat/completions` |
| 模型 | `gpt-oss:20b` |
| 模型 digest | `17052f91a42e97930aa6e28a6c6c06a983e6a58dbb00434885a0cf5313e376f7` |
| Worker thinking | `low` |
| short | 270 UTF-8 bytes；SHA-256 `3b82f664f26f11862c7da899668aaa0635461f5cc3fda711a8d7622cba5eb968` |
| long | 3888 UTF-8 bytes；SHA-256 `8234670f470a8f663d5436111b39cf10d97ae9aa1d123e6ff7c090561f93b752` |

## Worker-low 诊断

Worker 只用于模型/提示词诊断，不等同于 Workbench 路由验收；四步串行、无格式重试。

| 阶段 | 输入来源 | 输出格式 | 结果 | 耗时 | 输出 bytes | format retry |
|---|---|---|---|---:|---:|---|
| `expand-short` | fixture | text | PASS | 50,563 ms | 515 | false |
| `dsl-short` | Worker expansion | json_object_v1 | PASS | 14,671 ms | 486 | false |
| `expand-long` | fixture | text | PASS | 37,813 ms | 1,324 | false |
| `dsl-long` | Worker expansion | json_object_v1 | PASS | 12,685 ms | 207 | false |

Worker 证据：`artifacts/PersonaWorkbench-r73-20260822-worker-evidence.json`。

## Workbench 本地真实路由

r73 服务使用真实 session/CSRF；所有请求串行，未自动重试。

| 阶段 | 输入来源 | HTTP | 结果 | 耗时 | 输出/DSL bytes | draft | Usage total | canonical diagnostic |
|---|---|---:|---|---:|---:|---|---:|---|
| `expand-short` | fixture | 200 | PASS | 13,091 ms | 411 | — | 619 | — |
| `convert-short` | Workbench expansion | 200 | PASS | 56,166 ms | 1,577 | yes | 1,552 | uncompressed |
| `expand-long` | fixture | 200 | PASS | 37,112 ms | 783 | — | 1,878 | — |
| `convert-long` | Workbench expansion | 200 | PASS | 72,430 ms | 1,945 | yes | 1,897 | uncompressed |
| `direct-short` | fixture | 200 | PASS | 28,935 ms | 1,397 | yes | 1,119 | uncompressed |
| `direct-long` | fixture | 200 | PASS | 9,784 ms | 3,899 | yes | 2,147 | `compressed_optional_fields`; `trimmed_core_evidence` |

`direct-long` 的最终 DSL 为 3,899 bytes，低于 4,096-byte canonical 上限；诊断显示仅压缩可分段核心证据，身份、人格轴、反应字段、承诺字段均保留。Workbench 证据：`artifacts/PersonaWorkbench-r73-20260822-evidence.json`。

## 候选包完整性

- 候选包：`artifacts/PersonaWorkbench-FreePreview-r73-20260822`。
- `PersonaWorkbench.Web.exe` SHA-256：`55692319617759753653D3C71EF792F04F5C12D960FED91AF7DCC53D678843D5`。
- `PersonaWorkbench.Launcher.exe` SHA-256：`070F42F40079E1D9641E8534D14B713300DEFD186DF28CFB51D5B19802F49439`。
- `PACKAGE-MANIFEST.sha256.txt` SHA-256：`674B3978F3C2A0F15EEAEFA950C2BD4A2E32847DDB0F78293BD625291C8FA70B`。
- Manifest：21 项，0 个缺失，0 个哈希错误，0 个测试日志/临时 runtime 文件。
- r72 未覆盖；r73 服务停止后 `51337` 已确认释放。

## 结论

- Worker-low 诊断：PASS。
- Workbench optional expansion + conversion：PASS。
- Workbench direct recognition + conversion：PASS。
- 本轮候选总体验收：PASS；这是候选包，不代表正式发行或游戏内验证。
