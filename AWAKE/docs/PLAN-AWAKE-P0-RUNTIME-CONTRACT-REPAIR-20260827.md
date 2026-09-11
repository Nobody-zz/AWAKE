# AWAKE P0：运行时契约与候选身份修复计划

**计划日期：** 2026-08-27  
**计划状态：** `proposed_for_user_signoff`  
**审查状态：** `APPROVED`（最终独立只读复核）  
**用户签收：** `required`  
**实现授权：** `false`  
**目标：** 在不覆盖冻结候选、不修改游戏目录的前提下，先解决当前源构建身份与世界书运行契约不一致的问题。

## 1. 为什么建立本计划

当前磁盘存在三个不能混为一谈的状态：

```text
当前源构建：
_build_out\1.3.15\Release\Awake.dll
SHA-256：02853D3D01788B6D5D51085173FC1D5DF3CA53BCE0C151371F1C5B8F3E548502

冻结候选（dist）：
dist\Modules\AWAKE\bin\Win64_Shipping_Client\Awake.dll
SHA-256：F03477F4B3F26B7D800526E3A3260D1593085BCDBDAECC8AD35691DBFDE0A5A7

冻结候选（游戏目录）：
Modules\AWAKE\bin\Win64_Shipping_Client\Awake.dll
SHA-256：F03477F4B3F26B7D800526E3A3260D1593085BCDBDAECC8AD35691DBFDE0A5A7
```

当前源码仍使用旧 BuildId：

```text
awake-20260820-syncpack-001
```

三处世界书 manifest 仍为：

```text
schemaVersion = awake.worldbook.v1
SHA-256 = 2115664E1BB6BB26E63D8CB2097B891B51C03A00504D76DF6F8520FD9448468A
```

当前世界书目录没有：

```text
runtime.json
index.json
package-manifest.json
```

而当前源读取器路径会尝试按 v2 运行时包读取。因此在任何新同步或实机验证前，必须先解决“源代码到底对应哪个运行包”的问题。

## 2. 范围

### 2.1 本批允许处理

1. 读取当前世界书编辑器/编译端的正式输出契约和产物位置。
2. 确认 AWAKE 运行时唯一应消费的世界书 schema、manifest 和运行索引。
3. 检查当前源读取器、世界书编译端和安装包的契约是否一致。
4. 为新源构建建立新的唯一 BuildId 和候选记录设计。
5. 增加或调整离线契约检查，使缺文件、版本错配和哈希错配在进入游戏前失败。
6. 在不覆盖冻结候选的条件下，为后续实现准备最小补丁和离线验证方案。

本批只允许在 AWAKE 运行时工程内做只读核验、诊断补充或修复已确认的读取路径；Worldbook Studio、编译端和公共内容契约只作为证据来源，不在本批修改。

### 2.2 本批明确不处理

- 不覆盖 `dist` 中的冻结候选。
- 不覆盖 Bannerlord 游戏目录。
- 不启动 Bannerlord，不做 E4/E5。
- 不修改世界书正文、四档文风或作者内容。
- 不修改 Worldbook Studio、编译端、公共世界书 schema 或其输出格式；如需修改，另立计划并重新审查。
- 不实现季度报告迁移。
- 不实现 AI 生成客观世界事件。
- 不实现玩家内容覆盖、NPC 长期学习或后台自主行动。
- 不修改 Marcus P3D Provider/凭据批次。
- 不直接实现 B3 询问入口；B3 仍以现有计划的用户签收状态为准。
- 不修改 `Storage`、存档 schema、Save key 或存档迁移；如发现必须修改，立即停止并另立计划。
- 不用临时双读取器掩盖 v1/v2 权威不明的问题。

## 3. 当前关键假设与待验证事实

### 3.1 不能直接假设

- 编辑器当前一定已经生成了可安装的 v2 运行包。
- `AWAKE-CURRENT.md` 中的 Studio 输出记录一定存在于当前磁盘。
- 当前源 DLL 的变更已经修复世界书读取问题。
- v1 世界书应当被运行时继续兼容。
- 通过增加 v1 fallback 就能解决问题。

### 3.2 必须先取得的证据

1. 当前 Worldbook Studio/编译端正式输出目录和清单。
2. v2 `runtime.json`、`index.json`、`package-manifest.json` 的实际 schema 和生成入口。
3. 运行时读取器期待的完整路径和版本条件。
4. 当前源 DLL 的 BuildId、构建来源和对应计划批次。
5. 当前源构建是否已经由其他批次修改，避免覆盖并行任务成果。

## 4. 设计选项

### 选项 A：以编辑器正式 v2 产物为唯一运行格式（推荐）

```text
作者内容
→ Worldbook Studio 编译
→ v2 runtime/index/package manifest
→ AWAKE 运行时只读已验证包
```

**优点：** 与当前源读取器方向一致；运行时索引可预生成；便于多世界观和内容包隔离。  
**风险：** 必须先确认编辑器输出在当前磁盘和安装流程中真实存在；不能只依据文档记录。  
**前提：** 编译端可以稳定生成、校验和打包 v2 产物。

### 选项 B：把运行时重新改回 v1

