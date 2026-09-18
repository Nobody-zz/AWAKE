using System;
using System.IO;
using System.Linq;
using Awake;

// ============================================================
// 角色卡运行时模拟器（PersonaRuntimeSim）—— 角色卡线自有测试装置（只读 / 离线 / 不写回）
//
//   「一份 definition ＋ 一份标签表 ＋ 一个 heroId ⇒ 真实 DSL ⇒ 模型开口」
//
//   刻意与世界书门控模拟器（tools/worldbook-runtime-sim）分家：
//   角色卡不需要世界书，就不该依赖世界书包能否加载、也不该搭那条链的工程。
//
// 用法：
//   dotnet run -c Release -- persona <definitions目录> <registry.json> <heroId> [输出.txt] [最大字节] [--force-approved]
//   dotnet run -c Release -- persona-list [definitions目录]
//   dotnet run -c Release -- persona-roster <persona定义根> [out.json] [charIds,逗号分隔] [dslOutDir]
//
// 例：
//   dotnet run -c Release -- persona \
//     "D:\AWAKE-Dev\AWAKE\ModuleData\Worldbook\persona_definitions\definitions" \
//     "D:\AWAKE-Dev\AWAKE\ModuleData\Worldbook\persona_definitions\tag_registry.json" \
//     garios_empire_west "garios.dsl.txt" 6144 --force-approved
// ============================================================

// 从输出目录向上找仓库根（同时含 src 与 ModuleData 的那一层）
string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "src"))
            && Directory.Exists(Path.Combine(dir.FullName, "ModuleData")))
            return dir.FullName;
        dir = dir.Parent;
    }
    return Directory.GetCurrentDirectory();
}

string repoRoot = FindRepoRoot();

// persona 子命令：生成 persona DSL 落盘 / 列举 heroes
if (args.Length > 0 && StringComparer.OrdinalIgnoreCase.Equals(args[0], "persona"))
{
    return PersonaDialogueSim.Run(args.Skip(1).ToArray());
}

// persona-list：列举 definitions 目录下的可用 heroId
if (args.Length > 0 && StringComparer.OrdinalIgnoreCase.Equals(args[0], "persona-list"))
{
    string defsDir = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])
        ? args[1]
        : Path.Combine(repoRoot, "ModuleData", "Worldbook", "persona_definitions", "definitions");
    return PersonaDialogueSim.ListHeroes(defsDir);
}

// persona-roster：角色卡名册离线探针——扫目录、按 characterId 选卡、跑真实生成器。
if (args.Length > 0 && StringComparer.OrdinalIgnoreCase.Equals(args[0], "persona-roster"))
{
    return PersonaRosterSim.Run(args.Skip(1).ToArray());
}

Console.WriteLine("PersonaRuntimeSim —— 角色卡运行时模拟器（离线）");
Console.WriteLine();
Console.WriteLine("用法:");
Console.WriteLine("  persona        <definitions目录> <registry.json> <heroId> [输出.txt] [最大字节] [--force-approved]");
Console.WriteLine("  persona-list   [definitions目录]");
Console.WriteLine("  persona-roster <persona定义根> [out.json] [charIds,逗号分隔] [dslOutDir]");
Console.WriteLine();
Console.WriteLine("详解见 README.md。");
return 2;
