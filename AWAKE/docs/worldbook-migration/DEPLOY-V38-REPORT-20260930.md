# v38 部署报告（R1 引文处置产物 → 仓库侧 → 游戏目录）

- **日期**：2026-09-30 16:54–16:56
- **产物**：`tools/worldbook-studio/workspace/full-geo1/compiled/geo1-v38-r1/`
- **指令**：甲方「继续做」
- **一句话**：游戏目录世界书从 **v37（790 条）→ v38（790 条）**，唯一内容变更 = `war.war-prisoners` 一条正文修正。

---

## 一、为什么值得部署（v38 是什么）

v38 是 **R1「一料多用」处置**（`docs/R1-REUSE-DISPOSAL-20260930.md`）的重编译产物。
本轮改动大部分只动**引文**（引文不进 runtime），真正改了 runtime 正文的只有一条：

| 项 | v37 | v38 |
|---|---|---|
| `war-prisoners` detail | 「沙漠里的部族**讲究赎**——贾瓦勒又名"漫游者"…」 | 「下令**处决俘虏**这件事不被赞成，还要折损领主的声望，除非那人确实罪无可赦…」 |
| 依据 | 引文只有「收商队保护费」，**不支撑「赎」这个断言** | 换成 `game-concept-strings.txt#死亡和继承`（逐字命中） |

⇒ **v38 修的是「引文不支撑断言」的硬伤**，不是文风调整。

---

## 二、部署链（两步，均已完成）

### 阶段一：产物 → 仓库侧
`tools/_deploy_v38_stage1_repo.py`（照 v37 范式）

```
[0] 源三文件齐；schemaVersion=awake.worldbook.v2 packageId=awake:worldbook.calradia
    源 runtime entries=790
    包内 hashes: manifestHash=67D39F44…  contentHash=BD05EC17…  packageHash=566B375A…
    vs v37 manifest.json  49eab0b3… -> 9972e220…  (已变)
    vs v37 runtime.json   bb8df569… -> eb87b44e…  (已变)
    vs v37 index.json     b14c9222… -> b14c9222…  (未变)
[1] 已复制 3 文件（字节一致）
[2] 包目录现含: ['index.json', 'manifest.json', 'runtime.json']
[3] registry 已同步（小写口径）
    manifestHash  67d39f44… (未变)
    contentHash   7d197815… -> bd05ec17…
    packageHash   5072d2a5… -> 566b375a…
[4] 目标 runtime entries=790 polity=5
    ✓ 含「处决俘虏」 ✓ 不含「沙漠部族讲究赎」 ✓ 不含「贾瓦勒」
```

### 阶段二：仓库侧 → 游戏目录
`AWAKE/tools/deploy_worldbook_to_game.ps1 -ConfirmDeploy`

```
DEPLOY_BACKUP manifest.json.before sha=2b1d370fb64b60e0
DEPLOY_COPY manifest.json sha=c0dd6227a78d1e10
DEPLOY_COPY packages\calradia files=index.json,manifest.json,runtime.json
DEPLOY_UNTOUCHED root_files=runtime.json,index.json,migration_report.json
DEPLOY_UNTOUCHED v1_dirs=rules,… files=755 (read by nothing under src/)
DEPLOY_UNTOUCHED persona_definitions definitions=77 (deployed by the persona line)
DEPLOY_ENTRYPOINT D:\SteamLibrary\…\Modules\AWAKE\ModuleData\Worldbook\manifest.json
DEPLOY_OK
```

---

## 三、部署后核验（四处逐字节相同）

| 文件 | compiled/v38 | 仓库侧 | 游戏侧 | 一致 |
|---|---|---|---|---|
| `manifest.json` | 9972e220b9c35f00 | 9972e220b9c35f00 | 9972e220b9c35f00 | ✓ |
| `runtime.json` | eb87b44ea7dcdc3a | eb87b44ea7dcdc3a | eb87b44ea7dcdc3a | ✓ |
| `index.json` | b14c9222c9893fa0 | b14c9222c9893fa0 | b14c9222c9893fa0 | ✓ |
| registry `manifest.json` | — | c0dd6227a78d1e10 | c0dd6227a78d1e10 | ✓ |

**三哈希口径**（registry 小写 vs 包内大写，逐条相符）：

| 字段 | 值 | 与 v37 比 |
|---|---|---|
| `manifestHash` | `67d39f44…` | **未变**（结构性） |
| `contentHash` | `bd05ec17…` | 变 |
| `packageHash` | `566b375a…` | 变 |

**游戏侧 runtime 读数**：`entries=790` / `identities=12` / `polity=5`；
`war-prisoners` 正文：含「处决俘虏」✓、不含「沙漠部族讲究赎」✓、不含「贾瓦勒」✓。

**校验链活性证明**（关键）：
- 部署**前** `-ValidateOnly` ⇒ **FAIL**（`deployed registry differs from source`）
- 部署**后** `-ValidateOnly` ⇒ **PASS**（`DEPLOY_VALIDATE_OK`）

---

## 四、备份与回滚

- **仓库侧备份**：`AWAKE/tools/worldbook-studio/artifacts/pre-v38-deploy-20260930-165339/repo-side/`（4 文件）
- **游戏侧备份**：`AWAKE/tools/worldbook-studio/artifacts/game-dir-deploy-20260930-165548/`（`manifest.json.before` 等）

回滚 = 把备份的 4 文件（仓库侧）+ 游戏侧备份文件按原路径拷回，再跑一次 `-ConfirmDeploy`。

---

## 五、版本时点表（更新）

| 时点 | 包内条数 | 说明 |
|---|---|---|
| 09-18 | 482 | v16，长期停在消费端 |
| 09-30 15:22 | 790 | v37（政体件），部署清 10 版欠账 |
| **09-30 16:56** | **790** | **v38（R1 引文处置），本条记录** |

### 5.1 ⚠️ 顺带查出：git 版本库里的世界包滞后两版

提交前逐文件比对发现，**`git` 里记录的世界包停在 v16（482 条）**：

| 文件 | git HEAD | 部署前仓库侧 | 部署后 |
|---|---|---|---|
| registry `manifest.json` | `94d6fecb…` | `49eab0b3…` | `c0dd6227…` |
| 包 `manifest.json` | `c8ad6fee…` | `49eab0b3…` | `9972e220…` |
| `runtime.json` | **`99d0731b…`（482 条那份）** | `bb8df569…`(v37/790) | `eb87b44e…`(v38/790) |
| `index.json` | `2af4c55d…` | `b14c9222…` | `b14c9222…`（未变） |

⇒ **v37 与 v38 两版部署都没进版本库**。本次提交会一并补齐。
⇒ 另注：`index.json` 的 git diff 显示为 M，但**部署前后逐字节相同**（`b14c9222…`）——
该 diff 来自 09-30 15:22 的 v37 那次未提交改动，非本次引入。

---

## 六、遗留

1. **68 个「2 档复用」** —— 同域相邻主题属可接受，尚未逐条登记理由。
2. **62 个 `clan-*` 家族档三层全无引文** —— 既有缺口，待甲方决定是否批量补料。
3. **真机确认未做** —— 需启动游戏看 `Awake.log` 的 `worldbook_runtime_initialized … entries=` 是否 790、
   `package=` 是否两段 `awake:worldbook.calradia`。本条需人工进游戏。
