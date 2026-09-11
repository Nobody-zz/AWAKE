using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace MarcusAwakeFramework.Tests
{
    internal static class EvidenceSchemaValidator
    {
        internal static void VerifyP3aE2(string evidencePath, string schemaPath = null)
        {
            if (string.IsNullOrWhiteSpace(evidencePath) || !File.Exists(evidencePath)) throw new InvalidOperationException("The P3A-E2 evidence file does not exist.");
            var directory = Path.GetDirectoryName(Path.GetFullPath(evidencePath));
            schemaPath = schemaPath ?? Path.Combine(directory, "schemas", "MARCUS-AWAKE-P3A-E2.schema.json");
            if (!File.Exists(schemaPath)) throw new InvalidOperationException("The P3A-E2 evidence schema does not exist.");
            var serializer = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 };
            var schema = Deserialize(serializer, File.ReadAllText(schemaPath));
            var evidence = Deserialize(serializer, File.ReadAllText(evidencePath));
            var required = GetArray(schema, "required");
            for (var index = 0; index < required.Count; index++) Require(evidence, Convert.ToString(required[index]), "Evidence field is missing.");
            AssertEqual("P3A-E2", Convert.ToString(evidence["evidence_level"]), "Evidence level is incorrect.");
            var runner = GetObject(evidence, "runner");
            AssertEqual("0", Convert.ToString(runner["exit_code"]), "Evidence runner did not exit successfully.");
            AssertEqual("P3A-RUNTIME-SUMMARY PASS", Convert.ToString(runner["stdout_prefix"]), "Evidence runner prefix is incorrect.");
            var fixtures = GetArray(evidence, "fixtures");
            if (fixtures.Count < 1) throw new InvalidOperationException("Evidence contains no fixture results.");
            for (var index = 0; index < fixtures.Count; index++)
            {
                var fixture = AsObject(fixtures[index], "fixture");
                Require(fixture, "expected", "Fixture expected result is missing.");
                Require(fixture, "actual", "Fixture actual result is missing.");
                Require(fixture, "failure_code", "Fixture failure code is missing.");
                AssertEqual("pass", Convert.ToString(fixture["actual"]), "A fixture did not pass.");
            }
            var redactions = GetArray(evidence, "redaction_assertions");
            for (var index = 0; index < redactions.Count; index++)
            {
                var assertion = AsObject(redactions[index], "redaction assertion");
                AssertEqual("True", Convert.ToString(assertion["passed"]), "A redaction assertion failed.");
                var expectedMode = Convert.ToString(assertion["expected_mode"]);
                if (expectedMode != "omitted" && expectedMode != "marker") throw new InvalidOperationException("A redaction assertion has an unknown expected mode.");
                AssertEqual(expectedMode, Convert.ToString(assertion["actual_mode"]), "A redaction assertion mode changed.");
                AssertEqual("False", Convert.ToString(assertion["forbidden_value_present"]), "A forbidden diagnostic value was present.");
            }
            var summary = GetObject(evidence, "summary");
            AssertEqual("passed", Convert.ToString(summary["status"]), "Evidence summary is not passed.");
        }

        private static Dictionary<string, object> Deserialize(JavaScriptSerializer serializer, string json)
        {
            var value = serializer.DeserializeObject(json) as Dictionary<string, object>;
            if (value == null) throw new InvalidOperationException("Evidence JSON root must be an object.");
            return value;
        }

        private static Dictionary<string, object> GetObject(Dictionary<string, object> parent, string name) => AsObject(parent[name], name);

        private static Dictionary<string, object> AsObject(object value, string name)
        {
            var result = value as Dictionary<string, object>;
            if (result == null) throw new InvalidOperationException(name + " must be an object.");
            return result;
        }

        private static List<object> GetArray(Dictionary<string, object> parent, string name)
        {
            var value = parent[name] as ArrayList;
            if (value != null) return new List<object>(value.ToArray());
            var objects = parent[name] as object[];
            if (objects != null) return new List<object>(objects);
            throw new InvalidOperationException(name + " must be an array.");
        }

        private static void Require(Dictionary<string, object> parent, string name, string message)
        {
            if (!parent.ContainsKey(name)) throw new InvalidOperationException(message + " field=" + name);
        }

        private static void AssertEqual(string expected, string actual, string message)
        {
            if (!StringComparer.Ordinal.Equals(expected, actual)) throw new InvalidOperationException(message + " expected=" + expected + " actual=" + actual);
        }
    }
}
