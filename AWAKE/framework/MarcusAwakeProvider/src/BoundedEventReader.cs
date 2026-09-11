using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace MarcusAwakeProvider;

internal sealed class BoundedInputException : Exception
{
    internal BoundedInputException(string code)
        : base(code)
    {
        Code = code;
    }

    internal string Code { get; }
}

internal sealed record SseFrame(string EventName, string Data);

internal static class BoundedEventReader
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

    internal static async IAsyncEnumerable<SseFrame> ReadSseFramesAsync(
        Stream stream,
        ProviderLimits limits,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var lines = new BoundedLineReader(stream, limits.MaxStreamBytes, limits.MaxSseLineBytes);
        var eventName = string.Empty;
        var data = new StringBuilder();
        var dataBytes = 0;
        var eventBytes = 0;
        var hasData = false;

        while (true)
        {
            var line = await lines.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line == null) break;
            var text = StrictUtf8.GetString(line);
            if (text.EndsWith("\r", StringComparison.Ordinal)) text = text[..^1];
            if (text.Length == 0)
            {
                if (hasData)
                {
                    yield return new SseFrame(eventName, data.ToString());
                    eventName = string.Empty;
                    data.Clear();
                    dataBytes = 0;
                    eventBytes = 0;
                    hasData = false;
                }

                continue;
            }

            eventBytes += line.Length;
            if (eventBytes > limits.MaxSseEventBytes) throw new BoundedInputException("sse_event_too_large");
            if (text.StartsWith(":", StringComparison.Ordinal)) continue;
            var separator = text.IndexOf(':');
            var field = separator < 0 ? text : text[..separator];
            var value = separator < 0 ? string.Empty : text[(separator + 1)..];
            if (value.StartsWith(" ", StringComparison.Ordinal)) value = value[1..];
            if (field == "event")
            {
                eventName = value;
            }
            else if (field == "data")
            {
                var valueBytes = StrictUtf8.GetByteCount(value) + (hasData ? 1 : 0);
                dataBytes += valueBytes;
                if (dataBytes > limits.MaxSseDataBytes) throw new BoundedInputException("sse_data_too_large");
                if (hasData) data.Append('\n');
                data.Append(value);
                hasData = true;
            }
        }

        if (hasData) yield return new SseFrame(eventName, data.ToString());
    }

    internal static async IAsyncEnumerable<string> ReadLinesAsync(
        Stream stream,
        ProviderLimits limits,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var lines = new BoundedLineReader(stream, limits.MaxStreamBytes, limits.MaxSseLineBytes);
        while (true)
        {
            var line = await lines.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line == null) yield break;
            if (line[^1] == 13) Array.Resize(ref line, line.Length - 1);
            if (line.Length == 0) continue;
            yield return StrictUtf8.GetString(line);
        }
    }
}

internal sealed class BoundedLineReader : IDisposable
{
    private readonly Stream stream;
    private readonly int maximumAggregateBytes;
    private readonly int maximumLineBytes;
    private readonly byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);
    private readonly List<byte> line = new List<byte>();
    private int bufferOffset;
    private int bufferedBytes;
    private int aggregateBytes;
    private bool endOfStream;
    private bool disposed;

    internal BoundedLineReader(Stream stream, int maximumAggregateBytes, int maximumLineBytes)
    {
        this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
        this.maximumAggregateBytes = maximumAggregateBytes;
        this.maximumLineBytes = maximumLineBytes;
    }

    internal async ValueTask<byte[]?> ReadLineAsync(CancellationToken cancellationToken)
    {
        if (disposed) throw new ObjectDisposedException(nameof(BoundedLineReader));
        while (true)
        {
            if (bufferOffset >= bufferedBytes)
            {
                if (endOfStream)
                {
                    if (line.Count == 0) return null;
                    var finalLine = line.ToArray();
                    line.Clear();
                    return finalLine;
                }

                bufferOffset = 0;
                bufferedBytes = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (bufferedBytes == 0)
                {
                    endOfStream = true;
                    continue;
                }

                aggregateBytes += bufferedBytes;
                if (aggregateBytes > maximumAggregateBytes) throw new BoundedInputException("stream_too_large");
            }

            var value = buffer[bufferOffset++];
            if (value == 10)
            {
                var result = line.ToArray();
                line.Clear();
                return result;
            }

            line.Add(value);
            if (line.Count > maximumLineBytes) throw new BoundedInputException("sse_line_too_large");
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        line.Clear();
        Array.Clear(buffer, 0, buffer.Length);
        ArrayPool<byte>.Shared.Return(buffer);
    }
}
