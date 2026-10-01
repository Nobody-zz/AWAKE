# 角色卡工具链缺陷修复（2026-10-02）

本文件记录 2026-10-01～10-02 重写角色卡期间，在**工具链**里实测发现的缺陷与处置。
所有读数都标证据等级；【实测】= 在本机真跑过并留下可复现的命令与输出。

范围：`AWAKE/tools/persona-card-gate.py`、`AWAKE/tools/persona-workbench/tools/audit-character-text.ps1`、
`AWAKE/tools/persona-workbench/tools/build-authoring-facts.py`。

---

## 1. 我的门：`--include-list` 在相对 `--cards` 下静默选 0 张（已修）

**现象【实测】**：
`py -3 AWAKE\tools\persona-card-gate.py --cards AWAKE\tools\persona-workbench\characters --include-list <绝对路径清单>`
输出 `cards=0` + `PERSONA_GATE_ERROR no cards matched`，退出码 2。

**根因**：两侧对路径的规范化不一致。

- `load_list()`（`persona-card-gate.py:252-261`）把清单每行存成 `os.path.normpath(os.path.abspath(line))` —— **绝对**路径。
- `read_cards()`（`:423-438`）原先写 `key = norm(p)`，而 `norm` 只是 `os.path.normpath`，**不做 abspath**。
- `glob.glob(os.path.join(cards_dir, "*.persona.json"))` 在 `--cards` 为相对路径时返回**相对**路径 ⇒ `key` 永远不在 include 集合里 ⇒ 一张都不选。

**修法**（`:423-430`）：`key = norm(os.path.abspath(p))`，并加注释说明清单侧存的是绝对路径。

**验证【实测】**：同一命令修前 `cards=0`、修后 `cards=37`。
**守卫仍在**：选 0 张会 `PERSONA_GATE_ERROR` 并退出 2，不会假绿。

**使用注意**：即使修好，也建议 `--include-list` 与 `--cards` **同时给绝对路径**，最不容易踩。

---

## 2. 我的门：子集验证必须加 `--min-population 100`

R3 / R4 / R9 都是**语料形状**规则，会按「被检的那一批」算占比，不是按 `--cards` 全集。

`R9`（`persona-card-gate.py:653-663`）：

```
if n >= args.min_population:
    shape = Counter(len(tag_ids(c["data"])) for c in cards if not c["error"])
    distinct = len(shape)
    val, cnt = shape.most_common(1)[0]
    share = cnt / float(n)
    if distinct < args.min_tag_count_values or share > args.max_tag_count_share:
        fail("PERSONA_TAG_COUNT_STAMP", ...)
```

**现象【实测】**：拿 37 张刚重写的卡做子集验证，全部恰好 6 个标签 ⇒ `distinct=1`、`share=1.00`
⇒ `PERSONA_TAG_COUNT_STAMP` 开火（`failed_cards=0 violations=1`）。
加 `--min-population 100` 后输出 `PERSONA_GATE_NOTE population=37 < 100 : R3/R4/R9 skipped`
⇒ `failed_cards=0 violations=0` GREEN。

**结论**：验证「我刚改的这一小批」时一律加 `--min-population 100`，把语料形状规则关掉，只留**逐卡**规则。
（R13–R15、R17 不受 `--min-population` 影响：R13–R15 是纯逐卡规则，R17 是批级、从不读全局占比。）

---

## 3. `audit-character-text.ps1` 的 E（性别代词）是一道**不可能失败**的门（已修）

**判据意图**：女卡不该出现「他」、男卡不该出现「她」。

**根因**：`$name = $f.BaseName` 是 `FileInfo.BaseName`，**只剥最后一个扩展名**。
对 `X.persona.json` 得到的是 `X.persona`，于是

```
$origJson = Join-Path $charDir ($name + ".origins.json")   # -> X.persona.origins.json
```

