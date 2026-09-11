using System;
using System.IO;
using System.Text;
using System.Threading;

namespace MarcusAwakeRuntimeService;

/// <summary>
/// Mirrors the runtime service's stderr diagnostics into a bounded log file next to the provider
/// outcome ledger. The stderr stream itself is unchanged - the framework still drains it - so this
/// only adds a durable copy that outlives the parent process discarding the pipes.
/// </summary>
internal static class RuntimeServiceLog
{
    internal const string FileName = "awake-runtime-service.log";
    internal const long MaximumBytes = 2L * 1024 * 1024;

    private static int enabled;

    internal static void Enable()
    {
        if (Interlocked.Exchange(ref enabled, 1) == 1) return;
        try
        {
            var path = ResolvePath();
            WriteBanner(path);
            Console.SetError(CreateTee(Console.Error, path));
        }
        catch
        {
            // Diagnostics must never prevent the service from starting.
        }
    }

    internal static string ResolvePath()
    {
        var root = Environment.GetEnvironmentVariable("MARCUS_AWAKE_RUNTIME_DATA_ROOT");
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AWAKE",
                "RuntimeData");
        }

        return Path.Combine(Path.GetFullPath(root), FileName);
    }

    internal static TextWriter CreateTee(TextWriter inner, string path)
    {
        return new TeeWriter(inner, path);
    }

    private static void WriteBanner(string path)
    {
        var writer = TryOpen(path);
        if (writer == null) return;
        try
        {
            writer.Write("runtime_service_log_enabled path=" + path + Environment.NewLine);
            writer.Flush();
        }
        catch
        {
            // Diagnostics must never break the service.
        }
        finally
        {
            try { writer.Dispose(); } catch { }
        }
    }

    private static StreamWriter? TryOpen(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            return new StreamWriter(
                new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete),
                new UTF8Encoding(false));
        }
        catch
        {
            return null;
        }
    }

    private sealed class TeeWriter : TextWriter
    {
        private readonly TextWriter inner;
        private readonly string path;
        private readonly object gate = new object();
        private StreamWriter? file;

        internal TeeWriter(TextWriter inner, string path)
        {
            this.inner = inner;
            this.path = path;
        }

        public override Encoding Encoding => inner == null ? Encoding.UTF8 : inner.Encoding;

        public override void Write(char value)
        {
            Forward(value.ToString());
        }

        public override void Write(string? value)
        {
            Forward(value);
        }

        public override void Write(char[] buffer, int index, int count)
        {
            Forward(buffer == null ? null : new string(buffer, index, count));
        }

        public override void WriteLine()
        {
            Forward(Environment.NewLine);
        }

        public override void WriteLine(string? value)
        {
            Forward(value + Environment.NewLine);
        }

        public override void Flush()
        {
            try { inner?.Flush(); } catch { }
            lock (gate)
            {
                try { file?.Flush(); } catch { }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (gate)
                {
                    try { file?.Dispose(); } catch { }
                    file = null;
                }
            }

            base.Dispose(disposing);
        }

        private void Forward(string? value)
        {
            try { inner?.Write(value); } catch { }
            if (string.IsNullOrEmpty(value)) return;
            lock (gate)
            {
                try
                {
                    file ??= TryOpen(path);
                    if (file == null) return;
                    if (file.BaseStream.Length >= MaximumBytes) Rotate();
                    if (file == null) return;
                    file.Write(value);
                    file.Flush();
                }
                catch
                {
                    // Diagnostics must never break the service.
                }
            }
        }

        private void Rotate()
        {
            try { file?.Dispose(); } catch { }
            file = null;
            try
            {
                var rotated = path + ".1";
                if (File.Exists(rotated)) File.Delete(rotated);
                if (File.Exists(path)) File.Move(path, rotated);
            }
            catch
            {
                // A failed rotation must not lose diagnostics.
            }

            file = TryOpen(path);
        }
    }
}
