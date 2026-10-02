# persona-awake-joint 投送链：根因、修复与夹具三分类

日期：2026-10-02　范围：`AWAKE/tools/persona-awake-joint/`　证据等级：E2（离线实跑）

> 本文只记录**实测**结果。所有读数都给出原样命令，任何人都能复跑。
> 本文不修改任何夹具内容、不改契约、不动 `docs/fixtures/`。

---

## 一、路线图的归因是错的

`docs/AWAKE-ROADMAP.md` 把这条链跑不起来归因为「脚本硬调 `pwsh`」。**这是症状，不是病因。**

实测（PowerShell 5.1 探针）：

| 探测 | 结果 |
| --- | --- |
| `$PSVersionTable` | `5.1.26100.9444` / `PSEdition=Desktop` / `CLR=4.0.30319.42000` |
| `[System.Reflection.Assembly]::LoadWithPartialName('System.Text.Json')` | `NULL` |
| `'System.Text.Json.JsonDocument' -as [type]` | `NULL` |
| `Add-Type` 引用 `System.Text.Json` | `命名空间"System.Text"中不存在类型或命名空间名称"Json"` |
| `Get-Command pwsh` | 不在 PATH |
| `C:\Program Files\PowerShell\7\pwsh.exe` | 不存在 |

`persona-awake-joint.ps1:22-141` 内嵌 C# 类 `AwakePersonaJointCanonicalJson`，用了 PS 5.1 的 C# 5 编译器不支持的特性：

- `:29` `using System.Text.Json;` —— **.NET Framework 上根本不存在这个程序集**
- `:37` `using JsonDocument document = JsonDocument.Parse(...)` —— C# 8 using 声明
- `:129-131` 内联 `out` 变量 —— C# 7

⇒ 这条链的设计前提是 **PowerShell 7+ / .NET 5+**。硬调 `pwsh` 只是这个前提的症状。
**不能**靠改那 6 处 `& pwsh` 修好；也**不能**换掉 `System.Text.Json` 规范化器 —— 那会改变 canonical 字节，
连带作废 `contractLockSha256`、crosswalk 的 `registrySha256` 和全部夹具期望哈希。

### 修复：装 PowerShell 7

```
winget install --id Microsoft.PowerShell --scope user --accept-package-agreements --accept-source-agreements --disable-interactivity
```

实测装上 **7.6.6**（`pwsh -NoProfile` 探针报 `VER=7.6.6` / `EDITION=Core` /
`STJ=System.Text.Json.JsonDocument, System.Text.Json, Version=10.0.0.0`）。

装完后：

```
pwsh -NoProfile -File tools\persona-awake-joint\verify-contract.ps1 -ReportPath <绝对路径>\vc.json
```

⇒ `status=pass exitCode=0 assertions=47`，0 失败，0 错误。

---

## 二、静默 exit 40：根因与诊断补丁

### 现象

```
pwsh -NoProfile -File <tool>\verify-contract.ps1 -ReportPath '.\artifacts\vc.json'
```

⇒ **exit 40，stdout 与 stderr 全空，报告文件不生成。**

### 根因链

1. `Assert-JointOutputPath`（`persona-awake-joint.ps1:200`）→ `Get-JointFullPath`（`:161`），
   后者的 `$BasePath` **默认是 `$script:JointWorkspaceRoot`**。
2. 相对路径 `.\artifacts\vc.json` 被拼成 `D:\AWAKE-Dev\.\artifacts\vc.json`，不在 tool root 之下
   ⇒ `Throw-JointReject 'persona.path_protected'`。
3. 底层机制：`[IO.Path]::GetFullPath` 用的是 `[Environment]::CurrentDirectory`（进程 cwd），
   而 **PowerShell 的 `Set-Location` 不会更新它**。实测 `PSLocation=D:\AWAKE-Dev\AWAKE\tools\persona-awake-joint`
   但 `ProcessCWD=[D:\AWAKE-Dev]`。
4. `verify-contract.ps1:150` 的 `finally` 只在 `if ($null -ne $reportPathFull)` 时写报告。
   路径被拒 ⇒ 不写报告、不打任何字 ⇒ 只剩 exit 40。

**⇒ 链没有坏，是调用方传了错的相对路径。** 但「路径被拒就彻底静默」是个真实的可用性缺陷。

### 补丁

| 文件 | 改动 |
| --- | --- |
| `tools/persona-awake-joint/verify-contract.ps1` | `finally` 加 `else` 分支：路径被拒时往 **stderr** 打两行（状态/退出码/原因 + `-ReportPath` 必须落在 tool root 之下） |
| `tools/persona-awake-joint/verify-e2-matrix.ps1` | 同上 |

