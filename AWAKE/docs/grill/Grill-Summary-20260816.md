# 五条修正 · 50 轮对抗审查汇总

_审查方式：每条修正使用独立 Codex 只读会话，生成 50 个对抗检查点并给出 VERDICT。产物见本目录五个文件。_

## 评估原则（用户反馈 2026-08-16）

- 后续所有设计、PLAN 与审查必须同时覆盖“玩家观感”和“开发者制作/维护难度”，不能只问效率和可行性。
- 玩家观感：入口是否好懂、状态是否清晰、操作是否可信、压缩/固定/删除是否有掌控感。
- 开发者维护：是否只有一套 UI/session/schema、命令是否统一登记、是否避免两套 overlay/VM、是否靠 lint/测试而不是自觉。
- 两条轴冲突时：默认先保玩家可理解；若玩家想要但维护成本过高，先做低维护版本，把扩展留给内容包。

## GRILLME 轮次判断（用户反馈 2026-08-16）

- 不再默认“每条修正 50 轮”。50 轮是在概念文字阶段空转，且第二轮已证明：不把概念变成 schema/命令/UI/测试契约，再多数轮也不会 APPROVED。
- 重新判断后的策略：
  - 默认每份 PLAN：`MAX_ROUNDS = 5`，遇到 `VERDICT: APPROVED` 提前终止。
  - 高风险项（统一会话、交互命令）：`MAX_ROUNDS = 8`。
  - 低风险项（资产边界、历史命令）：`MAX_ROUNDS = 3`。
  - 5 轮后仍 REVISE，不再继续空转；把未收敛点交给用户拆解决策。
- 轮次的价值取决于 PLAN 粒度，不取决于轮数本身。

## 结果

| 修正 | VERDICT | 主要缺口 |
|---|---|---|
| C1 原始对话 / 压缩记忆 / 关系状态分层 | REVISE | 没有明确 transcript schema、rollover、迁移、AI 原文注入、压缩后删除时序、会话追踪。 |
| C2 借鉴面板但不做文件直接编辑/快照回滚 | APPROVED | 方向成立，但实现时必须用命令适配器和紧凑审计，不能演化成另一种“全文档快照”。 |
| C3 借鉴 Alice 结构但不复制美术 | REVISE | 只禁 artwork/portrait 太窄，prefab/VM/命名/brush/icon/sound 都可能被抄；需要更完整的资产边界。 |
| C4 交互动作先做代码保底 | REVISE | 需要新增 action schema、适配器、权限、存储、幂等、快照校验；v1 应收窄到给金币/请求承诺，物品/交易需原生支持。 |
| C5 地图相遇对话进入统一面板 | REVISE | 当前 coordinator 只是互斥锁，不是真实会话；需要统一 session 模型、服务所有权、历史写入、UI 去重与迁移。 |

## 下一动作

1. 把五条修正升级为 `Five-Corrections-Revised-20260816.md`。
2. 按修订版把 `AWAKE-ContactPanel-Concept-20260816.md` 的 Phase A-E 收窄。
3. 需要用户拍板的边界：v1 是否只做“给金币 + 请求承诺”，历史是否升级 messenger v2，场景入口是否保留轻量覆盖层。

## 第二轮 50 点复核（修订版 v2）

第二轮针对修订版再次各生成 50 个新检查点，产物为 `Round2-Grill-Correction*-20260816.md`。

| 修正 | 第二轮 VERDICT | 结论 |
|---|---|---|
| C1 | REVISE | 概念仍缺 transcript schema、chunk 键、迁移状态、审计/roll 命令、prompt 替换与字节预算的具体契约。 |
| C2 | REVISE | 方向正确，但删除/固定/清空/撤销需要具体命令 schema、per-contact 存储、审计与幂等边界。 |
| C3 | REVISE | 需要可执行的资产禁令 lint、avatar 数据模型、内容 Tab API、以及“扩展现有面板而非新 hub”的文档一致性。 |
| C4 | REVISE | v1 需锁死为 `give_gold.v1 + promise_request.v1`，并给出 session token、快照、幂等、补偿、typed error 的完整契约。 |
| C5 | REVISE | 需要真实 session 对象、统一 hub 删除旧双 overlay、持久队列、按 token 关闭、迁移与 SdkSmoke 的明确验收。 |

## 结论

- 两轮共给每条修正生成 100 个对抗检查点；没有一条在“概念文字”层面通过。
- 这不是方向失败，而是审查正确指出：这些修正必须落到 schema、命令、存储、UI、迁移和测试契约上才能 APPROVED。
- 下一步不应继续无限改概念文字，而应选择 Phase A 的确定边界，把五条修正分别写成可实施 PLAN / 契约文件，再用 grill 收敛到 APPROVED。
