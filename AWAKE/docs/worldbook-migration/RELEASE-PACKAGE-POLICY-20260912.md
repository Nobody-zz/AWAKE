
---

## 增补（09-13 晨，Max：modder 翻包也要能对上中文名）

- **双语标题随包出境**：runtime.json 每条 entry 自带 `title: {zh-CN, en}`，index.json 检索键含中文词——modder 翻包看到 `awake:entry:geography.tanaesis-lake` 的同时就能看到「塔奈西斯湖 / Lake Tanaesis」，无需对照表。**验收项**：发布前抽查 runtime 每条 entry 的 title.zh-CN 非空。
- **doc id 命名规范（钉死）**：slug 一律取**官方英文原名**（paravenos / tanaesis-lake / miron-river），不发明变体词（pavendoria 教训）；中文概念无官方英文名时用语义英文并登记。
- expr/断言级调试 ID 实测**不出境**（runtime 无 expr. 键），维护期命名自由度不受此规范约束。
 游戏加载面 | `WorldbookRuntime.LocateManifest` → 读 `manifest.json` → `runtime.json`（`WorldKnowledgeLoader` L15/L21） | 只读这两份 |
| 整包校验 | `contentHash` 只锁 `runtime.json` + `index.json`（编译器 L85 ＝ 校验器 L68） | 其余文件**不在校验范围** |

**结论**：运行时玩家可见的数据天然干净；带来源的是 Studio 侧审计产物，发布时**不拷贝**即可，无需改任何代码。

## 二、发布白名单（部署到游戏 `ModuleData/Worldbook/` 只拷）

1. `manifest.json`（入口 + 哈希）
2. `runtime.json`（知识本体，被 contentHash 锁定）
3. `index.json`（被 contentHash 锁定；registry v1 模式另按其 entrypoints 补齐）

**不随发布**（留在 Studio 工作区）：
`source-report.json` · `documents.json` · `content-graph.json` · `audit-report.json` · `validation.json` · `id-report.json` · `runtime_mapping_report.json` · `SHA256SUMS.txt`

⚠ 抽掉上述文件**不会**破坏 WB2-HASH 校验（已核实校验范围）；反之若未来改编译器把来源编进 runtime.json，须同步改本纪律。

## 三、开发期保留

来源登记与 quote 可定位机制**照常使用**——它是"每句有出处"的质检根基；只是止步于 Studio 工作区与审计报告，不出门。
