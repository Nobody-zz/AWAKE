# 身份 × 档矩阵验收报告（步骤 5，2026-09-12）

> IMPL-GEO1-PERMISSION §六 的产出。**以运行时模拟器为准**（真实 `WorldKnowledgeQueryService`/`WorldbookIdentityEvaluator` 源码，`worldbook-runtime-sim probe` 模式）。
> 被测包：`workspace/full-geo1/compiled/geo1`（22 档全量编译，0 诊断）。规格 `matrix-spec.json`，原始结果 `matrix-result.json`（28 行）。
> state 语义（实测归纳）：known=拿到 ≥ 请求详细度的文本；partial=拿到了但低于请求详细度；blocked=deny 或 grant 全不匹配（permission）；not_found=无候选或 unknown。

## 一、§六矩阵行逐条判定

| # | 身份构造 | 提问（关键词） | 预期 | 实测 | 判定 |
|---|---|---|---|---|---|
| R1 | 沙拉斯本地村民（villager/vlandia/town_v7） | 城与港 | rumor 层可见 | partial，文本=③海湾群岛（T2L 以 +30 settlement 分胜出 T1） | ✅ |
| R1b | 同上 | 科尔坦家的账 | cortain-secret 不可见 | **not_found**（无 grant 无 deny→unknown） | ✅ |
| R2 | 外地村民（settlement=town_s1） | 城与港 | T2L 不命中 | partial，文本=沙拉斯 V2 编年史变体（villager→commoner 命中 culture=vlandia 的 B 表达，+20 分胜 T1）；**t13（T2L 文本）未出现** | ✅ |
| R3a | 巴旦尼亚文化×斯特吉亚王国村民 | 海崖与港 | T3（sturgia×sturgia）不可见 | partial，文本=A 级白描，**V1 斯特吉亚版未出现** | ✅ golden 4 负向 |
| R3b | 同上 | 三易其手 | V5（battania）可见 | partial，文本=「不要雇比你更贪婪的人…」巴旦尼亚版 | ✅ |
| R4a | 斯特吉亚村民（sturgia×sturgia） | 海崖与港 | V1 可见 | partial，文本=「卡恰尔半岛是我们斯特吉亚东边的大门…」 | ✅ |
| R4b | 同上 | 三易其手 | T3 可见 | partial，文本=镇名由来 A 版 | ✅ golden 4 正向 |
| R5a | 酒馆老板 | 鬼点子 | rumor 可见 | partial，文本=桥段 A 版 | ✅ |
| R5b | 酒馆老板 | 科尔坦家的账 | golden 1：整档 deny | partial——但文本是**隔壁归属档**的（关键词双向子串跨档命中）；**秘密档公开面/秘密面文本一字未出**=deny 生效。隔离包实测（探针 A1）：单档场景 state=**blocked** | ✅（见 §三.1） |
| R5c | 酒馆老板 | 归属与科尔坦家 | 稿内预期 not_found | partial，拿到归属档 summary——**经继承链 tavernkeeper→townsfolk 命中 T2**，权限语义正确非泄漏 | ✅（稿内预期欠考虑继承链，见 §三.2） |
| R6 | 商人 | 银矿 | detail 可见（faction scope 首战） | partial，文本=矿与兵祸 A 版 | ✅ |
| R7 | 士兵 | 归属 / 三易其手 | detail 可见 | partial，两条 detail 文本均出 | ✅ |
| R8a | 贵族 50 岁（detail=secret） | 科尔坦家的账 | golden 2：secret 可见 | **known**，文本含秘密面「这些财富正被拿来操办科尔坦家的图谋…」 | ✅ |
| R8b | 贵族 30 岁（detail=detail） | 科尔坦家的账 | golden 2：secret 不可见 | partial——文本=**公开面 summary**（T2 含 noble 行），秘密面未出现 | ✅（预期表述精确化，见 §三.3） |
| R9a | 头人 | 城与港 | T1+T2 可见 | partial | ✅ |
| R9b | 头人 | 科尔坦家的账 | 公开面 T2 含 headman | partial，文本=公开面两条 summary；秘密面未出现 | ✅ |
| R9c | 头人 | 银矿 | T4 可见 | partial | ✅ |
| R10a | 仅传 Role=soldier（回退链） | 归属 | 与显式身份一致 | partial，同 R7 | ✅ |
| R10b | 仅传 Role=noble+50 岁 | 科尔坦家的账 | 回退链 noble（硬编码链仅 noble）+noble_mature | **known**，秘密面可见 | ✅ 两条身份路径差异已记录：Role 回退链不含 notable→commoner 祖链，但本档 grant 恰在 noble 行，两路径同果 |
| R10c | 仅传 Role=tavernkeeper | 科尔坦家的账 | deny 压制 | partial（拿到归属档②detail——回退链含 merchant 命中 T4；秘密档文本未出现） | ✅ |

