# AWAKE 世界书平台硬化计划草案

状态：APPROVED，已进入短任务实现
日期：2026-08-22
关联目标：WORLDBOOK-PLATFORM-HARDENING-20260822

## Goal

在不修改冻结候选、不启动 Bannerlord、不同步游戏目录的前提下，把当前 AWAKE Worldbook v2 离线骨架推进到权限、包完整性、身份映射、事件唯一性和 referral 行为可验证的可靠候选基础。所有改动必须保持 Studio、Runtime、Contract 现有测试通过，并为每个审查问题增加能直接证明行为的回归用例。

## Working semantics

- `rule.scope` 表示该表达要求的最低知识范围；范围顺序固定为 `local < regional < national < faction < elite < private`。
- 查询上下文必须携带 typed identity snapshot：`profile_id`、`knowledge_scope`、`effective_detail`、能力可用性标记、请求细节、content tier 和 gate 状态；缺失能力与数值为零必须区分。
- `effective_detail` 由显式可信 profile 的基础能力和年龄、管理/相关技能、职务修正计算；未知身份不继承贵族或平民能力，直接按权限不明处理。
- 只有 `effective_scope >= rule.scope`、`effective_detail >= rule.min_detail` 且 `expression.detail <= effective_detail` 时，表达才具备可见资格。
- NPC 对话入口默认请求 `secret`，其他调用方必须显式传入请求细节；高层表达不可见但低层表达可见时返回低层文本并标记 `partial`。
- 内容/战役 gate 在候选筛选前执行；query-wide gate、包完整性失败和权限不明返回 terminal `blocked`，不得输出标题、摘要、正文、referral、source 或规则元数据。
- 对每个候选 entry 先合并评估所有启用表达的 deny；任一匹配 deny 终止该 entry 的 grant 选择。只有所有候选都被阻断且没有其他可见结果时，公共状态才为 `blocked`。
- Referral 只从最终未被 blocked 的可见路径产生，并且目标必须在编译期标记为公开可询问、可解析、非自身且通过目标策略；不会从禁用表达或被 deny 表达产生。
- `partial` 只返回已授权低层表达文本，不携带被拒绝高层的标题、摘要、来源或规则信息；byte budget 截断不能把 `known` 改成 `partial`，必须返回明确的 budget 错误或保持已提交的可见结果。

## Work packages

1. **Permission contract and query model**
   - 为查询上下文补充 typed identity snapshot、`knowledge_scope`、`effective_detail`、capability presence、请求细节等级和 query/entry gate 输入，明确 unknown、unavailable 与 zero 的区别。
   - 把权限结算集中到一个权威 evaluator；每个 entry 先做跨表达 deny phase，再做 grant/detail 选择，query-wide blocked 必须 terminal。
   - 保留公共 `blocked` 状态，同时使用不泄露内容的 machine-safe reason class 区分 campaign/content/deny/unknown/package-integrity。
   - 增加 decision-table fixture：known、partial、referral、not_found、blocked、跨表达 deny、blocked+referral、partial+content gate、byte budget 和 zero-leak。

2. **Studio condition mapping**
   - 固定转换表：`entity.kingdom.vlandia` → `awake:kingdom:vlandia`、`entity.culture.vlandia` → `awake:culture:vlandia`、`entity.settlement.pravend` → `awake:settlement:pravend`、`entity.role.headman` → `awake:role:headman`。
   - 所有输入做小写、分隔符和字段类型归一化；runtime adapter 按字段类型把游戏裸 ID 转成同一 canonical ID。
   - 移除或证明安全的 suffix-only 匹配；别名冲突、未知类型和无法解析 ID 在 Studio 编译期报错，不静默生成。
   - 覆盖四种 entity 条件的 Studio→runtime→query 闭环。

3. **Runtime package integrity**
   - 定义并复用 Contract v1 的 canonical JSON、UTF-8、LF、规范化路径和 hash 输入；`manifestHash` 去除 `hashes` 后对 canonical manifest 做 SHA-256，`contentHash` 必须严格等于 `ContractHashing.ContentHash` 的“规范化路径 → canonical JSON 对象”索引哈希，`packageHash` 必须是两个 32-byte digest 的原始字节拼接后 SHA-256。
   - Studio 与 Runtime 共享同一组命名算法/fixture vectors；Runtime 不得改用 raw serialized file bytes 作为 contentHash，也不得只比较 manifest 中的声明字符串。
   - 在 activation 构造 live service 前一次性读取不可变 manifest/runtime/index bytes，先验证 registry→manifest、manifest→entrypoints、packageId/version/worldId、schema 和三种 hash，再解析同一份已验证 bytes。
   - 缺包、损坏、哈希不符、schema 不兼容、index mismatch 或 TOCTOU 变化时保持 knowledge unavailable，不能创建 live query service。
   - 增加 tampered runtime、tampered index、registry/manifest mismatch、canonical reorder、wrong package hash 和 exact-bytes verification 回归。

