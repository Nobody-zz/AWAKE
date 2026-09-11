using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeTransport
{
    public interface IFrameWriteSink
    {
        Task<int> WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
        Task FlushAsync(CancellationToken cancellationToken);
    }

    public sealed class FrameWriteResult
    {
        internal FrameWriteResult(bool writeStarted, bool firstByteCommitted, int bytesWritten, int frameBytesExpected, bool flushCompleted, bool frameComplete, bool responseSuppressed, string failureKind)
        {
            WriteStarted = writeStarted;
            FirstByteCommitted = firstByteCommitted;
            BytesWritten = bytesWritten;
            FrameBytesExpected = frameBytesExpected;
            FlushCompleted = flushCompleted;
            FrameComplete = frameComplete;
            ResponseSuppressed = responseSuppressed;
            FailureKind = failureKind ?? string.Empty;
        }

        public bool WriteStarted { get; }
        public bool FirstByteCommitted { get; }
        public int BytesWritten { get; }
        public int FrameBytesExpected { get; }
        public bool FlushCompleted { get; }
        public bool FrameComplete { get; }
        public bool ResponseSuppressed { get; }
        public string FailureKind { get; }
    }

    public static class PipeFrameIO
    {
        public static byte[] EncodeFrame(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            byte[] body;
            try
            {
                body = StrictJson.Utf8.GetBytes(json);
            }
            catch (EncoderFallbackException)
            {
                throw new InvalidDataException("frame_utf8_invalid");
            }
            ValidateLength(body.Length);
            var frame = new byte[ProtocolConstants.FramePrefixBytes + body.Length];
            WriteLittleEndian(body.Length, frame, 0);
            Buffer.BlockCopy(body, 0, frame, ProtocolConstants.FramePrefixBytes, body.Length);
            return frame;
        }

        public static string DecodeFrame(byte[] frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (frame.Length < ProtocolConstants.FramePrefixBytes) throw new InvalidDataException("frame_header_incomplete");
            var length = ReadLittleEndian(frame, 0);
            ValidateLength(length);
            if (frame.Length != ProtocolConstants.FramePrefixBytes + length) throw new InvalidDataException("frame_length_mismatch");
            return DecodeBody(frame, ProtocolConstants.FramePrefixBytes, length);
        }

        public static async Task WriteFrameAsync(Stream stream, string json, CancellationToken cancellationToken)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            var result = await WriteFrameWithResultAsync(new StreamFrameWriteSink(stream), json, cancellationToken, null, null).ConfigureAwait(false);
            if (!result.FrameComplete) throw new IOException(string.IsNullOrWhiteSpace(result.FailureKind) ? "frame_write_failed" : result.FailureKind);
        }

        public static Task<FrameWriteResult> WriteFrameWithResultAsync(Stream stream, string json, CancellationToken cancellationToken, Func<bool> beforeFirstByteWrite, Action<FrameWriteResult> observe)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            return WriteFrameWithResultAsync(new StreamFrameWriteSink(stream), json, cancellationToken, beforeFirstByteWrite, observe);
        }

        public static async Task<FrameWriteResult> WriteFrameWithResultAsync(IFrameWriteSink sink, string json, CancellationToken cancellationToken, Func<bool> beforeFirstByteWrite, Action<FrameWriteResult> observe)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            var frame = EncodeFrame(json);
            var writeStarted = false;
            var firstByteCommitted = false;
            var bytesWritten = 0;
            var flushCompleted = false;
            var failureKind = string.Empty;
            var responseSuppressed = false;

            if (beforeFirstByteWrite != null)
            {
                bool allowed;
                try
                {
                    allowed = beforeFirstByteWrite();
                }
                catch (Exception)
                {
                    return Publish(new FrameWriteResult(false, false, 0, frame.Length, false, false, true, "gate_exception"), observe);
                }

                if (!allowed)
                {
                    responseSuppressed = true;
                    return Publish(new FrameWriteResult(false, false, 0, frame.Length, false, false, true, "gate_rejected"), observe);
                }
            }

            writeStarted = true;
            int actualBytes;
            try
            {
                actualBytes = await sink.WriteAsync(frame, 0, frame.Length, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Publish(new FrameWriteResult(writeStarted, false, 0, frame.Length, false, false, responseSuppressed, "cancelled"), observe);
            }
            catch (Exception)
            {
                return Publish(new FrameWriteResult(writeStarted, false, 0, frame.Length, false, false, responseSuppressed, "write_exception"), observe);
            }

            if (actualBytes < 0 || actualBytes > frame.Length)
            {
                return Publish(new FrameWriteResult(writeStarted, false, 0, frame.Length, false, false, responseSuppressed, "invalid_write_count"), observe);
            }

            bytesWritten = actualBytes;
            firstByteCommitted = actualBytes > 0;
            if (actualBytes == 0)
            {
                return Publish(new FrameWriteResult(writeStarted, false, bytesWritten, frame.Length, false, false, responseSuppressed, "zero_write"), observe);
            }

            if (actualBytes != frame.Length)
            {
                return Publish(new FrameWriteResult(writeStarted, firstByteCommitted, bytesWritten, frame.Length, false, false, responseSuppressed, "partial_write"), observe);
            }

            try
            {
                await sink.FlushAsync(cancellationToken).ConfigureAwait(false);
                flushCompleted = true;
            }
            catch (Exception)
            {
                return Publish(new FrameWriteResult(writeStarted, firstByteCommitted, bytesWritten, frame.Length, false, false, responseSuppressed, "flush_failure"), observe);
            }

            return Publish(new FrameWriteResult(writeStarted, firstByteCommitted, bytesWritten, frame.Length, flushCompleted, true, responseSuppressed, failureKind), observe);
        }

        private static FrameWriteResult Publish(FrameWriteResult result, Action<FrameWriteResult> observe)
        {
            observe?.Invoke(result);
            return result;
        }

        public static async Task<string> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            var header = new byte[ProtocolConstants.FramePrefixBytes];
            await ReadExactlyAsync(stream, header, 0, header.Length, cancellationToken).ConfigureAwait(false);
            var length = ReadLittleEndian(header, 0);
            ValidateLength(length);
            var body = new byte[length];
            await ReadExactlyAsync(stream, body, 0, body.Length, cancellationToken).ConfigureAwait(false);
            return DecodeBody(body, 0, body.Length);
        }

        private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var read = 0;
            while (read < count)
            {
                var current = await stream.ReadAsync(buffer, offset + read, count - read, cancellationToken).ConfigureAwait(false);
                if (current <= 0) throw new EndOfStreamException();
                read += current;
            }
        }

        private static string DecodeBody(byte[] bytes, int offset, int count)
        {
            try
            {
                return StrictJson.Utf8.GetString(bytes, offset, count);
            }
            catch (DecoderFallbackException)
            {
                throw new InvalidDataException("frame_utf8_invalid");
            }
        }

        private static void ValidateLength(int length)
        {
            if (length < 1 || length > ProtocolConstants.MaxFrameBytes) throw new InvalidDataException("frame_length_invalid");
        }

        private static int ReadLittleEndian(byte[] buffer, int offset)
        {
            var value = (uint)buffer[offset] | ((uint)buffer[offset + 1] << 8) | ((uint)buffer[offset + 2] << 16) | ((uint)buffer[offset + 3] << 24);
            if (value > int.MaxValue) throw new InvalidDataException("frame_length_invalid");
            return (int)value;
        }

        private static void WriteLittleEndian(int value, byte[] buffer, int offset)
        {
            buffer[offset] = (byte)(value & 0xff);
            buffer[offset + 1] = (byte)((value >> 8) & 0xff);
            buffer[offset + 2] = (byte)((value >> 16) & 0xff);
            buffer[offset + 3] = (byte)((value >> 24) & 0xff);
        }

        private sealed class StreamFrameWriteSink : IFrameWriteSink
        {
            private readonly Stream stream;

            internal StreamFrameWriteSink(Stream stream)
            {
                this.stream = stream;
            }

            public async Task<int> WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                await stream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
                return count;
            }

            public Task FlushAsync(CancellationToken cancellationToken)
            {
                return stream.FlushAsync(cancellationToken);
            }
        }
    }
}
