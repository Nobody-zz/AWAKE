using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Awake.UiLab;

namespace Awake.UiLab.Tests
{
    /// <summary>
    /// 本地 UI Lab 的 E2 生命周期用例执行器。
    /// 纯离线：不需要 Bannerlord、不渲染 Gauntlet、不碰游戏目录。
    /// 用法：dotnet run --project tools/awake-ui-lab/tests/Awake.UiLab.Tests
    /// </summary>
    internal static class Program
    {
        private sealed class Case
        {
            public string Name;
            public Action Run;

            public Case(string name, Action run)
            {
                Name = name;
                Run = run;
            }
        }

        private static int Main(string[] args)
        {
            string labRoot;
            if (!TryResolveLabRoot(args, out labRoot))
            {
                Console.WriteLine("FAIL lab_root_not_found（未找到 awake-ui-lab/import-manifest.v1.json）");
                return 2;
            }

            Console.WriteLine("lab_root=" + labRoot);

            if (ArgsContain(args, "--drive-fixtures"))
            {
                return DriveFixtures(args, labRoot);
            }

            string flowJsonPath = ResolveArgValue(args, "--flow-json");
            if (flowJsonPath != null)
            {
                return RunFlowJson(args, labRoot, flowJsonPath);
            }

            List<Case> cases = BuildCases(labRoot);

            List<string> passed = new List<string>();
            List<string> failed = new List<string>();

            for (int i = 0; i < cases.Count; i++)
            {
                try
                {
                    cases[i].Run();
                    passed.Add(cases[i].Name);
                    Console.WriteLine("PASS " + cases[i].Name);
                }
                catch (Exception ex)
                {
                    failed.Add(cases[i].Name);
                    Console.WriteLine("FAIL " + cases[i].Name + " :: " + ex.Message);
                }
            }

            Console.WriteLine("SUMMARY passed=" + passed.Count.ToString()
                + " failed=" + failed.Count.ToString()
                + " total=" + cases.Count.ToString());

            StringBuilder json = new StringBuilder();
            json.Append("{\"schema\":\"awake.ui-lab.lifecycle-e2.v1\",\"evidence_level\":\"E2\",");
            json.Append("\"game_launch\":false,\"rendering\":false,\"total\":").Append(cases.Count).Append(",");
            json.Append("\"passed\":").Append(passed.Count).Append(",");
            json.Append("\"failed\":").Append(failed.Count).Append(",");
            json.Append("\"passed_cases\":[");
            for (int i = 0; i < passed.Count; i++)
            {
                if (i > 0) json.Append(",");
                json.Append("\"").Append(passed[i]).Append("\"");
            }
            json.Append("],\"failed_cases\":[");
            for (int i = 0; i < failed.Count; i++)
            {
                if (i > 0) json.Append(",");
                json.Append("\"").Append(failed[i]).Append("\"");
            }
            json.Append("]}");
            Console.WriteLine("EVIDENCE " + json.ToString());

            string evidencePath = ResolveEvidencePath(args);
            if (evidencePath != null)
            {
                StringBuilder evidence = new StringBuilder();
                evidence.Append("{\r\n");
                evidence.Append("  \"schema\": \"awake.ui-lab.lifecycle-e2.v1\",\r\n");
                evidence.Append("  \"evidenceLevel\": \"E2\",\r\n");
                evidence.Append("  \"generatedAtUtc\": \"").Append(DateTime.UtcNow.ToString("o")).Append("\",\r\n");
                evidence.Append("  \"command\": \"dotnet run --project tools/awake-ui-lab/tests/Awake.UiLab.Tests\",\r\n");
                evidence.Append("  \"labRoot\": \"").Append(Escape(labRoot)).Append("\",\r\n");
                evidence.Append("  \"gameLaunch\": false,\r\n");
                evidence.Append("  \"rendering\": false,\r\n");
                evidence.Append("  \"limitation\": \"本地离线用例；不等于 Bannerlord 真机渲染，渲染/输入/焦点最终判定为 E4。\",\r\n");
                evidence.Append("  \"total\": ").Append(cases.Count).Append(",\r\n");
                evidence.Append("  \"passed\": ").Append(passed.Count).Append(",\r\n");
                evidence.Append("  \"failed\": ").Append(failed.Count).Append(",\r\n");
                evidence.Append("  \"assertionsCovered\": [\r\n");
                evidence.Append("    \"movie/layer/焦点/输入限制 四项释放\",\r\n");
                evidence.Append("    \"创建计数 == 释放计数\",\r\n");
                evidence.Append("    \"generation 单调且旧 generation 被栅栏忽略\",\r\n");
                evidence.Append("    \"确认/拒绝只改夹具内部状态\"\r\n");
                evidence.Append("  ],\r\n");
                evidence.Append("  \"passedCases\": [");
                for (int i = 0; i < passed.Count; i++)
                {
                    if (i > 0) evidence.Append(", ");
                    evidence.Append("\"").Append(passed[i]).Append("\"");
                }
                evidence.Append("],\r\n  \"failedCases\": [");
                for (int i = 0; i < failed.Count; i++)
                {
                    if (i > 0) evidence.Append(", ");
                    evidence.Append("\"").Append(failed[i]).Append("\"");
                }
                evidence.Append("]\r\n}\r\n");

                string directory = Path.GetDirectoryName(Path.GetFullPath(evidencePath));
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(Path.GetFullPath(evidencePath), evidence.ToString(), new UTF8Encoding(false));
                Console.WriteLine("EVIDENCE_FILE " + Path.GetFullPath(evidencePath));
            }

            if (failed.Count > 0)
            {
                Console.WriteLine("FAIL ALL UI LAB E2");
                return 1;
            }
            Console.WriteLine("PASS ALL UI LAB E2");
            return 0;
        }

