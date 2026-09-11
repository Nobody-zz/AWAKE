using System;
using System.Collections.Generic;
using MarcusAwakeFramework.Api;

namespace MarcusAwakeFramework.Tests
{
    internal static class TaskRequestCanonicalizerVectors
    {
        internal static void Run(RuntimeVerticalEvidence evidence)
        {
            var schema = new SchemaRef("schema", new ApiVersion(1, 0));
            var taskInput = new TaskRequestCanonicalInput("route", "provider", "profile", "hello", "{\"z\":null,\"a\":\"e\\u0301\"}", schema, "not_applicable", 1, 2, 3, 4);
            var task = TaskRequestCanonicalizer.ForTask(taskInput);
            AssertEx.True(task.IsSuccess, "Task canonicalizer vector failed.");
            AssertEx.Equal("{\"delta_budget\":4,\"input\":{\"a\":\"é\",\"z\":null},\"input_byte_budget\":1,\"message\":\"hello\",\"output_byte_budget\":2,\"output_schema\":{\"id\":\"schema\",\"version\":{\"major\":1,\"minor\":0}},\"profile_id\":\"profile\",\"provider_id\":\"provider\",\"route_id\":\"route\",\"settlement_requirement\":\"not_applicable\",\"token_budget\":3}", task.Value.CanonicalJson, "Task canonical JSON vector changed.");
            AssertEx.Equal("c44fe3b366d94141dd7a96fdc3a8421b842e88a9b3d357c8b8ea397ecfcb8523", task.Value.Hash, "Task canonical hash vector changed.");

            var nullValue = TaskRequestCanonicalizer.CanonicalizeJson("{\"value\":null}", TaskRequestCanonicalizer.TaskDomain);
            AssertEx.True(nullValue.IsSuccess && StringComparer.Ordinal.Equals("{\"value\":null}", nullValue.Value.CanonicalJson), "Null canonicalization vector failed.");
            var nfc = TaskRequestCanonicalizer.CanonicalizeJson("{\"text\":\"e\\u0301\"}", TaskRequestCanonicalizer.TaskDomain);
            AssertEx.True(nfc.IsSuccess && StringComparer.Ordinal.Equals("{\"text\":\"é\"}", nfc.Value.CanonicalJson), "NFC canonicalization vector failed.");
            var numbers = TaskRequestCanonicalizer.CanonicalizeJson("{\"n\":1.5,\"i\":-2}", TaskRequestCanonicalizer.TaskDomain);
            AssertEx.True(numbers.IsSuccess && StringComparer.Ordinal.Equals("{\"i\":-2,\"n\":1.5}", numbers.Value.CanonicalJson), "Number canonicalization vector failed.");
            var egress = TaskRequestCanonicalizer.ForEgress(new EgressCanonicalInput("route", "provider", "profile", "{\"b\":2,\"a\":1}", new[] { "field-b", "field-a", "field-a" }, new[] { "grant-b", "grant-a" }, new[] { "archive-2", "archive-1" }, new[] { "entry-2", "entry-1" }, new[] { "api-b", "api-a", "api-a" }));
            AssertEx.True(egress.IsSuccess, "Egress canonicalizer vector failed.");
            AssertEx.Equal("{\"allowed_domains\":[\"api-a\",\"api-b\"],\"allowed_field_ids\":[\"field-a\",\"field-b\"],\"archive_ids\":[\"archive-1\",\"archive-2\"],\"entry_ids\":[\"entry-1\",\"entry-2\"],\"grant_rule_ids\":[\"grant-a\",\"grant-b\"],\"input\":{\"a\":1,\"b\":2},\"profile_id\":\"profile\",\"provider_id\":\"provider\",\"route_id\":\"route\"}", egress.Value.CanonicalJson, "Egress canonical JSON vector changed.");
            AssertEx.Equal("e996f0318293ffbce6418f07b54d3c770cab93ce466dd03a864ef39a52724db2", egress.Value.Hash, "Egress canonical hash vector changed.");
            AssertEx.True(!StringComparer.Ordinal.Equals(task.Value.Hash, egress.Value.Hash), "Task and egress domains were not isolated.");

            AssertFailure("{\"a\":1,\"a\":2}", "canonical_json_duplicate_key");
            AssertFailure("{\"n\":1.0}", "canonical_number_noncanonical");
            AssertFailure("{\"n\":NaN}", "canonical_json_invalid");
            AssertFailure("{\"n\":Infinity}", "canonical_json_invalid");
            AssertFailure("{\"x\":1} trailing", "canonical_json_trailing_data");
            AssertFailure("{\"x\":", "canonical_json_unexpected_end");
            AssertFailure("", "canonical_json_missing");
        }

        private static void AssertFailure(string json, string code)
        {
            var result = TaskRequestCanonicalizer.CanonicalizeJson(json, TaskRequestCanonicalizer.TaskDomain, correlationId: "canonical-vector");
            AssertEx.Error(result, code, FrameworkErrorCategory.InvalidRequest);
        }
    }
}
