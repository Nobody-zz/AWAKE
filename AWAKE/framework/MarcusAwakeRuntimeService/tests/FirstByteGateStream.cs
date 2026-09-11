using MarcusAwakeTransport;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeRuntimeService.Tests;

internal enum ControlledWriteMode
{
    Success,
    ZeroWrite,
    PartialWrite,
    WriteException,
    FlushFailure
}

internal sealed class FirstByteGateStream : IFrameWriteSink
{
    private readonly ControlledWriteMode mode;
    private readonly int partialBytes;
    private readonly List<byte> bytes = new List<byte>();

    internal FirstByteGateStream(ControlledWriteMode mode, int partialBytes = 0)
    {
        this.mode = mode;
        this.partialBytes = partialBytes;
    }

    internal IReadOnlyList<byte> Bytes => bytes;
    internal int WriteCallCount { get; private set; }
    internal int FlushCallCount { get; private set; }

    public Task<int> WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        WriteCallCount++;
        cancellationToken.ThrowIfCancellationRequested();
        if (mode == ControlledWriteMode.WriteException) throw new IOException("controlled_write_exception");
        if (mode == ControlledWriteMode.ZeroWrite) return Task.FromResult(0);

        var written = mode == ControlledWriteMode.PartialWrite
            ? Math.Min(Math.Max(partialBytes, 1), Math.Max(count - 1, 1))
            : count;
        for (var index = 0; index < written; index++) bytes.Add(buffer[offset + index]);
        return Task.FromResult(written);
    }

    public Task FlushAsync(CancellationToken cancellationToken)
    {
        FlushCallCount++;
        cancellationToken.ThrowIfCancellationRequested();
        if (mode == ControlledWriteMode.FlushFailure) throw new IOException("controlled_flush_failure");
        return Task.CompletedTask;
    }
}
