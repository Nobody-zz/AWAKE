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
- GitHub 镜像 `Nobody-zz/AWAKE`（本地旧克隆 `AWAKE-Repo`）是公开镜像，**落后于本工作区**；同步方向只允许 本工作区 → 镜像。
- 不得用 `_houkai_merge\AWAKE` 或 `AWAKE-Repo` 反向覆盖本工作区。

## 环境基线

- 游戏：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord`，Bannerlord API `v1.3.15`。
- 目标框架 `net472`；编译依赖游戏目录下的 TaleWorlds 程序集与 `Modules\Bannerlord.MBOptionScreen`。
- 运行时主模组名 `AWAKE`，程序集 `Awake.dll`，命名空间 `Awake`；新代码不得引入其它模组前缀。

## 构建与同步

- 构建入口 `AWAKE\tools\build.ps1`（调 MSBuild + `AWAKE.csproj`）；质量门见 `AWAKE\AGENTS.md`。
- `AWAKE\framework` 是构建依赖，`AWAKE.csproj` 通过 `ProjectReference` 引用它，不得删除。
- 同步入口 `AWAKE\tools\sync_module.ps1`；游戏运行时不得覆盖游戏模块目录。
- 构建产物 `_build_out\`、`obj\`、`bin\` 不入库，也不要在本工作区堆积。

## 版本控制

- 本目录是 git 仓库（branch `main`，未配置 upstream）。
- 2026-09-11 拆分后，工作树相对 `cc7b057` 有 35 处修改 + 230 处新增，**尚未提交**。
- `framework\` 目前未入库，导致公开镜像不可独立构建。框架源码应入库；`.nuget`、`_build_out`、`obj` 不入库。

## 临时产物

- 一次性检查脚本与验证报告放 `AWAKE\docs\` 或系统临时目录，不落 `src\`、`GUI\`、`ModuleData\`。
- 编辑前只备份本次要改的精确文件，不做整目录复制。
- 不在本工作区生成发布包与客户交付包。
