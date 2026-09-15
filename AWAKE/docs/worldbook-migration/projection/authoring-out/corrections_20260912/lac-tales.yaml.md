# corrections_20260912 · lac-tales.yaml

> IMPL-GEO1-PERMISSION-20260912 §4（授权落上限+锚点迁移+aliases），原值逐字保留如下。

## entity_ids（原值）
```
entity_ids: [entity.lore.lakonis_lake]
```
处置: 整行删除。依据: 4.3 湖泊概念无聚落可挂

## grants（原值，逐字）
```yaml
# x1 grants: [{profile_id: profile.villager, scope: local, min_detail: summary}]
```
处置依据: 见各条对应 IMPL §4.2 villager行：rumor层表达 min_detail 落 rumor（不变式）

## aliases（新增）

zh-CN: 拉科尼斯湖, 血染湖水（锚点缺失/概念档靠 aliases 进 K1 检索，IMPL §4.4）

