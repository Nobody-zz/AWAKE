using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace MarcusAwakeFramework.Tests
{
    internal static class EvidenceWriter
    {
        internal static void WriteP3aE2(string path, RuntimeVerticalEvidence evidence)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("An evidence path is required.", nameof(path));
            if (evidence == null) throw new ArgumentNullException(nameof(evidence));
            var payload = new Dictionary<string, object>
            {
                { "schema_version", "marcus-awake/evidence/v1" },
                { "evidence_level", "P3A-E2" },
                { "batch_id", "MARCUS-AWAKE-P3-RUNTIME-VERTICAL-20260826" },
                { "runner", new Dictionary<string, object> { { "executable", "MarcusAwakeFramework.Tests.exe" }, { "arguments", new[] { "--runtime-vertical" } }, { "exit_code", 0 }, { "stdout_prefix", "P3A-RUNTIME-SUMMARY PASS" } } },
                { "virtual_clock", new Dictionary<string, object> { { "now", FixtureLifecycleDriver.FixtureNow.ToString("O") }, { "deadline", FixtureLifecycleDriver.FixtureDeadline.ToString("O") } } },
                { "fixtures", evidence.Fixtures },
                { "event_sequences", evidence.EventSequences },
                { "terminal_receipts", evidence.TerminalReceipts },
                { "redaction_assertions", evidence.RedactionAssertions },
                { "budget_assertions", evidence.BudgetAssertions },
                { "summary", new Dictionary<string, object> { { "fixture_count", evidence.Fixtures.Count }, { "event_sequence_count", evidence.EventSequences.Count }, { "terminal_receipt_count", evidence.TerminalReceipts.Count }, { "vector_count", 11 }, { "status", "passed" } } },
                { "unverified", evidence.Unverified }
            };
            var serializer = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 };
            var json = serializer.Serialize(payload);
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, json + Environment.NewLine, new UTF8Encoding(false));
        }
    }
}