## 二、B 源对照行（§3.5 新增 3 行，实际 6 行含负向）

| 行 | 身份 | 命中 | 结果 |
|---|---|---|---|
| B1 | 帝国平民（culture=empire） | charas-town V4 | ✅ 拿到「我爷爷说那地方原本是帝国最老最老的城…」帝国版（+20 胜 T1） |
| B2 | 巴旦尼亚村民 | varcheg-swap V5 | ✅ 巴旦尼亚版 |
| B3 | 吕卡隆本地老兵（settlement=town_es4） | lycaron-town V5 | ✅ 「我在吕卡隆当了二十年兵…」老兵版（+30 settlement 胜出） |
| B3n | 外地老兵（town_s1） | V5 不命中 | ✅ 拿到 A 级白描——**settlement_ids 门生效** |
| B4 | 南帝国市民（kingdom=empire_s） | lycaron-town V2 | ✅ 「吕卡隆是帝国正统的最后堡垒…」南帝国版（+25 胜出） |
| B4n | 斯特吉亚市民 | V2 不命中 | ✅ 拿到 A 级白描——**kingdom_ids 门生效** |

## 三、实测对稿内预期的三处精细化（均非缺陷、不触"停下"红线）

1. **deny 在多档命中场景的表现**：关键词双向子串（`FindCandidates` L212）会让「科尔坦家的账」同时命中归属档与秘密档。deny 只让**秘密档整条消失**，总 state 由其它命中档决定（partial），不再显示 blocked——但秘密档两个文本一字不出，deny 的档级核弹语义实测成立（隔离场景 state=blocked 见探针 A1）。
2. **继承链让"无 grant"判断必须看全链**：酒馆老板显式身份链=tavernkeeper→townsfolk→commoner，T2 的 townsfolk 行、T4 的 merchant 行（经 Role 回退）都会经祖链命中。稿内 §3.3「其余身份（headman/merchant/soldier/…）无 grant 无 deny→unknown」一句与 PUB 模板自身矛盾——**实测：headman/merchant/soldier 在公开面有 grant（T2 全行），可读公开 summary**；秘密面保护不受影响。已回写 §3.3。
3. **golden 2 的精确表述**：30 岁贵族不是整档不可见，而是「秘密面不可见、公开面可见」（T2 含 noble 行）；50 岁与 30 岁的文本差异（秘密面有无）即对照成立。

## 四、验收结论

- 通过线（§六）：**28/28 行与预期一致**（含 3 处预期精细化）；新档不存在"目标身份取不到"的格——村民本地/外地、酒馆老板、商人、士兵、头人、贵族 30/50、仅 Role 各路径全部按 grant 拿到对应层文本；两条身份路径（显式 IdentityId vs 仅 Role）差异已记录。
- golden case 1–5 全部实证；scope 五档（local/regional/faction/national/elite）、11 profile、culture/kingdom/settlement 三维度、一表达多 grant OR、B 源 9 条表达全部过测。
- `SelectExpression` 的"一表达胜出"实测为**最高分表达**（维度条件 +20/+25/+30 加权），同一断言多表达时按身份就近给版本——分层"同一件事不同身份说法不同"的机制实测成立。

