# corrections_20260917 · 词条摘要里的作者台账（原值逐字保留）

## 为什么

上线包 448 档里，**2 档**的 `summary`（词条摘要）混进了「写给自己看的话」——
1 处是**工程坐标**、1 处是**作者裁量印记**（两类的分辨见下节）。
`summary` **不是内部字段**：`WorldKnowledgeQueryService.FormatEntry`（非 `partial` 时带表头）把它拼成

```
【标题】
<summary>
<该档正文>
```

一起进 `RetrievedText`，而 `RetrievedText` 被当**「知识正文」喂给模型**（`WorldKnowledgeModels.BuildPromptBlock`）。
同时它还进 **term 索引**（`WorldKnowledgeLoader.EnumerateTerms(entry.Summary)`）与**向量**
（`WorldKnowledgeSemantic.JoinParts(Title, keywords, Summary, firstExpression)`）。

⇒ 写在 `summary` 里的台账，**模型会念、检索会认**。这既是内容问题，也是检索污染源
（`economy` 能翻出 `geography.mines-lycaron`，根子就在这半句）。

章程口径：留痕归**章程**与 `corrections_<date>`（见 `WORLDBOOK-ENTRY-CHARTER-20260913.md`
「该留痕的走了 corrections（原值逐字保留）」），**不归词条正文**。

## 「台账」指哪两类（09-17 甲方追问「台账是什么」后补）

「台账」是本文用词，指**写在词条里、只有作者与工程自己用得上的信息**。它其实是两类，性质不同：

| 类 | 是什么 | 本次实例 | 该怎么处理 |
|---|---|---|---|
| **① 工程坐标** | 内部路标：章节号、挂在哪个档、文件路径、半角文件名 | `mines-lycaron` 的「承 IMPL §3.1 留痕：economy 档承载一条政治沿革」 | **纯内部坐标，读者无从理解 ⇒ 清掉；出处由章程逐字保管** |
| **② 作者裁量印记** | 记录一项创作判断的**分量与来路**：谁定的、何时、几级裁定 | `throne-saneopa` 的「定都史为 D 级裁定（Max 09-12，登记于研究稿）」 | **创作本身正当 ⇒ 内容保留**；只是不该出现在**模型可见的这一格**里 |

⇒ 笼统叫「台账」把这两类混成一种，是本文表述的糙处。**真正该清的是①；②要挪家，不是删。**

## 改了什么（两个目录同步，与 09-14 那次同规）

- `tools/worldbook-studio/workspace/full-geo1/authoring/`
- `docs/worldbook-migration/projection/authoring-out/`

| 档 | 字段 | 原值（逐字） | 新值 |
|---|---|---|---|
| `doc.geography.mines-lycaron` | `summary.zh-CN` | `吕卡隆银矿与兵祸的由来（沿革断言系银矿因果链的一环，承 IMPL §3.1 留痕：economy 档承载一条政治沿革）。` | `吕卡隆银矿与兵祸的由来。` |
| `doc.politics.throne-saneopa` | `summary.zh-CN` | `萨涅俄帕：隘口之上的内陆商埠与涅雷采斯家的京城；旧都岁月与迁都后的迁回之争。定都史为 D 级裁定（Max 09-12，登记于研究稿）。` | `萨涅俄帕：隘口之上的内陆商埠与涅雷采斯家的京城；旧都岁月与迁都后的迁回之争。` |

删掉的只是**附在摘要后面的那截内部话**；正文、断言、来源五元组、grants 一律未动。
⚠️ 两档删掉的**不是同一类东西**：`mines-lycaron` 删的是工程坐标（该清），
`throne-saneopa` 删的是**作者裁量的署名与日期**（创作本身正当，缺的是它的**家**）。

## 被删掉的那两截，出处仍可查

- **吕卡隆那条**（"沿革断言系银矿因果链的一环，不另立 politics 档"）：
  章程已逐字记着 ——
  `WORLDBOOK-ENTRY-CHARTER-20260913.md` §「不为拆而拆」：
  「两块内容若是同一条因果链，合档不同断言，并在闭合复验留痕（先例：吕卡隆银矿档承载一条政治沿革断言，因"合邦→被夺→银矿招兵祸"是一条因果链，不另立 politics 档）」。**原文不动**。
