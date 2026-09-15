# corrections_20260912 · lac-lake.yaml

> IMPL-GEO1-PERMISSION-20260912 §4（授权落上限+锚点迁移+aliases），原值逐字保留如下。

## entity_ids（原值）
```
entity_ids: [entity.lore.lakonis_lake]
```
处置: 整行删除。依据: 4.3 湖泊概念，本批无邻近聚落可挂

## grants（原值，逐字）
```yaml
# x5 grants: [{profile_id: profile.commoner, scope: regional, min_detail: summary}]
# x1 grants: [{profile_id: profile.noble_high_steward, scope: elite, min_detail: detail}]
```
处置依据: 见各条对应 IMPL §4.2 noble_high_steward→profile.noble（registry链即含noble；运行时只产profile.noble，链中无noble_high_steward）；§4.2 白描类→layer降rumor+T1全员OR

## aliases（新增）

zh-CN: 拉科尼斯湖, 弥戎河, 喀拉卡兹河（锚点缺失/概念档靠 aliases 进 K1 检索，IMPL §4.4）


## layer 同步（补丁）

以下表达 layer: summary → rumor（IMPL §4.2 白描类明文：layer 降 rumor + T1；初次替换漏了 layer，不变式自检抓出，当场落改）：

- lac-lake-1-summary
- lac-lake-2-summary
- lac-lake-3-summary
- lac-lake-4-summary
- lac-lake-5-summary
