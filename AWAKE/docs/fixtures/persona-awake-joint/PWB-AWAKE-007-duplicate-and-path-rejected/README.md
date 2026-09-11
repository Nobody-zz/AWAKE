# PWB-AWAKE-007-duplicate-and-path-rejected

`partialExport=true` 让当前 runner 能稳定先触发 `persona.partial_export`；重复 ID、受保护路径、reparse/junction 越界作为 deferred checks 保留，但本轮不谎称它们已由同一次 operation 全部执行到。所有破坏性操作都应在计划阶段拒绝，不得先写后回滚。