        private static string ResolveEvidencePath(string[] args)
        {
            return ResolveArgValue(args, "--evidence");
        }

        private static string ResolveArgValue(string[] args, string name)
        {
            if (args == null) return null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        /// <summary>E2 生命周期用例清单（与 --flow-json 共用同一份，避免两处漂移）。</summary>
        private static List<Case> BuildCases(string labRoot)
        {
            List<Case> cases = new List<Case>();
            cases.Add(new Case("duplicate_open_coalesced", UiLabLifecycleFixtures.DuplicateOpenCoalesced));
            cases.Add(new Case("open_while_closing_reopens_after_cleanup", UiLabLifecycleFixtures.OpenWhileClosingReopensAfterCleanup));
            cases.Add(new Case("screen_switch_releases_all", UiLabLifecycleFixtures.ScreenSwitchReleasesAll));
            cases.Add(new Case("partial_load_failure_rolls_back", UiLabLifecycleFixtures.PartialLoadFailureRollsBack));
            cases.Add(new Case("close_reopen_no_leak", UiLabLifecycleFixtures.CloseReopenNoLeak));
            cases.Add(new Case("unload_close_intent_only_then_tick_cleanup", UiLabLifecycleFixtures.UnloadCloseIntentOnlyThenTickCleanup));
            cases.Add(new Case("cleanup_retry_then_exhausted", UiLabLifecycleFixtures.CleanupRetryThenExhausted));
            cases.Add(new Case("stale_generation_ignored", UiLabLifecycleFixtures.StaleGenerationIgnored));
            cases.Add(new Case("dual_owner_conflict", UiLabLifecycleFixtures.DualOwnerConflict));
            cases.Add(new Case("reserved_production_movie_rejected", UiLabLifecycleFixtures.ReservedProductionMovieRejected));
            cases.Add(new Case("fixture_decision_state_is_internal_only", UiLabLifecycleFixtures.FixtureDecisionStateIsInternalOnly));
            cases.Add(new Case("fixture_state_ids_match_json", delegate { UiLabLifecycleFixtures.FixtureStateIdsMatchJson(labRoot); }));
            cases.Add(new Case("prefab_binding_parity", delegate { UiLabLifecycleFixtures.PrefabBindingParity(labRoot); }));
            cases.Add(new Case("fixture_runner_builds_all_known_states", UiLabLifecycleFixtures.FixtureRunnerBuildsAllKnownStates));
            cases.Add(new Case("fixture_decision_lifecycle_only_internal", UiLabLifecycleFixtures.FixtureDecisionLifecycleOnlyInternal));
            cases.Add(new Case("long_text_fixture_has_content", UiLabLifecycleFixtures.LongTextFixtureHasContent));
            return cases;
        }

        /// <summary>
        /// 一次跑完「E2 生命周期用例 + 全量 fixture 驱动」，写成一份机器可读的 flow JSON，
        /// 供 awake_ui.py 直接读进报告的 flow 字段。纯离线，不启动游戏、不渲染 Gauntlet。
        /// </summary>
        private static int RunFlowJson(string[] args, string labRoot, string path)
        {
            List<Case> cases = BuildCases(labRoot);
            List<string> passed = new List<string>();
            List<string> failed = new List<string>();
            StringBuilder failedDetail = new StringBuilder();

            for (int i = 0; i < cases.Count; i++)
            {
                try
                {
                    cases[i].Run();
                    passed.Add(cases[i].Name);
                }
                catch (Exception ex)
                {
                    failed.Add(cases[i].Name);
                    if (failedDetail.Length > 0) failedDetail.Append(", ");
                    failedDetail.Append("{\"case\":\"").Append(cases[i].Name)
                        .Append("\",\"error\":\"").Append(Escape(ex.Message)).Append("\"}");
                }
            }

            UiLabFixtureReport fixtures = UiLabFixtureRunner.RunAll();

            Console.WriteLine("flow_json lab_root=" + labRoot);
            Console.WriteLine("  e2       passed=" + passed.Count.ToString()
                + " failed=" + failed.Count.ToString() + " total=" + cases.Count.ToString());
            Console.WriteLine("  fixtures built=" + fixtures.Built.ToString()
                + " failed=" + fixtures.Failed.ToString() + " total=" + fixtures.Total.ToString());

            StringBuilder sb = new StringBuilder();
            sb.Append("{\r\n");
            sb.Append("  \"schema\": \"awake.ui-lab.flow.v1\",\r\n");
            sb.Append("  \"evidenceLevel\": \"E2\",\r\n");
            sb.Append("  \"generatedAtUtc\": \"").Append(DateTime.UtcNow.ToString("o")).Append("\",\r\n");
            sb.Append("  \"labRoot\": \"").Append(Escape(labRoot)).Append("\",\r\n");
            sb.Append("  \"gameLaunch\": false,\r\n");
            sb.Append("  \"rendering\": false,\r\n");
            sb.Append("  \"limitation\": \"本地离线验证；不启动 Bannerlord、不渲染 Gauntlet，不等于真机（真机为 E4）。\",\r\n");
            sb.Append("  \"ok\": ").Append((failed.Count == 0 && fixtures.Failed == 0) ? "true" : "false").Append(",\r\n");
            sb.Append("  \"e2\": {\r\n");
            sb.Append("    \"total\": ").Append(cases.Count).Append(",\r\n");
            sb.Append("    \"passed\": ").Append(passed.Count).Append(",\r\n");
            sb.Append("    \"failed\": ").Append(failed.Count).Append(",\r\n");
            sb.Append("    \"passedCases\": [");
            for (int i = 0; i < passed.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append("\"").Append(passed[i]).Append("\"");
            }
            sb.Append("],\r\n    \"failedCases\": [");
            for (int i = 0; i < failed.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append("\"").Append(failed[i]).Append("\"");
            }
            sb.Append("],\r\n    \"failedDetail\": [").Append(failedDetail).Append("]\r\n  },\r\n");
            sb.Append("  \"fixtures\": {\r\n");
            sb.Append("    \"total\": ").Append(fixtures.Total).Append(",\r\n");
            sb.Append("    \"built\": ").Append(fixtures.Built).Append(",\r\n");
            sb.Append("    \"failed\": ").Append(fixtures.Failed).Append(",\r\n");
            sb.Append("    \"entries\": [");
            for (int i = 0; i < fixtures.Entries.Count; i++)
            {
                if (i > 0) sb.Append(",");
                UiLabFixtureReportEntry e = fixtures.Entries[i];
                sb.Append("\r\n      {\"stateId\":\"").Append(e.StateId).Append("\"");
                sb.Append(",\"built\":").Append(e.Built ? "true" : "false");
                sb.Append(",\"rowCount\":").Append(e.RowCount);
                sb.Append(",\"decision\":\"").Append(e.Decision).Append("\"");
                sb.Append(",\"detailLength\":").Append(e.DetailLength);
                sb.Append(",\"error\":\"").Append(Escape(e.Error ?? string.Empty)).Append("\"}");
            }
            sb.Append("\r\n    ]\r\n  }\r\n}\r\n");

            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(Path.GetFullPath(path), sb.ToString(), new UTF8Encoding(false));
            Console.WriteLine("FLOW_JSON_FILE " + Path.GetFullPath(path));

            return (failed.Count == 0 && fixtures.Failed == 0) ? 0 : 1;
        }

        private static string Escape(string value)
        {
            if (value == null) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static bool TryResolveLabRoot(string[] args, out string labRoot)
        {
            labRoot = null;

            if (args != null)
            {
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--lab-root", StringComparison.Ordinal))
                    {
                        labRoot = Path.GetFullPath(args[i + 1]);
                        return File.Exists(Path.Combine(labRoot, "import-manifest.v1.json"));
                    }
                }
            }

            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
            for (int depth = 0; depth < 12 && directory != null; depth++)
            {
                if (File.Exists(Path.Combine(directory.FullName, "import-manifest.v1.json"))
                    && Directory.Exists(Path.Combine(directory.FullName, "fixtures")))
                {
                    labRoot = directory.FullName;
                    return true;
                }
                directory = directory.Parent;
            }
            return false;
        }

        private static bool ArgsContain(string[] args, string flag)
        {
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>
        /// 本地全量 fixture 验收入口（不启动游戏、不渲染 Gauntlet）。
        /// 遍历所有登记的状态 ID，逐个造夹具并判定非空壳，输出本地报告。
        /// </summary>
        private static int DriveFixtures(string[] args, string labRoot)
        {
            UiLabFixtureReport report = UiLabFixtureRunner.RunAll();
            Console.WriteLine("drive_fixtures total=" + report.Total.ToString()
                + " built=" + report.Built.ToString()
                + " failed=" + report.Failed.ToString());
            for (int i = 0; i < report.Entries.Count; i++)
            {
                UiLabFixtureReportEntry entry = report.Entries[i];
                Console.WriteLine((entry.Built ? "OK   " : "BAD  ") + entry.StateId
                    + " rows=" + entry.RowCount.ToString()
                    + " decision=" + entry.Decision
                    + " detailLen=" + entry.DetailLength.ToString()
                    + (entry.Built ? "" : " err=" + entry.Error));
            }

            string evidencePath = ResolveEvidencePath(args);
            if (evidencePath != null)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("{\r\n");
                sb.Append("  \"schema\": \"awake.ui-lab.fixture-drive.v1\",\r\n");
                sb.Append("  \"evidenceLevel\": \"E2\",\r\n");
                sb.Append("  \"generatedAtUtc\": \"").Append(DateTime.UtcNow.ToString("o")).Append("\",\r\n");
                sb.Append("  \"gameLaunch\": false,\r\n");
                sb.Append("  \"rendering\": false,\r\n");
                sb.Append("  \"limitation\": \"本地离线夹具验收；不渲染 Gauntlet，不等于真机。\",\r\n");
                sb.Append("  \"total\": ").Append(report.Total).Append(",\r\n");
                sb.Append("  \"built\": ").Append(report.Built).Append(",\r\n");
                sb.Append("  \"failed\": ").Append(report.Failed).Append(",\r\n");
                sb.Append("  \"entries\": [");
                for (int i = 0; i < report.Entries.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    UiLabFixtureReportEntry e = report.Entries[i];
                    sb.Append("\r\n    {\"stateId\":\"").Append(e.StateId).Append("\"");
                    sb.Append(",\"built\":").Append(e.Built ? "true" : "false");
                    sb.Append(",\"rowCount\":").Append(e.RowCount);
                    sb.Append(",\"decision\":\"").Append(e.Decision).Append("\"");
                    sb.Append(",\"detailLength\":").Append(e.DetailLength);
                    sb.Append(",\"error\":\"").Append(Escape(e.Error ?? string.Empty)).Append("\"}");
                }
                sb.Append("\r\n  ]\r\n}\r\n");
                string directory = Path.GetDirectoryName(Path.GetFullPath(evidencePath));
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(Path.GetFullPath(evidencePath), sb.ToString(), new UTF8Encoding(false));
                Console.WriteLine("EVIDENCE_FILE " + Path.GetFullPath(evidencePath));
            }

            return report.Failed > 0 ? 1 : 0;
        }
    }
}
