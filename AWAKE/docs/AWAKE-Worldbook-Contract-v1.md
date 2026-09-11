# AWAKE Worldbook Contract v1

## 目标

Contract v1 是 Studio 与游戏 Runtime 之间的唯一结构边界。它不读取 YAML、来源全文或 Worker 建议；这些只属于开发者编辑侧。

## 对象边界

| 对象 | 用途 | 是否写入存档 |
|---|---|---|
| `registry` | 安装包发现、受信任相对路径、摘要 | 否，运行时安装状态 |
| `package manifest` | 单个世界观/扩展包的身份、依赖、入口、哈希 | 否 |
| `campaign activation` | 当前存档选中的一个主世界观和扩展包 | 是 |
| `runtime snapshot` | 激活结果、索引、知识条目、身份与转介 | 否，由 activation + overlay 重建 |
| `campaign overlay` | 玩家对既有知识的允许范围内修改 | 是，可导出复用 |
| `event record` | 代码产生的世界事实事件 | 由事件系统保存 |
| `weekly report` | 周期模板根据事件记录生成的报告 | 由周报系统保存或重建 |

## 稳定 ID

对象 ID 使用 `<命名空间>:<对象类型>:<本地 ID>`，例如 `calradia:entry:grain_tax`。包 ID 使用 `<命名空间>:<包 ID>`，例如 `calradia:base`。两者不能混用。

## 知识判定

查询顺序固定为：`blocked → identity → conditions → denies → grants → detail → referral → not_found`。

- `known`：当前身份有足够细节的表达。
- `partial`：有内容，但只能返回低于请求细节的表达。
- `referral`：当前身份不知道，但存在允许公开询问的知识面广 NPC。
- `not_found`：激活闭包没有匹配条目。
- `blocked`：战役、内容层级或明确否定规则阻止返回。

身份修正顺序：显式身份、职务、技能、年龄、文化、国家、聚落、基础身份。基础身份只提供起点，不能越过否定规则。

## 哈希

- SHA-256、UTF-8、对象键按序、无多余空白、文本统一 LF。
- 路径统一 `/`，按大小写折叠后排序。
- `manifestHash` 对去掉自引用 `hashes` 字段后的规范化 manifest 计算。
- `contentHash` 对包内允许的 runtime JSON/index 按规范化路径组成规范化对象后计算。
- `packageHash = SHA256(raw_manifest_digest || raw_content_digest)`。
- 固定输入和预期结果见 `tools/worldbook-contract/v1/golden-*.json`。

## Overlay 边界

玩家可以改已有知识的标题、摘要、表达文本、关键词和表达启用状态；不能改世界观、领域、身份授予、否定规则或来源权威。每个操作带 `baseRevision`，不匹配时拒绝写入，避免覆盖基础包升级或其他修改。

## 周报边界

Runtime 只记录结构化事件；唯一周报生成器按周读取事件记录，使用代码模板按政治、经济、文化、战争分栏生成 `weekly-report`。不让 NPC 逐个调用 AI 学习。
