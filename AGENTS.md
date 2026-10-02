# AWAKE 独立开发工作区 · 任务准则

> 本目录是 AWAKE（Awakened World AI / 醒世）的独立开发工作区，2026-09-11 从 OneDrive 的旧综合工作区拆出。
> 全局 `~/.codex/AGENTS.md`（Karpathy 准则）继续有效；`AWAKE\AGENTS.md` 是本工作区的项目级细则，与本文冲突时以更靠近目标文件的规则优先。

## 作用域

- 本工作区只承载 AWAKE 运行时、工具链、测试与 SDK 参考。
- 分级内容、内容包工程、世界书正文与旧发布包**不在本工作区**，也不得复制进来。
- 文档引用内容机制时只写契约与门控，不复述正文。
- 需要内容侧工作时另开工作区，不要把内容目录加回本目录。

## 源码权威

- `AWAKE\` 是 AWAKE 运行时的唯一权威工作副本（2026-09-11 从 `_houkai_merge\AWAKE` 复制，含当日未提交改动）。
- GitHub 镜像 `Nobody-zz/AWAKE`（`origin` = `ssh://git@ssh.github.com:443/Nobody-zz/AWAKE.git`）是公开镜像，
  **落后于本工作区**；同步方向只允许 本工作区 → 镜像（只推不拉）。
- 不得用镜像或历史克隆反向覆盖本工作区。
  ⚠️ **2026-10-02 实测：曾经提到的两个回退源都已经不在本机了** ——
  `AWAKE-Repo`（旧克隆）与 `_houkai_merge\AWAKE` 三个候选路径全部 `Test-Path = False`。
  所以「用旧克隆回滚」这条路**已经不存在**；镜像 `origin/main` 是本机之外**唯一**的副本，
  回退只能靠本仓库自己的提交历史。这条别按历史文档假设还有备份。

## 环境基线

- 游戏：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord`，Bannerlord API **`v1.4.8`**
  （2026-10-02 实测 `Modules\Native\SubModule.xml`；`AWAKE.csproj` 与 `AWAKE\tools\build.ps1` 的 `BannerlordApi` 默认值同为 `1.4.8`）。
  - 四个前置依赖（Harmony / ButterLib / MBOptionScreen / UIExtenderEx）必须是**工坊 1.4.8 版**，且装在标准模块名下；`Modules\` 下不得再留 `*.stale-*` 之类的隔离件（本工作区 09 月那次隔离已还原）。
  - `build.ps1` 自带游戏版本校验（比 `Native` 版本与 `-BannerlordApi`），不匹配会**直接拒跑**，所以换游戏版本必须同步改 `BannerlordApi`。
- 目标框架 `net472`；编译依赖游戏目录下的 TaleWorlds 程序集与 `Modules\Bannerlord.MBOptionScreen`。
- 运行时主模组名 `AWAKE`，程序集 `Awake.dll`，命名空间 `Awake`；新代码不得引入其它模组前缀。

## 构建与同步

- 构建入口 `AWAKE\tools\build.ps1`（调 MSBuild + `AWAKE.csproj`）；质量门见 `AWAKE\AGENTS.md`。
  - `build.ps1` 自带 `/restore`：MSBuild.exe 不隐式还原 NuGet，去掉它会让干净克隆直接 `NETSDK1004`。
  - 内嵌 Runtime 顺序固定：先 `package_embedded_runtime.ps1`，再 `sync_module.ps1`；后者只校验不重建，顺序反了会把旧 Runtime 留在游戏目录。
    ⚠️ **这两个脚本不由 `build.ps1` 调用**——`build.ps1` 只做 编译 → `AWAKE.Tests` 编译 → `SdkSmoke`，全程不碰游戏目录。投送必须手工按上面的顺序跑两步。
    `sync_module.ps1` 写游戏目录必须带 `-ConfirmGameSync`（干跑用 `-WhatIf`），它会拒绝在 Bannerlord/TaleWorlds 进程运行时执行。
    ⚠️ 它的 `-BuildDllPath` 默认值**曾经写死 `1.3.15`**（2026-10-02 已改为 `1.4.8`，见 `598cf01`）。那次不是「路径不存在会报错」——
    `_build_out\1.3.15\` 当时**还在**，里面是 09-22 的旧构建，所以裸跑会**静默把旧 DLL 投进游戏目录并报 OK**。
    投送类脚本的默认值必须跟住环境基线；改版本时这几处（`sync_module.ps1` / `release_check.ps1` / `verify_*.ps1`）要一起查。
    `sync_module.ps1` 会把工作区当前的**世界书一并投送**（这是它的固有行为），而 `SdkSmoke` 的 dialogue-chain-redtest 读的就是已投送的世界书 ⇒ 投送后必须重跑烟测。
- `AWAKE\framework` 是构建依赖，`AWAKE.csproj` 通过 `ProjectReference` 引用它，不得删除。
  - framework 各子工程之间也必须整体走 `ProjectReference`（含 net8.0 → net472 的跨目标框架引用）；`_build_out` 只作产物目录，不得再当 `<Reference HintPath>` 输入，否则干净克隆编不出来。
- 同步入口 `AWAKE\tools\sync_module.ps1`；游戏运行时不得覆盖游戏模块目录。
- 构建产物 `_build_out\`、`obj\`、`bin\` 不入库，也不要在本工作区堆积。

## 版本控制

- 本目录是 git 仓库（branch `main`，**upstream = `origin/main`**，但只允许本工作区 → 镜像单向推送）。
- 2026-09-11 拆分批次已提交为 `69b9fc7`（含 `framework\` 的 160 个源码文件）。
- **镜像是否跟得上要用命令查，不要凭印象**：`git rev-list --left-right --count origin/main...main`
  给出「落后 / 领先」。注意 `origin/main` 只是**本机上次 fetch 时的记录**，不等于 GitHub 的实时状态——
  要拿实时状态得 `git ls-remote origin refs/heads/main`（需要网络，本机 SSH 走 `ssh.github.com:443`）。
  **判断当前代码一律看本工作区的 `HEAD`，不是 `origin/main`。**
- 推送前先确认能否快进：远端 HEAD 必须等于本机记录的 `origin/main`（即没有分叉），否则先商量再动，不擅自 rebase/merge。
- `framework\` 已入库，公开镜像可独立构建：干净克隆下 `AWAKE\tools\build.ps1` 一条命令即可产出 `Awake.dll`，不需要预先手工编译任何依赖。
- `.nuget`、`_build_out`、`obj`、`bin` 不入库。

## 临时产物

- 一次性检查脚本与验证报告放 `AWAKE\docs\` 或系统临时目录，不落 `src\`、`GUI\`、`ModuleData\`。
- 编辑前只备份本次要改的精确文件，不做整目录复制。
- 不在本工作区生成发布包与客户交付包。