- **萨涅俄帕那条**（"定都史为 D 级裁定，Max 09-12，登记于研究稿"）：
  裁定本身属 `throne-saneopa` 的立项依据。甲方 09-17 定性「**是合理的作者创作**」
  ⇒ ① 该裁定**不是**待清理的工程台账，而是一条**正当的创作裁量**，内容照旧保留
  （摘要里的定都沿革陈述"旧都岁月与迁都后的迁回之争"未动，删掉的只是附在后面的署名与日期）；
  ② 甲方说「就没有正本」——**经查，指的是没有项目约定的正式落点；记录本身是有的**，
  见下条更正。
  ⚠️ **本条更正（09-17 17:2x）：我此前写"无正本、无处可指"是错的，是我只听了结论没自查。**
  实际查得的登记有两处，且都带逐字原话 ——

  | 落点 | 内容 |
  |---|---|
  | **正本** `docs/worldbook-migration/WORLDBOOK-IMPERIAL-CAPITALS-RESEARCH-20260912.md` §四／§五 | 逐条裁定表第 4 行「已裁（Max 09-12）：采纳，定为涅雷采斯朝都城」；§五 裁定记录第 1 条含 **Max 原话逐字** |
  | **登记** `docs/worldbook-migration/WORLDBOOK-CHRONICLE-LEDGER-20260912.md`（都城批注记） | 「含涅雷采斯朝定都史＝D 级裁定 Max 09-12，登记于 `…IMPERIAL-CAPITALS-RESEARCH…` §五」 |

  ⇒ 摘要里那句「**登记于研究稿**」是**真的**，"研究稿"就是上表那份。
  真正缺的只是**正式落点**（`DECISION-<日期>-<题目>.md` 这套 09-15 才起，该裁定在 09-12）。
  **已回填**：`docs/DECISION-20260917-萨涅俄帕定都史-D级裁定补登.md`（补登，只挂指针、不新增内容）。
  ⚠️ 另记一处**措辞与现状不符**（历史文档按原样保留、不追改）：研究稿 §四／§五 都写「须登记**来源台账**」，
  但全库查「来源台账」只有那两处提及，**无同名台账文件**。

## 顺手修的两处生成器（防复发）

被改的两句原本是**生成器写出来的**，只改 yaml 的话下次重跑会把台账写回去：

- `projection/authoring-out/_gen_new10_20260912.py`（吕卡隆；该脚本写的 id 是早期的
  `doc.economy.lycaron-mines`，后经 09-14 改名/改域为 `doc.geography.mines-lycaron`）
- `projection/authoring-out/_gen_batch2_capitals_20260912.py`（萨涅俄帕）

## 普查（清之前做的，两个筛子）

**教训先记**：第一版筛子只查 `IMPL／§／留痕／档承载／TODO／半角文件名`，报出"**孤例**"——
但那只证明**这几类**是孤例。随后肉眼就撞见 `throne-saneopa` 的「D 级裁定（Max 09-12，登记于研究稿）」，
「人名＋日期＋研究稿」这种形状**一个字也抓不到**。
⇒ **筛子窄 ≠ 目标不存在；报"没有"之前先问"我这把筛子能抓到哪几类"。**

第二版按类扫（工程缩略/章节号、留痕词、裁定与级别标记、作者名、日期串、TODO、档位术语、半角文件名、commit、英文内部域名、复核标记），
覆盖**两个目录各 458 份源档**与**上线包 448 档的模型可见文字**（title ＋ summary ＋ `expressions[].text`）：

| 结果 | 内容 |
|---|---|
| **硬案例 2 档** | `mines-lycaron`、`throne-saneopa`（本次已清） |
| 软案例 1 档 | `clans-charas-cortain-secret`：`科尔坦家财富的公开面（所有权事实）与秘密面（政治解读），分层分档。` —— 括号内是**作者视角的定性**、末句「分层分档」是**结构语**，不是正文口吻。**已于 09-17 改（见下节）** |
| 假阳性 1 处 | `villages-tememos` 的 detail 正文「木材按树种**分档**走湖运」—— 这是在讲木材等级，正文真词，**不是台账**。⇒ 筛子必须人工过一遍。 |

