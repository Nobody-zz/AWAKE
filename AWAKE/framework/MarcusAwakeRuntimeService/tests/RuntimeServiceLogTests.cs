using System;
using System.IO;
using System.Text;

namespace MarcusAwakeRuntimeService.Tests;

internal static class RuntimeServiceLogTests
{
    internal static int Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "awake-runtime-service-log-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, RuntimeServiceLog.FileName);

            var inner = new StringWriter();
            using (var writer = RuntimeServiceLog.CreateTee(inner, path))
            {
                writer.WriteLine("provider_dispatch_failed:InvalidOperationException:correlation=abc");
                writer.Flush();
            }

            Require(inner.ToString().Contains("provider_dispatch_failed"), "stderr_tee_dropped_stream");
            var written = File.ReadAllText(path, Encoding.UTF8);
            Require(written.Contains("provider_dispatch_failed:InvalidOperationException:correlation=abc"), "stderr_tee_dropped_file");
            Require(written.EndsWith(Environment.NewLine, StringComparison.Ordinal), "stderr_tee_missing_newline");
            Console.WriteLine("CASE stderr_tee_mirrors_stream_and_file PASS");

            using (var writer = RuntimeServiceLog.CreateTee(TextWriter.Null, path))
            {
                writer.Write(new string('x', (int)RuntimeServiceLog.MaximumBytes));
                writer.Flush();
                writer.Write(new string('y', 1024));
                writer.Flush();
            }

            var rotated = path + ".1";
            Require(File.Exists(rotated), "rotation_did_not_produce_backup");
            Require(new FileInfo(rotated).Length <= RuntimeServiceLog.MaximumBytes + 4096, "rotation_left_oversized_backup");
            Require(File.ReadAllText(path, Encoding.UTF8).Equals(new string('y', 1024), StringComparison.Ordinal), "rotation_lost_current_file");
            Console.WriteLine("CASE stderr_log_rotates_at_cap PASS");

            using (var writer = RuntimeServiceLog.CreateTee(new StringWriter(), Path.Combine(root, "unwritable", "bad\u0000name")))
            {
                writer.WriteLine("still forwarded to stderr");
                writer.Flush();
            }

            Console.WriteLine("CASE unwritable_log_path_is_non_fatal PASS");

            var previousRoot = Environment.GetEnvironmentVariable("MARCUS_AWAKE_RUNTIME_DATA_ROOT");
            var previousError = Console.Error;
            var overrideRoot = Path.Combine(root, "override-root");
            Environment.SetEnvironmentVariable("MARCUS_AWAKE_RUNTIME_DATA_ROOT", overrideRoot);
            try
            {
                var expected = Path.Combine(Path.GetFullPath(overrideRoot), RuntimeServiceLog.FileName);
                Require(
                    string.Equals(RuntimeServiceLog.ResolvePath(), expected, StringComparison.OrdinalIgnoreCase),
                    "resolve_path_ignored_env_root");

                RuntimeServiceLog.Enable();

                Require(File.Exists(expected), "enable_did_not_create_log_file");
                Require(
                    ReadShared(expected).Contains("runtime_service_log_enabled path=", StringComparison.Ordinal),
                    "enable_banner_missing");

                Console.Error.WriteLine("provider_dispatch_failed:InvalidOperationException:correlation=enable-path");
                Console.Error.Flush();

                Require(
                    ReadShared(expected).Contains("correlation=enable-path", StringComparison.Ordinal),
                    "enable_path_did_not_mirror_stderr");
            }
            finally
            {
                Console.SetError(previousError);
                Environment.SetEnvironmentVariable("MARCUS_AWAKE_RUNTIME_DATA_ROOT", previousRoot);
            }

            Console.WriteLine("CASE enable_creates_file_and_honours_env_root PASS");
            Console.WriteLine("RUNTIME_SERVICE_LOG 4/4 PASS");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("RUNTIME_SERVICE_LOG FAIL " + exception.GetType().Name + ":" + exception.Message);
            return 1;
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
