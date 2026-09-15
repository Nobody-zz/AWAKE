# corrections_20260912 · kach-tales.yaml

> IMPL-GEO1-PERMISSION-20260912 §4（授权落上限+锚点迁移+aliases），原值逐字保留如下。

## entity_ids（原值）
```
entity_ids: [entity.lore.kachar_peninsula]
```
处置: 改为 `[entity.settlement.town_s1]`。依据: 4.3 半岛簇挂半岛上的城镇

## grants（原值，逐字）
```yaml
# x4 grants: [{profile_id: profile.townsfolk, scope: regional, min_detail: detail}]
```
处置依据: 见各条对应 IMPL §4.2 townsfolk行：detail>市民上限summary，内行细节改挂notable

## aliases（新增）

zh-CN: 卡恰尔半岛, 卡恰尔旧闻（锚点缺失/概念档靠 aliases 进 K1 检索，IMPL §4.4）

