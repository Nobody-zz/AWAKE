# 世界书 v37 部署报告（2026-09-30）

> 甲方 09-30 指令：**「部署。」**
> 本轮把 v37 全量编译产物（政体件文本质量整改后）落进**仓库侧部署根**并投送到**游戏目录**，
> 一举清掉自 09-18 起累积的 **离线欠账**。

---

## 一、一句话结果

**游戏目录里的世界书包，从 482 条（09-18，v16）升到 790 条（09-30，v37）——落后 10 个版本的欠账已清。**

| 位置 | 部署前 | 部署后 |
|---|---|---|
| 仓库侧 `AWAKE/ModuleData/Worldbook/packages/calradia/` | 482 条（09-18 01:17，`contentHash ac2d2830…`） | **790 条**（`contentHash 7d197815…`） |
| 游戏目录 `…\Modules\AWAKE\ModuleData\Worldbook\packages\calradia\` | 482 条（09-18 01:17，与仓库侧同） | **790 条**（与仓库侧逐字节一致） |

---

## 二、部署链（两步，都是既定入口）

```
compiled/geo1-v37-polity-qa/            ← Studio compile 产物（790 条）
        │  ① 世界书线：字节复制 3 文件 + 同步 registry 三哈希
        ▼
AWAKE/ModuleData/Worldbook/             ← 仓库侧唯一根（契约：RUNTIME-MAPPING-CONTRACT.md）
        │  ② tools/deploy_worldbook_to_game.ps1 -ConfirmDeploy
        ▼
