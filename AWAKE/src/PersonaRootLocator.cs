using System.IO;

namespace Awake;

/// <summary>
/// 定位「角色卡根目录」——即含 <c>persona_definitions/</c> 的那一层。
///
/// <b>刻意不读 manifest、不引用 TaleWorlds / AwakeLog</b>，原因有二：
/// <list type="number">
/// <item>角色卡装载本就**不该**依赖世界书能否加载：manifest 缺失、v2 包校验失败、
/// 世界书目录结构变动，都不该让 76 张卡一起失效。</item>
/// <item>没有外部依赖，测试工程就能编同一份源码、对真实逻辑做单元测试
/// （避免出现「测的是另一套平行实现」）。</item>
/// </list>
///
/// 候选目录形状沿用既有世界书目录约定；将来两线商定把角色卡搬到自有目录时，
/// 只需在 <see cref="CandidateShapes"/> 里追加形状，调用点不用改。
/// </summary>
internal static class PersonaRootLocator
{
    /// <summary>根目录下必须存在这个子目录，才认作角色卡根。</summary>
    internal const string DefinitionsFolderName = "persona_definitions";

    /// <summary>从起点最多向上找几层（与运行时定位世界书 manifest 的口径一致）。</summary>
    internal const int MaximumAncestorLevels = 6;

    private static readonly string[][] CandidateShapes =
    {
        new[] { "ModuleData", "Worldbook" },
        new[] { "Worldbook" }
    };

    /// <summary>
    /// 自 <paramref name="startDirectory"/> 起逐级向上，返回第一个含
    /// <c>persona_definitions/</c> 的目录；找不到返回 null。
    /// </summary>
    internal static string Locate(string startDirectory)
    {
        if (string.IsNullOrWhiteSpace(startDirectory)) return null;

        DirectoryInfo current;
        try
        {
            current = new DirectoryInfo(startDirectory);
        }
        catch
        {
            return null;
        }

        for (int level = 0; level < MaximumAncestorLevels && current != null; level++)
        {
            foreach (string[] shape in CandidateShapes)
            {
                string candidate = current.FullName;
                foreach (string part in shape) candidate = Path.Combine(candidate, part);
                if (Directory.Exists(Path.Combine(candidate, DefinitionsFolderName))) return candidate;
            }
            current = current.Parent;
        }
        return null;
    }
}