**不推荐。** 这会把当前已存在的 v2 读取器和编辑器发展方向倒退到旧格式，并可能重新引入全量扫描、旧路径和未定义 fallback。只有在确认 v2 编译端不存在且用户明确放弃 v2 时，才重新评估。

### 选项 C：v2 优先、v1 隐式 fallback

**拒绝作为默认方案。** 两种格式同时作为“正常运行格式”会造成：

- 权威版本不清；
- 同一内容两种读取结果；
- 错误被 fallback 掩盖；
- BuildId、manifest 和日志难以对应；
- 后续内容包无法确定应编译哪一种格式。

如果未来必须迁移旧存档或旧内容，只允许增加明确的一次性离线迁移器，不允许运行时静默选择两套权威。

## 5. 推荐执行路径

### 阶段 A：证据和身份归属

- 确认当前源 DLL 属于哪个批次和工作树状态。
- 为该批次建立独立候选名称，不复用 `awake-20260820-syncpack-001`。
- 更新候选记录所需的 BuildId、源哈希、构建时间和变更范围。
- 不修改 `dist`、游戏目录和冻结候选。

**验证：** 源 DLL、BuildId、计划、状态记录能够一一对应。

### 阶段 B：世界书契约确认

- 从编译端找到正式 v2 产物或证明当前尚不存在。
- 对照运行时读取器的 manifest、runtime、index、package manifest 要求。
- 明确“作者源文件”和“运行时编译包”不是同一层。
- 明确缺失 v2 产物时的状态是 `not_ready`，不是自动读取旧 v1。

**验证：** 离线检查能明确判定一个包是可运行、版本错配还是缺文件。

### 阶段 C：最小代码/工具修复方案

只有 A、B 完成且计划获签收后，才在 AWAKE 侧决定是：

- 调整读取器以消费已经确认的唯一契约；或
- 仅增加清晰失败诊断，不改读取语义。

如果证据表明必须调整 Worldbook Studio 编译端、公共 schema 或输出格式，本批只记录阻断原因，不实施该调整；必须另立计划、重新审查并取得对应签收。不得在证据不足时同时修改编译端、读取器和世界书正文。

### 阶段 D：离线验证与新候选

- JSON/schema/manifest 校验；
- 世界观命名空间和内容包隔离校验；
- 运行时索引加载 smoke；
- 缺文件、错版本、错哈希和空内容负例；
- BuildId 和所有产物哈希记录。
- `tools\build.ps1` Release 构建通过，0 warnings / 0 errors；
- `Awake.SdkSmoke.exe`、`maf-lint.ps1` 和 `BUILD_VERIFICATION.txt` 质量门按 AWAKE 当前规则完成记录。

本批不执行游戏目录同步；只有在新候选具备独立 BuildId、哈希记录和 E1/E2 证据后，才可另行申请 E3。

**验证：** 达到离线 E1/E2 后，才考虑创建新的 E3 同步候选；旧冻结候选不改。

### 6.1 Bannerlord 运行时安全门

- 不在 `OnSubModuleLoad` 访问 `Campaign.Current` 或其他未确认可用的战役状态。
- 不在 UI、Campaign 或 Tick 路径阻塞等待文件、网络或 Storage。
- 不改变 Save key、Saveable ID、存档容器或读档重建顺序。
- 若实现过程中触及生命周期、线程、存档 schema 或公共接口，立即停止本批并另立计划。

## 6. 完成定义

本计划只有在以下条件全部满足时，才允许进入代码实现或新候选构建：

1. 唯一运行世界书 schema 已由编译端和读取端共同确认。
2. 当前源 DLL 的批次归属和新 BuildId 已记录。
3. v1/v2 不再存在隐式双权威。
4. 离线检查能在进入游戏前阻止缺文件、错版本和错哈希。
5. 计划经过独立只读审查并得到 `VERDICT: APPROVED`。
6. 用户签收本计划或明确授权进入实现。

即使满足上述条件，也只代表可以创建新的离线候选；不代表游戏目录已经更新，也不代表 E4/E5 已完成。

## 7. 风险和停止条件

遇到以下情况立即停止，不自行扩大范围：

- 发现当前源 DLL 属于其他并行批次且正在使用；
- 无法确认 v2 产物的正式生成入口；
- 需要修改 Marcus、Worldbook Studio 或 B3 批次的公共接口；
- 需要修改存档字段或改变旧候选的读取语义；
- 需要把 v1 内容直接转换成 v2 但没有迁移契约；
- 需要覆盖游戏目录才能继续验证；
- BuildId、哈希或日志无法唯一归属；
- 任何外部请求结果不确定或出现 429/取消。

## 8. 相关文件

- `AWAKE-CURRENT.md`
- `AWAKE-WORLD-RUNTIME-CONTROL-RULES-20260827.md`
- `AWAKE-GLOBAL-MECHANISM-AUDIT-20260827.md`
- `AWAKE-NativeState-Adapter-Spec-v1.md`
- `PLAN-AWAKE-NativeKnowledge-B3-InquiryOnly-20260826.md`
- `WORLDBOOK-EVENT-REPORT-CONTRACT-v1.md`

## 9. 当前门禁结论

```text
plan_status = proposed_for_user_signoff
review_status = pending
user_signoff_required = true
implementation_authorized = false
game_directory_sync = forbidden_in_this_batch
frozen_candidate = unchanged
```
