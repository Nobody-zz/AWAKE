# Worldbook Studio：runtime 锚点贯通（C 批）报告（2026-09-10）

> 目标（active goal 第 ③ 项）：让编译出的 runtime 包消费 authoring 文档的 `entity.settlement.*` 锚点，
> 并用**真实本地 Worker 生成的建档文档**留可重复证据。
> 边界：未启动游戏、未同步游戏目录、未访问云端 Provider/API、未修改迁移候选与世界书正文。

## 1. 实现

| 位置 | 改动 |
|---|---|
| `src/Awake.WorldbookStudio.Core/RuntimePackageCompiler.cs` | `BuildEntry` 把文档 `entity_ids` 规范化为 `awake:<kind>:<code>` 写入条目扩展 `entry_extensions.entityRefs`；新增 `CanonicalEntityRef`，kind 白名单仅 `hero\|clan\|settlement`，其余抛 `WB-DOC-003`（fail-closed） |
| `tests/Awake.WorldbookStudio.EditorContent.Tests/Program.cs` | 新增 `runtime package carries settlement anchors`：正向 `entity.settlement.town_v5` → runtime `entityRefs` 含 `awake:settlement:town_v5`；负向 `entity.bogus.thing` → `WB-DOC-003` |

`entry_extensions` 在 `tools/worldbook-contract/v1/runtime.schema.json` 中为 `additionalProperties: true`，
因此新增字段不需要改契约版本；已确认无 schema 变更。

## 2. 证据链（可重复执行）

脚本：`scripts/runtime-anchor-evidence.ps1`（打包版 CLI，走 register → select → approve → compile-proof → compile 权威链）

1. 从 `_tmp/pravend-real-worker-evidence.json` 读取**真实 AI 建档工作区**与文档路径
   （`doc.politics.entry-b4b7318c423f4264bdf4e58ba238e7cb`，Ollama `qwen2.5:latest` 生成、`needs_review`）。
2. 把该工作区的 authoring 输入克隆到一次性临时工作区（跳过 `authoring-v1`/`compiled`），保证每次运行都是干净权威状态，
   且不污染作者闭环验收工作区。
3. 依次执行权威链并校验编译产物的 `runtime.json`。

实测结果（连续两次运行，字节级一致）：

```text
register       PASS
select         PASS  doc.politics.entry-b4b7318c423f4264bdf4e58ba238e7cb
approve        PASS  approval.5ed624f07a1841dc8ca46be5ba5320a0
compile-proof  PASS  compile.3e289c68b1e043da8e701432648c9b75
compile        PASS
runtime_entity_refs = awake:settlement:town_v3, awake:settlement:town_v7
manifest_hash = 0712c83811d6fa90b73b83455b75a3b8b0d7b4ebe7e8645ddb782831b332c928
passed = true
```

证据文件：`docs/evidence/WORLDBOOKSTUDIO-RUNTIME-ANCHOR-EVIDENCE.json`

> 说明：`town_v7` 同样出现在该文档 `entity_ids` 中（AI 从同一段落识别到的关联聚落），
> 因此运行包含两个锚点属于预期，而非误挂。

## 3. 排障记录（避免后续重复踩坑）

1. **CLI 投影是驼峰命名**：`AuthorityPublicProjection` 输出 `documentId` / `selectionId` / `approvalId` / `compileProofId`；
   早期脚本按蛇形读取导致 `selectionId` 为空，approve 去读 `selections/.json` → `WB-AUTHORITY-404`。已修正并加空值断言。
2. **`compile --out` 只能落在工作区 `compiled/` 内**：`NormalizeCompileOutputRoot` → `Policy.RequireCompiled` 会拒绝工作区外路径。
3. **不要在编译后删除产物**：权威层会再次校验已提交 operation 的工件清单，目录被删会被判 `WB-AUTHORITY-RECOVERY-409`
   并 quarantine 该 operation（fail-closed 行为，不是缺陷）。脚本因此改为一次性克隆工作区、结束整目录清理。

## 4. 已验证 / 未验证

- 已验证：作者文档的 `entity.settlement.*` 锚点经权威链编译后出现在 runtime 包条目扩展中；未知 kind 编译期阻断；测试套件通过。
- **未验证**：AWAKE 模组本体在运行期读取 `extensions.entityRefs` 并据此做聚合/检索——属模组本体闭环，不在本会话范围。
  因此本批只能声明“运行包已携带锚点”，不能声明“游戏内已消费锚点”。

## 5. 无副作用声明

未启动 Bannerlord；未读写或同步游戏目录；未访问云端 Provider / 真实 API Key / 外部网络服务；
未修改真实源目录、五个世界书迁移候选、世界书正文、AWAKE 模组本体与任何冻结构建产物。
