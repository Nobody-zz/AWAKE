using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Awake;

// ============================================================
// 人物层（P 层）：把 persona 定义生成真实 DSL，落盘供 e2e_ollama.py 接本地模型。
//   收获 46 张 persona 卡的产物 definition.v1 + tag_registry.json，
//   用与线上完全相同的 PersonaDslGenerator.Generate 生成 persona_dsl。
//   不启动游戏、不联网、不写回仓库。
//
// 用法：
//   dotnet run -c Release -- persona <definitions目录> <registry.json> <heroId> [输出.txt] [最大字节]
// 例：
//   dotnet run -c Release -- persona ^
//     "D:\AWAKE-Dev\AWAKE\ModuleData\Worldbook\persona_definitions\definitions" ^
//     "D:\AWAKE-Dev\AWAKE\ModuleData\Worldbook\persona_definitions\tag_registry.json" ^
//     garios_empire_west "garios.dsl.txt" 8000
// ============================================================

internal static class PersonaDialogueSim
{
    internal static int Run(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("persona 模式需要: <definitions目录> <registry.json> <heroId> [输出.txt] [最大字节]");
            return 2;
        }
        string defsDir = args[0];
        string registryPath = args[1];
        string heroId = args[2];
        string outPath = args.Length > 3 && !string.IsNullOrWhiteSpace(args[3]) ? args[3] : null;
        int maximumBytes = args.Length > 4 && int.TryParse(args[4], out int mb) ? mb : 8000;
        bool forceApproved = args.Any(a => StringComparer.OrdinalIgnoreCase.Equals(a, "--force-approved"));

        if (!File.Exists(registryPath))
        {
            Console.Error.WriteLine("找不到 registry: " + registryPath);
            return 2;
        }
        if (!Directory.Exists(defsDir))
        {
            Console.Error.WriteLine("找不到 definitions 目录: " + defsDir);
            return 2;
        }

        // 构建 registry（同 PersonaDataLoader 解析，走真实构造函数）
        PersonaTagRegistry registry = new PersonaTagRegistry(ParseRegistry(JObject.Parse(File.ReadAllText(registryPath))));

        // 找目标 hero 的 definition 文件
        string targetFile = FindDefinition(defsDir, heroId);
        if (targetFile == null)
        {
            Console.WriteLine("未找到 hero（可用 --persona-list 列举）：" + heroId);
            return 1;
        }

        PersonaDefinition definition = ParseDefinition(JObject.Parse(File.ReadAllText(targetFile)), Path.GetFileNameWithoutExtension(targetFile));
        if (definition == null)
        {
            Console.WriteLine("definition 解析失败: " + heroId);
            return 1;
        }
        // 状态以真实 PersonaDslGenerator 的处理为准，这里只报告不改写
        Console.WriteLine("[P_STATE] hero={0}  definitionStatus={1}  forceApproved={2}", heroId, definition.Status, forceApproved);
        if (forceApproved)
        {
            // 测试专用：仅内存置为 approved（不写回源文件），让真实 Generate 走角色 DSL 而非 legacy 兜底
            definition.Status = PersonaSchemaConstants.StatusApproved;
        }

        // 构造最小上下文
        PersonaContext context = new PersonaContext
        {
            CharacterId = definition.CharacterId,
            HeroName = heroId,
            KingdomId = definition.CharacterId,
            Role = definition.Role,
            Continuity = new PersonaContinuityState()
        };

        PersonaGenerationResult result = PersonaDslGenerator.Generate(
            definition, registry, context, string.Empty, string.Empty, maximumBytes);

        if (!result.IsUsable || string.IsNullOrWhiteSpace(result.Dsl))
        {
            Console.WriteLine("生成失败: " + heroId);
            foreach (string w in result.Warnings) Console.WriteLine("  WARN " + w);
            return 1;
        }

