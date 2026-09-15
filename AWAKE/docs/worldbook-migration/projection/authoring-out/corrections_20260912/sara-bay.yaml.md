# corrections_20260912 · sara-bay.yaml

> IMPL-GEO1-PERMISSION-20260912 §4（授权落上限+锚点迁移+aliases），原值逐字保留如下。

## entity_ids（原值）
```
entity_ids: [entity.lore.shalas_bay]
```
处置: 改为 `[entity.settlement.town_v7]`。依据: 4.3 湾与沙拉斯港本就一体（官方文：沙拉斯=海务贸易港+周边海湾）

## grants（原值，逐字）
```yaml
# x4 grants: [{profile_id: profile.commoner, scope: regional, min_detail: summary}]
```
处置依据: 见各条对应 IMPL §4.2 白描类→layer降rumor+T1全员OR

## aliases（新增）

zh-CN: 沙拉斯湾, 加隆托海峡, 沙拉斯港（锚点缺失/概念档靠 aliases 进 K1 检索，IMPL §4.4）


## layer 同步（补丁）

以下表达 layer: summary → rumor（IMPL §4.2 白描类明文：layer 降 rumor + T1；初次替换漏了 layer，不变式自检抓出，当场落改）：

- sara-bay-1-summary
- sara-bay-2-summary
- sara-bay-3-summary
- sara-bay-4-summary
