# corrections_20260912 · dawn-mtn.yaml

> IMPL-GEO1-PERMISSION-20260912 §4（授权落上限+锚点迁移+aliases），原值逐字保留如下。

## entity_ids（原值）
```
entity_ids: [entity.lore.dawn_mountains]
```
处置: 整行删除。依据: 4.3 山脉概念无聚落可挂，去掉entity_ids+补aliases

## grants（原值，逐字）
```yaml
# x3 grants: [{profile_id: profile.commoner, scope: regional, min_detail: summary}]
# x3 grants: [{profile_id: profile.commoner, scope: regional, min_detail: summary}]
# x3 grants: [{profile_id: profile.commoner, scope: regional, min_detail: summary}]
```
处置依据: 见各条对应 IMPL §4.2 白描类→layer降rumor+T1全员OR

## aliases（新增）

zh-CN: 黎明山脉, 黎明山（锚点缺失/概念档靠 aliases 进 K1 检索，IMPL §4.4）


## layer 同步（补丁）

以下表达 layer: summary → rumor（IMPL §4.2 白描类明文：layer 降 rumor + T1；初次替换漏了 layer，不变式自检抓出，当场落改）：

- dawn-mtn-1-summary
- dawn-mtn-2-summary
- dawn-mtn-3-summary