故意**不写报告** —— 写到 tool root 之外正是路径守卫要禁止的行为。

实测回归：

```
verify-contract: error/40 :: Output must remain under tools/persona-awake-joint.
verify-contract: -ReportPath must resolve under D:\AWAKE-Dev\AWAKE\tools\persona-awake-joint; pass an absolute path.
```

happy path 未变（`status=pass exitCode=0 assertions=47`）。两文件仍为纯 ASCII、无 BOM。

---

## 三、E2 矩阵 23 个夹具三分类

原读数：`fixtureCount=23 matched=16 unexpected=7`。逐夹具查完，7 个「unexpected」是**三种完全不同的东西**。

现读数（修复后）：**`matched=16 stub=3 drift=1 unexpected=4`**（16 + 3 + 4 = 23；`drift` 是叠加计数）。

### 3.1 矩阵读错键名造成的假红（2 个）→ 已修

三个 `-g3-` 夹具的 `expected.json` 用 `expectedStatus` / `expectedExitCode`，
而其余 20 个用 `status` / `exitCode`。`verify-e2-matrix.ps1:93-94` 只读后一套 ⇒ 读到 `''` / `0`
⇒ 与实测 `reject` / `10` 不符 ⇒ 判 unexpected。

**修法**：两套键名都认（`status` 为空时回退 `expectedStatus`；`exitCode` 不存在时回退 `expectedExitCode`）。

### 3.2 但修完 017/018 变成「matched」是**假绿** ⇒ 补 `fixture_stub` 分类

017 / 018 的 `input.json` **既没有 `gate` 也没有 `operation`**。
`run-fixtures.ps1:281` `$gate = Get-JointRequiredString $inputObject 'gate' '$'` 直接抛
`persona.schema_required_field` ⇒ `reject/10`。它们的 `expected.json` 恰好也声明 `reject/10`
⇒ 键名修好后**因为错误的原因匹配**。

判据（从源码推出，不是猜的）：`run-fixtures.ps1` 在 `:281-292` 解析完输入后，才会在 `:348`（成功路径）
或 `:382`（失败路径）调 `Add-JointExpectedFixtureAssertions`，而该函数只要 `expected.json` 存在就必然贡献
`expected_status_match`。**报告里没有 `expected_status_match` ⇒ 运行器在输入契约上就中止了，夹具根本没执行。**

**修法**：`verify-e2-matrix.ps1` 增加 `fixture_stub` 分类（计入 `stubCount`，**不计入** matched 也不计入 unexpected），
并发 `persona.e2_matrix_fixture_stub` 告警；结果行新增 `executed` 字段。

### 3.3 三个夹具是空壳（`fixture_stub`）

| 夹具 | `gate` | `operation` | 声明 | 实测 |
| --- | --- | --- | --- | --- |
| `PWB-AWAKE-017-g3-scope-authority` | 缺 | 缺 | reject/10 | reject/10（输入契约中止） |
| `PWB-AWAKE-018-g3-a0-static-gates` | 缺 | 缺 | reject/10 | reject/10（输入契约中止） |
| `PWB-AWAKE-021-g3-g0-completion` | 缺 | `completion_record_validation` | pass/0 | reject/10（输入契约中止） |

三者都只有 `input.json` + `expected.json`，**没有 `source.json` / `README.md` / `manifest.sha256.txt`**。
即便补上 `gate`：017/018 会撞 `$.operation` 缺失；021 会撞
`Invoke-JointNonMigrationFixture`（`:173`）的 `persona.operation_unsupported`
—— 该函数只处理 `validate-export-plan` / `reload-runtime-bundle` / `load-runtime-entry` / `frozen-candidate-isolation` 四种操作。

⇒ **这三个夹具写在功能之前**，在对应 G3 操作实现出来之前不可能通过。这不是工具缺陷，也不是夹具写错，
是**未实现**。已如实归类，不再计成 matched 或 unexpected。

### 3.4 一个冻结基线漂移（`protected_baseline_drift`，不计 unexpected）

`PWB-AWAKE-013-frozen-candidate-isolation` 声明 `protectedRoots[0].beforeSha256 = 1E08A7A86916BD3C...`，
而实测源码树 `82181BC849F26BB8190302849CD90D5E0947B2E51BA7FCE590B7113A4AC81A11`。
运行器按设计**保护它、不重写它**，并把观测差异归为 `persona.frozen_baseline_drift` 告警。
这是有意的安全行为（冻结候选不得被运行器改写），所以单独计数。

