# Plan Review Log: Worldbook Studio A3.4 Registry validation projection seam
Act 1 (grill) complete — plan locked with autonomous execution authorization; independent read-only review pending.

## Round 1 — Independent read-only review

- `Schema-invalid` 没有独立 golden case：四个输入无法执行“Schema 诊断与 parent 诊断顺序”断言。修复：增加 `schema_invalid` case，固定可解析但违反 Schema 的 registry 及诊断序列。
- 缺失 registry 的 Builder 输入语义不够明确，A2 读取上限没有固定。修复：明确缺失分支使用两个空 JSON 对象与 `Array.Empty<byte>()`，不读取缺失 registry/schema，并断言三阶段读取计数。
- `RegistrySnapshot` 使用 HashSet/Dictionary，计划不能依赖集合枚举顺序。修复：Builder 直接按输入数组顺序执行 parent traversal，测试固定数组/诊断顺序而不把集合枚举当契约。
- authority 扫描范围和计数不够精确。修复：固定 top-level Core `*.cs` 扫描根和 `Build`/`new RegistrySnapshot`/集合写入/parent while 的精确允许与禁止计数。
- 93-case manifest、golden provenance 修订为固定 hash、旧 ValidationServices hash、harness hash/命令、原始 projection bytes/base64/hash 和完整输入 inventory。

### Claude response

- All findings accepted. The plan now includes five executable cases, explicit missing-file/read-count behavior, input-order traversal, exact scan rules, fixed 93-case hash `eb6e76fe216bddb066797e6bb77823387b72e14044face9bf865e352f889802c`, and raw provenance requirements. No production code has been changed for A3.4.

## Round 2 — Independent read-only review

- 五个 case 已列出但测试 1 仍写四个。修复：明确 golden 比较包含 `schema_invalid` 的 snapshot、raw projection 与诊断顺序。
- authority 扫描还缺少 `Profiles.Add`、`Referrals.Add`、`PubliclyAskableReferrals.Add` 和两类输入遍历。修复：将这些写入和遍历加入 `ValidationServices.cs` 精确零计数，唯一允许位置为 Builder。
- 缺失 registry 的现有实现会在任一文件缺失时直接返回，连存在的 sibling registry 也不读取。修复：计划明确两个 registry 与相关 Schema 都不读取，probe 读数为零。

### Claude response

- All findings accepted. The plan now has an executable five-case golden comparison, complete registry projection authority scans, and the exact any-missing-file no-read behavior. Re-review requested.

## Final review

- Round 3 independent read-only review: `VERDICT: APPROVED`.
- The revised plan is accepted for implementation. The five-case golden, Schema→Builder diagnostic order, explicit input-array ordering, exact authority scans, missing-file no-read boundary, fixed manifest hash and raw provenance are sufficiently bounded.
- Implementation authorization: proceed with TDD; do not modify the AWAKE runtime candidate, game directory, dist, PlayerExports, or existing A1 package.
