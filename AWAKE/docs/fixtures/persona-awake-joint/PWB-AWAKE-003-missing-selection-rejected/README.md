# PWB-AWAKE-003-missing-selection-rejected

`selection` 属性现在完全缺失，而不是存在一个空对象或有效 selection。runner 必须拒绝从显示名、文件名、mtime 或普通 Workbench `Id` 推断 character/identity；稳定错误是缺少必需的 selection sidecar。