—— 阿砚，2026-09-12 21:4x

## 五、matrix2 增补验收：都城批 + 第三批地理 6 档（2026-09-12 深夜，49 行）

> 被测包：`workspace/full-geo1/compiled/geo1-full2`（34 档全量编译，273 entries，0 诊断，无 sources 泄漏）。
> 规格 `workspace/full-geo1/matrix2-spec.json`，原始结果 `matrix2-result.json`，判定脚本 `_verify_matrix2_20260913.py`（程序化判 state+文本片段，非肉眼）。

### 行构成（49）
- **A1–A10 回归行**：上一轮 28 行中的关键 10 行（沙拉斯本地/外地村民、酒馆老板、秘密账 deny 与 no-grant、贵族 50/30、仅 Role 回退链、帝国/巴旦尼亚/南帝国版本行）——全部原样复现，22→34 档扩容未破坏旧行为。
- **T/P/D/E/M/NH（34 行）**：第三批 6 地理档（塔奈西斯湖/珀拉斯海/德律亚山/德夫赛格高原/弥戎河/纳哈撒沙漠）× 身份分层（villager/townsfolk/merchant/soldier/headman/noble）+ 文化门正反向（khuzait/empire/aserai/battania 门 + 错误文化落无门兜底行）。
- **S/I（9 行）**：都城批萨涅俄帕五身份行；帕拉汶德文化门行 + 贵族索 secret 行 + 士兵 summary 行。

### 结论：49/49 全绿（程序化判定）
- **文化门互斥 rumor 实测成立**：同档同层多版本按 `culture_ids` 条件 +20 分胜出——塔奈西斯湖库赛特版/帝国版、珀拉斯海阿塞莱版、德夫赛格库赛特版、弥戎河巴旦版、纳哈撒绿洲版、帕拉汶德瓦兰迪亚版/巴旦版全部命中对应版本；错误文化身份拿到无门兜底版（T3/P2/E2/M2/NH2）。
- **I3 观察行（设计如实测）**：帕拉汶德 rumor 层只设瓦兰迪亚/巴旦尼亚两门、无帝国兜底——帝国村民 rumor 不可得（not_found）。帝国视角由 summary/detail 层覆盖（paravenos-capital-summary/imperial-noble），属分层设计而非缺陷；如需帝国村民版本待 Max 裁是否补。
- **I4**：贵族 50 岁索 secret，帕拉汶德无 secret 层 → partial 拿 detail 封顶文本（imperial-noble 等），分层封顶行为正确。
- **T7**：模拟 <18 岁贵族（detail=rumor 覆写）→ **blocked**，detail 层 noble 表达不可达，能力门生效。

### probe 语义三处实测精化（工具认知，非缺陷）
1. **probe 的 `age` 参数不驱动能力**：年龄→detail 映射（<18 rumor / 18–24 summary / 25–44 detail / ≥45 secret）在游戏侧 `WorldbookIdentityCapabilityRules.Resolve`；sim probe 用静态 CAP 表，`age` 只透传（供 min_age 类条件）。测贵族年龄上限须用 `detail` 键显式覆写（T7 即此法）。
2. **spec 键语义**：`detail`/`scope`＝身份能力覆写；`requested_detail`＝请求层（缺省 secret）。故除 secret 层行外全部显 partial 是正常形态，known 仅在请求层=拿到层时出现。
3. **blocked 与 not_found 的实测分野**：无任何「profile×维度条件」匹配 → not_found（档不可见，如 A4 村民对秘密档、I3 帝国村民对帕拉汶德 rumor）；有 profile 匹配但能力门（scope/min_detail）不过 → blocked（如 T7）。二者均无文本=不可达，判定"谁能看到"以文本为准。

—— 阿砚，2026-09-12 21:5x

## §六 matrix2 增补（二）：I3 闭合重跑（2026-09-13）

