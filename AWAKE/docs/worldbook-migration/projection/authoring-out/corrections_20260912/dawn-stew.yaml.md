# corrections_20260912 · dawn-stew.yaml

> IMPL-GEO1-PERMISSION-20260912 §4（授权落上限+锚点迁移+aliases），原值逐字保留如下。

## entity_ids（原值）
```
entity_ids: [entity.lore.dawn_mountains, entity.lore.khergit_wardens]
```
处置: 整行删除。依据: 4.3 山脉与看守人概念均无聚落可挂；khergit_wardens概念不硬挂

## grants（原值，逐字）
```yaml
# x1 grants: [{profile_id: profile.commoner, scope: regional, min_detail: summary}]
```
处置依据: 见各条对应 IMPL §4.2 叙事类→grant改挂T2（commoner读summary=越限泄漏）

## 文本修正（quote 字段不动）

- 原: `summary: {zh-CN: "合儿必特部西征后进入山地并被解释为融入守护的沿革；当前实际控制未决。"}`
- 改: `summary: {zh-CN: "库吉特西征后进入山地并被解释为融入守护的沿革；当前实际控制未决。"}`
- 原: `text: {zh-CN: "合儿必特部进入山地被来源解释为通过通婚、共俗和守护者身份融入当地。"}`
- 改: `text: {zh-CN: "库吉特进入山地被来源解释为通过通婚、共俗和守护者身份融入当地。"}`
- 原: `库赛特西征之后，山地便归了合儿必特部治下。合儿必特人不强改当地风俗`
- 改: `库赛特西征之后，山地便归了库吉特人治下。库吉特人不强改当地风俗`

依据: 4.4 译名三审定案：编年史转写「合儿必特」废弃（合儿必特=Harfit=另一封臣家族），Khergit官方CN=库吉特；quote字段保持B源原字不动

## aliases（新增）

zh-CN: 黎明山脉, 库吉特, 看守人（锚点缺失/概念档靠 aliases 进 K1 检索，IMPL §4.4）

