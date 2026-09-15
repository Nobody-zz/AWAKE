using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Awake;
using Newtonsoft.Json.Linq;

/// <summary>
/// 角色卡名册离线探针（方向丙-a 的验证装置）。
///
/// 用与运行时**同一份** src/PersonaRoster.cs 扫描 definitions 目录，逐条打印：
///   · 名册统计与全部警告（含"哪几张草稿被试行名单放行"）
///   · 按 characterId 的选卡结果（exact / fallback / miss）与源状态 vs 运行时状态
///   · 选中卡经真实 PersonaDslGenerator.Generate 生成的 DSL（字节数、是否退兜底）
///
/// 用法：
///   dotnet run -c Release -- persona-roster <worldbookRoot> [out.json] [charIds,逗号分隔] [dslOutDir]
/// </summary>
internal static class PersonaRosterSim
{
    internal static int Run(string[] args)
    {
        if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.WriteLine("用法: persona-roster <worldbookRoot> [out.json] [charIds,逗号分隔] [dslOutDir]");
            return 2;
        }
        string root = Path.GetFullPath(args[0]);
        string outPath = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]) ? args[1] : null;
        string dslOutDir = args.Length > 3 && !string.IsNullOrWhiteSpace(args[3]) ? args[3] : null;
        string[] probes = args.Length > 2 && !string.IsNullOrWhiteSpace(args[2])
            ? args[2].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : new[] { "lord_2_12", "lord_4_6", "lord_1_47_1", "lord_2_2", "lord_3_2", "lord_1_5", "lord_5_1" };

        Console.WriteLine("== persona 运行时名册 ==");
        Console.WriteLine("root          = " + root);
        if (!Directory.Exists(root))
        {
            Console.WriteLine("找不到世界书根目录。");
            return 2;
        }

        PersonaRoster roster = PersonaRoster.Create(root, root);
        Console.WriteLine("definitionDir = " + roster.DefinitionDirectory);
        Console.WriteLine("allowlist     = " + roster.AllowlistPath + "  exists=" + File.Exists(roster.AllowlistPath));
        Console.WriteLine("registry      = " + (roster.Registry == null ? "<missing>" : roster.Registry.Count + " tags / " + roster.Registry.BundleCount + " bundles"));
        Console.WriteLine("roster        = " + roster.BuildStatusText());
        Console.WriteLine();
        Console.WriteLine("-- 名册警告 " + roster.Warnings.Count + " 条 --");
        foreach (WorldbookImportWarning warning in roster.Warnings)
        {
            Console.WriteLine("  [" + warning.Code + "] " + warning.Source);
            Console.WriteLine("      " + warning.Message);
        }

        List<JObject> rows = new List<JObject>();
        int exactHits = 0;
        int fallbackHits = 0;
        int usableDsl = 0;
        Console.WriteLine();
        Console.WriteLine("== 选卡（source = 卡文件里的 status；runtime = 名册放行后的实际状态）==");
        foreach (string probe in probes)
        {
            PersonaDefinition definition;
            bool exact;
            bool found = roster.TrySelect(probe, out definition, out exact);
            string defId = found && definition != null ? definition.Id : "<none>";
            string sourceStatus = found && definition != null ? (definition.Raw?["status"]?.ToString() ?? "<missing>") : string.Empty;
            string runtimeStatus = found && definition != null ? definition.Status : string.Empty;
            if (found && exact) exactHits++;
            if (found && !exact) fallbackHits++;

            Console.WriteLine("  " + probe.PadRight(14) + " -> "
                + (found ? (exact ? "exact   " : "fallback") : "MISS    ")
                + "  def=" + defId
                + "  source=" + sourceStatus + " runtime=" + runtimeStatus);

            JObject row = new JObject
            {
                ["characterId"] = probe,
                ["result"] = found ? (exact ? "exact" : "fallback") : "miss",
                ["definitionId"] = defId,
                ["sourceStatus"] = sourceStatus,
                ["runtimeStatus"] = runtimeStatus
            };

            if (found && definition != null && roster.Registry != null)
            {
                ContextSnapshot snapshot = new ContextSnapshot
                {
                    CharacterId = probe,
                    HeroName = probe,
                    Role = definition.Role,
                    PersonaContinuity = new PersonaContinuityState()
                };
                PersonaGenerationResult generated = PersonaDslGenerator.Generate(
                    definition, roster.Registry, snapshot, PersonaDslGenerator.DefaultMaximumDslBytes);
                int bytes = Encoding.UTF8.GetByteCount(generated.Dsl ?? string.Empty);
                if (!generated.IsRuntimeFallback && generated.IsUsable) usableDsl++;
                row["dslBytes"] = bytes;
                row["runtimeFallback"] = generated.IsRuntimeFallback;
                row["legacyFallback"] = generated.UsedLegacyFallback;
                row["trimmed"] = generated.WasTrimmed;
                row["conflict"] = generated.HadConflict;
                row["dslFirstLine"] = (generated.Dsl ?? string.Empty).Split('\n')[0];
                row["dslWarnings"] = new JArray(generated.Warnings);

                Console.WriteLine("        dsl=" + bytes + "B runtimeFallback=" + generated.IsRuntimeFallback
                    + " legacyFallback=" + generated.UsedLegacyFallback
                    + " trimmed=" + generated.WasTrimmed
                    + " conflict=" + generated.HadConflict
                    + "  first=" + row.Value<string>("dslFirstLine"));
                foreach (string warning in generated.Warnings) Console.WriteLine("        WARN " + warning);

                if (!string.IsNullOrWhiteSpace(dslOutDir))
                {
                    Directory.CreateDirectory(dslOutDir);
                    string file = Path.Combine(dslOutDir, probe + ".dsl.txt");
                    File.WriteAllText(file, generated.Dsl ?? string.Empty, new UTF8Encoding(false));
                    row["dslPath"] = Path.GetFullPath(file);
                }
            }
            rows.Add(row);
        }

        Console.WriteLine();
        Console.WriteLine("== 汇总 ==");
        Console.WriteLine("  名册命中 testcase 数     = " + (exactHits + fallbackHits));
        Console.WriteLine("  精确命中 characterId     = " + exactHits);
        Console.WriteLine("  退兜底池                 = " + fallbackHits);
        Console.WriteLine("  生成出非兜底可用 DSL     = " + usableDsl);

        if (!string.IsNullOrWhiteSpace(outPath))
        {
            JObject output = new JObject
            {
                ["schemaVersion"] = "awake.persona.roster-probe.v1",
                ["root"] = root,
                ["definitionDirectory"] = roster.DefinitionDirectory,
                ["allowlistPath"] = roster.AllowlistPath,
                ["allowlistEntries"] = roster.AllowlistEntryCount,
                ["registryTags"] = roster.Registry == null ? 0 : roster.Registry.Count,
                ["registryBundles"] = roster.Registry == null ? 0 : roster.Registry.BundleCount,
                ["characters"] = roster.CharacterCount,
                ["fallbacks"] = roster.FallbackCount,
                ["digest"] = roster.Digest,
                ["admittedDraftIds"] = new JArray(roster.AdmittedDraftIds),
                ["warnings"] = new JArray(roster.Warnings.Select(item => new JObject
                {
                    ["code"] = item.Code,
                    ["source"] = item.Source,
                    ["message"] = item.Message
                })),
                ["selections"] = new JArray(rows)
            };
            File.WriteAllText(Path.GetFullPath(outPath), output.ToString(Newtonsoft.Json.Formatting.Indented), new UTF8Encoding(false));
            Console.WriteLine("  导出: " + Path.GetFullPath(outPath));
        }
        Console.WriteLine("ROSTER-OK");
        return 0;
    }
}
