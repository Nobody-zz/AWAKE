# 探针实测记录（步骤 0+1，2026-09-12）

> 实施稿 `IMPL-GEO1-PERMISSION-20260912.md` §一 步骤 0/1 的产出。编译链 + 运行时模拟器（真实 `WorldKnowledgeQueryService`/`WorldbookIdentityEvaluator` 源码）实测。**验收一律以本记录的运行时实测为准**（引擎分工：Studio 投影仅内容预览，不作权限验收）。

## 一、编译链（全部通过）

- 工作区：`tools/worldbook-studio/workspace/authoring-test/`（gitignore 区，不入正典库）
- 探针档：`authoring/test-deny-probe.yaml`（doc.geography.test-deny-probe）、`authoring/test-min-detail-probe.yaml`（doc.geography.test-min-detail-probe）
- 来源登记：`authoring/sources/source-local-test-probe.yaml` + `probe-source.txt`（quote 全部可定位、hash 全对）
- 链：validate ✅ → register×2 → select → approve → proof → compile ✅，产物 11 文件落 `compiled/probe/`
- 过程诊断（均已修，留痕）：subdomain `probe_test` 不在 taxonomy 白名单 → `terrain`；`source_nature: probe_fixture` 非法枚举 → `developer_original`；来源必须登记 + 文件 hash 一致 + 引文可定位（WB-SOURCE-001 三连）
- 模拟器扩能：`worldbook-runtime-sim` 新增 `probe` 子命令（JSON 规格驱动，支持 age/culture/kingdom/settlement/management/能力覆写），`dotnet run -c Release -- probe <manifest> <spec> <out>`

## 二、探针 A（deny）：实测 vs 预期

| # | 身份构造 | 预期（实施稿 §五） | 实测（runtime state） | 判定 |
|---|---|---|---|---|
| A1 | tavernkeeper（有 grant：`{tavernkeeper, local, rumor}`；命中定向 deny） | not_found（整档消失） | **blocked**（reason=permission），文本空 | ✅ 语义一致，state 名实测为 `blocked` 不是 `not_found` |
| A2 | noble 50 岁（EffectiveDetail=secret，对应能力规则 age≥45→secret） | secret 层可见 | **known**，返回 secret 表达全文 | ✅ |
| A3 | noble 30 岁（detail<secret） | not_found（grant 不匹配） | **blocked**（reason=permission） | ✅ 语义一致（grant 不匹配收敛为 blocked/permission） |
| A4 | villager（无 grant 无 deny） | not_found（unknown，不默认 public） | **not_found** | ✅ golden case 5 实测 |
| A5 | anonymous（命中 anonymous-deny） | not_found | **blocked**（命中定向 deny） | ✅ |

**结论**：deny 命中 → 整档 `blocked`（`WorldKnowledgeQueryService.Query` L52-56 `continue` 的推演与实测一致）；定向 deny（`{profile.tavernkeeper}` / `{profile.anonymous}`）不波及 noble 链（noble→notable→commoner，registry 确认无 tavernkeeper/anonymous）——A2 的 known 即规避面实证。

## 三、探针 B（min_detail 不变式）：实测 vs 预期

同一条 detail 层断言两条表达：变体 A grant=`{villager, local, rumor}`（故意违反不变式 min_detail≠layer）、变体 B grant=`{villager, local, detail}`（合法）。

| # | 查询 | 预期（实施稿 §五 审查改定版） | 实测 | 判定 |
|---|---|---|---|---|
| B1 | villager，requested=secret | **读到 detail 文本（泄漏实锤）** | state=**partial**，文本=「PROBE-B-VIOLATE…」——`SelectExpression` 只看 RequestedDetail(secret=4)≥表达层(detail=3)，不看身份能力；grant 匹配只看 EffectiveDetail(rumor)≥min_detail(rumor) | ✅ **泄漏复现，不变式②必要性实证** |
| B2 | villager，requested=rumor（对照） | blocked（表达 3 > 请求 1，易踩反） | **blocked**（reason=permission） | ✅ 与既有实测记忆一致 |

**结论**：表达层与身份能力之间**无自动闸**——「低 min_detail 挂高层表达」的内容两引擎都放行。全量自检（步骤 4）必须逐表达断言 `grant.min_detail == 所在表达 layer`。

## 四、对实施稿的回写

- §五预期表 state 名替换为实测值（not_found→blocked 的两处语义等价修正）；其余预期全部实测命中，**无读码结论被推翻**，不触发"停下回写"红线。
- 新增实测知识（写档用）：运行时 state 枚举实测值=`known / partial / blocked / referral / not_found`；grant 不匹配与 deny 命中在 state 上都表现为 `blocked`（reason=permission），**无法从 state 区分 deny 与 grant 拒**——矩阵验收判定"整档被 deny"时须结合该身份的 grant 是否存在来推。

—— 阿砚，2026-09-12 21:0x
