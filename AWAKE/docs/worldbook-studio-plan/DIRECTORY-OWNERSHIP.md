# Worldbook Studio MVP 目录所有权

| 路径 | Studio 权限 | 说明 |
|---|---|---|
| `authoring/` | 读写 | 正典源文件、来源登记、别名和审阅记录 |
| `authoring/suggestions/` | 读写但不可编译 | AI 建议和待确认草稿 |
| `compiled/` | 工具生成/可删除 | 候选包、索引、报告、哈希 |
| `fixtures/` | 读写 | 固定离线样本和预期结果 |
| `export/` | 工具生成 | 独立导出包，不是游戏目录 |
| `Modules\AWAKE\ModuleData\Worldbook\rules` | MVP 禁止 | 当前运行时内容，不直接修改 |
| `Modules\AWAKE\ModuleData\Worldbook\personality_background` | MVP 禁止 | 当前人物内容，不直接修改 |
| `Modules\AWAKE\ModuleData\Worldbook\event_data` | MVP 禁止 | 运行时事件数据，不直接修改 |
| `Modules\AWAKE\ModuleData\Worldbook\debt` | 禁止 | 运行时状态 |
| `Modules\AWAKE\ModuleData\Worldbook\dialogue_history` | 禁止 | 运行时状态 |
| `Modules\AWAKE\ModuleData\Worldbook\compressed_memory` | 禁止 | 运行时状态 |
| `Modules\AWAKE\ModuleData\Worldbook\voice_mapping` | 禁止 | 运行时配置/资产映射 |
| `Modules\AWAKE\ModuleData\Worldbook\manifest.json` | MVP 禁止 | 当前运行时清单 |

## 路径安全

- 所有根目录和目标路径先做 realpath 规范化。
- 拒绝 junction、symbolic link、reparse point 和路径大小写绕过。
- 导出目标必须严格位于显式 `export/` 根目录下。
- MVP 不调用任何游戏同步脚本。
