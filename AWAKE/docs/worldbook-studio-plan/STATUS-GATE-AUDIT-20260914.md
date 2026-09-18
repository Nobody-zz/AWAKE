# Worldbook Studio 功能现状 + 质量闸实测（2026-09-14）

> 起因：问「现在世界书编辑器的功能情况怎么样」。本文件是这一问的完整账面。
> 全部结论出自**本机实测**（命令与日志见各节），不是从记忆或文档抄的。
> 本机环境：Windows PowerShell **5.1**（无 `pwsh`）、.NET SDK 10.0.301、仓库在 `D:\AWAKE-Dev`。

## 0. 一句话

编辑器本身**是完整可用的**（启动器 ＋ 浏览器界面 ＋ 命令行 ＋ 打包产物都在），功能面比 README 写的还多；
**坏的是它自己的质量闸**——构建与 153 条后端断言、36 条前端断言全过，但 `test.ps1` 这道总闸在这台机器上**立不起来**，
原因有三层，其中一层是本次改动引起的（已修）。

## 1. 编辑器实际是什么

| 件 | 位置 | 状态 |
|---|---|---|
| 启动器（WinForms，起 Web 进程 ＋ 开浏览器 ＋ 管工作区） | `src/Awake.WorldbookStudio.Launcher`（6 文件 1422 行） | 在 |
| 浏览器界面 | `src/Awake.WorldbookStudio.Web`（`wwwroot/index.html` 207 行 ＋ 20 个前端模块） | 在 |
| 核心（编译／编辑模型／审核／批量／AI） | `src/Awake.WorldbookStudio.Core`（80+ 文件） | 在 |
| 命令行（六步编译链） | `src/Awake.WorldbookStudio.Cli` | 在 |
| 自包含打包产物 | `artifacts/current-test/WorldbookStudio-win-x64.zip`（132 MB，09-14 12:45 打） | 在 |

界面提供的功能（从 README ＋ 新手指引 ＋ Web 端点清单核出）：新建档案 → 五步填写（标题／分类／**客观事实**／**NPC 表达**／**谁知道这条知识**）→ 按身份预览 → 校验 → 保存 → 导出；
AI 辅助分「本机 Worker」与「云端 Provider」两条路，**没有 AI 时编辑、校验、预览、导出仍可用**；
还有「导入资料 → 生成待审核草稿 → 逐条人工采纳 → 建档」的批量线（Quick Authoring）。
AI 草稿**必须逐条人工采纳**才进正典，不会被自动写入。

## 2. 实测跑了什么

| 项 | 命令 | 结果 |
|---|---|---|
| 构建 | `dotnet build Awake.WorldbookStudio.slnx -c Release` | **11 工程 0 警告 0 错误**（8 秒） |
| 前端 harness | `scripts/test.ps1` 前段（node） | **36 条断言全过**（6 个 harness：editor-session 12／editor-safety 4／draft-race／draft-dom-state 8／batch-race／editor-content／customer-closure） |
| 后端测试 A | `EditorContent.Tests` | **11/11** |
| 后端测试 B | `BatchTests` | **23/23** |
| 后端测试 C | `Draft.Tests` | **107/107** |
| 后端测试 D | `Workstation.Tests` | **12/12** |
| 后端测试 E | `Awake.WorldbookStudio.Tests`（F01–F74 ＋ HTTP ＋ A1/A2/A3.1/A3.2/A3.3） | **111 条通过后中断**，见 §3.1 / §3.3 |
| 5 个端到端 HTTP 冒烟 | `scripts/test.ps1` 后段 | **一个都跑不起来**，见 §3.2 |
| 打包前的总检查 | `scripts/release-check.ps1` | 红（`WB-RELEASE-020: 当前源码 test.ps1 失败`），见 §3 |

日志落在 `tools/worldbook-studio/artifacts/`：`_studio_test_all_20260914.log`、`_studio_remaining_tests_20260914.log`、
`_studio_smokes_20260914.log`、`_studio_release_check_20260914.log`、`_studio_main_tests_after_fix_20260914.log`、
`_probe_web_smoke_20260914.log`、`_probe_save_route_20260914.log`、`_parse_check_20260914.log`。

## 3. 三处红灯，三层不同的真因

### 3.1 R1 · F24「compiled runtime package contract」——**本次改动引起，已修**

**现象**：`FAIL F24 compiled runtime package contract: runtime contract invalid: ... properties: Some properties did not match the required schema: ["packageId"]; /packageId; pattern: ...`

**真因**：09-14 把包身份写成常量时，`packageId = "awake:worldbook:calradia"` 是**三段**（两个冒号），
而契约里 `package_id` 的 pattern 只收**两段**：

```
common.schema.json        $defs/package_id  ^[a-z][a-z0-9_-]*:[a-z][a-z0-9_.-]*$
runtime.schema.json       $defs/package_id  （同一 pattern，本地复制了一份，不是 $ref）
```

