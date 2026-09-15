# corrections_20260912 · kach-own.yaml

> IMPL-GEO1-PERMISSION-20260912 §4（授权落上限+锚点迁移+aliases），原值逐字保留如下。

## entity_ids（原值）
```
entity_ids: [entity.lore.kachar_peninsula]
```
处置: 改为 `[entity.settlement.town_s1]`。依据: 4.3 半岛簇挂半岛上的城镇

## grants（原值，逐字）
```yaml
# x2 grants: [{profile_id: profile.commoner, scope: regional, min_detail: summary}]
```
处置依据: 见各条对应 IMPL §4.2 叙事类→grant改挂T2（commoner读summary=越限泄漏）

## aliases（新增）

zh-CN: 卡恰尔半岛, 卡恰尔归属（锚点缺失/概念档靠 aliases 进 K1 检索，IMPL §4.4）

