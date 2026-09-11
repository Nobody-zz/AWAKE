# Worldbook Studio MVP Profile Registry

机器注册表是唯一权威：`profile-registry.v1.json` 与 `referral-registry.v1.json`。

- `registry_version`: `1.0.0`
- `referral_registry_version`: `1.0.0`
- 两个机器文件的 SHA-256 必须写入候选 manifest、mapping report 和 preview fixture。
- 未知 profile/referral、继承成环、版本不匹配或 hash 失配阻止编译。
- 本 Markdown 只提供中文说明，不承担机器解析职责。

| ID | 中文显示 | 继承 | 说明 |
|---|---|---|---|
| `profile.villager` | 普通村民 | `profile.commoner` | 本地生计、传闻和传统层 |
| `profile.townsfolk` | 城镇居民 | `profile.commoner` | 城镇公共消息与市场层 |
| `profile.commoner` | 普通平民 | 无 | 平民默认上限 |
| `profile.headman` | 头人 | `profile.notable` | 本地行政和本国公共信息 |
| `profile.notable` | 地方重要人物 | `profile.commoner` | 地方资源、治安和关系 |
| `profile.merchant` | 商人 | `profile.townsfolk` | 商品、商路、债务和跨地消息 |
| `profile.tavernkeeper` | 酒馆老板 | `profile.townsfolk` | 多来源传闻，可信度混合 |
| `profile.ransom_broker` | 赎金经纪人 | `profile.merchant` | 俘虏、赎金和战争后果 |
| `profile.soldier` | 士兵 | `profile.commoner` | 本国战争和军务常识 |
| `profile.noble` | 贵族 | `profile.notable` | 贵族政治、家族和领地信息 |
| `profile.noble_high_steward` | 高管理贵族 | `profile.noble` | 政治/经济细节增强，不突破秘密来源条件 |
| `profile.anonymous` | 身份未知 | 无 | 默认最小权限 |