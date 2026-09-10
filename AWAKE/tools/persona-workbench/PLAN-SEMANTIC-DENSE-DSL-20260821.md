# Plan: PersonaWorkbench 语义丰富高密度 DSL
_Locked via grill — revised after Codex adversarial review; implementation authorized by the user autonomous request_

## Goal
将 PersonaWorkbench 从“把 PersonaDocument 渲染到固定字节上限”升级为“从自由文本保留丰富行为语义并生成高密度、分层、可验证 DSL”。运行时 DSL 保持现有语法和字段兼容；生成器按身份、核心人格、行为机制、情境扩展、编辑证据的价值分层，优先去重和压缩低价值文本，核心身份与可执行行为不能被静默删除；当核心内容在预算内无法完整保留时明确返回可诊断错误，而不是生成看似成功的残缺 DSL。

## Approach
1. 盘点当前 `PersonaDocument` 字段、canonical DSL 段落、解析器与运行时消费边界，建立明确的字段保护矩阵；既有 section 名称、`dsl`/`draft` 响应字段和解析契约保持兼容。
2. 将生成结果从单一字符串内部升级为类型化结果：保留现有 `PersonaDslResult.Dsl`，新增明确的 `Success`/`Compressed`/`BudgetExceeded`/`CoreBudgetExceeded` 状态和可选诊断字段，包括预算、实际字节数、是否压缩、去重/省略/截短动作、保留字段摘要和核心保留状态；`/api/preview` 与 Provider DSL 响应都以 nullable 字段向后兼容暴露诊断，诊断不写入运行时 DSL。
3. 明确不可静默删除的字段：`PERSONA_IDENTITY` 的 `ID`/`NAME`、核心人格轴/标签、`PERSONALITY_CONTRADICTION` 的触发器/边界/承诺机制、`PERSONALITY_PRIVATE` 的行为轴和反应轴。保护集合只包含当前文档中已验证且实际存在的字段；未配置的轴、标签或条件不视为缺失。若仅保留这些已存在的原子项和可分段核心证据仍超限，返回 `persona.template_core_budget_exceeded`；普通超限但所有已存在保护项完整时返回成功并标记 `compressed=true`。
4. 重写 canonical 生成器的预算算法：先对同一字段内的完全相同规范化文本去重，再按固定优先级移除或压缩可选字段；不得整段删除包含受保护字段的 section。跨字段只在字段语义相同且证据完全相同的情况下去重，并保留诊断动作，不做模糊语义合并。
   固定字段优先级（高到低）为：① `PERSONA_IDENTITY.ID`、`PERSONA_IDENTITY.NAME` 等身份原子字段；② 已配置的核心人格轴与标签；③ `PERSONALITY_CONTRADICTION` 中已配置的触发器、边界、承诺机制与破例后果；④ `PERSONALITY_PRIVATE` 中已配置的行为轴、反应轴与条件响应；⑤核心行为文本（能拆分时按完整句子/引文片段保留）；⑥矛盾描述；⑦私下描述；⑧公开描述；⑨自述规则、行为示例与编辑性证据；⑩外观、来源包 ID 及其他非运行时证据。身份、轴/标签、触发器/边界/承诺和反应原子项只允许整体保留或报告核心预算失败；可分段文本在同一优先级内按原文顺序稳定保留，删除顺序为低优先级先删、同优先级后出现者先删，绝不以字段长度反转优先级。
   同一优先级内的字段顺序固定为 DSL 的既有 section 顺序，字段数组保持输入顺序；只有在同一语义字段内做精确规范化相等去重。规范化函数为：Unicode NFC；首尾 `Trim`；连续空白（含换行、制表）折叠为一个 ASCII 空格；保留所有标点、引号和数字；中文不改写；英文仅在比较键中使用 `OrdinalIgnoreCase`，输出保留第一次出现的原始值。去重不跨语义字段、不做同义词/模糊相似度合并；重复项移除后，保留首次出现的位置并记录一次 `deduplicated` 诊断动作。