        string resolved = outPath ?? (heroId + ".dsl.txt");
        File.WriteAllText(resolved, result.Dsl, new System.Text.UTF8Encoding(false));
        Console.WriteLine("[PERSONA_DSL_OK] hero=" + heroId + "  defId=" + result.DefinitionId + "  fallback=" + result.UsedLegacyFallback + "  conflict=" + result.HadConflict);
        Console.WriteLine("  source=" + targetFile);
        Console.WriteLine("  bytes=" + Encoding.UTF8.GetByteCount(result.Dsl) + "  trimmed=" + result.WasTrimmed);
        Console.WriteLine("  out=" + Path.GetFullPath(resolved));
        foreach (string w in result.Warnings) Console.WriteLine("  WARN " + w);
        if (result.WasTrimmed) Console.WriteLine("  NOTE 超过字节预算，已按 UTF-8 安全截断（真实运行时同样处理）");
        Console.WriteLine("DSL-FIRST-LINE: " + result.Dsl.Split('\n')[0]);
        return 0;
    }

    internal static int ListHeroes(string defsDir)
    {
        if (!Directory.Exists(defsDir))
        {
            Console.Error.WriteLine("找不到 definitions 目录: " + defsDir);
            return 2;
        }
        Console.WriteLine("== heroes == " + defsDir);
        foreach (string file in Directory.GetFiles(defsDir, "*.json", SearchOption.TopDirectoryOnly).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (StringComparer.OrdinalIgnoreCase.Equals(name, "hero_default")) continue;
            Console.WriteLine("  " + name);
        }
        return 0;
    }

    private static string FindDefinition(string defsDir, string heroId)
    {
        // 优先精确文件名（去掉 .persona 前后缀），否则扫全部找 characterId 命中
        string hero = (heroId ?? string.Empty).Trim();
        string[] candidates =
        {
            hero,
            hero.EndsWith(".persona.json", StringComparison.OrdinalIgnoreCase) ? hero.Substring(0, hero.Length - ".persona.json".Length) : hero,
            hero.EndsWith(".definition.json", StringComparison.OrdinalIgnoreCase) ? hero.Substring(0, hero.Length - ".definition.json".Length) : hero
        };
        foreach (string cand in candidates)
        {
            string direct = Path.Combine(defsDir, cand + ".definition.json");
            if (File.Exists(direct)) return direct;
        }
        foreach (string file in Directory.GetFiles(defsDir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                JObject obj = JObject.Parse(File.ReadAllText(file));
                string characterId = (obj["characterId"] ?? "").ToString();
                if (StringComparer.OrdinalIgnoreCase.Equals(characterId, hero)
                    || StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileNameWithoutExtension(file), hero))
                    return file;
            }
            catch { }
        }
        return null;
    }

    private static PersonaTagRegistryDocument ParseRegistry(JObject obj)
    {
        var doc = new PersonaTagRegistryDocument();
        foreach (JToken token in obj["tags"] as JArray ?? new JArray())
        {
            JObject item = token as JObject;
            if (item == null) continue;
            doc.Tags.Add(new PersonaTagDefinition
            {
                Id = S(item, "id"),
                Category = S(item, "category"),
                DisplayName = S(item, "displayName"),
                Meaning = S(item, "meaning"),
                PromptText = S(item, "promptText"),
                Conflicts = SL(item, "conflicts")
            });
        }
        foreach (JToken token in obj["bundles"] as JArray ?? new JArray())
        {
            JObject item = token as JObject;
            if (item == null) continue;
            doc.Bundles.Add(new PersonaBundleDefinition
            {
                Id = S(item, "id"),
                DisplayName = S(item, "displayName"),
                Tags = SL(item, "tags")
            });
        }
        return doc;
    }

    private static PersonaDefinition ParseDefinition(JObject obj, string fallbackId)
    {
        var def = new PersonaDefinition
        {
            SchemaVersion = S(obj, "schemaVersion") ?? PersonaSchemaConstants.DefinitionSchema,
            Id = S(obj, "id") ?? fallbackId,
            CharacterId = S(obj, "characterId"),
            IdentityId = S(obj, "identityId"),
            Role = S(obj, "role"),
            SourcePackId = S(obj, "sourcePackId"),
            TemplateVersion = S(obj, "templateVersion") ?? "1",
            Status = S(obj, "status") ?? PersonaSchemaConstants.StatusDraft,
            Priority = I(obj, "priority"),
            Scope = S(obj, "scope") ?? "character",
            Core = S(obj, "core"),
            IdentityFacts = S(obj, "identityFacts"),
            RelationStyle = S(obj, "relationStyle"),
            CurrentStateHints = S(obj, "currentStateHints"),
            Summary = S(obj, "summary"),
            PublicDescription = S(obj, "publicDescription"),
            PrivateDescription = S(obj, "privateDescription"),
            ContradictionDescription = S(obj, "contradictionDescription"),
            SelfClaimRules = SL(obj, "selfClaimRules"),
            RealSelfBehaviors = SL(obj, "realSelfBehaviors"),
            SelfClaimExamples = SL(obj, "selfClaimExamples"),
            Bundles = SL(obj, "bundles")
        };
        foreach (JToken token in obj["tags"] as JArray ?? new JArray())
        {
            JObject item = token as JObject;
            if (item == null) continue;
            def.Tags.Add(new PersonaTagUse
            {
                Id = S(item, "id"),
                Priority = I(item, "priority"),
                SceneKeywords = SL(item, "sceneKeywords"),
                ContextModes = SL(item, "contextModes")
            });
        }
        foreach (JToken token in obj["experiences"] as JArray ?? new JArray())
        {
            JObject item = token as JObject;
            if (item == null) continue;
            def.Experiences.Add(new PersonaExperience
            {
                Id = S(item, "id"),
                Text = S(item, "text"),
                Status = S(item, "status") ?? "active",
                Source = S(item, "source") ?? "content",
                Priority = I(item, "priority")
            });
        }
        return def;
    }

    private static string S(JObject obj, string key)
    {
        JToken token = obj?[key];
        return token == null || token.Type == JTokenType.Null ? null : token.ToString();
    }

    private static int I(JObject obj, string key)
    {
        string v = S(obj, key);
        return int.TryParse(v, out int n) ? n : 0;
    }

    private static List<string> SL(JObject obj, string key)
    {
        var list = new List<string>();
        foreach (JToken token in obj?[key] as JArray ?? new JArray())
        {
            string v = token?.ToString();
            if (!string.IsNullOrWhiteSpace(v)) list.Add(v);
        }
        return list;
    }
}