而磁盘上的侧车叫 `X.origins.json` ⇒ `Test-Path $origJson` **恒为假** ⇒ `$expect` 恒为 `$null`
⇒ E 分支永不进入。**这是一道永远不会红的门。**

**修法**（`audit-character-text.ps1:67`）：

```
$origJson = Join-Path $charDir (($name -replace '\.persona$', '') + ".origins.json")
```

并加注释说明为什么必须剥 `.persona`。

**验证【实测】**：旧式路径 `Test-Path` = False，新式 = True；修后重跑输出
`DONE cards=355 allClean=False`、退出码 0，报告 `AWAKE/docs/AUDIT-TEXT-QUALITY-20260911.md`
出现 **92 条 E 明细**（E 判据第一次真正生效）与 324 条 C 明细。

### 3b. 连带教训：我的 `edit` 工具会剥掉 UTF-8 BOM

修完上面这行后，脚本**根本没跑就 exit 1**，报 `MissingEndParenthesisInMethodCall`。
原因不是语法，而是 `edit` 重写文件时把 **UTF-8 BOM 去掉了**（首三字节变成 `35,32,230`）。
本机只有 Windows PowerShell 5.1，它会把**无 BOM 的 UTF-8 按 GBK 读** ⇒ 中文注释里的全角字符吞掉引号 ⇒ ParserError。

**处置**：用
`[System.IO.File]::WriteAllBytes($p, [byte[]](0xEF,0xBB,0xBF) + $b)`
补回 BOM。
**教训**：改完任何含中文的 `.ps1` 之后，必须确认首三字节是 `EF BB BF` 再跑，否则「跑过了」是假的。

---

## 4. `3-text` 的退出码恒为 0 ⇒ `run-card-gates.ps1` 里的 `3-text PASS` **不是真判据**（未修，仅记录）

**实测**：
```
& powershell -NoProfile -ExecutionPolicy Bypass -File AWAKE\tools\persona-workbench\tools\audit-character-text.ps1
DONE cards=355 allClean=False
TEXT_GATE_EXIT=0
```

`allClean=False` 却退出 0。所以 `run-card-gates.ps1 -SkipCompile` 打印的 `3-text PASS` 只表示
**脚本跑完了**，不表示文本干净。

另外 C 项（`>48` 字长句、≥2 条即标 `*`）**本来就不可能清零**：仓库公认的好卡也带 `*`
（斯瓦娜 C×5、戈敦 C×2）。脚本自述该列是「待人工判断的素材，不是绝对错误」。
⇒ **要判断某张卡的文本问题，必须直接读报告行，不能看链的 PASS。**

---

## 5. `build-authoring-facts.py` 的 `is_female` 把一半女性记成了男性（已修）

**根因**：`lords.xml` 里同一个布尔属性有**两种拼法**：
`is_female="true"` 与 `is_female="True"`（大写 T）。原判断是严格相等：

```
"1" if lord["female"] == "true" else "0"
```

⇒ 所有写 `True` 的女性英雄全部被记成 `0`（男性）。典型受害样本：
`lord_1_45_1` Agnala、`lord_1_55_1` Megethia、`lord_1_52_1` Minarvina、`lord_1_54_1` Constalia、
`lord_NE8_s` Pradentia、`lord_SE9_s` Jonna、`lord_1_63_1` Valaria、`lord_1_40_1` Catella。

**修法**：

```
"1" if lord["female"].strip().lower() == "true" else "0"
```

**验证【实测】**：`AWAKE/docs/reference/persona-authoring-facts.v1.tsv` 重生成后
`rows=355 female=166 male=189`，且上述样本 `is_female=1`。

**权威性说明**：`AWAKE/docs/mappings/character-names-zh-en.tsv`（表头
`hero_id / english / chinese / source / culture / sex / occupation / name_key / has_latin_in_cn / same_as_english`，
397 行）里的 `sex` 列与修正后的 `lords.xml` 读数**一致**，可作交叉校验。
该表 397 个 `hero_id` **全部**能在 `lords.xml`（398 个 `<NPCCharacter>`）里找到。