4. **Referral and event correctness**
   - 收紧 referral 生成策略，修正禁用表达、被 deny 表达、blocked 查询和不可公开目标的泄漏；编译期拒绝自引用、重复目标、未解析目标和循环 referral 图。
   - 事件输入必须携带稳定 `source_id` 与 `source_sequence`/`event_key`；所有生产者、存储、reload 和 replay 都保留同一唯一键，内存和存储边界都执行唯一约束。
   - 对“同文本合法两次发生”和“同 event_key 重试”分别定义语义；增加并发重复提交、reload/replay、容量保留、顺序和周报来源闭包回归。

5. **NPC identity mapping**
   - 将 Hero 对象类型与社会身份分离，不再把 `hero/lord/clan_leader` 直接当成贵族。
   - 固定并遵守 Contract v1 现有 precedence：`trusted explicit identity → office/role → skill → age → culture → kingdom → settlement → base_identity`；选出主身份后再展开其 inheritance，inheritance 不改变轴优先级；每项记录 provenance/confidence，并增加冲突 fixture。
   - 无法确定社会身份时使用 `unknown`，不回退为 `commoner` 或 `noble`；未知身份对受保护知识 fail closed。
   - 明确贵族、伙伴、漫游者、士兵、头人、商人入口，并为所有可声明技能建立 capability presence + numeric value 映射。
   - 将 Bannerlord 的裸 kingdom/culture/settlement/role ID 转成 canonical runtime ID 的窄版 `BannerlordWorldbookIdentityAdapter` 纳入本批范围；不扩展为其他大型模组 Adapter。

6. **Verification and candidate preparation**
   - 依次运行针对性 regression、Studio F01-F25、Runtime smoke、Contract JSON parse 和 AWAKE Release build，并记录命令、exit code、fixture ID/hash、artifact path 和 BuildId。
   - 在离线批次前后核对冻结候选 DLL/manifest hash 完全一致；新候选单独记录 Contract/schema version、canonical hash inputs、package hash、测试证据和文件清单。
   - 更新硬化 checkpoint 与 `AWAKE-CURRENT.md`，记录文件、证据等级、哈希、测试来源和剩余限制。
   - 只有所有离线门禁通过后，才创建独立的新 v2 candidate BuildId；不覆盖冻结候选。

## Verification requirements

- 每个 work package 至少有一个能在失败实现上失败、在修复后通过的回归；所有负向测试都检查状态、空输出和零泄漏。
- 权限矩阵必须覆盖：平民/贵族、范围不足、细节不足、partial、blocked、跨表达 deny、deny+grant、blocked+referral、content gate 和 unknown capability。
- 完整性测试必须篡改 runtime/index/registry/manifest 并确认加载边界拒绝，且验证哈希针对随后解析的同一 immutable bytes。
- 事件测试必须区分合法重复事件与幂等重试，并覆盖并发、reload、replay、容量和顺序。
- Contract/runtime parity 测试必须证明每个编译字段（scope、min_detail、conditions、referrals、index）实际到达 evaluator 或被明确拒绝。
- Hash parity 测试必须使用 Studio/Runtime 共同的 canonical path→JSON index fixture vectors，并证明 manifest/content/package 三种 digest 完全一致。
- 所有测试只使用离线 fixture，不启动游戏，不读取或覆盖游戏目录；checkpoint 必须记录 exit code 和 artifact provenance。

## Out of scope

- 不修改 `awake-20260820-syncpack-001`。
- 不同步 `D:\\SteamLibrary\\steamapps\\common\\Mount & Blade II Bannerlord`。
- 不启动 Bannerlord，不做 E4/E5 游戏验证。
- 不重写旧四版世界书正文。
- 不引入 NPC 逐个 AI 学习循环。
- 不扩展新的 UI、世界观内容包或大型模组 Adapter。

## Locked decisions for next review

- `effective_detail` 由显式可信 profile 的基础能力和年龄、管理/相关技能、职务修正计算；identity registry 不直接把未知对象提升为贵族。
- NPC 对话默认请求 `secret`，其他调用方必须显式传入请求细节；低层可见、高层不可见时返回 `partial`。
- Hero 社会身份无法确定时统一为 `unknown`，对受保护知识 fail closed，不回退为 `commoner`。
- content gate 在候选筛选前按 query-wide / entry-level 两层结算；query-wide blocked 不输出任何内容，entry-level deny 只阻断该 entry。
- 相同文本事件只有在 `event_key` 不同且来源序号不同才允许作为两次合法事件；相同 `event_key` 必须幂等丢弃或返回已应用结果。