对照：`worldId` 走的是 `stable_id`（`^...[a-z0-9_-]*:[a-z0-9_.-]*$` 之上的**三段**形）所以 `awake:world:calradia` 合法；
`packageId` 走 `package_id`（两段）所以 `awake:worldbook:calradia` 不合法。**两者规则本来就不同**。
旧代码是 `packageId = "awake:{universe}"`（两段，例如 `awake:awake_current`），一直合法 —— 09-14 的定名**没有对照这个 pattern**。

**修法（已落）**：放宽 `package_id`，**可加不可减**（既有两段 id 仍然合法）：

```
^[a-z][a-z0-9_-]*:([a-z][a-z0-9_-]*:)?[a-z][a-z0-9_.-]*$
```

改了两个文件：`tools/worldbook-contract/v1/common.schema.json`、`tools/worldbook-contract/v1/runtime.schema.json`。
选「放宽契约」而不是「改掉身份名」的理由：① 身份名是 09-14 已拍板并写进 `RUNTIME-MAPPING-CONTRACT.md` 的；
② 放宽是**超集**，不会让任何既有产物失效；③ 见 §3.1.1，游戏侧根本不校验它。

**为什么别的判据都没察觉**（这是本次最值得记的一条）：

| 环节 | 是否校验 `package_id` |
|---|---|
| 编译链（`compile`） | ✗ 不校验 v2 包 |
| `tools/worldbook-runtime-smoke`（离线阅读器） | ✗ 只重算 hash ＋ 读条目 |
| **游戏侧（主干）** | ✗ **没有任何 schema 校验器**；`WorldbookPackageIntegrity.cs` 只查「非空 ＋ 跨文件一致」；`Modules/AWAKE` 里**不带任何 `.schema.json`** |
| **Studio 测试 F24** | **✓ 唯一** |

⇒ 整套 `tools/worldbook-contract/v1/*.schema.json` **只在 Studio 的测试里被执行一次**（`FindContractRoot` 往上找 `tools/worldbook-contract/v1`）。
**游戏收得下这个 id**（这也是世界书线离线冒烟一直是绿的原因），所以这是一条**只在测试里成立的**红线。

**验证**：改前 F24 红 → 改后 **111 条全过、F24 绿**。F24 曾**确实红过**，所以它不是恒绿判据。

### 3.2 R2 · 5 个端到端冒烟在这台机器上跑不起来——**两层原因，都与本机环境有关**

**(a) 硬要 PowerShell 7（本机没有）**

```
type -a pwsh            → NOT FOUND
powershell (5.1.26100.9444) → C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe
```

- `draft-workflow-smoke.ps1:414`、`batch-workflow-smoke.ps1:231` 写的是 `(Get-Command pwsh -ErrorAction Stop).Source` ⇒ 直接 `CommandNotFoundException`。
- 连带：`scripts/*.ps1` 与文档里所有 `pwsh -NoProfile -File ...` 的**复跑命令在这台机器上照抄跑不起来**
  （`RESULT-QUICK-AUTHORING-E2E-20260911.md` 的「如何复跑」就是这套写法）。

**(b) PS 5.1 与 PS 7 的行为差异（实测）**

探针 `artifacts/_probe_web_smoke_20260914.ps1` 起真 Web 服务后逐条打：

```
--- retired-route (expect 410 JSON)     POST /api/ai/batch/test/start
    THROWN type=System.Net.WebException
    ErrorDetails is null = True            ← 关键
    Exception.Response null = False
    Response.StatusCode=410
--- document (expect 200)               → OK status=200 content_len=3760
```

⇒ 在 **Windows PowerShell 5.1** 里，`Invoke-WebRequest -UseBasicParsing` 遇到非 2xx 时抛 `WebException`，
**`$_.ErrorDetails` 是 null**（PS 7 才有）。而这几个冒烟脚本的错误路径全都依赖它取响应体：

```
$content = $_.ErrorDetails.Message      # PS 5.1 下 = null
$body = $response.Content | ConvertFrom-Json   # → 无法将 null 绑定到 InputObject
```

命中者：`public-contract-smoke.ps1`（期望 410）、`authoring-save-smoke.ps1`（后面的 422/409 用例）。

**(c) 中文脚本缺 BOM ⇒ PS 5.1 直接解析失败**

纯解析检查（`artifacts/_parse_check_20260914.log`，用 `[Language.Parser]::ParseFile`）：

```
   5   workstation-handoff-loopback-smoke.ps1   表达式或语句中包含意外的标记 “:"persona-1"”
   0   draft-workflow-smoke.ps1
   0   batch-workflow-smoke.ps1
   0   authoring-save-smoke.ps1
   0   public-contract-smoke.ps1
   0   test.ps1
```

`workstation-handoff-loopback-smoke.ps1` 含中文且**无 BOM** ⇒ PS 5.1 按 GBK 读 ⇒ 引号被咬 ⇒ 5 个语法错。
全库同类（中文但无 BOM）还有：`_cloud-batch-smoke`、`author-loop-acceptance`、`batch3-runtime-keywords-check`、
`pravend-expected-facts-check`、`real-worker-pravend-smoke`、`real-worker-worldbook-smoke`。

**(d) 顺手排除：保存功能本身是好的**

`artifacts/_probe_save_route_20260914.ps1` 对 `POST /api/authoring/save-authoring` 打真请求：

