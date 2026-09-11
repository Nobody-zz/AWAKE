# Plan: Persona Workbench Reaction and Commitment Profiles
_Locked via grill — by user + Codex, 2026-08-18_

## Goal

将 Persona Workbench 的第 4 组“受刺激时”和第 5 组“底线与承诺”从不可扩展的具体事件复选框，改为可选的跨情境反应轴、承诺轴和结构化自由文本。新模型必须能表达“未设定”和“平衡”的区别，保留旧 `trigger` / `boundary` 标签兼容，不枚举无限具体事件，并稳定进入 Persona DSL、JSON 保存/读取和本机预览。

## Approach

1. 在 `persona-workbench.character.v1` 增加可选 `reactionProfile` 与 `commitmentProfile`；保持旧文件可读，新增字段缺省时不输出任何新 DSL。
2. `reactionProfile` 使用五条可空双极轴，取值 `-2..2`：迎击↔回避、外露↔压抑、即时↔延迟、缓和↔记恨、独自控制↔寻求依靠。`null` 表示未设定，`0` 表示刻意平衡。
3. `reactionProfile` 增加 `sensitiveConditions` 与 `conditionalResponses` 两个自由文本字段；具体人物敏感点和条件反应只写这里，不进入基础标签注册表。
4. `commitmentProfile` 使用三条可空双极轴，取值 `-2..2`：轻易承诺↔谨慎承诺、灵活履行↔坚持履行、可交换↔不可交换。
5. `commitmentProfile` 增加 `priorityOrder`、`protectedValues`、`applicableScope`、`exceptionCost`、`breachResponse`；这些字段表达价值排序、保护对象、适用范围、破例代价和突破后的反应。
6. DSL 固定顺序为 core → identity → trait → expression → behavior → reaction → commitment → legacy trigger → legacy boundary；轴和值使用稳定中文映射，相同输入必须产生相同输出。
7. 编辑器第 4、5 组改为通用轴选择器；结构化文本放入可展开的高级区域。旧 trigger/boundary 复选框移入“旧格式兼容”，加载与保存时不得丢失。
8. 增加模型范围、null/zero 区分、JSON 往返、DSL 顺序、旧标签兼容、Web 请求映射、页面控件和保存/读取静态门禁；完成 Release 构建、实际测试程序、候选包 HTTP 200 与正式包精确停止。

## Key decisions & tradeoffs

- 不再尝试列举背叛、抛弃、善意、家族、誓言等具体事件；它们属于角色内容，不是基础 taxonomy。
- 双极轴使用 `-2..2` 而不是 1..4 强度，因为两端都是有效倾向；`null` 与 `0` 必须分开。
- 本批使用结构化字段而不是动态无限规则行，优先保证新手可理解、JSON 稳定和实现规模可控；后续可在不破坏字段语义的前提下增加重复规则对象。
- 保持 schema ID 为 `persona-workbench.character.v1`，新增字段全部可选，当前 Free Preview 尚未宣告该 schema 冻结；严格 reader 同步增加字段白名单。
- 旧 `trigger` / `boundary` 标签继续解析和输出，但不再作为主要编辑方式，也不扩充具体事件标签。

## Risks / open questions

- 双极轴标签必须避免让用户误以为某一端永远更好；UI 需要同时显示两端含义。
- 自由文本可能重复 core/behavior 内容；本批只通过分区说明降低重复，不做语义去重。
- 旧版 Workbench 无法读取带新字段的新文件；Free Preview 发布包将整体替换，并保留回退包。
- 尚未验证真人浏览器视觉布局，静态与 HTTP 门禁不能替代视觉验收。

## Out of scope

- 不实现无限可添加的条件规则列表、规则优先级或冲突求解。
- 不实现浪漫、关系、情绪状态轴；它们继续作为独立后续模型。
- 不调用 Provider，不修改 AI 请求格式，不启动 Bannerlord，不同步游戏目录。
- 不扩充正式 Persona 标签 taxonomy，不增加具体事件 trigger/boundary 标签。

## 2026-08-18 Addendum: unify groups 1-3

用户进一步确认第 1～3 组也采用与第 4、5 组一致的双极程度轴，不再以多个单向标签强度作为主要编辑方式。新增可选 `traitProfile`、`expressionProfile`、`behaviorProfile`，全部使用可空 `-2..2` 轴；旧 `Tags` 与 `FacetStrengths` 继续兼容，但对应新轴已设置时由新轴优先，避免重复注入相同倾向。具体轴名称和两端语义必须在 UI 同时显示。
