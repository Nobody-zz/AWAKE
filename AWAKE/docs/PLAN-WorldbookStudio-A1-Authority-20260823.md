# Plan: Worldbook Studio A1 单一权威整理
_Locked via grill — by Claude + user_

## Goal
在不改变世界书 Schema、Web 路由、CLI 命令、AI Provider 请求/结果契约和运行候选的前提下，消除 Worldbook Studio 第一批已确认的重复权威：先冻结现有哈希与入口输出，再统一 JSON 规范化实现，并只抽取经边界测试证明完全等价的无状态输入辅助逻辑。第一批只做行为保持型重构，不拆 `WorldbookApplicationService`，不引入新框架。

## Approach
1. 先建立现有行为基线：记录所有 `CanonicalJson`/`ContractHashing`/`Hashing` 调用点，生成并提交不可随意重生成的版本化 golden artifact，固定 manifest/content/package、对象、ledger、Unicode、null、数组、路径和数值边界的 vectors，并保存真实 CLI 与 Web Suggestion 输出快照。
2. 以 `CanonicalJson` 作为唯一 JSON 规范化实现；让 `ContractHashing` 继续提供哈希 API，但委托给 `CanonicalJson`，只有在差异测试全部通过后才删除重复实现。
3. 只抽取经测试证明等价的 `ProviderId` 规范化和 Revision 读取；CLI/Web 的 Suggestion DTO 保留各自 wire adapter，不能因为内部对象相同而合并不同的字段命名（例如 camelCase 与 snake_case）。
4. 暂不抽取 Token/Nonce 生成。CLI Consent、Web Session、CAS 的安全和生命周期边界保持原样，Token 合并另立安全专项。
5. 为每个抽取边界补充聚焦回归测试，覆盖 null、错误类型、零/负数、溢出、字段大小写、缺失字段、Unicode 和错误码；通过真实 CLI 进程和 Web HTTP 入口分别验证 provider/revision 错误状态、错误码、消息和 JSON 命名；CLI/Web 的 Suggestion 输出分别做 byte-for-byte golden comparison。
6. 如果 canonicalization 的数值、Unicode、null、数组、路径碰撞或 hashes 排除向量出现任何变化，立即停止 A1，不删除旧实现；变化只能另立语义兼容修复批次处理。
7. 运行 `F01-F70`、CLI subprocess/golden smoke、Web HTTP contract smoke、Release build、合同 JSON/Schema 解析、Package/Launcher smoke，并重新运行代码债务审计，确认生产重复项减少且没有新增复杂度风险。

## Key decisions & tradeoffs
- 选择“Facade/入口不动，Core 只共享已证明等价的纯函数”而不是一次性拆分应用服务，优先降低兼容风险。
- 选择 `CanonicalJson` 为规范化唯一权威，因为现有内容哈希和运行时契约都依赖相同的 JSON 字段排序语义。
- 不在 A1 合并不同生命周期的 Token/Nonce 管理；避免把安全边界误判成重复代码。
- CLI/Web 的 Suggestion DTO 不共用 wire 对象；只允许共用内部语义模型或映射测试，保留各自的字段命名和序列化契约。
- 哈希重构采用“基线向量 → 委托实现 → 差异验证 → 删除重复”的顺序，任何向量变化都停止删除步骤。
- golden artifact 必须纳入版本控制/发布源文件并被测试读取，不能在测试运行前由当前实现自动重生成。
- 不引入 DI、Repository、Mediator 或新测试框架；A1 的目标是减少重复，不增加抽象层。
- 所有稳定路由、命令、Schema 字段、Provider wire 字段和错误码视为兼容契约。

## Risks / open questions
- `ContractHashing.Canonicalize` 与 `CanonicalJson.Canonicalize` 当前静态文本相同，但数值格式、Unicode、null、数组顺序和自引用 hashes 规则仍需 golden vectors 证明。
- CLI/Web 的 DTO 映射存在字段命名差异；本轮不合并 wire adapter，只验证各自输出没有漂移。
- `ProviderId` 与 Revision 辅助函数只有在边界测试证明行为一致后才抽取；否则保留两份并记录为有意边界。
- CLI 与 Web 的行为证据必须来自真实入口，而不是只测试共享 Core 函数；CLI 使用真实进程，Web 使用实际 HTTP 路由。
- 本轮不证明真实云端模型质量或本机 Worker 版本兼容；这些属于已有外部验证边界。

## Out of scope
- 不拆 `WorldbookApplicationService`、`AssistanceProviders`、Web 路由文件或测试总文件。
- 不改变 `awake.worldbook.*`、`assistance.*`、runtime package、manifest、CLI 参数、HTTP 路由和错误码。
- 不合并 CLI/Web Suggestion wire DTO，不抽取 Token/Nonce 生成，不改变会话/同意/CAS 状态管理。
- 不修改 Bannerlord、`Modules\\AWAKE`、`PlayerExports`、dist、冻结候选或游戏目录。
- 不做无基准支持的性能优化，不删除仅凭静态搜索判定的代码，不顺手整理无关历史债务。
