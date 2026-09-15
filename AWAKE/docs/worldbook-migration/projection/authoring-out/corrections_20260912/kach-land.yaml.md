# corrections_20260912 · kach-land.yaml

> IMPL-GEO1-PERMISSION-20260912 §4（授权落上限+锚点迁移+aliases），原值逐字保留如下。

## entity_ids（原值）
```
entity_ids: [entity.lore.kachar_peninsula]
```
处置: 改为 `[entity.settlement.town_s1]`。依据: 4.3 半岛簇挂半岛上的城镇（官方文：瓦尔切格正在卡恰尔半岛深处）

## grants（原值，逐字）
```yaml
# x3 grants: [{profile_id: profile.commoner, scope: regional, min_detail: summary}]
# x3 grants: [{profile_id: profile.commoner, scope: regional, min_detail: summary}]
# x3 grants: [{profile_id: profile.commoner, scope: regional, min_detail: summary}]
# x1 grants: [{profile_id: profile.townsfolk, scope: regional, min_detail: detail}]
```
处置依据: 见各条对应 IMPL §4.2 townsfolk行：detail>市民上限summary，内行细节改挂notable；§4.2 白描类→layer降rumor+T1全员OR

## aliases（新增）

zh-CN: 卡恰尔半岛, 卡恰尔, 伊卡拉荒原（锚点缺失/概念档靠 aliases 进 K1 检索，IMPL §4.4）


## layer 同步（补丁）

以下表达 layer: summary → rumor（IMPL §4.2 白描类明文：layer 降 rumor + T1；初次替换漏了 layer，不变式自检抓出，当场落改）：

- kach-land-1-summary
- kach-land-2-summary
- kach-land-3-summary
