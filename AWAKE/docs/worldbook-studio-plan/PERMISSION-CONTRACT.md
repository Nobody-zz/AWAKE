# Worldbook Studio MVP 权限组合契约

## 匹配算法

一条 `KnowledgeGrant` 或 `KnowledgeDeny` 规则匹配，当且仅当：

1. `profile_id` 等于当前 profile，或当前 profile 继承链包含该 profile；
2. 同一规则内的 `culture_ids`、`kingdom_ids`、`settlement_ids` 各自是 OR；
3. 不同维度之间是 AND；空数组表示该维度不限制；
4. `min_age`、`min_steward` 是额外的 AND 条件；缺失表示不限制；
5. `scope` 按 `local < regional < national < faction < elite < private` 排序，当前知识范围达到规则要求才满足；
6. `min_detail` 是允许读取的最低表达层，表达层不足时降级到更低层或进入 fallback，不得升级细节。

## 多规则优先级

```text
任意匹配 Deny
    > 任意匹配 Grant
    > Profile 继承默认值
    > 无匹配 = unknown
```

同一层多个 Grant 采用 OR；同一层多个 Deny 采用 OR；Grant 与 Deny 同时匹配时 Deny 胜出。空 grants 和空 denies 都表示没有显式授权/禁止，不表示全体允许。

## 预览隔离

- `npc_preview` 只返回 NPC 有权看到的文本、传闻、未知和推荐对象。
- `author_diagnostics` 才能返回拒绝原因、命中的规则 ID、来源 ID、权限计算过程和冲突信息。
- NPC 预览不得包含被拒绝正文、隐藏来源摘录或内部规则全文。

## Golden cases

- 普通村民匹配 Grant，但同时匹配贵族秘密 Deny：拒绝。
- 高 Steward 贵族没有 `scope=elite` Grant：不能读取贵族秘密。
- 酒馆老板只有 `layer=rumor` Grant：不得返回 `detail`。
- 文化匹配但国家不匹配：两个维度同时存在时必须同时满足。
- 空 grants/denies：返回 unknown，不得默认 public。
- 未知 profile：编译阻止；预览使用 `profile.anonymous` 仅用于诊断 fixture。

机器 DTO 结构由 `npc-preview.v1.schema.json` 与 `author-diagnostics.v1.schema.json` 固定。`npc_preview` 的 `additionalProperties=false` 结构不得出现 rule_ids、source_ids、explanation_chain 或隐藏正文；F08 必须对这些字段做反向断言。
Preview envelope 语义校验必须要求 envelope.fixture_id/profile_id 与两个嵌套 DTO 的同名字段一致，并要求 `author_diagnostics.identity_snapshot.profile_id` 同时等于 envelope/profile DTO 的 profile_id；registry version/hash 来自同一编译输入，任何不一致阻止 fixture。该跨对象相等性由 envelope semantic validator 强制，JSON Schema 负责各自字段白名单。