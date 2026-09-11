using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MarcusAwakeTransport
{
    public static class TransportSecurity
    {
        private const int Sha256Bytes = 32;
        private const int MaxHkdfBytes = 255 * Sha256Bytes;
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static byte[] Sha256(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using (var algorithm = SHA256.Create()) return algorithm.ComputeHash(bytes);
        }

        public static string Sha256Hex(byte[] bytes) => ToHex(Sha256(bytes));
        public static string Sha256Hex(string value) => Sha256Hex(StrictUtf8.GetBytes(value ?? string.Empty));

        public static string Sha256FileHex(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return string.Empty;
            using (var stream = File.OpenRead(path))
            using (var algorithm = SHA256.Create()) return ToHex(algorithm.ComputeHash(stream));
        }

        public static byte[] RandomBytes(int length)
        {
            if (length < 1 || length > 4096) throw new ArgumentOutOfRangeException(nameof(length));
            var value = new byte[length];
            using (var generator = RandomNumberGenerator.Create()) generator.GetBytes(value);
            return value;
        }

        public static string RandomHex(int byteLength) => ToHex(RandomBytes(byteLength));
        public static string SidFingerprint(string sid) => Sha256Hex("marcus-awake/sid/v1\n" + (sid ?? string.Empty));

        public static byte[] HmacSha256(byte[] key, byte[] message)
        {
            RequireKey(key);
            if (message == null) throw new ArgumentNullException(nameof(message));
            using (var algorithm = new HMACSHA256(key)) return algorithm.ComputeHash(message);
        }

        public static string HmacSha256Hex(byte[] key, byte[] message) => ToHex(HmacSha256(key, message));

        public static string HmacSha256Hex(byte[] key, string domain, params string[] values)
        {
            return HmacSha256Hex(key, StrictUtf8.GetBytes(Transcript(domain, values)));
        }

        public static byte[] HkdfSha256(byte[] input, byte[] salt, byte[] info, int length)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (length < 1 || length > MaxHkdfBytes) throw new ArgumentOutOfRangeException(nameof(length));
            var effectiveSalt = salt == null || salt.Length == 0 ? new byte[Sha256Bytes] : salt;
            var pseudorandomKey = HmacSha256(effectiveSalt, input);
            var output = new byte[length];
            var previous = Array.Empty<byte>();
            var offset = 0;
            byte counter = 1;
            using (var expand = new HMACSHA256(pseudorandomKey))
            {
                while (offset < length)
                {
                    var inputBlock = new byte[previous.Length + (info == null ? 0 : info.Length) + 1];
                    Buffer.BlockCopy(previous, 0, inputBlock, 0, previous.Length);
                    if (info != null) Buffer.BlockCopy(info, 0, inputBlock, previous.Length, info.Length);
                    inputBlock[inputBlock.Length - 1] = counter++;
                    previous = expand.ComputeHash(inputBlock);
                    var copyLength = Math.Min(previous.Length, length - offset);
                    Buffer.BlockCopy(previous, 0, output, offset, copyLength);
                    offset += copyLength;
                }
            }
            return output;
        }

        public static byte[] DeriveAuthKey(byte[] bootstrapSecret, string launchNonce, string authDomain, string launchTransactionId, string authKeyId)
        {
            if (bootstrapSecret == null || bootstrapSecret.Length < Sha256Bytes) throw new ArgumentException("bootstrap_secret_256_bits_required", nameof(bootstrapSecret));
            var salt = StrictUtf8.GetBytes(launchNonce ?? string.Empty);
            var info = StrictUtf8.GetBytes(Transcript("marcus-awake/auth-key/v2", authDomain, launchTransactionId, authKeyId));
            return HkdfSha256(bootstrapSecret, salt, info, Sha256Bytes);
        }

        public static byte[] DeriveDirectionKey(byte[] authKey, string direction, long connectionEpoch, string directionNonce, string sessionId)
        {
            if (connectionEpoch < 1) throw new ArgumentOutOfRangeException(nameof(connectionEpoch));
            var salt = StrictUtf8.GetBytes(directionNonce ?? string.Empty);
            var info = StrictUtf8.GetBytes(Transcript("marcus-awake/direction-key/v2", direction, connectionEpoch.ToString(CultureInfo.InvariantCulture), sessionId));
            return HkdfSha256(authKey, salt, info, Sha256Bytes);
        }

        public static byte[] DeriveFenceKey(byte[] authKey, string direction, long connectionEpoch, string directionNonce, string sessionId)
        {
            return DeriveDirectionKey(authKey, direction, connectionEpoch, directionNonce, sessionId);
        }

        public static string ComputeClientBootstrapProof(BootstrapDescriptor descriptor, byte[] clientKey)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            return HmacSha256Hex(clientKey, "client-bootstrap-v2", descriptor.ProtocolId, descriptor.ProtocolMajor.ToString(CultureInfo.InvariantCulture), descriptor.ProtocolMinor.ToString(CultureInfo.InvariantCulture), descriptor.FrameworkApiMajor.ToString(CultureInfo.InvariantCulture), descriptor.ServiceId, descriptor.ClientInstanceId, descriptor.LaunchTransactionId, descriptor.LaunchNonce, descriptor.ChallengeId, descriptor.SessionNonce, descriptor.UserSidFingerprint, descriptor.ParentProcessId.ToString(CultureInfo.InvariantCulture), descriptor.ParentStartUnixMilliseconds.ToString(CultureInfo.InvariantCulture), descriptor.InstanceEpoch.ToString(CultureInfo.InvariantCulture), descriptor.PipeName, descriptor.ExpectedServiceArtifactSha256);
        }

        public static string ComputeServiceBootstrapProof(BootstrapDescriptor descriptor, byte[] serviceKey, string serviceInstanceId, int serviceProcessId, long serviceStartUnixMilliseconds, string serviceArtifactSha256, string parentStartProof)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            return HmacSha256Hex(serviceKey, "service-bootstrap-v2", descriptor.ProtocolId, descriptor.ProtocolMajor.ToString(CultureInfo.InvariantCulture), descriptor.ProtocolMinor.ToString(CultureInfo.InvariantCulture), descriptor.FrameworkApiMajor.ToString(CultureInfo.InvariantCulture), descriptor.ServiceId, descriptor.ClientInstanceId, serviceInstanceId, descriptor.LaunchTransactionId, descriptor.LaunchNonce, descriptor.ChallengeId, descriptor.SessionNonce, descriptor.UserSidFingerprint, descriptor.ParentProcessId.ToString(CultureInfo.InvariantCulture), descriptor.ParentStartUnixMilliseconds.ToString(CultureInfo.InvariantCulture), serviceProcessId.ToString(CultureInfo.InvariantCulture), serviceStartUnixMilliseconds.ToString(CultureInfo.InvariantCulture), serviceArtifactSha256, parentStartProof, descriptor.InstanceEpoch.ToString(CultureInfo.InvariantCulture), descriptor.PipeName);
        }

        public static string ComputeParentStartProof(BootstrapDescriptor descriptor, byte[] serviceKey, int serviceProcessId, long serviceStartUnixMilliseconds, string serviceArtifactSha256)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            return HmacSha256Hex(serviceKey, "parent-proof-v2", descriptor.LaunchTransactionId, descriptor.ParentProcessId.ToString(CultureInfo.InvariantCulture), descriptor.ParentStartUnixMilliseconds.ToString(CultureInfo.InvariantCulture), serviceProcessId.ToString(CultureInfo.InvariantCulture), serviceStartUnixMilliseconds.ToString(CultureInfo.InvariantCulture), descriptor.UserSidFingerprint, descriptor.InstanceEpoch.ToString(CultureInfo.InvariantCulture), serviceArtifactSha256);
        }

        public static string ComputeChallengeResponse(HandshakeRequest request, byte[] clientKey)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return HmacSha256Hex(clientKey, "client-auth-v2", request.ProtocolId, request.ProtocolMajor.ToString(CultureInfo.InvariantCulture), request.ProtocolMinor.ToString(CultureInfo.InvariantCulture), request.FrameworkApiMajor.ToString(CultureInfo.InvariantCulture), request.ServiceId, request.ClientInstanceId, request.ServiceInstanceId, request.LaunchNonce, request.SessionNonce, request.UserSidFingerprint, request.ParentProcessId.ToString(CultureInfo.InvariantCulture), request.ChallengeId, request.ServiceBootstrapProof, request.PipeName, request.InstanceEpoch.ToString(CultureInfo.InvariantCulture), request.MaxFrameBytes.ToString(CultureInfo.InvariantCulture), request.ChecksumAlgorithm, request.CorrelationId);
        }

        public static string ComputeServiceAuthResponse(HandshakeRequest request, byte[] serviceKey, string serviceInstanceId, long connectionEpoch, string clientDirectionNonce, string serviceDirectionNonce, string parentStartProof)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return HmacSha256Hex(serviceKey, "service-auth-v2", request.ProtocolId, request.ProtocolMajor.ToString(CultureInfo.InvariantCulture), request.ProtocolMinor.ToString(CultureInfo.InvariantCulture), request.FrameworkApiMajor.ToString(CultureInfo.InvariantCulture), request.ServiceId, request.ClientInstanceId, serviceInstanceId, request.LaunchNonce, request.SessionNonce, request.UserSidFingerprint, request.ParentProcessId.ToString(CultureInfo.InvariantCulture), request.ChallengeId, request.PipeName, request.InstanceEpoch.ToString(CultureInfo.InvariantCulture), connectionEpoch.ToString(CultureInfo.InvariantCulture), clientDirectionNonce, serviceDirectionNonce, parentStartProof, request.MaxFrameBytes.ToString(CultureInfo.InvariantCulture), request.ChecksumAlgorithm, request.CorrelationId);
        }

        public static string ComputeFrameFenceProof(byte[] fenceKey, PipeEnvelope envelope)
        {
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));
            return HmacSha256Hex(fenceKey, "frame-fence-v2", envelope.ProtocolId, envelope.ProtocolMajor.ToString(CultureInfo.InvariantCulture), envelope.ProtocolMinor.ToString(CultureInfo.InvariantCulture), envelope.MessageType, envelope.MessageId, envelope.CorrelationId, envelope.CampaignGuid, envelope.TimelineId, envelope.SessionId, envelope.SessionGeneration.ToString(CultureInfo.InvariantCulture), envelope.OwnerId, envelope.InstanceEpoch.ToString(CultureInfo.InvariantCulture), envelope.ConnectionEpoch.ToString(CultureInfo.InvariantCulture), envelope.DirectionNonce, envelope.Sequence.ToString(CultureInfo.InvariantCulture), envelope.PayloadSchema, envelope.PayloadLength.ToString(CultureInfo.InvariantCulture), envelope.PayloadSha256, envelope.TaskScope == null ? string.Empty : envelope.TaskScope.TaskId, envelope.TaskScope == null ? string.Empty : envelope.TaskScope.IdempotencyKey);
        }

        public static bool FixedTimeEquals(string left, string right)
        {
            if (left == null || right == null) return false;
            byte[] leftBytes;
            byte[] rightBytes;
            try
            {
                leftBytes = StrictUtf8.GetBytes(left);
                rightBytes = StrictUtf8.GetBytes(right);
            }
            catch (EncoderFallbackException)
            {
                return false;
            }
            if (leftBytes.Length != rightBytes.Length) return false;
            var difference = 0;
            for (var index = 0; index < leftBytes.Length; index++) difference |= leftBytes[index] ^ rightBytes[index];
            return difference == 0;
        }

        private static void RequireKey(byte[] key)
        {
            if (key == null || key.Length < 1) throw new ArgumentException("hmac_key_required", nameof(key));
        }

        private static string Transcript(string domain, params string[] values)
        {
            var builder = new StringBuilder(domain ?? string.Empty);
            builder.Append('\n');
            if (values != null)
            {
                for (var index = 0; index < values.Length; index++)
                {
                    var value = values[index] ?? string.Empty;
                    builder.Append(StrictUtf8.GetByteCount(value).ToString(CultureInfo.InvariantCulture)).Append(':').Append(value).Append('|');
                }
            }
            return builder.ToString();
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (var index = 0; index < bytes.Length; index++) builder.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }
}