**裁定**：Max 09-13 拍板补帝国版——「3补但要注意村民的受教育程度/知识面，巴拉维诺斯可以是类似说书人传唱的传说，但不可能人人都记得清清楚楚，毕竟时间久了，而且离得远的、整日种地的农民也不太能了解，这是中世纪啊」。

**改动**：
- `paravenos.yaml` doc rev 2→3、assertion.paravenos-3 rev 1→2，新增 `expr.paravenos-imperial-rumor`（rumor 层，grant＝villager/local/rumor + `entity.culture.empire` 门，min_detail==layer 不变式成立）。文本＝说书人传说口径：说书词唱旧名巴拉维诺斯、大帝亲手筑的荣耀之城、做过都城——"多少年前的事，种地人谁记得真切"；现状句（市集官司掌事换瓦兰迪亚老爷）落在 V0a 引文内。引文双源：编年史 V0a（21E39B1E）+ V2a（95790BE4）。
- 六步重编译 `compiled/geo1-full3`（validate 0 诊断→register rev3→select 34 档→approve→proof→compile）；新表达正文程序化确认入包（`runtime.json` entries 内）。
- 矩阵重跑 49/49 全绿（`matrix2-result.json` 重导出，判定脚本 I3 预期改 `known` + any_of「说书词里还唱着」+ none_of「咱瓦兰迪亚自己的城」——片段均不与档简介撞词）。

**I3 闭合实测**：帝国村民（villager+empire）请求 rumor → **known**，返回文本＝帝国版说书人传说正文；SelectExpression 文化条件 +20 使帝国版胜过无门兜底路径（本档 rumor 层并无兜底版，瓦/巴两门依旧互斥不泄漏）。I3 行名由「观察：无帝国门兜底」改「帝国版说书传说」。

**工具认知补遗**：`compile --out` 路径相对**工作区根**解析（传 `compiled/...` 报 WB-PATH-003，须传 `workspace/full-geo1/compiled/...`）；`tail -c` 按字节截断会把 UTF-8 中文切成假乱码（读档用按行工具，勿用字节截断判编码）。

—— 阿砚，2026-09-13 21:5x

## 七、战争批矩阵增补（geo1-war1，2026-09-13 午后）

全量包 **geo1-war1**（40 档/186 表达/546 grants，validate 0 诊断）落地后矩阵扩至 **61 行：61/61 全绿**。新增 W1–W12 战争批行。

**W 行覆盖点**：
- 文化门正反向：斯特吉亚村民 rumor（sturgia 门）↔ 无门兜底 summary；帝国村民对瓦兰迪亚军力的"耳闻不知其所以然"（rumor）；帝国贵族对库赛特的威胁评估（empire 门 detail）；达西村民对弩的"没听说过"（darshi 门不知说）；巴旦尼亚村民"弩无灵魂"（battania 门）。
- **别名检索首测**：`怯薛`（zh 别名→库赛特军档，merchant detail）、`Huscarl`（英别名→皇家侍卫档，soldier summary）均 known——KeywordIndex 双向子串对无锚点条目生效实证。
- **身份覆盖缺口整改（本轮实 lesson）**：首跑 W10/W11 not_found——不是别名检索问题，是**军制档漏配 merchant/soldier 表达**：merchant 无任何 detail grant、soldier 无文化时无 summary grant，落 not_found（无 profile×条件匹配）。当场补 6 处 grant（商人问军制=佣兵/军贸视角，士兵问精锐=同行视角）重编译后全绿。**教训：新档身份覆盖至少查 villager/townsfolk/soldier/merchant/headman/noble 六身份×各层是否有落点，尤其"无文化门兜底行"必须覆盖 soldier（national）与 merchant（faction）。**

**译名实钉**：可汗亲卫（B 级草判名）→官方 CN＝**可汗卫士**（Khan's Guard，`lar4lfKR`）；皇家侍卫＝**Huscarl**（DHbF9JvO，CNt 繁体作"諾德侍衛"）；正文一律官方名，草判名只进对照。

—— 阿砚，2026-09-13 12:0x