5. 将长文本处理从按 UTF-16 字符位置截断改为原始值上的 UTF-8 安全片段选择：按完整句子、引号片段或 Unicode scalar 边界选择，保留 raw value，最后只 Escape 一次；每次候选输出都重新解析/校验 DSL，并比较受保护字段的规范化语义快照与选中值，禁止只因“语法可解析”就接受字段变化、损坏引号、二次转义或半个语义片段。
6. 在服务层使用固定映射表：`Success`→HTTP 200/空 errorCode；`Compressed`→HTTP 200/`compressed_optional_fields`；`BudgetExceeded`→HTTP 400/`persona.template_budget_exceeded`；`CoreBudgetExceeded`→HTTP 400/`persona.template_core_budget_exceeded`。Provider 的 canonical failure 使用独立 action status，绝不进入 Provider quarantine；普通压缩成功返回 DSL 与诊断摘要。`/api/preview` 保留现有校验失败的 400 响应形状，只把新的预算失败转换为同一可解析错误 envelope，不抛出 500；旧客户端忽略新增 nullable 字段仍能按 `response.ok` 处理。诊断只在当前响应中返回，包含请求相关 ID、预算、实际大小、动作和 protectedFieldsPreserved，不包含原始文本或密钥；本轮不新增持久化诊断存储或混合 Provider failure endpoint。字节统计固定为最终 emitted `dsl` 字符串的 UTF-8 byte count，包含 section 分隔符和换行，不包含 transport JSON 或诊断。
7. 锁定唯一生产生成入口：`PersonaDslGenerator.Generate` 委托类型化 canonical 生成器；删除或改为 `internal` 的 `GenerateLegacy` 不得被生产服务调用。共享 `PersonaIdentityNormalizer` 在预览、Provider 转换和生成入口统一保证非空 `DisplayName`（用户空缺时使用现有本地默认名），因此 `ID`/`NAME` 都是可验证的原子字段。增加确定性契约与 golden 测试：重复文本去重、字段优先级、每个已存在受保护字段保留、稀疏文档不因未配置字段失败、身份不可删除、UTF-8/代理项/引号转义边界、合法 DSL 解析及受保护字段 parse-render 语义快照相等、typed canonical failure 不进入 Provider quarantine、预览 4xx 映射、旧客户端忽略新增 nullable 字段仍能读取 `dsl`/`draft`、预览/Provider DTO 诊断序列化兼容、旧短文本 golden 不变，以及长自由文本的行为层覆盖；Ollama 仅作为补充 smoke，不作为压缩算法正确性的唯一证据。
8. 发布新包并用本机 Ollama `gpt-oss:20b` 串行验证短/长自由文本的直接识别、扩充与 DSL 转换；记录 Provider 与 canonical 两层的输出长度、诊断状态和字段覆盖，不调用云端、不启动 Bannerlord。

## Key decisions & tradeoffs
- “丰富”定义为可影响角色行为的语义覆盖，而不是 DSL 字符数；重复外貌或同义形容词不增加丰富度。
- 运行时 DSL 语法保持兼容，第一版不引入新的懒加载协议；通过现有 section 分层和内部压缩诊断实现目标。
- 身份、核心人格轴/标签、触发器、边界、反应、承诺机制属于不可静默丢失内容；公开/私下长文和编辑证据属于可压缩内容。
- 去重只做同一语义字段内的精确规范化相等匹配；不做可能抹掉不同来源的模糊相似度合并。
- 不把原始 `SourceDescription`、AI 解释或压缩报告塞入运行时 DSL；这些内容只保留在编辑态或响应诊断中。
- 预算仍是安全上限，但上限不再决定“删到能塞下”为止。ID、NAME、轴/标签、触发器/边界和结构键是原子保护项；核心文本证据按完整句子或引文片段分段，能保留完整片段就保留，连最小合法片段都放不下则返回 `template_core_budget_exceeded`。
- 诊断面向用户和本地调试，但不记录原始人物文本、API Key 或完整 Provider 返回，避免敏感信息扩散。

## Risks / open questions
- 需要确认 `PersonaDslGenerator` 与 `CanonicalPersonaTemplateGenerator` 的唯一权威入口；实现前以调用图和 golden tests 锁定，避免只修测试路径。
- 运行时对长 `DATA_CN` 的实际消费方式需要以当前 parser/golden tests 为准；不能把可编辑证据误写成运行时行为规则。
- 当前 UI 可能只显示字符串错误；需要增加不破坏旧客户端的压缩摘要显示、预览 4xx 映射、Provider canonical failure 不 quarantine 和核心预算失败恢复测试。

## Out of scope
- 不修改 Bannerlord 运行时、游戏模块、世界书、全局模型配置或云端 Provider。
- 不新增推测人格、置信度或自动批准机制；没有原文证据的内容继续省略。
- 不修改历史发布包和历史日志；只生成新版本并保留当前运行包可回退。