…\Modules\AWAKE\ModuleData\Worldbook/   ← 游戏实际读取的包
```

**包只认 3 个文件**（`manifest.json` ＋ `entrypoints.runtime` ＋ `entrypoints.index`）——
`WorldbookPackageIntegrity.ReadAndVerify` **不扫目录**，故报告文件（`validation.json` /
`content-graph.json` / `SHA256SUMS.txt` 等 9 个）留在 tools 侧不入库。

---

## 三、阶段一：产物 → 仓库侧（`tools/_deploy_v37_stage1_repo.py`）

字节复制，不重序列化（三哈希按字节算）。

| 文件 | 部署前 SHA256(前 16) | 部署后 SHA256(前 16) |
|---|---|---|
| `packages/calradia/manifest.json` | `C8AD6FEE755E57F0` | `49EAB0B3BECEEE43` |
| `packages/calradia/runtime.json` | `99D0731B29B82942` | `BB8DF56912B731D9` |
| `packages/calradia/index.json` | `2AF4C55D58B4ACFC` | `B14C9222C9893FA0` |
| `manifest.json`（registry） | `94D6FECBD665FBE6` | `2B1D370FB64B60E0` |

**registry 三哈希同步**（registry 用**小写**、包内 `hashes` 用**大写**，仅大小写不同）：

| 字段 | 部署前 | 部署后 | 说明 |
|---|---|---|---|
| `manifestHash` | `67d39f44…` | `67d39f44…` | **未变** —— 清单结构没动 |
| `contentHash` | `ac2d2830…` | `7d197815…` | 变（正文改了） |
| `packageHash` | `ea9cb5b1…` | `5072d2a5…` | 变 |

回读校验：registry 三值 == 包内 `manifest.hashes` 三值 ✓

### 记账口径（本轮弄清）

- **compile 返回的 `manifest_hash`（`49eab0b3…`）= `packages/calradia/manifest.json` 文件的字节 SHA256**（已实测）。
- 而包内 `hashes.manifestHash`（`67d39f44…`）是**另一套口径**（结构性哈希）——
  **内容变它不变**，所以 `module` 结构没动时 registry 的 `manifestHash` 也不用改。
- 玩家看得见的是 `runtime.json`（`entries` / `expressions` / `index.json`），
  **`assertions[].text`（YAML 里那个平铺正文）不进包、也不参与三哈希**（09-18 发现二）。
  本轮改的是 `assertions[].expressions[].text` ⇒ **在三哈希里看得见**（三哈希确实全变）。

---

## 四、阶段二：仓库侧 → 游戏目录（`deploy_worldbook_to_game.ps1 -ConfirmDeploy`）

脚本自带四道闸，全过：

1. `Bannerlord` / `TaleWorlds` 进程检查 —— **running=0** ✓
2. game module 目录结构检查（grandparent 存在） ✓
3. **旧 registry 自动备份** → `artifacts/game-dir-deploy-20260930-152247/manifest.json.before`（`sha 94d6fecb…`） ✓
4. 逐文件 SHA256 复制校验 + 包目录「文件集必须恰好 = 3」硬断言 ✓

脚本输出（节选）：

```
DEPLOY_SOURCE root=…\AWAKE\ModuleData\Worldbook registry=awake:registry:installed packages=1
DEPLOY_COPY manifest.json sha=2b1d370fb64b60e0
DEPLOY_COPY packages\calradia files=index.json,manifest.json,runtime.json
DEPLOY_UNTOUCHED root_files=runtime.json,index.json,migration_report.json
DEPLOY_UNTOUCHED v1_dirs=rules,personality_background,… files=755 (read by nothing under src/)
DEPLOY_UNTOUCHED persona_definitions definitions=77 (deployed by the persona line)
DEPLOY_OK root=…\Modules\AWAKE\ModuleData\Worldbook packages=1
```

**未动**（刻意）：根目录 `runtime.json`/`index.json`/`migration_report.json`（旧平铺 v2 pilot 遗留）、
8 个 v1 内容目录（755 文件，`src/` 下无读取方）、`persona_definitions/`（角色卡线的资产，有自己的审批门）。

---

## 五、阶段三：核验读数

### 5.1 三处逐字节一致

| 文件 | 产物 | 仓库侧 | 游戏侧 |
|---|---|---|---|
| `packages/calradia/manifest.json` | `49EAB0B3…` | `49EAB0B3…` | `49EAB0B3…` |
| `packages/calradia/runtime.json` | `BB8DF569…` | `BB8DF569…` | `BB8DF569…` |
| `packages/calradia/index.json` | `B14C9222…` | `B14C9222…` | `B14C9222…` |
| `manifest.json` | —（非产物） | `2B1D370F…` | `2B1D370F…` |

### 5.2 游戏侧 runtime 回读

| 项 | 读数 |
|---|---|
| `entries` | **790** |
| `identities` | 12（含 anonymous） |
| polity 条目 | **5**（`politics.polity-{senate,feudal,tribal,sacred-kingship,pastoral}`） |

**域分布（这是本次最直观的收益）**：

| domain | 部署前（482） | **部署后（790）** | 变化 |
|---|---|---|---|
| geography | 411 | 416 | +5 |
| **economy** | 21 | **151** | **+130** |
| **politics** | 7 | **132** | **+125** |
| **war** | 37 | **71** | **+34** |
| **culture** | 6 | **20** | **+14** |

⇒ 单支柱（geography 85.3%）→ 双支柱（geography 52.7%），politics 从 1.5% 涨到 16.7%。

### 5.3 新文本落包（游戏侧实读）

| 标记 | 结果 |
|---|---|
| 王国律法（senate） | ✓ 在包内 |
| 拿土地换效忠（feudal） | ✓ 在包内 |
| 松散的部族联盟（tribal） | ✓ 在包内 |
| 借神意抬举王位（sacred-kingship） | ✓ 在包内 |
| 可汗的铁律（pastoral） | ✓ 在包内 |
| ~~政治神学~~（已删的学术腔硬伤） | ✗ 不在 ✓ |
| ~~行会律法~~（已删的术语张冠李戴） | ✗ 不在 ✓ |

### 5.4 脚本自带复核

```
DEPLOY_VALIDATE_OK root=…\Modules\AWAKE\ModuleData\Worldbook packages=1
```

> 同一个 `-ValidateOnly` 命令**部署前 FAIL、部署后 PASS**，本身即校验链活性证明。

---

## 六、备份（回滚入口）

| 备份 | 位置 | 内容 |
|---|---|---|
| 部署前（仓库侧 + 游戏侧） | `tools/worldbook-studio/artifacts/pre-v37-deploy-20260930-152138/` | `repo-side/`（旧 registry + 旧包 3 文件）、`game-side/`（同上） |
| 部署时（脚本自建） | `tools/worldbook-studio/artifacts/game-dir-deploy-20260930-152247/` | `manifest.json.before` |

回滚 = 把 `pre-v37-deploy-*` 的 `repo-side/` 灌回仓库侧、再跑一次 `-ConfirmDeploy`。

---

## 七、复现

```bash
# ① 产物 -> 仓库侧（幂等，可复跑）
python -u D:/AWAKE-Dev/tools/_deploy_v37_stage1_repo.py

# ② 仓库侧 -> 游戏目录
powershell -File D:/AWAKE-Dev/AWAKE/tools/deploy_worldbook_to_game.ps1 -ValidateOnly
powershell -File D:/AWAKE-Dev/AWAKE/tools/deploy_worldbook_to_game.ps1 -ConfirmDeploy
```

---

## 八、遗留

1. **`docs/AWAKE-ROADMAP.md` 现状节**仍写着「游戏目录那份包还是 448」——
   该句已被本轮推翻，需同步（本报告即依据）。
2. `docs/worldbook-migration/AUDIT-WORLDBOOK-PROGRESS-20260930.md` 的 §零表停在 v35（792）——
   可补入 v36(790) / v37(790)。
3. 仍有 9 条 `goods-*` 一料多用（规范 R1 拆引文）、tools 侧 12 灰色件 + 10 断链待处置。