### 3.5 一个夹具自身内容与钉值不一致（001）

`PWB-AWAKE-001-valid-approved`：`manifest.sha256.txt` 钉 `source.json = F9EF9D06915D6F67...`，
磁盘实际 `D9E63E3457C9AE17...`（1906 字节）；同一 manifest 钉 `input.json = 49956AA3...`，实际 `B1DB1C28...`。
`expected.json` / `README.md` / `registry.json` 三条与磁盘**完全相符**。

排除项：**不是换行符漂移** —— `git ls-files --eol` 显示该目录 `i/lf w/lf`，LF 归一后哈希不变；
`git hash-object` 工作区与 `HEAD:` 五个文件全部相同 ⇒ **已提交状态即如此**，
该目录只有 `27506af` 与 `69b9fc7` 两次提交。任何工具侧改动都修不好它。

### 3.6 三个夹具被同一个未知字段拦住（004 / 014 / 016）

三个都报 `persona.schema_unknown_field`，路径**完全相同**：
`$.crosswalk.targetRegistry.tags[13].conflicts`。它们本要验的规则是
`persona.unknown_tag` / `persona.approval_missing` / `persona.rule_unsupported_for_definition_v1`
—— 一个都没走到。

⇒ 见下节：这是**契约与数据不一致**，不是夹具写错。

---

## 四、未解阻塞：注册表 `conflicts` 字段三方不一致（需 owner 拍板）

| 方 | 立场 |
| --- | --- |
| 契约 `docs/PLAN-PersonaWorkbench-AWAKE-Joint-CONTRACT-LOCK-20260823.md:325` | 注册表形状是 `{ schemaVersion, tags: [{id, category, displayName, meaning, promptText}], bundles: [{id, displayName, tags}] }` —— **没有 `conflicts`** |
| 校验器 `tools/persona-awake-joint/persona-awake-joint.ps1:611` | `Assert-JointExactFields $tag @('id','category','displayName','meaning','promptText') ...` ⇒ 出现 `conflicts` 即抛 |
| 实际数据 `ModuleData/Worldbook/persona_definitions/tag_registry.json` | 至少一个 tag 带 `conflicts`：`tags[13].id = expression.indirect`，`conflicts = expression.direct` |
| 运行时代码 `src/PersonaTagRegistry.cs:42-67` `TryExpand` | **消费 `conflicts`**；`src/PersonaDslGenerator.cs:127 if (!expanded || hasConflict)` |

⇒ 两难：改校验器（放行 `conflicts`）会改变 canonical 字节、作废 `contractLockSha256` 与 crosswalk 的 `registrySha256`；
删注册表里的 `conflicts` 会**破坏运行时的冲突检测**（`expression.direct ↔ expression.indirect` 是注册表里唯一的冲突对）。

**我没有动任何一方。** 这是契约层面的决定，属于该链 owner 的职责范围。

---

## 五、复现命令

```powershell
$root = 'D:\AWAKE-Dev\AWAKE\tools\persona-awake-joint'
# 契约自检（必须给绝对路径，且必须落在 $root 之下）
pwsh -NoProfile -File "$root\verify-contract.ps1"  -ReportPath "$root\artifacts\vc.json"
# E2 矩阵（同上）
pwsh -NoProfile -File "$root\verify-e2-matrix.ps1" -ReportPath "$root\artifacts\e2.json"
# 只看分类
(Get-Content "$root\artifacts\e2.json" -Raw -Encoding UTF8 | ConvertFrom-Json) |
    Select-Object fixtureCount, matchedCount, stubCount, protectedBaselineDriftCount, unexpectedCount
```

`artifacts/` 被 `.gitignore:61` 排除，报告不会污染工作区。

---

## 六、已知局限

1. **`fixture_stub` 的判据是「报告缺 `expected_status_match`」**，它证明运行器在输入契约上中止了，
   但**不能**区分「夹具是空壳」与「输入契约被工具收紧」两种成因。两者都会让这个判据为真。
2. **本次没有修改任何夹具、任何契约、任何期望哈希**，所以 3.5 / 3.6 的两个不一致仍然存在。
3. **`verify-e2-matrix.ps1` 只跑离线 E2 层**，不构成真机（E4）或存读档（E5）证据。
4. **`stubCount` 不是通过标准**：三个空壳夹具目前合法地计入 stub，一旦对应功能实现，
   它们应当**从 stub 变成 matched 或 unexpected**，届时这个数字的变化本身就是进度信号。
