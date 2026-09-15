# O5 v2：跨卡同句并入门禁（验证记录）

日期：2026-09-13
范围：角色卡侧第五道内容门禁的观测项 O5。
改动文件：
- `AWAKE/tools/persona-workbench/tools/audit-character-enhancement.ps1`
- `AWAKE/tools/persona-workbench/tools/gen-persona-quality-baseline.py`（口径纪律：两处必须同源）

> 详细验证证据（参考实现、注入式自测、交叉验证 fixture、口径探针）在
> `AWAKE/docs/evidence/awake-prose-qc-20260913/`。该目录按 `.gitignore:48` **不入库**，本地保留，可随时复跑。

---

## 1. 动因

红队 `C2 新模板·句式趋同` 与 `C3 跨卡可互换` 两栏都写着「机械层只以 **O5 跨卡印章**作粗筛」——
但旧版 O5 **只比 `selfClaimExamples` 的开头 8 字**，而真实发生的印章是：

> `我最大的拧巴就在这儿：` 逐字出现在 **5 张卡的 `core`** 段（因加泰尔／埃尔贡／德泰尔／温吉德／阿尔德里克），
> 是批 4–6 改 `core` 时留下的作者腔。旧 O5 **查不到**（不在 examples 里）。

⇒ 缓解措施名存实亡。本次把 O5 扩成 v2，这条粗筛才真正落实。

---

## 2. 口径定稿

| 项 | 值 |
|---|---|
| 通道 A | `core` 全文（去所有空白）的 **10-gram 滑窗** |
| 通道 B | `selfClaimExamples` 每条**开头 8 字**（去空白、去前导「（」） |
| 命中阈值 | 同一串出现在 **≥ 3 张**不同卡 |
| 折叠 | 按命中卡集（frozenset）分组；锚卡＝组内 **Ordinal** 序最小者；在锚卡 `core` 里把相邻命中的 10-gram 合并成片段，取最长者 |
| 首尾修剪 | 片段首尾标点剥掉（中间不动），只为可读 |
| 判定 | **告警（WARN）**，不作生死 |

### 为什么只扫 `core` ＋ `examples`

实测（`probe_o5_scope.py`）：把扫描面扩到 `identityFacts` 后，命中大头**全是卡务模板**：

| 串 | 命中卡数 | 所在字段 | 性质 |
|---|---|---|---|
| `依卡拉迪亚编年史补写其性格。` | 22 | `identityFacts` | 结构性正当（第三人称设定摘要体固定收尾） |
| `官方仅载其家世与亲缘，本卡依…` | 19 | `identityFacts` | 同上 |
| `clan_empire_` / `an_empire_` | 5 | `identityFacts`、`sourceDescription` | 卡务 id 标注（`sourceDescription` 本就该有） |

⇒ `identityFacts` 与三个 `description` 是**第三人称设定摘要体，本就应同构**，扫了没有信噪比。
`core` 是第一人称独白、`examples` 是台词样本——**作者腔只会沉淀在这两处**。

> 注：`identityFacts` 里混进 `（clan_xxx_1）` 这类 id 标注是**另一个问题**（卡务信息渗进正文），
> 归 `awake-prose-qc` 技能的 `authoring_meta_leak` 判据管，不是 O5 的活。

---

## 3. 规则有效性：注入式自测

| 场景 | 构造 | 期望 | 实测 |
|---|---|---|---|
| S0 | 真实库 | 0 命中 | **0** ✓ |
| S1 | 印章注入 5 张卡 `core` | 1 条、跨 5 张、片段含印章 | **1 条 / 跨 5 张** ✓ |
| S2 | 只注入 2 张 | 0（阈值 ≥3） | **0** ✓ |
| S3 | 同一开头注入 3 张 examples | examples 通道命中 | **命中** ✓ |
| S4 | 各卡仅共享 9 字（不成立 10-gram） | 0 | **0** ✓ |

⇒ **抓得住真印章，且 n 与阈值都卡在正确位置。**

---

## 4. ps1 ↔ py 交叉验证（防"两把尺子"）

`o5_fixture.py` 造合成卡目录（6 张真卡副本，其中 5 张 `core` 注入印章），产出 py 侧期望；
ps1 用 `-CharRoot` 指向同一目录跑。

| 项 | 结果 |
|---|---|
| ps1 报告卡数 | 6（与 fixture 一致） |
| ps1 O5 消息集合 | `{O5:core 与其它卡同句「…」（跨 5 张：…）}` |
| py 期望消息集合 | 同上 |
| **逐字相等** | **True**（`only ps1 = ∅`，`only py = ∅`） |

⇒ 两边实现**同源**（含片段折叠、标点修剪、卡名 Ordinal 序、消息模板）。

---

## 5. 改动后全库结果

| 门禁 | 结果 |
|---|---|
| `audit-character-enhancement.ps1` 全量 | `allPass=True`，**76/76 PASS**，**O5 零告警** |
| `gen-persona-quality-baseline.py` | `passed 76 / failed 0`，`byWarn {O2:3, O5:0}`，`o5TotalFindings=0` |
| 红队 `run-attack-suite.py --spec v4` | **BREAK 0/17**（与基线一致） |
| 耗时 | **6.0 s**（扩面后仍在容忍范围） |

**改动边界**：`git diff` 逐行审查确认，仅限 ① 新增参数 `-CharRoot`（＋报告头新增「卡目录」一行自证）；
② O5 预扫描块替换；③ O5 告警块替换；④ 注释与报告文案。**E1/E2/E2b/E4/E5 与 O2/O3/O4 逻辑一行未动。**

---

## 6. 本次踩的坑（已记入 `awake-persona-card-gates` 技能）

**PowerShell 变量名不区分大小写** —— 新加参数 `$CharDir` 与脚本内部既有 `$charDir` 是**同一个变量**：

```powershell
param([string]$CharDir = '')
...
$charDir = Join-Path $base '...characters'   # ← 把参数值覆盖成默认值
if ($CharDir) { $charDir = $CharDir }        # ← 判断的已是默认路径，赋值无效
```

后果＝**参数静默失效**：读错目录却照常出报告。露馅方式只有"报告行数不对"——
第一次跑出来 **89 行（= 76 张全量）而非 fixture 应有的约 30 行**。

修法：参数改名 `$CharRoot`；并把卡目录写进报告头。
教训：**新加参数后，先跑一个"只有 N 张卡"的 fixture，核对报告行数与卡目录。**

---

## 7. 复跑方法

```bash
PY="C:/Users/26811/.workbuddy/binaries/python/versions/3.13.12/python.exe"
cd D:/AWAKE-Dev/AWAKE/docs/evidence/awake-prose-qc-20260913
"$PY" o5_selftest.py      # 参考实现 5 场景自测
"$PY" o5_fixture.py       # 重建 fixture（然后 ps1 用 -CharRoot 指过去）
```

其余文件：`o5_algo.py`（参考实现）、`probe_o5_core.py` / `probe_o5_scope.py`（口径探针）、
`check_worldbook_7.py`（世界书侧 7 处文案存续核对，只扫 `zh-CN` 正文、跳过 `quote`）。
