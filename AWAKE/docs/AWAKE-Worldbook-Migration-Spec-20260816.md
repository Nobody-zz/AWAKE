# AWAKE 世界书一次性迁移规范

> 日期：2026-08-16
> 目的：把 AF 格式世界书迁移为 AWAKE 原生格式，`sourceFormat` 统一为 `awake`，不再保留 AF 外部标记。
> 适用：制作组自有 AF 世界书资产，可直接使用。

## 1. 结论

- AF 世界书不需要在 AWAKE 中永久以“AF 兼容”身份存在。
- 一次性迁移后，规则字段归一化为 AWAKE camelCase，manifest 使用 `sourceFormat: "awake"`。
- AF 的变体选择语义通过每条规则显式写入 `variantSelection: "af-best"` 保留，不丢失行为。
- 源目录只读，迁移脚本不修改原文件。

## 2. 使用

```powershell
cd "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE"

.\tools\migrate_af_worldbook.ps1 `
  -Source "D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AnimusForge\PlayerExports\卡拉迪亚编年史" `
  -Destination "D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AWAKE\ModuleData\Worldbook" `
  -Id "awake.calradia.chronicle"
```

先试跑：

```powershell
.\tools\migrate_af_worldbook.ps1 -Source "<AF世界书目录>" -Destination "<输出目录>" -DryRun
```

`-DryRun` 不写文件，只输出文件数、规则数和示例。

## 3. 字段映射

### Rule

| AF | AWAKE |
| --- | --- |
| `Id` | `id` |
| `Keywords` | `keywords` |
| `RagShortTexts` | `ragShortTexts` |
| `SemanticPrototypes` | `semanticPrototypes` |
| `Priority` | `priority` |
| `Variants` | `variants` |
| `TextMappings` | `textMappings` |

迁移脚本还会补齐 AWAKE 默认字段：`kind=background`、`scope=npc`、`persistence=persistent`、`priority=0`，并写入 `variantSelection=af-best`。

### Variant / When

| AF | AWAKE |
| --- | --- |
| `Priority` | `priority` |
| `When` | `when` |
| `Content` | `content` |
| `HeroIds` | `heroIds` |
| `Cultures` | `cultures` |
| `KingdomIds` | `kingdomIds` |
| `SettlementIds` | `settlementIds` |
| `Roles` | `roles` |
| `IdentityIds` | `identityIds` |
| `IsFemale` | `isFemale` |
| `IsClanLeader` | `isClanLeader` |
| `SkillMin` | `skillMin` |

### TextMapping

| AF | AWAKE |
| --- | --- |
| `SourceText` | `sourceText` |
| `Kind` | `kind` |
| `TargetId` | `targetId` |
| `AgeMin` | `ageMin` |
| `AgeMax` | `ageMax` |
| `EmptyValueText` | `emptyValueText` |
| `TrueText` | `trueText` |
| `FalseText` | `falseText` |

### Persona

| AF | AWAKE |
| --- | --- |
| `Personality` | `personality` |
| `Background` | `background` |
| `VoiceId` | `voiceId` |
| `CharacterId` / `CharacterObjectId` | `characterId` |

文件内没有 `CharacterId` 时，使用源文件名作为 `characterId`。

## 4. 输出结构

```text
<Destination>/
  manifest.json
  migration_report.json
  rules/
  personality_background/
  unnamed_persona/     # 如源目录存在
  voice_mapping/       # 如源目录存在
  event_data/          # 如源目录存在
  debt/                # 如源目录存在
  dialogue_history/    # 如源目录存在
  compressed_memory/   # 如源目录存在
```

`manifest.json` 示例：

```json
{
  "schemaVersion": "awake.worldbook.v1",
  "id": "awake.calradia.chronicle",
  "sourceFormat": "awake",
  "rulesDirectory": "rules",
  "personaDirectory": "personality_background"
}
```

## 5. 验证

迁移后至少检查：

- `rules/` 文件数与源 `knowledge/rules/` 一致。
- `personality_background/` 文件数与源一致。
- 所有 JSON 可解析。
- 每条规则存在 `id` 和 `variantSelection=af-best`。
- `manifest.json` 的 `sourceFormat=awake`。
- 放入 `Modules/AWAKE/ModuleData/Worldbook/` 后，游戏日志出现 `worldbook_runtime_initialized`。

## 6. 什么时候不迁移

- 只是临时测试一本 AF 世界书：直接复制目录并写 `sourceFormat: "af"` 的 manifest 更快。
- 希望以后持续跟随 AF 更新：保留兼容模式，避免每次更新重复迁移。
- 已经决定脱离 AF 内容更新源：一次性迁移，之后以 AWAKE 原生格式维护。