脚本：`AWAKE/tools/_probe_authoring_ledger_census_20260917.py`（扫包）、
`AWAKE/tools/_probe_authoring_ledger_sources_20260917.py`（扫源档）、
`AWAKE/tools/_probe_lycaron_summary_20260917.py`（字段形态与可见性取证）。

## 验收

见重编脚本的 `VERDICT`（日志 `AWAKE/tools/_v11_chain_log_20260917.txt`）：新包 448 档、两处摘要已无台账串、
相对基线包（`geo1-v6`）的差异**只有**两类 —— 「这两条摘要」＋「编译器已生效的 K1（内部文档 id 不再进 keywords）」。
更强的一条归因：相对**同为 K1 之后**编的 `geo1-v10-kwclean`，差异**只有这 2 条摘要**
（keywords 变 0 条、expressions 变 0 条、其它 0 条）⇒ 本次改动是单因的。

生成物：`compiled/geo1-v11-summaryfix`（`manifest_hash=1bacbd82…`、`result_hash=9324B8B6…`、validation 21 条 0 error）。

## 上线与复测

- **仓库侧**：`ModuleData/Worldbook/packages/calradia/{index,manifest,runtime}.json` 已换为 v11，
  注册表 `ModuleData/Worldbook/manifest.json` 的三个哈希已同步
  （`manifestHash` 不变；`contentHash` `F04CB23D…`→`DADE0019…`；`packageHash` `3BA62B05…`→`0067E83A…`）。
  备份：`tools/worldbook-studio/artifacts/repo-package-before-v11-20260917-163950`。
  脚本：`AWAKE/tools/_publish_v11_to_repo_20260917.py`（不带 `--apply` 是预演）。
- **真机**：已用 `tools/deploy_worldbook_to_game.ps1 -ConfirmDeploy` 推到
  Steam 模块目录（部署前确认游戏未运行）。备份：`tools/worldbook-studio/artifacts/game-dir-deploy-20260917-164436`。
  日志：`AWAKE/tools/_deploy_worldbook_log.txt`（`DEPLOY_OK`）。
- **完整性**：真机包经真加载器校验通过（探针 `PROBE-OK`），且与仓库侧四件 md5 全同。
- **复测**：`AWAKE/tools/_redtest_chain_verify_v11_20260917.py` —— 同批 68 条 × 清理前/后上线形态，
  **变坏 0 条、变好 1 条**（`KWI16 「economy」` 由过匹配转 OK）⇒ **缺口 ⑤ 由 4/5 补到 5/5**。
  三根门禁在换包后重跑仍逐字全绿（`RETRIEVAL_GATE` A=10/13 B=9/11 ALL=19/24；`MERGE_GATE` 5 条子判据全 PASS；
  `IDENTITY_GATE` 不挂 78/180/0/0、挂上 78/205/0/0）。

## 第二批（09-17 晚，甲方「1改」）：`clans-charas-cortain-secret` 摘要改正文口吻

| 档 | 字段 | 原值（逐字） | 新值 |
|---|---|---|---|
| `doc.politics.clans-charas-cortain-secret` | `summary.zh-CN` | `科尔坦家财富的公开面（所有权事实）与秘密面（政治解读），分层分档。` | `戴·科尔坦家与沙拉斯港的海务财富。` |

**为什么这么改**：

1. **原句与内容无关** —— 「公开面／秘密面」是本档**自己的两层**，「分层分档」是**机制名**；
   整句在说"这份条目是怎么组织的"，而不是在说科尔坦家。这是**元描述**，不是内容。
2. **摘要不单独门控，所以只能讲公开面** —— `FormatEntry` 把 `summary` 拼在
   「该身份能看的那一层正文」**前面**（`WorldKnowledgeQueryService.cs:509`）⇒ 谁读得到什么层，
   摘要就跟着那一层一起出门。**若把秘密面的事写进摘要，等于对所有人越权透露。**
   故新值只保留公开面（沙拉斯的海务财富归这户人家），**未写**「港税／渔市／地租／养其势」（那些在 secret 层）。
