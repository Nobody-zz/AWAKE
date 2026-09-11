# PersonaWorkbench × AWAKE 固定夹具矩阵

本目录只包含隔离 E0–E2 fixture 数据；不调用 Provider/Ollama/游戏进程，不修改 AWAKE runtime、ModuleData、dist、游戏目录、冻结候选、计划、账本、persona-contract 或 tools。

参考夹具：`../persona-load-v2-golden.json`，仅借用字段形状，不覆盖原文件。每个子目录的 `manifest.sha256.txt` 列出除 manifest 自身外的本目录文件摘要。

`PWB-AWAKE-007-duplicate-and-path-rejected` 保留 partial-first 主门禁；其重复 ID、受保护路径和 reparse 路径分别由 `PWB-AWAKE-007A`、`PWB-AWAKE-007B`、`PWB-AWAKE-007C` 单独覆盖，避免首错遮蔽后续分支。

G2-C handoff 负例由 `PWB-AWAKE-014`、`PWB-AWAKE-015`、`PWB-AWAKE-016`、`PWB-AWAKE-017` 分别覆盖审批证据缺失、selection source revision 过期、definition-v1 不支持的启用规则和 selection schema 缺失。
