using System;
using System.IO;
using System.Reflection;

namespace Awake;

/// <summary>
/// 模块目录解析：从 DLL 所在目录向上寻找含 SubModule.xml 的目录。
/// 说明：AwakeLog 内联了同一套查找逻辑（本批不改动日志代码），后续统一时以本类为准。
/// </summary>
internal static class AwakeModulePaths
{
    internal static string ResolveModuleDirectory()
    {
        string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
        DirectoryInfo current = new DirectoryInfo(assemblyDir);
        for (int i = 0; i < 6 && current != null; i++)
        {
            if (File.Exists(Path.Combine(current.FullName, "SubModule.xml")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        return assemblyDir;
    }
}
