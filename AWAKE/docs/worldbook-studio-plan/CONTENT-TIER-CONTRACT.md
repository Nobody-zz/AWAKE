# Worldbook Studio MVP 内容层导出契约

## 参数分离

`content_tier` 是内容层，`profile_id` 是 NPC 知识画像，二者不得共用 CLI 参数。导出使用 `--content-tier base|adult_optional`；省略时等价于 `base`，`adult_optional` 必须显式确认。预览另行使用 `--profile`。

## 图模型

编译器生成 `reports/content-graph.json`，包含 `nodes` 和 `edges`。节点 kind 固定为 `document`、`assertion`、`expression`、`source`、`referral`、`alias`、`redirect`、`profile_registry`、`referral_registry`、`id_ledger`、`index`、`cache`、`report`；每个节点有 `node_id`、`node_kind`、`content_tier` 和 `origin_pointer`；边类型固定为 `contains`、`references_source`、`fallback`、`alias`、`redirect`、`index_entry`、`cache_entry`、`report_reference`。document 显式声明 tier，assertion/expression 从所属 document 继承且不得覆盖；source 从来源登记继承；referral、alias、redirect、registry、id_ledger、索引、缓存和报告必须带来源节点或显式 `base`/`unknown` 推导结果；不能推导的节点为 `unknown` 并阻止 clean 导出。

## 闭包规则

纯净 `base` 导出从所有导出档案开始遍历上述完整图。闭包中只要出现 `adult_optional` 或 `unknown` 节点，导出阻止并输出阻断边。成人层不得通过 fallback、redirect、缓存或索引间接进入纯净包；未知年龄或身份信息按不满足成人门槛处理。
机器结构由 `content-graph.v1.schema.json` 固定；纯净导出根为显式选定的 document 节点，graph closure 通过边端点逐条遍历，未列出的边类型不得进入编译。