```
initial revision=1  hash_len=64  content_len=1663
SAVE OK status=200 content_len=6105
{"ok":true, ... "revision": 2 ... "text":{"zh-CN":"第一次更新"} ...}
```

⇒ 保存路径正常（status 200、`ok:true`、revision 1→2、正文回读正确）。
所以 `authoring-save-smoke` 的死因**不是**保存坏了，而是 (b)。

### 3.3 R3 · A3.3「preview golden」——**跨工作副本的快照，与环境有关，未修**

**现象**：`FAIL A3.3 preview golden: baseline_commoner input file closure/hash changed`

**真因（实测）**：golden 里记录的输入文件闭包，路径**写死了旧工作副本的绝对位置**：

```
../../../../../OneDrive/文档/New project/_houkai_merge/AWAKE/docs/worldbook-studio-plan/knowledge-taxonomy.v1.json
（共 16 条，全部是这个形状）
```

判据 `AssertA33InputFiles`（`Program.cs:2504`）比的是 `Path.GetRelativePath(fixture.Root, path)` —— 相对的是**临时 fixture 根**。
仓库一旦从 `C:\Users\...\OneDrive\文档\New project\_houkai_merge\AWAKE` 挪到 `D:\AWAKE-Dev\AWAKE`，
相对串必然不同 ⇒ **与内容无关，纯位置相关**。这就是 09-14 22:09 那份 `_refresh_package_taxonomy_20260914.ps1` 里记的那笔
「PRE-EXISTING debt（A3.3 preview golden, cross-repo snapshot）」——**当时是推断，现在是实测。**

> 附带更正：当时那份脚本说 release-check 红是「因为 A3.3」。实际那一刻 **F24 也红、而且排得更前**
> （`release-check` 的证据文件 `worldbook-studio-release-check-test-a6bc29d2...txt` 里写的就是 F24）。
> 两个都红，A3.3 不是当时的阻断点。

**修法建议（未做，见 §5）**：让闭包**与位置无关** —— 只记 `<文件名> ＋ sha256`，或把 schema 文件先拷进 workspace 再按内部相对路径记。
**不建议**就地重采 golden：那只是把同一个缺陷重新冻结一次，下次挪仓库再红。

## 4. 本次改动清单

| 文件 | 改什么 |
|---|---|
| `tools/worldbook-contract/v1/common.schema.json` | `$defs/package_id` 的 pattern 放宽为可收三段（超集） |
| `tools/worldbook-contract/v1/runtime.schema.json` | 同上（该文件的 `$defs/package_id` 是本地复制的一份） |
| `tools/worldbook-studio/artifacts/_probe_web_smoke_20260914.ps1` | 新增：起真 Web 服务、逐条打印非 2xx 的异常形态（探针，留档） |
| `tools/worldbook-studio/artifacts/_probe_save_route_20260914.ps1` | 新增：验证保存接口在 PS 5.1 下确实返回 200（探针，留档） |
| `tools/worldbook-studio/artifacts/_check_a33_golden_20260914.py` | 新增：比对 A3.3 golden 的输入闭包与磁盘（探针，留档） |

性质：**契约放宽一处（三边共享文件，可加不可减，已在本文件写明）**；其余是探针留档。
`RuntimePackageCompiler.cs`（09-14 的身份常量改动）**未回退**，因为问题不在它。

## 5. 待决 / 未验

1. **`package_id` 放宽要不要保留？**（三边共享契约）
   备选是改掉身份名本身（两段拼法），但那要动 `RUNTIME-MAPPING-CONTRACT.md` 的身份表 ＋ `AWAKE-ROADMAP.md:100`
   ＋ 重编译重投包（三件套带 id，hash 全变）。**建议保留放宽** —— 游戏侧不校验，改动更小。
2. **冒烟脚本怎么修？**（二选一）
   - ① 本机装 PowerShell 7（一处，机器级）；
   - ② 把这 5 个脚本改成 5.1 也能跑（`pwsh` 回退 `powershell.exe`；非 2xx 改从 `$_.Exception.Response.GetResponseStream()` 读体；
     中文脚本补 BOM）。**建议 ②** —— 不依赖「某台机器装了什么」。
3. **A3.3 要不要按 §3.3 改成位置无关？（未做）** 要动判据 ＋ 重采 golden，属「改判据把红变绿」，**须经确认**。
4. **未跑到的部分**（诚实边界）：`test.ps1` 在 A3.3 处中断 ⇒ A3.3 之后的用例、以及 5 个 HTTP 冒烟
   **这一轮既没通过也没失败，是没跑到**。`release-check.ps1` 在 `test.ps1` 这道闸之后就停 ⇒ 它后面的闸
   （含网络边界、包清单等）**这一轮同样没验到**。
5. `artifacts/current-test/CURRENT-TEST-PACKAGE.json` **缺失**：该文件只由 `package.ps1` 写（`package.ps1:219`），
   而 `package.ps1` 会调 `release-check.ps1` ⇒ 闸红 ⇒ 写不到。README 第 17–20 行指向它，**当前是断的**。