3. **改后与同批提要式一致** —— 比照 `towns-charas`「沙拉斯的位置、起源与海港生计；…」、
   `mines-lycaron`「吕卡隆银矿与兵祸的由来。」；本档实体是**那户人家**，故把这家人写在前面。

**顺带查过：这类"收尾一句结构语"全库共 9 档，只改这 1 档。**
`rivers-sethys`「两说如实分挂」／`tales-charas-origins`「三种口径分层记录」／
`tales-lakonis-red-water`「两种来由说法并置，均不作定论」／`tales-lycaron-vultures`「只陈述"传言"本身」／
`territories-varcheg-swap`「（斯特吉亚说法与巴旦尼亚说法并录）」／`throne-paravenos`「帝国遗民与巴旦尼亚人的观感分层」／
`towns-charas`「帝国与瓦兰迪亚两说并录」／`towns-lycaron`「本地老兵与南帝国说法并录」。
⇒ 那 8 档的"两说"是**游戏世界里并存的诸说**（属正当的既有写法），
只有 `cortain` 的"两说"是**文档自己的层** ⇒ **只改它**。判据＝**"两说"是世界里的，还是文件里的**。

**重编与归因（单因可证）**：脚本 `_v12_cortainsum_20260917.py` → `compiled/geo1-v12-cortain-summary`，
`manifest_hash=a60241e1…`、`result_hash=4455838D…`、validation 21 条 0 error、**1.3 分钟**、VERDICT PASS。
相对上一版 `geo1-v11-summaryfix`（同为 448 档、同一基线选择）**只有这 1 条摘要变**
（keywords 变 0 条、expressions 变 0 条、其它 0 条）；新包全文里「公开面／秘密面／分层／分档」各 **0** 次。

**上线**：仓库侧换包＋注册表哈希同步（`manifestHash` 不变＝包身份未变；`contentHash` `DADE0019…→AA3795AD…`、
`packageHash` `0067E83A…→7223029D…`），备份 `artifacts/repo-package-before-v12-20260917-170521`；
真机 `-ConfirmDeploy` → `DEPLOY_OK`，备份 `artifacts/game-dir-deploy-20260917-170732`。
真机与仓库侧四件 md5 全同；真机包内「公开面／秘密面／分层／分档／IMPL／留痕／研究稿／Max」**各 0**，`entries=448`；
上一版清的那两条摘要**仍在**（回归检查 `REGRESS` 逐字相同）。

**复测**（同批 68 条 × 上一版／本版 × 不挂／挂语义腿）：
两版读数完全一致（不挂 24/68、挂 16/68），**变坏 0 条、变好 0 条**，
**命中集合被挪动 0 条**（摘要进向量，改一句本可能挪动语义腿近邻 —— 实测没有），缺口 ⑤ 仍 **5/5** ⇒ VERDICT PASS。

## ⚠️ 一条要记住的仓库事实：**权威源档不在版本库里**

`AWAKE/tools/worldbook-studio/workspace/` **整棵被 `.gitignore:54` 排除** ⇒
**authoring 源档、注册表（`authoring-v1/workspace-head.json`）、选择/审批/证明、编译产物、
以及本次那条重编链条脚本，全都在 git 之外。**
`docs/worldbook-migration/projection/authoring-out/` 那份镜像也**从未 `git add` 过**（untracked）。

⇒ 所以这次改动的**可入库凭据只有四处**：本文件（原值逐字）＋ 两个生成器的改动 ＋
`projection/authoring-out/` 里那**两份被改的镜像 yaml**（本次一并 `git add`，其余 456 份仍 untracked）
＋ 复制到 `AWAKE/tools/` 的链条脚本与日志（`_v11_summaryfix_chain_20260917.py`、`_v11_chain_log_20260917.txt`）。
**下次要复查"源档到底长什么样"，读的是 gitignore 掉的那棵工作区，不是 git。**