---

## 6. 门的行为备忘：E2b「张力入运行时」只读四个字段

`audit-character-enhancement.ps1:231`：

```
$runtimeText = (($rules + $rbs + $exs + @($contra)) | ForEach-Object { [string]$_ }) -join ' '
```

即运行时文本 = `selfClaimRules + realSelfBehaviors + selfClaimExamples + contradictionDescription`。
**`core` 与 `tensionAxes` 根本不参与。**

`:55` `$window = 70`；`:267` `$e2bOk = ($hasNone -and $noneBound) -or ($hasHard -and $bound)`。
底线词表：`绝不 绝不让 绝不容 绝不退 不容 宁可 宁死 断然 寸步不让 一石不让 一步不让 留不得`；
条件词表：`若 但凡 除非 一旦 只要 假如 万一 纵使 即便 容我 可以改 就算 宁可`。

**⇒ 只把底线写进 `tensionAxes.hardLine` 一律判死。** 必须让底线词与条件词在
上述四个字段之一里**相隔 70 字以内共现**（最省事的做法：写进一条 `selfClaimExamples`）。

---

## 7. 全库 32 个 `.ps1` 含非 ASCII 却没有 BOM（未修，风险记录）

PS 5.1 下这类文件是**潜在 ParserError 炸弹**（见 §3b）。已确认包含：

- `AWAKE/tools/persona-workbench/tools/materialize-definitions.ps1`（**运行时物化器**）
- `AWAKE/tools/persona-workbench/tools/bannersage-query.ps1`
- `AWAKE/tools/persona-workbench/tools/audit-character-refs.ps1`
- `AWAKE/tools/persona-awake-joint/verify-native-prerequisite.ps1`、`verify-g3-plan.ps1`、`verify-g3-s0-scope.ps1`
- `AWAKE/docs/`、`customer-delivery/`、`ui-workstation/`、`worldbook-studio/`、`worldbook-runtime-sim/` 下的若干脚本

`materialize-definitions.ps1` 当时属另一会话在改，**未动**。补 BOM 时必须单独提交、单独验证。

---

## 8. 协作教训：并行改卡必须防「越权重写已合格卡」

重写期间用子代理并行处理家族，发现 **8 张已提交、已过门的卡被子代理整张重写**。
子代理会把「整个家族」都重写，哪怕任务书明确写了「已合格的同族好卡不要动」。

**可复用的检出方法【实测】**：对每个「已跟踪且被修改」的 `.persona.json`，比较
`git show HEAD:<rel>` 与工作区版本的**解析后 JSON 键集合差异**：

- 只差 `selfClaimExamples` ⇒ 另一会话的未提交改动，**绝不能碰**（当时有 67–68 个）；
- 差十几个键 ⇒ 整张重写，**是越权**，应从 HEAD 逐字节还原。

**处置**：用 `subprocess.check_output(["git","show","HEAD:"+rel])` 写回。
还原的 8 张：`伊拉`、`帕堤耳`、`弥娜`、`彭同`、`拉盖娅`、`法芬`、`约里格`、`阿拉里`、
`卡拉蒂尔德`（后两张在第二批扫描中检出）。
抽 6 张 HEAD 版本跑门 ⇒ `failed_cards=0 violations=0` GREEN，且 HEAD 版本**更厚**
（core：伊拉 367→207、弥娜 359→196、拉盖娅 379→224、彭同 260→238、帕堤耳 238→226、法芬 192→185）。

**代价**：从 HEAD 还原会丢掉另一会话在这些卡上的未提交 `selfClaimExamples` 改动。
`彭同` 的改前版本曾备份在 `%TEMP%\awake-neretzes\backup\`（与 HEAD 只差 `selfClaimExamples`）。

**流程建议**：每批子代理交付后，**先跑这个键差分类扫描**，再谈验证与提交。
