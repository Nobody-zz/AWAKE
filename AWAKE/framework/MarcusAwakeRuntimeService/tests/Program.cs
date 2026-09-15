using MarcusAwakeTransport;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeRuntimeService.Tests;

[SupportedOSPlatform("windows")]
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 1 && StringComparer.Ordinal.Equals(args[0], "--parent-exit-child")) return await ParentExitChildAsync().ConfigureAwait(false);
        if (args.Length == 1 && StringComparer.Ordinal.Equals(args[0], "--p3c")) return await new P3CHarnessRunner().RunAsync().ConfigureAwait(false);
        if (args.Length == 1 && StringComparer.Ordinal.Equals(args[0], "--p3d-a0")) return await new P3DA0HarnessRunner().RunAsync().ConfigureAwait(false);
        if (args.Length == 1 && StringComparer.Ordinal.Equals(args[0], "--p3d-a1")) return await new P3DA1HarnessRunner().RunAsync().ConfigureAwait(false);
        if (args.Length == 1 && StringComparer.Ordinal.Equals(args[0], "--p3d-a2")) return await new P3DA2HarnessRunner().RunAsync().ConfigureAwait(false);
        if (args.Length == 1 && StringComparer.Ordinal.Equals(args[0], "--provider-stream-models")) return await ProviderStreamModelsTests.RunAsync().ConfigureAwait(false);
        if (args.Length == 1 && StringComparer.Ordinal.Equals(args[0], "--provider-outcome-ledger")) return RuntimeProviderOutcomeLedgerTests.Run();
        if (args.Length == 1 && StringComparer.Ordinal.Equals(args[0], "--runtime-service-log")) return RuntimeServiceLogTests.Run();
        if (args.Length == 1 && StringComparer.Ordinal.Equals(args[0], "--rag-client")) return await RagClientTests.RunAsync().ConfigureAwait(false);
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("FAIL windows_required");
            return 1;
        }

        return await new HarnessRunner().RunAsync().ConfigureAwait(false);
    }

    private static async Task<int> ParentExitChildAsync()
    {
        var servicePath = ResolveServicePath();
        var service = await LaunchedService.StartAsync(servicePath, 2001).ConfigureAwait(false);
        var connection = await service.ConnectAsync(service.Descriptor.ChallengeId, "parent-exit-session").ConfigureAwait(false);
        var delayedEcho = connection.CreateTaskFrame("echo", "parent-exit-echo", "parent-exit-task", "{\"value\":\"orphan\",\"delay_ms\":1200}");
        await connection.SendRawAsync(delayedEcho.RawJson).ConfigureAwait(false);
        await Task.Delay(100).ConfigureAwait(false);

        var health = connection.CreateFrame("health", "parent-exit-health", "{}", null, "campaign-p3b", "timeline-p3b", 1);
        await connection.SendRawAsync(health.RawJson).ConfigureAwait(false);
        var healthResponse = await connection.ReadResponseAsync(response => StringComparer.Ordinal.Equals(response.MessageId, health.Envelope.MessageId)).ConfigureAwait(false);
        Require(StringComparer.Ordinal.Equals(healthResponse.MessageType, "health_ack"), "parent_exit_health_not_observed");

        Console.WriteLine("SERVICE_PID=" + service.Process.Id.ToString());
        Console.WriteLine("SERVICE_START_MS=" + service.Ready.ServiceStartUnixMilliseconds.ToString());
        Console.Out.Flush();
        connection.Dispose();
        return 0;
    }

    private static string ResolveServicePath()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "_build_out", "Release", "MarcusAwakeRuntimeService.exe"));
        if (!File.Exists(path)) throw new InvalidOperationException("service_executable_missing:" + path);
        return path;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

[SupportedOSPlatform("windows")]
internal sealed class HarnessRunner
{
    private readonly List<string> passedCases = new List<string>();
    private readonly List<string> failedCases = new List<string>();

    internal async Task<int> RunAsync()
    {
        var servicePath = ResolveServicePath();
        LaunchedService? service = null;
        ServiceConnection? firstConnection = null;
        ServiceConnection? secondConnection = null;
        var evidenceCases = new List<string>();

        try
        {
            service = await LaunchedService.StartAsync(servicePath, 2000).ConfigureAwait(false);
            await RunCaseAsync("P3B-01 service_start_descriptor_pipe", async () =>
            {
                if (service.Process.HasExited)
                {
                    var serviceError = await service.Process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                    throw new InvalidOperationException("service_exited_after_ready:exit=" + service.Process.ExitCode.ToString() + ":stderr=" + serviceError.Trim());
                }
                Require(service.Ready.ServiceProcessId == service.Process.Id, "service_pid_mismatch");
                Require(service.Ready.ServiceArtifactSha256 == service.Descriptor.ExpectedServiceArtifactSha256, "service_hash_mismatch");
                Require(service.Ready.PipeName == service.Descriptor.PipeName, "pipe_name_mismatch");
                Require(service.CommandLineArguments.Count == 0, "unexpected_service_arguments");
                evidenceCases.Add("P3B-01");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3B-03 legacy_pipe_rejected", async () =>
            {
                Require(await LegacyPipeRejectedAsync().ConfigureAwait(false), "legacy_pipe_connected");
                evidenceCases.Add("P3B-03-legacy");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3B-03 wrong_protocol_rejected", async () =>
            {
                var response = await service.AttemptRawHandshakeAsync(raw => raw.Replace("\"protocol_major\":2", "\"protocol_major\":1", StringComparison.Ordinal), service.Descriptor.ChallengeId, "negative-protocol").ConfigureAwait(false);
                Require(response == null || !response.Accepted, "wrong_protocol_accepted");
                evidenceCases.Add("P3B-03-protocol");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3B-03 wrong_service_instance_rejected", async () =>
            {
                var response = await service.AttemptHandshakeAsync(request =>
                {
                    request.ServiceInstanceId = "legacy-service-instance";
                    request.ChallengeResponse = TransportSecurity.ComputeChallengeResponse(request, service.ServiceKey);
                    return request;
                }, service.Descriptor.ChallengeId, "negative-service").ConfigureAwait(false);
                Require(!response.Accepted && response.ErrorCode == "service_instance_mismatch", "wrong_service_accepted");
                evidenceCases.Add("P3B-03-service");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3B-03 wrong_sid_rejected", async () =>
            {
                var fakeSid = new string('0', 64);
                var response = await service.AttemptRawHandshakeAsync(raw => raw.Replace("\"user_sid_fingerprint\":\"" + service.Descriptor.UserSidFingerprint + "\"", "\"user_sid_fingerprint\":\"" + fakeSid + "\"", StringComparison.Ordinal).Replace("\"pipe_name\":\"" + service.Descriptor.PipeName + "\"", "\"pipe_name\":\"" + ProtocolConstants.PipePrefix + fakeSid + "\"", StringComparison.Ordinal), service.Descriptor.ChallengeId, "negative-sid").ConfigureAwait(false);
                Require(response == null || !response.Accepted, "wrong_sid_accepted");
                evidenceCases.Add("P3B-03-sid");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3B-03 wrong_parent_rejected", async () =>
            {
                var response = await service.AttemptHandshakeAsync(request =>
                {
                    request.ParentProcessId = service.Descriptor.ParentProcessId + 1;
                    request.ChallengeResponse = TransportSecurity.ComputeChallengeResponse(request, service.ServiceKey);
                    return request;
                }, service.Descriptor.ChallengeId, "negative-parent").ConfigureAwait(false);
                Require(!response.Accepted && response.ErrorCode == "ipc_parent_invalid", "wrong_parent_accepted");
                evidenceCases.Add("P3B-03-parent");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3B-03 wrong_nonce_rejected", async () =>
            {
                var response = await service.AttemptHandshakeAsync(request =>
                {
                    request.SessionNonce = "wrong-session-nonce";
                    request.ChallengeResponse = TransportSecurity.ComputeChallengeResponse(request, service.ServiceKey);
                    return request;
                }, service.Descriptor.ChallengeId, "negative-nonce").ConfigureAwait(false);
                Require(!response.Accepted && response.ErrorCode == "ipc_bootstrap_mismatch", "wrong_nonce_accepted");
                evidenceCases.Add("P3B-03-nonce");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3B-02 valid_handshake_and_health", async () =>
            {
                firstConnection = await service.ConnectAsync(service.Descriptor.ChallengeId, "session-one").ConfigureAwait(false);
                Require(firstConnection.HandshakeResponse.Accepted, "valid_handshake_rejected");
                var health = await firstConnection.SendAndReadAsync(firstConnection.CreateFrame("health", "health-one", "{}", null, "campaign-p3b", "timeline-p3b", 1)).ConfigureAwait(false);
                Require(health.MessageType == "health_ack" && health.AckStatus == "accepted" && health.NonDurable, "health_response_invalid");
                // Contract pin: the service answer must match the client's own acknowledged payload constant
                // byte-for-byte, read straight off the shipping client assembly.
                var healthContractField = typeof(MarcusAwakeFramework.Api.RuntimeServiceClient).GetField("HealthAckPayload", BindingFlags.NonPublic | BindingFlags.Static);
                Require(healthContractField != null, "client_health_payload_constant_missing");
                var healthContract = (string)healthContractField!.GetRawConstantValue()!;
                Require(StringComparer.Ordinal.Equals(health.PayloadSchema, "marcus-awake.health.v1") && StringComparer.Ordinal.Equals(health.PayloadJson, healthContract),
                    "health_payload_contract_mismatch client=" + healthContract + " service=" + health.PayloadJson);
                evidenceCases.Add("P3B-02");
                evidenceCases.Add("P3B-04-health");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3B-04 echo_response", async () =>
            {
                var echoFrame = firstConnection!.CreateTaskFrame("echo", "echo-one", "task-one", "{\"value\":\"hello\"}");
                var echo = await firstConnection.SendAndReadAsync(echoFrame).ConfigureAwait(false);
                Require(echo.MessageType == "echo_result" && echo.AckStatus == "accepted", "echo_response_invalid");
                Require(echo.PayloadJson.Contains("hello", StringComparison.Ordinal), "echo_value_missing");
                evidenceCases.Add("P3B-04-echo");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3B-05 checksum_and_length_rejected", async () =>
            {
                var checksumConnection = await service.ConnectAsync(service.Descriptor.ChallengeId + ".connection.2", "session-checksum").ConfigureAwait(false);
                var checksumFrame = checksumConnection.CreateTaskFrame("echo", "checksum-one", "checksum-task", "{\"value\":\"checksum\"}");
                var checksumRaw = checksumFrame.RawJson.Replace("\"checksum\":\"" + checksumFrame.Envelope.PayloadSha256 + "\"", "\"checksum\":\"" + new string('f', 64) + "\"", StringComparison.Ordinal);
                await checksumConnection.SendRawAsync(checksumRaw).ConfigureAwait(false);
                Require(await checksumConnection.DisconnectedAsync().ConfigureAwait(false), "bad_checksum_connection_alive");
                checksumConnection.Dispose();

                var lengthConnection = await service.ConnectAsync(service.Descriptor.ChallengeId + ".connection.3", "session-length").ConfigureAwait(false);
                var lengthFrame = lengthConnection.CreateTaskFrame("echo", "length-one", "length-task", "{\"value\":\"length\"}");
                var lengthRaw = lengthFrame.RawJson.Replace("\"payload_length\":" + lengthFrame.Envelope.PayloadLength.ToString(), "\"payload_length\":" + (lengthFrame.Envelope.PayloadLength + 1).ToString(), StringComparison.Ordinal);
                await lengthConnection.SendRawAsync(lengthRaw).ConfigureAwait(false);
                Require(await lengthConnection.DisconnectedAsync().ConfigureAwait(false), "bad_length_connection_alive");
                lengthConnection.Dispose();
                evidenceCases.Add("P3B-05-checksum");
                evidenceCases.Add("P3B-05-length");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3B-05 duplicate_and_deep_json_rejected", async () =>
            {
                var malformedConnection = await service.ConnectAsync(service.Descriptor.ChallengeId + ".connection.4", "session-malformed").ConfigureAwait(false);
                var duplicateFrame = malformedConnection.CreateTaskFrame("echo", "duplicate-key", "duplicate-task", "{\"value\":\"duplicate\"}");
                var duplicateRaw = duplicateFrame.RawJson.Replace("\"payload\":{\"value\":\"duplicate\"}", "\"payload\":{\"value\":\"duplicate\",\"value\":\"second\"}", StringComparison.Ordinal);
                await malformedConnection.SendRawAsync(duplicateRaw).ConfigureAwait(false);
                Require(await malformedConnection.DisconnectedAsync().ConfigureAwait(false), "duplicate_key_connection_alive");

                malformedConnection.Dispose();
                var deepConnection = await service.ConnectAsync(service.Descriptor.ChallengeId + ".connection.5", "session-deep").ConfigureAwait(false);
                var deepFrame = deepConnection.CreateTaskFrame("echo", "deep-json", "deep-task", "{\"value\":\"deep\"}");
                var nested = "{}";
                for (var index = 0; index < ProtocolConstants.MaxJsonDepth + 3; index++) nested = "{\"x\":" + nested + "}";
                var deepRaw = deepFrame.RawJson.Replace("\"payload\":{\"value\":\"deep\"}", "\"payload\":" + nested, StringComparison.Ordinal);
                await deepConnection.SendRawAsync(deepRaw).ConfigureAwait(false);
                Require(await deepConnection.DisconnectedAsync().ConfigureAwait(false), "deep_json_connection_alive");
                deepConnection.Dispose();
                evidenceCases.Add("P3B-05-duplicate");
                evidenceCases.Add("P3B-05-depth");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3B-05 oversized_frame_rejected", async () =>
            {
                var oversizedConnection = await service.ConnectAsync(service.Descriptor.ChallengeId + ".connection.6", "session-oversized").ConfigureAwait(false);
                await oversizedConnection.SendOversizedFrameAsync().ConfigureAwait(false);
                Require(await oversizedConnection.DisconnectedAsync().ConfigureAwait(false), "oversized_frame_connection_alive");
                oversizedConnection.Dispose();
                evidenceCases.Add("P3B-05-size");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3B-06 duplicate_and_conflicting_replay", async () =>
            {
                var replayFrame = firstConnection!.CreateTaskFrame("echo", "replay-one", "replay-task", "{\"value\":\"replay\"}");
                await firstConnection.SendRawAsync(replayFrame.RawJson).ConfigureAwait(false);
                var firstReplayResponse = await firstConnection.ReadResponseAsync(response => response.MessageId == replayFrame.Envelope.MessageId).ConfigureAwait(false);
                Require(firstReplayResponse.MessageType == "echo_result", "replay_seed_missing");

                await firstConnection.SendRawAsync(replayFrame.RawJson).ConfigureAwait(false);
                var duplicateResponse = await firstConnection.ReadResponseAsync(response => response.MessageId == replayFrame.Envelope.MessageId && response.OutcomeKind == "duplicate").ConfigureAwait(false);
                Require(duplicateResponse.AckStatus == "accepted" && duplicateResponse.NonDurable, "duplicate_replay_invalid");

                var conflictingRaw = firstConnection.RewriteMessageIdWithValidFence(replayFrame.RawJson, "replay-conflict");
                await firstConnection.SendRawAsync(conflictingRaw).ConfigureAwait(false);
                var conflictResponse = await firstConnection.ReadResponseAsync(response => response.ErrorCode == "replay_rejected").ConfigureAwait(false);
                Require(conflictResponse.OutcomeKind == "rejected", "conflicting_replay_accepted");
                evidenceCases.Add("P3B-06-duplicate");
                evidenceCases.Add("P3B-06-conflict");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3B-06 sequence_gap_rejected", async () =>
            {
                var gapConnection = await service.ConnectAsync(service.Descriptor.ChallengeId + ".connection.7", "session-gap").ConfigureAwait(false);
                var gapFrame = gapConnection.CreateFrame("health", "gap-health", "{}", 40, "campaign-p3b", "timeline-p3b", 1);
                await gapConnection.SendRawAsync(gapFrame.RawJson).ConfigureAwait(false);
                var gapResponse = await gapConnection.ReadResponseAsync(response => response.MessageId == gapFrame.Envelope.MessageId).ConfigureAwait(false);
                Require(gapResponse.ErrorCode == "sequence_gap" && gapResponse.OutcomeKind == "retryable_reject", "sequence_gap_not_rejected");
                gapConnection.Dispose();
                evidenceCases.Add("P3B-06-gap");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3B-08 cancellation_and_terminal_replay", async () =>
            {
                var delayedFrame = firstConnection!.CreateTaskFrame("echo", "cancel-echo", "cancel-task", "{\"value\":\"cancel-me\",\"delay_ms\":1200}");
                await firstConnection.SendRawAsync(delayedFrame.RawJson).ConfigureAwait(false);
                await Task.Delay(100).ConfigureAwait(false);
                var cancelFrame = firstConnection.CreateTaskFrame("cancel", "cancel-one", "cancel-task", "{\"task_id\":\"cancel-task\"}");
                await firstConnection.SendRawAsync(cancelFrame.RawJson).ConfigureAwait(false);
                var cancelResponse = await firstConnection.ReadResponseAsync(response => response.MessageId == cancelFrame.Envelope.MessageId).ConfigureAwait(false);
                Require(cancelResponse.MessageType == "cancel_ack" && cancelResponse.PayloadJson.Contains("cancel_requested", StringComparison.Ordinal), "cancel_not_accepted");
                var cancelledEcho = await firstConnection.ReadResponseAsync(response => response.MessageId == delayedFrame.Envelope.MessageId).ConfigureAwait(false);
                Require(cancelledEcho.MessageType == "echo_result" && cancelledEcho.PayloadJson.Contains("\"cancelled\":true", StringComparison.Ordinal), "cancelled_terminal_missing");

                var repeatedCancel = firstConnection.CreateTaskFrame("cancel", "cancel-two", "cancel-task", "{\"task_id\":\"cancel-task\"}");
                await firstConnection.SendRawAsync(repeatedCancel.RawJson).ConfigureAwait(false);
                var replayedTerminal = await firstConnection.ReadResponseAsync(response => response.MessageId == repeatedCancel.Envelope.MessageId).ConfigureAwait(false);
                Require(replayedTerminal.OutcomeKind == "terminal_replay" && replayedTerminal.NonDurable, "terminal_replay_invalid");
                evidenceCases.Add("P3B-08-cancel");
                evidenceCases.Add("P3B-08-terminal-replay");
            }).ConfigureAwait(false);

            var oldHealthFrame = firstConnection!.CreateFrame("health", "old-epoch-health", "{}", null, "campaign-p3b", "timeline-p3b", 1);
            await firstConnection.SendRawAsync(oldHealthFrame.RawJson).ConfigureAwait(false);
            await firstConnection.ReadResponseAsync(response => response.MessageId == oldHealthFrame.Envelope.MessageId).ConfigureAwait(false);
            firstConnection.Dispose();
            firstConnection = null;

            await RunCaseAsync("P3B-03 replayed_challenge_rejected", async () =>
            {
                var response = await service.AttemptHandshakeAsync(null, service.Descriptor.ChallengeId, "session-replay").ConfigureAwait(false);
                Require(!response.Accepted && response.ErrorCode == "challenge_replay", "replayed_challenge_accepted");
                evidenceCases.Add("P3B-03-replay");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3B-07 reconnect_new_epoch", async () =>
            {
                secondConnection = await service.ConnectAsync(service.Descriptor.ChallengeId + ".connection.8", "session-two").ConfigureAwait(false);
                Require(secondConnection.HandshakeResponse.ConnectionEpoch == 8, "connection_epoch_not_incremented");
                var staleResponse = await secondConnection.SendRawAndReadAsync(oldHealthFrame.RawJson).ConfigureAwait(false);
                Require(staleResponse.ErrorCode == "stale_connection_epoch", "old_epoch_frame_accepted");
                var health = await secondConnection.SendAndReadAsync(secondConnection.CreateFrame("health", "health-two", "{}", null, "campaign-p3b", "timeline-p3b", 1)).ConfigureAwait(false);
                Require(health.MessageType == "health_ack", "reconnect_health_failed");
                evidenceCases.Add("P3B-07");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3B-08 disconnect_does_not_orphan", async () =>
            {
                secondConnection!.Dispose();
                secondConnection = null;
                await Task.Delay(150).ConfigureAwait(false);
                Require(!service.Process.HasExited, "service_exited_after_client_disconnect");
                evidenceCases.Add("P3B-08-disconnect");
            }).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failedCases.Add("harness_fatal:" + exception.Message);
            Console.WriteLine("FAIL harness_fatal " + exception.Message);
        }
        finally
        {
            firstConnection?.Dispose();
            secondConnection?.Dispose();
            if (service != null) await service.DisposeAsync().ConfigureAwait(false);
        }

        await RunCaseAsync("P3B-09 parent_exit_orphan_cleanup", async () =>
        {
            var result = await RunParentExitFixtureAsync().ConfigureAwait(false);
            Require(result.ParentExitCode == 0, "parent_fixture_exit_code");
            Require(result.ServiceStarted, "parent_fixture_service_not_started");
            Require(result.ServiceExited, "orphan_service_survived_parent_exit");
            evidenceCases.Add("P3B-09");
        }).ConfigureAwait(false);

        var evidencePath = WriteAndValidateEvidence(servicePath, evidenceCases);
        Console.WriteLine("EVIDENCE " + evidencePath);
        Console.WriteLine("PASS_COUNT=" + passedCases.Count.ToString());
        Console.WriteLine("FAIL_COUNT=" + failedCases.Count.ToString());
        foreach (var failure in failedCases) Console.WriteLine("FAIL " + failure);
        return failedCases.Count == 0 ? 0 : 1;
    }

    private async Task RunCaseAsync(string name, Func<Task> test)
    {
        try
        {
            await test().ConfigureAwait(false);
            passedCases.Add(name);
            Console.WriteLine("PASS " + name);
        }
        catch (Exception exception)
        {
            failedCases.Add(name + ":" + exception.Message);
            Console.WriteLine("FAIL " + name + " " + exception.Message);
        }
    }

    private static async Task<bool> LegacyPipeRejectedAsync()
    {
        const string legacyPipeName = "MarcusAIFramework.Companion";
        using var client = new NamedPipeClientStream(".", legacyPipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            await client.ConnectAsync(250).ConfigureAwait(false);
            return false;
        }
        catch (TimeoutException)
        {
            return true;
        }
        catch (IOException)
        {
            return true;
        }
    }

    private static async Task<ParentExitResult> RunParentExitFixtureAsync()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath ?? throw new InvalidOperationException("test_process_path_missing"),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--parent-exit-child");
        using var parent = Process.Start(startInfo) ?? throw new InvalidOperationException("parent_fixture_start_failed");
        var stdoutTask = parent.StandardOutput.ReadToEndAsync();
        var stderrTask = parent.StandardError.ReadToEndAsync();
        await parent.WaitForExitAsync().ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        var pid = ReadTaggedInt(stdout, "SERVICE_PID=");
        var startMilliseconds = ReadTaggedLong(stdout, "SERVICE_START_MS=");
        var serviceStarted = pid > 0 && startMilliseconds > 0;
        var serviceExited = serviceStarted && await WaitForProcessGoneAsync(pid, startMilliseconds, 3000).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(stderr)) Console.WriteLine("PARENT_FIXTURE_STDERR=" + stderr.Trim());
        return new ParentExitResult(parent.ExitCode, serviceStarted, serviceExited);
    }

    private static int ReadTaggedInt(string output, string tag)
    {
        var value = ReadTaggedText(output, tag);
        return int.TryParse(value, out var result) ? result : 0;
    }

    private static long ReadTaggedLong(string output, string tag)
    {
        var value = ReadTaggedText(output, tag);
        return long.TryParse(value, out var result) ? result : 0;
    }

    private static string ReadTaggedText(string output, string tag)
    {
        var line = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(item => item.StartsWith(tag, StringComparison.Ordinal));
        return line == null ? string.Empty : line.Substring(tag.Length).Trim();
    }

    private static async Task<bool> WaitForProcessGoneAsync(int processId, long expectedStartMilliseconds, int timeoutMilliseconds)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMilliseconds);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                var actualStart = ReadStartUnixMilliseconds(process);
                if (actualStart != expectedStartMilliseconds) return true;
            }
            catch (ArgumentException)
            {
                return true;
            }
            catch (InvalidOperationException)
            {
                return true;
            }

            await Task.Delay(50).ConfigureAwait(false);
        }

        return false;
    }

    private static async Task<bool> WaitForExitAsync(Process process, int timeoutMilliseconds)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                process.Refresh();
                if (process.HasExited) return true;
            }
            catch (InvalidOperationException)
            {
                return true;
            }

            await Task.Delay(50).ConfigureAwait(false);
        }

        try
        {
            process.Refresh();
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static string ResolveServicePath()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "_build_out", "Release", "MarcusAwakeRuntimeService.exe"));
        if (!File.Exists(path)) throw new InvalidOperationException("service_executable_missing:" + path);
        return path;
    }

    private static string WriteAndValidateEvidence(string servicePath, List<string> cases)
    {
        var evidencePath = Path.Combine(AppContext.BaseDirectory, "MARCUS-AWAKE-P3B-evidence.json");
        var record = new
        {
            schema = "marcus-awake-p3b-evidence.v1",
            captured_at_utc = DateTimeOffset.UtcNow.ToString("O"),
            protocol_id = ProtocolConstants.ProtocolId,
            protocol_major = ProtocolConstants.ProtocolMajor,
            protocol_minor = ProtocolConstants.ProtocolMinor,
            framework_api_major = ProtocolConstants.FrameworkApiMajor,
            checksum_algorithm = ProtocolConstants.ChecksumAlgorithm,
            service_binary_sha256 = TransportSecurity.Sha256FileHex(servicePath),
            real_child_process = true,
            real_private_named_pipe = true,
            parent_proof = "windows_process_start_time_and_parent_pid",
            handshake_fields_redacted = true,
            secret_transport = "stdin_second_frame_key_material",
            non_durable_ledger = true,
            cases = cases.ToArray()
        };
        File.WriteAllText(evidencePath, JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        using var document = JsonDocument.Parse(File.ReadAllText(evidencePath));
        Require(document.RootElement.GetProperty("schema").GetString() == "marcus-awake-p3b-evidence.v1", "evidence_schema_invalid");
        Require(document.RootElement.GetProperty("real_child_process").GetBoolean(), "evidence_child_process_missing");
        Require(document.RootElement.GetProperty("real_private_named_pipe").GetBoolean(), "evidence_pipe_missing");
        Require(document.RootElement.GetProperty("handshake_fields_redacted").GetBoolean(), "evidence_redaction_missing");
        Require(!File.ReadAllText(evidencePath).Contains("bootstrap_secret_base64", StringComparison.Ordinal), "evidence_secret_field_present");
        return evidencePath;
    }

    private static long ReadStartUnixMilliseconds(Process process)
    {
        return new DateTimeOffset(process.StartTime.ToUniversalTime()).ToUnixTimeMilliseconds();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class ParentExitResult
    {
        internal ParentExitResult(int parentExitCode, bool serviceStarted, bool serviceExited)
        {
            ParentExitCode = parentExitCode;
            ServiceStarted = serviceStarted;
            ServiceExited = serviceExited;
        }

        internal int ParentExitCode { get; }
        internal bool ServiceStarted { get; }
        internal bool ServiceExited { get; }
    }
}

[SupportedOSPlatform("windows")]
internal sealed class P3CHarnessRunner
{
    private readonly List<P3CCaseResult> caseResults = new List<P3CCaseResult>();
    private bool durableReceiptVerified;
    private bool crashAfterCommitBeforeResponseVerified;

    internal async Task<int> RunAsync()
    {
        var servicePath = ResolveServicePath();
        var dataRoot = Path.Combine(Path.GetTempPath(), "marcus-awake-p3c-" + Guid.NewGuid().ToString("N"));
        LaunchedService? service = null;
        ServiceConnection? connection = null;

        try
        {
            service = await LaunchedService.StartAsync(servicePath, 3000, dataRoot, AllCapabilities()).ConfigureAwait(false);
            connection = await service.ConnectAsync(service.Descriptor.ChallengeId, "p3c-session-one").ConfigureAwait(false);

            await RunCaseAsync("P3C-01 kv_round_trip", async () =>
            {
                var current = connection ?? throw new InvalidOperationException("p3c_connection_missing");
                const string payload = "{\"scope\":\"campaign\",\"namespace_id\":\"p3c\",\"key\":\"answer\",\"value\":\"42\"}";
                var set = await current.SendAndReadAsync(current.CreateBusinessFrame("storage.kv_set", "p3c-kv-set", "p3c-kv-set-task", payload, "p3c-idem-kv-set")).ConfigureAwait(false);
                AssertBusinessResponse(set, ProtocolConstants.MessageTypeStorageResult, "marcus-awake.storage.result.v1");
                AssertDurable(set);

                var get = await current.SendAndReadAsync(current.CreateBusinessFrame("storage.kv_get", "p3c-kv-get", "p3c-kv-get-task", "{\"scope\":\"campaign\",\"namespace_id\":\"p3c\",\"key\":\"answer\"}", "p3c-idem-kv-get")).ConfigureAwait(false);
                AssertBusinessResponse(get, ProtocolConstants.MessageTypeStorageResult, "marcus-awake.storage.result.v1");
                Require(get.NonDurable && get.AckStatus == ProtocolConstants.AckAccepted, "kv_read_durability_invalid");
                Require(GetBoolean(get, "found"), "kv_value_not_found");
                Require(GetString(get, "value") == "42", "kv_value_invalid");

                var delete = await current.SendAndReadAsync(current.CreateBusinessFrame("storage.kv_delete", "p3c-kv-delete", "p3c-kv-delete-task", "{\"scope\":\"campaign\",\"namespace_id\":\"p3c\",\"key\":\"answer\"}", "p3c-idem-kv-delete")).ConfigureAwait(false);
                AssertBusinessResponse(delete, ProtocolConstants.MessageTypeStorageResult, "marcus-awake.storage.result.v1");
                AssertDurable(delete);
            }).ConfigureAwait(false);

            await RunCaseAsync("P3C-02 timeline_idempotency_conflict", async () =>
            {
                var current = connection ?? throw new InvalidOperationException("p3c_connection_missing");
                var occurred = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var payload = "{\"event_id\":\"p3c-event-one\",\"event_type\":\"world.test\",\"payload_json\":\"{\\\"value\\\":1}\",\"occurred_unix_ms\":" + occurred.ToString() + ",\"correlation_id\":\"p3c-correlation\",\"causation_id\":\"p3c-causation\"}";
                var append = await current.SendAndReadAsync(current.CreateBusinessFrame("storage.timeline_append", "p3c-timeline-append", "p3c-timeline-task", payload, "p3c-idem-timeline-append")).ConfigureAwait(false);
                AssertBusinessResponse(append, ProtocolConstants.MessageTypeStorageResult, "marcus-awake.storage.result.v1");
                AssertDurable(append);
                Require(append.EventIndex > 0, "timeline_event_index_missing");

                var duplicate = await current.SendAndReadAsync(current.CreateBusinessFrame("storage.timeline_append", "p3c-timeline-duplicate", "p3c-timeline-duplicate-task", payload, "p3c-idem-timeline-duplicate")).ConfigureAwait(false);
                AssertBusinessResponse(duplicate, ProtocolConstants.MessageTypeStorageResult, "marcus-awake.storage.result.v1");
                AssertDurable(duplicate);
                Require(duplicate.EventIndex == append.EventIndex && GetBoolean(duplicate, "duplicate"), "timeline_duplicate_invalid");

                var conflictPayload = payload.Replace("\\\"value\\\":1", "\\\"value\\\":2", StringComparison.Ordinal);
                var conflict = await current.SendAndReadAsync(current.CreateBusinessFrame("storage.timeline_append", "p3c-timeline-conflict", "p3c-timeline-conflict-task", conflictPayload, "p3c-idem-timeline-conflict")).ConfigureAwait(false);
                Require(conflict.MessageType == ProtocolConstants.MessageTypeError && conflict.ErrorCode == "storage.ledger_event_conflict", "timeline_conflict_not_rejected");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3C-03 rag_scope_and_stale_corpus", async () =>
            {
                var current = connection ?? throw new InvalidOperationException("p3c_connection_missing");
                var observed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                var ingestPayload = "{\"collection_id\":\"p3c-worldbook\",\"corpus_fingerprint\":\"p3c-corpus-v1\",\"documents\":[" +
                    "{\"document_id\":\"public-doc\",\"text\":\"The western harbor collects grain.\",\"source_locator\":\"fixture:public\",\"access_scope\":\"public\",\"source_class\":\"worldbook\",\"corpus_locator\":\"fixture:p3c\",\"observed_unix_ms\":" + observed + "}," +
                    "{\"document_id\":\"private-doc\",\"text\":\"The sealed crown ledger records the hidden treasury.\",\"source_locator\":\"fixture:private\",\"access_scope\":\"noble\",\"source_class\":\"worldbook\",\"corpus_locator\":\"fixture:p3c\",\"observed_unix_ms\":" + observed + "}]}";
                var ingest = await current.SendAndReadAsync(current.CreateBusinessFrame("rag.ingest", "p3c-rag-ingest", "p3c-rag-ingest-task", ingestPayload, "p3c-idem-rag-ingest")).ConfigureAwait(false);
                AssertBusinessResponse(ingest, ProtocolConstants.MessageTypeRagResult, "marcus-awake.rag.result.v1");
                AssertDurable(ingest);
                Require(GetInt32(ingest, "ingested") == 2, "rag_ingest_count_invalid");

                var searchPayload = "{\"collection_id\":\"p3c-worldbook\",\"corpus_fingerprint\":\"p3c-corpus-v1\",\"query\":\"harbor\",\"access_scopes\":[\"public\"],\"maximum_results\":8}";
                var search = await current.SendAndReadAsync(current.CreateBusinessFrame("rag.search", "p3c-rag-search", "p3c-rag-search-task", searchPayload, "p3c-idem-rag-search")).ConfigureAwait(false);
                AssertBusinessResponse(search, ProtocolConstants.MessageTypeRagResult, "marcus-awake.rag.result.v1");
                Require(search.NonDurable && search.AckStatus == ProtocolConstants.AckAccepted, "rag_search_durability_invalid");
                Require(GetArrayLength(search, "hits") == 1 && GetArrayItemString(search, "hits", 0, "document_id") == "public-doc", "rag_access_scope_invalid");

                var stalePayload = searchPayload.Replace("p3c-corpus-v1", "p3c-corpus-old", StringComparison.Ordinal);
                var stale = await current.SendAndReadAsync(current.CreateBusinessFrame("rag.search", "p3c-rag-stale", "p3c-rag-stale-task", stalePayload, "p3c-idem-rag-stale")).ConfigureAwait(false);
                Require(stale.MessageType == ProtocolConstants.MessageTypeError && stale.ErrorCode == "rag.index_stale", "rag_stale_corpus_not_rejected");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3C-04 capability_denial", async () =>
            {
                var restricted = await service!.ConnectAsync(
                    service.Descriptor.ChallengeId + ".connection.2",
                    "p3c-session-restricted",
                    new[] { "health", "echo", "cancel", "diagnostic" }).ConfigureAwait(false);
                try
                {
                    var frame = restricted.CreateBusinessFrame("storage.kv_get", "p3c-denied-get", "p3c-denied-task", "{\"scope\":\"campaign\",\"namespace_id\":\"p3c\",\"key\":\"answer\"}", "p3c-idem-denied");
                    var response = await restricted.SendAndReadAsync(frame).ConfigureAwait(false);
                    Require(response.MessageType == ProtocolConstants.MessageTypeError && response.ErrorCode == "capability_not_granted", "capability_denial_missing");
                }
                finally
                {
                    restricted.Dispose();
                }
            }).ConfigureAwait(false);

            await RunCaseAsync("P3C-05 malformed_and_oversized", async () =>
            {
                var current = connection ?? throw new InvalidOperationException("p3c_connection_missing");
                var malformed = await current.SendAndReadAsync(current.CreateBusinessFrame("storage.kv_get", "p3c-malformed", "p3c-malformed-task", "{\"scope\":\"campaign\",\"namespace_id\":\"p3c\",\"key\":\"bad\",\"extra\":true}", "p3c-idem-malformed")).ConfigureAwait(false);
                Require(malformed.MessageType == ProtocolConstants.MessageTypeError && malformed.ErrorCode == "business_unknown_field:extra", "unknown_business_field_accepted");

                var oversizedValue = new string('x', 100 * 1024);
                var oversizedPayload = "{\"scope\":\"campaign\",\"namespace_id\":\"p3c\",\"key\":\"oversized\",\"value\":" + JsonSerializer.Serialize(oversizedValue) + "}";
                var oversized = await current.SendAndReadAsync(current.CreateBusinessFrame("storage.kv_set", "p3c-oversized", "p3c-oversized-task", oversizedPayload, "p3c-idem-oversized")).ConfigureAwait(false);
                Require(oversized.MessageType == ProtocolConstants.MessageTypeError && oversized.ErrorCode == "business_payload_too_large", "oversized_business_payload_accepted");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3C-06 restart_receipt_replay", async () =>
            {
                connection?.Dispose();
                connection = null;
                if (service != null)
                {
                    await service.DisposeAsync().ConfigureAwait(false);
                    service = null;
                }

                service = await LaunchedService.StartAsync(servicePath, 3001, dataRoot, AllCapabilities()).ConfigureAwait(false);
                connection = await service.ConnectAsync(service.Descriptor.ChallengeId, "p3c-session-after-restart").ConfigureAwait(false);
                var replay = await connection.SendAndReadAsync(connection.CreateBusinessFrame("storage.kv_set", "p3c-kv-replay", "p3c-kv-replay-task", "{\"scope\":\"campaign\",\"namespace_id\":\"p3c\",\"key\":\"answer\",\"value\":\"42\"}", "p3c-idem-kv-set")).ConfigureAwait(false);
                AssertBusinessResponse(replay, ProtocolConstants.MessageTypeStorageResult, "marcus-awake.storage.result.v1");
                Require(replay.AckStatus == ProtocolConstants.AckDurablyRecorded && !replay.NonDurable && replay.OutcomeKind == ProtocolConstants.OutcomeTerminalReplay, "durable_receipt_replay_invalid");
                durableReceiptVerified = true;
            }).ConfigureAwait(false);

            await RunCaseAsync("P3C-07 crash_after_commit_before_response", async () =>
            {
                connection?.Dispose();
                connection = null;
                if (service != null)
                {
                    await service.DisposeAsync().ConfigureAwait(false);
                    service = null;
                }

                const string messageId = "p3c-crash-set";
                const string taskId = "p3c-crash-task";
                const string idempotencyKey = "p3c-idem-crash-set";
                const string payload = "{\"scope\":\"campaign\",\"namespace_id\":\"p3c\",\"key\":\"crash_answer\",\"value\":\"committed\"}";

                var crashService = await LaunchedService.StartAsync(
                    servicePath,
                    3002,
                    dataRoot,
                    AllCapabilities(),
                    messageId).ConfigureAwait(false);
                service = crashService;
                ServiceConnection? crashConnection = null;
                try
                {
                    crashConnection = await crashService.ConnectAsync(crashService.Descriptor.ChallengeId, "p3c-session-crash").ConfigureAwait(false);
                    var crashFrame = crashConnection.CreateBusinessFrame("storage.kv_set", messageId, taskId, payload, idempotencyKey);
                    var disconnected = false;
                    try
                    {
                        await crashConnection.SendAndReadAsync(crashFrame).ConfigureAwait(false);
                    }
                    catch (EndOfStreamException)
                    {
                        disconnected = true;
                    }
                    catch (IOException)
                    {
                        disconnected = true;
                    }
                    catch (OperationCanceledException)
                    {
                        disconnected = true;
                    }

                    Require(disconnected, "crash_response_was_sent");
                    Require(await WaitForExitAsync(crashService.Process, 3000).ConfigureAwait(false), "crash_service_did_not_exit");
                }
                finally
                {
                    crashConnection?.Dispose();
                }

                await crashService.DisposeAsync().ConfigureAwait(false);
                service = null;

                var recoveryService = await LaunchedService.StartAsync(servicePath, 3003, dataRoot, AllCapabilities()).ConfigureAwait(false);
                service = recoveryService;
                connection = await recoveryService.ConnectAsync(recoveryService.Descriptor.ChallengeId, "p3c-session-crash-recovery").ConfigureAwait(false);
                var replay = await connection.SendAndReadAsync(connection.CreateBusinessFrame("storage.kv_set", "p3c-crash-replay", "p3c-crash-replay-task", payload, idempotencyKey)).ConfigureAwait(false);
                AssertBusinessResponse(replay, ProtocolConstants.MessageTypeStorageResult, "marcus-awake.storage.result.v1");
                AssertDurable(replay);
                Require(replay.OutcomeKind == ProtocolConstants.OutcomeTerminalReplay, "crash_receipt_replay_invalid");

                var get = await connection.SendAndReadAsync(connection.CreateBusinessFrame("storage.kv_get", "p3c-crash-get", "p3c-crash-get-task", "{\"scope\":\"campaign\",\"namespace_id\":\"p3c\",\"key\":\"crash_answer\"}", "p3c-idem-crash-get")).ConfigureAwait(false);
                AssertBusinessResponse(get, ProtocolConstants.MessageTypeStorageResult, "marcus-awake.storage.result.v1");
                Require(get.NonDurable && get.AckStatus == ProtocolConstants.AckAccepted, "crash_value_read_durability_invalid");
                Require(GetBoolean(get, "found") && GetString(get, "value") == "committed", "crash_value_not_persisted_once");
                crashAfterCommitBeforeResponseVerified = true;
            }).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Console.WriteLine("FAIL p3c_harness_fatal " + exception.Message);
            caseResults.Add(new P3CCaseResult("P3C-FATAL", false, exception.GetType().Name));
        }
        finally
        {
            connection?.Dispose();
            if (service != null) await service.DisposeAsync().ConfigureAwait(false);
            CleanupDataRoot(dataRoot);
        }

        var evidencePath = WriteEvidence(servicePath);
        Console.WriteLine("EVIDENCE " + evidencePath);
        Console.WriteLine("PASS_COUNT=" + caseResults.Count(item => item.Passed).ToString());
        Console.WriteLine("FAIL_COUNT=" + caseResults.Count(item => !item.Passed).ToString());
        return caseResults.All(item => item.Passed) && durableReceiptVerified && crashAfterCommitBeforeResponseVerified ? 0 : 1;
    }

    private async Task RunCaseAsync(string caseId, Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
            caseResults.Add(new P3CCaseResult(caseId, true, string.Empty));
            Console.WriteLine("PASS " + caseId);
        }
        catch (Exception exception)
        {
            caseResults.Add(new P3CCaseResult(caseId, false, exception.GetType().Name));
            Console.WriteLine("FAIL " + caseId + " " + exception.Message);
        }
    }

    private static string[] AllCapabilities()
    {
        return new[]
        {
            "health",
            "echo",
            "cancel",
            "diagnostic",
            ProtocolConstants.CapabilityStorageRead,
            ProtocolConstants.CapabilityStorageWrite,
            ProtocolConstants.CapabilityRagRead,
            ProtocolConstants.CapabilityRagWrite
        };
    }

    private string WriteEvidence(string servicePath)
    {
        var evidencePath = Path.Combine(AppContext.BaseDirectory, "MARCUS-AWAKE-P3C-evidence.json");
        var transportPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MarcusAwakeTransport", "_build_out", "Release", "MarcusAwakeTransport.dll"));
        var harnessPath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "MarcusAwakeRuntimeService.Tests.exe");
        var record = new
        {
            schema = "marcus-awake-p3c-evidence.v1",
            captured_at_utc = DateTimeOffset.UtcNow.ToString("O"),
            protocol_id = ProtocolConstants.ProtocolId,
            protocol_major = ProtocolConstants.ProtocolMajor,
            protocol_minor = ProtocolConstants.ProtocolMinor,
            framework_api_major = ProtocolConstants.FrameworkApiMajor,
            checksum_algorithm = ProtocolConstants.ChecksumAlgorithm,
            service_binary_sha256 = TransportSecurity.Sha256FileHex(servicePath),
            transport_binary_sha256 = TransportSecurity.Sha256FileHex(transportPath),
            harness_binary_sha256 = TransportSecurity.Sha256FileHex(harnessPath),
            logical_database_root_fingerprint = TransportSecurity.Sha256Hex(Encoding.UTF8.GetBytes("marcus-awake-p3c-logical-database-root")),
            real_child_process = true,
            real_private_named_pipe = true,
            durable_receipt_verified = durableReceiptVerified,
            crash_after_commit_before_response_verified = crashAfterCommitBeforeResponseVerified,
            all_cases_passed = caseResults.All(item => item.Passed),
            cases = caseResults.Select(item => new { id = item.Id, passed = item.Passed, error_kind = item.ErrorKind }).ToArray()
        };
        var json = JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(evidencePath, json, new UTF8Encoding(false));
        using var document = JsonDocument.Parse(File.ReadAllText(evidencePath));
        Require(document.RootElement.GetProperty("schema").GetString() == "marcus-awake-p3c-evidence.v1", "p3c_evidence_schema_invalid");
        Require(document.RootElement.GetProperty("real_child_process").GetBoolean(), "p3c_evidence_child_process_missing");
        Require(document.RootElement.GetProperty("real_private_named_pipe").GetBoolean(), "p3c_evidence_pipe_missing");
        Require(!json.Contains("api_key", StringComparison.OrdinalIgnoreCase) && !json.Contains("authorization", StringComparison.OrdinalIgnoreCase) && !json.Contains("database_path", StringComparison.OrdinalIgnoreCase), "p3c_evidence_forbidden_field_present");
        return evidencePath;
    }

    private static string ResolveServicePath()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "_build_out", "Release", "MarcusAwakeRuntimeService.exe"));
        if (!File.Exists(path)) throw new InvalidOperationException("service_executable_missing:" + path);
        return path;
    }

    private static void CleanupDataRoot(string dataRoot)
    {
        if (Directory.Exists(dataRoot)) Directory.Delete(dataRoot, true);
    }

    private static async Task<bool> WaitForExitAsync(Process process, int timeoutMilliseconds)
    {
        using var cancellation = new CancellationTokenSource(timeoutMilliseconds);
        try
        {
            await process.WaitForExitAsync(cancellation.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static void AssertBusinessResponse(PipeEnvelope response, string messageType, string payloadSchema)
    {
        Require(response.MessageType == messageType, "business_response_type_invalid");
        Require(response.PayloadSchema == payloadSchema, "business_response_schema_invalid");
    }

    private static void AssertDurable(PipeEnvelope response)
    {
        Require(response.AckStatus == ProtocolConstants.AckDurablyRecorded && !response.NonDurable, "durable_response_invalid");
    }

    private static string GetString(PipeEnvelope response, string propertyName)
    {
        using var document = JsonDocument.Parse(response.PayloadJson);
        return document.RootElement.GetProperty(propertyName).GetString() ?? string.Empty;
    }

    private static bool GetBoolean(PipeEnvelope response, string propertyName)
    {
        using var document = JsonDocument.Parse(response.PayloadJson);
        return document.RootElement.GetProperty(propertyName).GetBoolean();
    }

    private static int GetInt32(PipeEnvelope response, string propertyName)
    {
        using var document = JsonDocument.Parse(response.PayloadJson);
        return document.RootElement.GetProperty(propertyName).GetInt32();
    }

    private static int GetArrayLength(PipeEnvelope response, string propertyName)
    {
        using var document = JsonDocument.Parse(response.PayloadJson);
        return document.RootElement.GetProperty(propertyName).GetArrayLength();
    }

    private static string GetArrayItemString(PipeEnvelope response, string arrayProperty, int index, string propertyName)
    {
        using var document = JsonDocument.Parse(response.PayloadJson);
        return document.RootElement.GetProperty(arrayProperty)[index].GetProperty(propertyName).GetString() ?? string.Empty;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class P3CCaseResult
    {
        internal P3CCaseResult(string id, bool passed, string errorKind)
        {
            Id = id;
            Passed = passed;
            ErrorKind = errorKind;
        }

        internal string Id { get; }
        internal bool Passed { get; }
        internal string ErrorKind { get; }
    }
}

[SupportedOSPlatform("windows")]
internal sealed class LaunchedService : IAsyncDisposable
{
    private Task<string>? standardErrorTask;
    private readonly SemaphoreSlim connectionGate = new SemaphoreSlim(1, 1);
    private long highestAcceptedConnectionEpoch;

    private LaunchedService(Process process, string servicePath, BootstrapDescriptor descriptor, ServiceReadyDescriptor ready, byte[] serviceKey, byte[] frameKey, IReadOnlyList<string> commandLineArguments)
    {
        Process = process;
        ServicePath = servicePath;
        Descriptor = descriptor;
        Ready = ready;
        ServiceKey = serviceKey;
        FrameKey = frameKey;
        CommandLineArguments = commandLineArguments;
    }

    internal Process Process { get; }
    internal string ServicePath { get; }
    internal BootstrapDescriptor Descriptor { get; }
    internal ServiceReadyDescriptor Ready { get; }
    internal byte[] ServiceKey { get; }
    internal byte[] FrameKey { get; }
    internal IReadOnlyList<string> CommandLineArguments { get; }
    internal int FinalExitCode { get; private set; } = -1;

    internal static async Task<LaunchedService> StartAsync(string servicePath, long instanceEpoch, string? dataRoot = null, IReadOnlyList<string>? requestedCapabilities = null, string? crashAfterCommitBeforeResponseMessageId = null, IReadOnlyDictionary<string, string>? environmentOverrides = null)
    {
        var keyMaterial = TransportSecurity.RandomBytes(32);
        var descriptor = CreateDescriptor(servicePath, instanceEpoch, keyMaterial, requestedCapabilities, out var serviceKey);
        var frameKey = TransportSecurity.DeriveAuthKey(keyMaterial, descriptor.LaunchNonce, "marcus-awake-runtime-frame", descriptor.LaunchTransactionId, descriptor.AuthKeyId);
        var effectiveDataRoot = string.IsNullOrWhiteSpace(dataRoot)
            ? string.Empty
            : Path.GetFullPath(dataRoot);
        var startInfo = new ProcessStartInfo
        {
            FileName = servicePath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        if (!string.IsNullOrWhiteSpace(effectiveDataRoot))
        {
            Directory.CreateDirectory(effectiveDataRoot);
            startInfo.Environment["MARCUS_AWAKE_RUNTIME_DATA_ROOT"] = effectiveDataRoot;
        }
        if (!string.IsNullOrWhiteSpace(crashAfterCommitBeforeResponseMessageId))
        {
            startInfo.Environment["MARCUS_AWAKE_RUNTIME_TEST_MODE"] = "p3c";
            startInfo.Environment["MARCUS_AWAKE_TEST_CRASH_AFTER_COMMIT_BEFORE_RESPONSE"] = crashAfterCommitBeforeResponseMessageId;
        }
        if (environmentOverrides != null)
        {
            foreach (var pair in environmentOverrides) startInfo.Environment[pair.Key] = pair.Value;
        }
        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("service_process_start_failed");

        try
        {
            using var timeout = new CancellationTokenSource(5000);
            var input = process.StandardInput.BaseStream;
            await PipeFrameIO.WriteFrameAsync(input, ProtocolCodec.SerializeBootstrap(descriptor), timeout.Token).ConfigureAwait(false);
            await PipeFrameIO.WriteFrameAsync(input, Convert.ToBase64String(keyMaterial), timeout.Token).ConfigureAwait(false);
            var readyJson = await PipeFrameIO.ReadFrameAsync(process.StandardOutput.BaseStream, timeout.Token).ConfigureAwait(false);
            if (!ProtocolCodec.TryParseServiceReady(readyJson, out var ready, out var error)) throw new InvalidOperationException("service_ready_invalid:" + error);
            RequireReady(descriptor, ready, serviceKey, servicePath);
            return new LaunchedService(process, servicePath, descriptor, ready, serviceKey, frameKey, startInfo.ArgumentList.ToArray());
        }
        catch
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            process.Dispose();
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyMaterial);
        }
    }

    internal async Task<HandshakeResponse> AttemptHandshakeAsync(Func<HandshakeRequest, HandshakeRequest>? mutate, string challengeId, string sessionId)
    {
        using var connection = await OpenPipeAsync().ConfigureAwait(false);
        var request = CreateHandshakeRequest(challengeId, sessionId);
        if (mutate != null) request = mutate(request);
        var raw = ProtocolCodec.SerializeHandshakeRequest(request);
        await PipeFrameIO.WriteFrameAsync(connection, raw, CancellationToken.None).ConfigureAwait(false);
        var responseJson = await ReadWithTimeoutAsync(connection, 3000).ConfigureAwait(false);
        if (!ProtocolCodec.TryParseHandshakeResponse(responseJson, out var response, out var error)) throw new InvalidOperationException("handshake_response_invalid:" + error);
        return response;
    }

    internal async Task<HandshakeResponse?> AttemptRawHandshakeAsync(Func<string, string> mutate, string challengeId, string sessionId)
    {
        using var connection = await OpenPipeAsync().ConfigureAwait(false);
        var request = CreateHandshakeRequest(challengeId, sessionId);
        var raw = mutate(ProtocolCodec.SerializeHandshakeRequest(request));
        await PipeFrameIO.WriteFrameAsync(connection, raw, CancellationToken.None).ConfigureAwait(false);
        try
        {
            var responseJson = await ReadWithTimeoutAsync(connection, 3000).ConfigureAwait(false);
            if (!ProtocolCodec.TryParseHandshakeResponse(responseJson, out var response, out var error)) throw new InvalidOperationException("handshake_response_invalid:" + error);
            return response;
        }
        catch (EndOfStreamException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    internal async Task<ServiceConnection> ConnectAsync(string challengeId, string sessionId, IReadOnlyList<string>? requestedCapabilities = null)
    {
        var connection = await OpenPipeAsync().ConfigureAwait(false);
        try
        {
            var request = CreateHandshakeRequest(challengeId, sessionId, requestedCapabilities);
            await PipeFrameIO.WriteFrameAsync(connection, ProtocolCodec.SerializeHandshakeRequest(request), CancellationToken.None).ConfigureAwait(false);
            var responseJson = await ReadWithTimeoutAsync(connection, 3000).ConfigureAwait(false);
            if (!ProtocolCodec.TryParseHandshakeResponse(responseJson, out var response, out var error)) throw new InvalidOperationException("handshake_response_invalid:" + error);
            if (!response.Accepted) throw new InvalidOperationException("handshake_rejected:" + response.ErrorCode);
            ValidateAcceptedHandshake(request, response);
            InterlockedMax(ref highestAcceptedConnectionEpoch, response.ConnectionEpoch);
            return new ServiceConnection(connection, Descriptor, FrameKey, request, response);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    internal async Task<ServiceConnection> ConnectNextAsync(string sessionId, IReadOnlyList<string>? requestedCapabilities = null)
    {
        await connectionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var nextEpoch = Interlocked.Read(ref highestAcceptedConnectionEpoch) + 1;
            var challengeId = Descriptor.ChallengeId + ".connection." + nextEpoch.ToString();
            return await ConnectAsync(challengeId, sessionId, requestedCapabilities).ConfigureAwait(false);
        }
        finally
        {
            connectionGate.Release();
        }
    }

    internal async Task RequestGracefulShutdownAsync(string sessionId)
    {
        if (Process.HasExited) return;
        using var connection = await ConnectNextAsync(sessionId, new[]
        {
            ProtocolConstants.MessageTypeHealth,
            ProtocolConstants.MessageTypeShutdown
        }).ConfigureAwait(false);
        var shutdown = connection.CreateFrame(
            ProtocolConstants.MessageTypeShutdown,
            "shutdown-" + Guid.NewGuid().ToString("N"),
            "{}",
            null,
            "campaign-p3d-a1",
            "timeline-p3d-a1",
            1);
        var response = await connection.SendAndReadAsync(shutdown).ConfigureAwait(false);
        Require(response.MessageType == ProtocolConstants.MessageTypeShutdownAck, "graceful_shutdown_ack_missing");
        Require(await WaitForExitAsync(Process, 3000).ConfigureAwait(false), "graceful_shutdown_did_not_exit");
    }

    private static void InterlockedMax(ref long location, long value)
    {
        while (true)
        {
            var current = Interlocked.Read(ref location);
            if (current >= value) return;
            if (Interlocked.CompareExchange(ref location, value, current) == current) return;
        }
    }

    internal Task<string> ReadStandardErrorAsync()
    {
        return standardErrorTask ??= Process.StandardError.ReadToEndAsync();
    }

    private async Task<NamedPipeClientStream> OpenPipeAsync()
    {
        var connection = new NamedPipeClientStream(".", Descriptor.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            await connection.ConnectAsync(3000).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private HandshakeRequest CreateHandshakeRequest(string challengeId, string sessionId, IReadOnlyList<string>? requestedCapabilities = null)
    {
        var request = new HandshakeRequest
        {
            ProtocolId = ProtocolConstants.ProtocolId,
            ProtocolMajor = ProtocolConstants.ProtocolMajor,
            ProtocolMinor = ProtocolConstants.ProtocolMinor,
            ClientInstanceId = Descriptor.ClientInstanceId,
            ServiceInstanceId = Ready.ServiceInstanceId,
            FrameworkApiMajor = ProtocolConstants.FrameworkApiMajor,
            BannerlordApiVersion = Descriptor.BannerlordApiVersion,
            RequestedCapabilities = (requestedCapabilities ?? Descriptor.RequestedCapabilities).ToArray(),
            SessionNonce = Descriptor.SessionNonce,
            UserSidFingerprint = Descriptor.UserSidFingerprint,
            ParentProcessId = Descriptor.ParentProcessId,
            ParentStartUnixMilliseconds = Descriptor.ParentStartUnixMilliseconds,
            InstanceEpoch = Descriptor.InstanceEpoch,
            LaunchTransactionId = Descriptor.LaunchTransactionId,
            LaunchNonce = Descriptor.LaunchNonce,
            ChallengeId = challengeId,
            AuthKeyId = Descriptor.AuthKeyId,
            PipeName = Descriptor.PipeName,
            ClientBootstrapProof = Descriptor.ClientBootstrapProof,
            ServiceBootstrapProof = Ready.ServiceBootstrapProof,
            SessionId = sessionId
        };
        request.ChallengeResponse = TransportSecurity.ComputeChallengeResponse(request, ServiceKey);
        return request;
    }

    private void ValidateAcceptedHandshake(HandshakeRequest request, HandshakeResponse response)
    {
        Require(response.ProtocolId == ProtocolConstants.ProtocolId, "handshake_protocol_id_invalid");
        Require(response.ProtocolMajor == ProtocolConstants.ProtocolMajor && response.ProtocolMinor == ProtocolConstants.ProtocolMinor, "handshake_protocol_version_invalid");
        Require(response.FrameworkApiMajor == ProtocolConstants.FrameworkApiMajor, "handshake_api_major_invalid");
        Require(response.ServiceId == ProtocolConstants.ServiceId, "handshake_service_id_invalid");
        Require(response.ServiceInstanceId == Ready.ServiceInstanceId, "handshake_service_instance_invalid");
        Require(response.UserSidFingerprint == Descriptor.UserSidFingerprint, "handshake_sid_invalid");
        Require(response.ParentProcessId == Descriptor.ParentProcessId && response.ParentStartProof == Ready.ParentStartProof, "handshake_parent_proof_invalid");
        Require(response.InstanceEpoch == Descriptor.InstanceEpoch && response.ConnectionEpoch > 0, "handshake_epoch_invalid");
        Require(response.AckStatus == "accepted" && response.OutcomeKind == "accepted" && response.NonDurable, "handshake_ack_invalid");
        var expectedServiceAuth = TransportSecurity.ComputeServiceAuthResponse(request, ServiceKey, Ready.ServiceInstanceId, response.ConnectionEpoch, response.ClientDirectionNonce, response.ServiceDirectionNonce, Ready.ParentStartProof);
        Require(TransportSecurity.FixedTimeEquals(expectedServiceAuth, response.ServiceBootstrapProof), "handshake_service_auth_invalid");
        Require(response.Capabilities.SequenceEqual(request.RequestedCapabilities, StringComparer.Ordinal), "handshake_capability_intersection_invalid");
    }

    private static void RequireReady(BootstrapDescriptor descriptor, ServiceReadyDescriptor ready, byte[] serviceKey, string servicePath)
    {
        Require(ready.ProtocolId == ProtocolConstants.ProtocolId && ready.ProtocolMajor == ProtocolConstants.ProtocolMajor && ready.ProtocolMinor == ProtocolConstants.ProtocolMinor, "service_ready_protocol_invalid");
        Require(ready.FrameworkApiMajor == ProtocolConstants.FrameworkApiMajor, "service_ready_api_invalid");
        Require(ready.ServiceId == ProtocolConstants.ServiceId, "service_ready_service_invalid");
        Require(ready.ServiceProcessId > 0 && ready.ServiceStartUnixMilliseconds > 0, "service_ready_process_invalid");
        Require(ready.ServiceArtifactSha256 == TransportSecurity.Sha256FileHex(servicePath), "service_ready_hash_invalid");
        Require(ready.PipeName == descriptor.PipeName, "service_ready_pipe_invalid");
        Require(ready.UserSidFingerprint == descriptor.UserSidFingerprint, "service_ready_sid_invalid");
        var expectedParentProof = TransportSecurity.ComputeParentStartProof(descriptor, serviceKey, ready.ServiceProcessId, ready.ServiceStartUnixMilliseconds, ready.ServiceArtifactSha256);
        Require(TransportSecurity.FixedTimeEquals(expectedParentProof, ready.ParentStartProof), "service_ready_parent_proof_invalid");
        var expectedServiceProof = TransportSecurity.ComputeServiceBootstrapProof(descriptor, serviceKey, ready.ServiceInstanceId, ready.ServiceProcessId, ready.ServiceStartUnixMilliseconds, ready.ServiceArtifactSha256, ready.ParentStartProof);
        Require(TransportSecurity.FixedTimeEquals(expectedServiceProof, ready.ServiceBootstrapProof), "service_ready_bootstrap_proof_invalid");
    }

    private static BootstrapDescriptor CreateDescriptor(string servicePath, long instanceEpoch, byte[] keyMaterial, IReadOnlyList<string>? requestedCapabilities, out byte[] serviceKey)
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (identity.User == null) throw new InvalidOperationException("current_sid_missing");
        var sidFingerprint = TransportSecurity.SidFingerprint(identity.User.Value);
        var descriptor = new BootstrapDescriptor
        {
            ProtocolId = ProtocolConstants.ProtocolId,
            ProtocolMajor = ProtocolConstants.ProtocolMajor,
            ProtocolMinor = ProtocolConstants.ProtocolMinor,
            FrameworkApiMajor = ProtocolConstants.FrameworkApiMajor,
            BannerlordApiVersion = "1.3.15",
            ServiceId = ProtocolConstants.ServiceId,
            ClientInstanceId = "client-" + TransportSecurity.RandomHex(12),
            LaunchTransactionId = "launch-" + TransportSecurity.RandomHex(12),
            LaunchNonce = TransportSecurity.RandomHex(32),
            ChallengeId = "challenge-" + TransportSecurity.RandomHex(12),
            SessionNonce = "session-nonce-" + TransportSecurity.RandomHex(16),
            UserSidFingerprint = sidFingerprint,
            ParentProcessId = Environment.ProcessId,
            ParentStartUnixMilliseconds = ReadStartUnixMilliseconds(Process.GetCurrentProcess()),
            InstanceEpoch = instanceEpoch,
            AuthKeyId = "bootstrap-v1",
            ExpectedServiceArtifactSha256 = TransportSecurity.Sha256FileHex(servicePath),
            PipeName = ProtocolConstants.PipePrefix + sidFingerprint,
            RequestedCapabilities = (requestedCapabilities == null || requestedCapabilities.Count == 0)
                ? new[] { "health", "echo", "cancel", "diagnostic" }
                : requestedCapabilities.ToArray()
        };
        serviceKey = TransportSecurity.DeriveAuthKey(keyMaterial, descriptor.LaunchNonce, "marcus-awake-runtime", descriptor.LaunchTransactionId, descriptor.AuthKeyId);
        descriptor.ClientBootstrapProof = TransportSecurity.ComputeClientBootstrapProof(descriptor, serviceKey);
        return descriptor;
    }

    private static async Task<string> ReadWithTimeoutAsync(Stream stream, int timeoutMilliseconds)
    {
        using var timeout = new CancellationTokenSource(timeoutMilliseconds);
        return await PipeFrameIO.ReadFrameAsync(stream, timeout.Token).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (!Process.HasExited)
        {
            try
            {
                Process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

                await WaitForExitAsync(Process, 3000).ConfigureAwait(false);
        }

        var errorText = await ReadStandardErrorAsync().ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(errorText)) Console.WriteLine("SERVICE_STDERR=" + errorText.Trim().Replace(Environment.NewLine, " | ", StringComparison.Ordinal));
        FinalExitCode = Process.HasExited ? Process.ExitCode : -1;
        if (ServiceKey.Length > 0) CryptographicOperations.ZeroMemory(ServiceKey);
        if (FrameKey.Length > 0) CryptographicOperations.ZeroMemory(FrameKey);
        Process.Dispose();
        connectionGate.Dispose();
    }

    private static long ReadStartUnixMilliseconds(Process process)
    {
        return new DateTimeOffset(process.StartTime.ToUniversalTime()).ToUnixTimeMilliseconds();
    }

    private static async Task<bool> WaitForExitAsync(Process process, int timeoutMilliseconds)
    {
        using var cancellation = new CancellationTokenSource(timeoutMilliseconds);
        try
        {
            await process.WaitForExitAsync(cancellation.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

[SupportedOSPlatform("windows")]
internal sealed class ServiceConnection : IDisposable
{
    private readonly NamedPipeClientStream pipe;
    private readonly BootstrapDescriptor descriptor;
    private readonly byte[] frameKey;
    private readonly HandshakeRequest request;
    private readonly Queue<PipeEnvelope> bufferedResponses = new Queue<PipeEnvelope>();
    private readonly SequenceWindow responseWindow;
    private long nextSequence;
    private bool disposed;

    internal ServiceConnection(NamedPipeClientStream pipe, BootstrapDescriptor descriptor, byte[] frameKey, HandshakeRequest request, HandshakeResponse handshakeResponse)
    {
        this.pipe = pipe;
        this.descriptor = descriptor;
        this.frameKey = frameKey;
        this.request = request;
        HandshakeResponse = handshakeResponse;
        responseWindow = new SequenceWindow(request.SessionId, handshakeResponse.ConnectionEpoch, handshakeResponse.ServiceDirectionNonce);
    }

    internal HandshakeResponse HandshakeResponse { get; }

    internal string SessionId => request.SessionId;
    internal byte[] FrameKey => frameKey;

    internal FrameBuildResult CreateFrame(string messageType, string messageId, string payloadJson, long? sequence = null, string campaignGuid = "", string timelineId = "", long sessionGeneration = 0, TaskScopeEnvelope? taskScope = null, string ownerId = "p3b-harness")
    {
        if (taskScope != null) taskScope.MessageId = messageId;
        var canonicalPayload = CanonicalizePayload(payloadJson);
        var payloadBytes = Encoding.UTF8.GetBytes(canonicalPayload);
        var payloadHash = TransportSecurity.Sha256Hex(payloadBytes);
        var envelope = new PipeEnvelope
        {
            MessageType = messageType,
            MessageId = messageId,
            RequestId = messageId,
            CorrelationId = "corr-" + messageId,
            CausationId = string.Empty,
            CampaignGuid = campaignGuid,
            TimelineId = timelineId,
            SessionId = request.SessionId,
            SessionGeneration = sessionGeneration,
            OwnerId = ownerId,
            DeadlineUnixMilliseconds = DateTimeOffset.UtcNow.AddSeconds(5).ToUnixTimeMilliseconds(),
            InstanceEpoch = descriptor.InstanceEpoch,
            ConnectionEpoch = HandshakeResponse.ConnectionEpoch,
            DirectionNonce = HandshakeResponse.ClientDirectionNonce,
            Sequence = sequence ?? Interlocked.Increment(ref nextSequence),
            PayloadSchema = "marcus-awake." + messageType + ".v1",
            PayloadJson = canonicalPayload,
            PayloadLength = payloadBytes.Length,
            PayloadSha256 = payloadHash,
            Checksum = payloadHash,
            TaskScope = taskScope
        };
        envelope.FenceProof = TransportSecurity.ComputeFrameFenceProof(frameKey, envelope);
        return new FrameBuildResult(envelope, ProtocolCodec.SerializeEnvelope(envelope));
    }

    internal FrameBuildResult CreateTaskFrame(string messageType, string messageId, string taskId, string payloadJson)
    {
        var canonicalPayload = CanonicalizePayload(payloadJson);
        var payloadHash = TransportSecurity.Sha256Hex(Encoding.UTF8.GetBytes(canonicalPayload));
        var taskScope = new TaskScopeEnvelope
        {
            TaskId = taskId,
            MessageId = messageId,
            OwnerId = "p3b-harness",
            RouteId = "marcus-awake.runtime." + messageType,
            ProviderId = "p3b.in-memory",
            ProfileId = "p3b",
            IdempotencyKey = "idem-" + taskId,
            RequestPayloadHash = payloadHash,
            OutputSchemaId = "marcus-awake." + messageType + ".v1",
            OutputSchemaMajor = 1,
            OutputSchemaMinor = 0,
            SettlementRequirement = "not_applicable"
        };
        return CreateFrame(messageType, messageId, canonicalPayload, null, "campaign-p3b", "timeline-p3b", 1, taskScope);
    }

    internal FrameBuildResult CreateBusinessFrame(string messageType, string messageId, string taskId, string payloadJson, string idempotencyKey, string ownerId = "p3c-harness", string campaignGuid = "campaign-p3c", string timelineId = "timeline-p3c", long sessionGeneration = 1)
    {
        var canonicalPayload = CanonicalizePayload(payloadJson);
        var payloadHash = TransportSecurity.Sha256Hex(Encoding.UTF8.GetBytes(canonicalPayload));
        var taskScope = new TaskScopeEnvelope
        {
            TaskId = taskId,
            MessageId = messageId,
            OwnerId = ownerId,
            RouteId = "marcus-awake.runtime." + messageType,
            ProviderId = "p3c.sqlite",
            ProfileId = "p3c",
            IdempotencyKey = idempotencyKey,
            RequestPayloadHash = payloadHash,
            OutputSchemaId = "marcus-awake." + messageType + ".v1",
            OutputSchemaMajor = 1,
            OutputSchemaMinor = 0,
            SettlementRequirement = "not_applicable"
        };
        return CreateFrame(messageType, messageId, canonicalPayload, null, campaignGuid, timelineId, sessionGeneration, taskScope, ownerId);
    }

    internal FrameBuildResult RewriteFrame(FrameBuildResult original, Action<PipeEnvelope> mutate)
    {
        if (original == null) throw new ArgumentNullException(nameof(original));
        if (mutate == null) throw new ArgumentNullException(nameof(mutate));
        if (!ProtocolCodec.TryParseEnvelope(original.RawJson, out var envelope, out var error)) throw new InvalidDataException("frame_decode_failed:" + error);
        mutate(envelope);
        var canonicalPayload = CanonicalizePayload(envelope.PayloadJson);
        var payloadBytes = Encoding.UTF8.GetBytes(canonicalPayload);
        envelope.PayloadJson = canonicalPayload;
        envelope.PayloadLength = payloadBytes.Length;
        envelope.PayloadSha256 = TransportSecurity.Sha256Hex(payloadBytes);
        envelope.Checksum = envelope.PayloadSha256;
        envelope.FenceProof = TransportSecurity.ComputeFrameFenceProof(frameKey, envelope);
        return new FrameBuildResult(envelope, ProtocolCodec.SerializeEnvelope(envelope));
    }

    internal string RewriteRawFrame(FrameBuildResult original, Action<PipeEnvelope> mutate, Func<string, string> rewriteJson)
    {
        if (original == null) throw new ArgumentNullException(nameof(original));
        if (mutate == null) throw new ArgumentNullException(nameof(mutate));
        if (rewriteJson == null) throw new ArgumentNullException(nameof(rewriteJson));
        if (!ProtocolCodec.TryParseEnvelope(original.RawJson, out var envelope, out var error)) throw new InvalidDataException("frame_decode_failed:" + error);
        mutate(envelope);
        var canonicalPayload = CanonicalizePayload(envelope.PayloadJson);
        var payloadBytes = Encoding.UTF8.GetBytes(canonicalPayload);
        envelope.PayloadJson = canonicalPayload;
        envelope.PayloadLength = payloadBytes.Length;
        envelope.PayloadSha256 = TransportSecurity.Sha256Hex(payloadBytes);
        envelope.Checksum = envelope.PayloadSha256;
        envelope.FenceProof = TransportSecurity.ComputeFrameFenceProof(frameKey, envelope);
        var validJson = ProtocolCodec.SerializeEnvelope(envelope);
        return rewriteJson(validJson);
    }

    internal async Task SendRawAsync(string rawJson)
    {
        await PipeFrameIO.WriteFrameAsync(pipe, rawJson, CancellationToken.None).ConfigureAwait(false);
    }

    internal string RewriteMessageIdWithValidFence(string rawJson, string messageId)
    {
        if (!ProtocolCodec.TryParseEnvelope(rawJson, out var envelope, out var error)) throw new InvalidDataException(error);
        var oldMessageId = envelope.MessageId;
        var oldFenceProof = envelope.FenceProof;
        envelope.MessageId = messageId;
        var newFenceProof = TransportSecurity.ComputeFrameFenceProof(frameKey, envelope);
        var rewritten = rawJson.Replace("\"message_id\":\"" + oldMessageId + "\"", "\"message_id\":\"" + messageId + "\"", StringComparison.Ordinal);
        rewritten = rewritten.Replace("\"fence_proof\":\"" + oldFenceProof + "\"", "\"fence_proof\":\"" + newFenceProof + "\"", StringComparison.Ordinal);
        return rewritten;
    }

    internal async Task<PipeEnvelope> SendAndReadAsync(FrameBuildResult frame)
    {
        await SendRawAsync(frame.RawJson).ConfigureAwait(false);
        return await ReadResponseAsync(response => response.MessageId == frame.Envelope.MessageId).ConfigureAwait(false);
    }

    internal async Task<PipeEnvelope> SendRawAndReadAsync(string rawJson)
    {
        await SendRawAsync(rawJson).ConfigureAwait(false);
        return await ReadResponseAsync().ConfigureAwait(false);
    }

    internal async Task<PipeEnvelope> ReadResponseAsync(Func<PipeEnvelope, bool>? predicate = null, int timeoutMilliseconds = 5000)
    {
        while (bufferedResponses.Count > 0)
        {
            var buffered = bufferedResponses.Dequeue();
            if (predicate == null || predicate(buffered)) return buffered;
        }

        while (true)
        {
            var rawJson = await ReadWithTimeoutAsync(pipe, timeoutMilliseconds).ConfigureAwait(false);
            if (!ProtocolCodec.TryParseEnvelope(rawJson, out var response, out var error)) throw new InvalidOperationException("response_decode_failed:" + error);
            ValidateResponse(response, rawJson);
            if (predicate == null || predicate(response)) return response;
            bufferedResponses.Enqueue(response);
        }
    }

    internal async Task<bool> DisconnectedAsync()
    {
        try
        {
            await ReadWithTimeoutAsync(pipe, 1500).ConfigureAwait(false);
            return false;
        }
        catch (EndOfStreamException)
        {
            return true;
        }
        catch (IOException)
        {
            return true;
        }
        catch (InvalidDataException)
        {
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    internal async Task SendOversizedFrameAsync()
    {
        var length = ProtocolConstants.MaxFrameBytes + 1;
        var header = new byte[]
        {
            (byte)(length & 0xff),
            (byte)((length >> 8) & 0xff),
            (byte)((length >> 16) & 0xff),
            (byte)((length >> 24) & 0xff)
        };
        await pipe.WriteAsync(header, 0, header.Length, CancellationToken.None).ConfigureAwait(false);
        await pipe.FlushAsync(CancellationToken.None).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        pipe.Dispose();
    }

    private void ValidateResponse(PipeEnvelope response, string rawJson)
    {
        Require(response.InstanceEpoch == descriptor.InstanceEpoch, "response_instance_epoch_invalid");
        Require(response.ConnectionEpoch == HandshakeResponse.ConnectionEpoch, "response_connection_epoch_invalid");
        Require(response.SessionId == request.SessionId, "response_session_invalid");
        Require(response.DirectionNonce == HandshakeResponse.ServiceDirectionNonce, "response_nonce_invalid");
        Require(response.PayloadLength == Encoding.UTF8.GetByteCount(response.PayloadJson), "response_length_invalid");
        var payloadHash = TransportSecurity.Sha256Hex(Encoding.UTF8.GetBytes(response.PayloadJson));
        Require(TransportSecurity.FixedTimeEquals(payloadHash, response.PayloadSha256) && TransportSecurity.FixedTimeEquals(payloadHash, response.Checksum), "response_checksum_invalid");
        Require(TransportSecurity.FixedTimeEquals(response.FenceProof, TransportSecurity.ComputeFrameFenceProof(frameKey, response)), "response_fence_invalid");
        var sequence = responseWindow.Evaluate(response.SessionId, response.ConnectionEpoch, response.DirectionNonce, response.Sequence, response.MessageId, response.PayloadSha256);
        Require(sequence.Kind == SequenceDecisionKind.Accepted, "response_sequence_invalid:" + sequence.Kind.ToString());
        if (StringComparer.Ordinal.Equals(response.AckStatus, ProtocolConstants.AckDurablyRecorded)) Require(!response.NonDurable, "durable_response_marked_non_durable");
        else Require(response.NonDurable, "response_durability_label_missing");
        Require(!rawJson.Contains("api_key", StringComparison.OrdinalIgnoreCase) && !rawJson.Contains("authorization", StringComparison.OrdinalIgnoreCase) && !rawJson.Contains("database_path", StringComparison.OrdinalIgnoreCase), "response_forbidden_field_echoed");
    }

    private static string CanonicalizePayload(string payloadJson)
    {
        var skeleton = new PipeEnvelope
        {
            MessageType = "health",
            MessageId = "canonicalize",
            CorrelationId = "canonicalize",
            SessionId = string.Empty,
            DeadlineUnixMilliseconds = 1,
            InstanceEpoch = 1,
            ConnectionEpoch = 1,
            DirectionNonce = "canonicalize",
            Sequence = 1,
            PayloadSchema = "canonicalize",
            PayloadJson = payloadJson,
            PayloadLength = 0,
            PayloadSha256 = string.Empty,
            Checksum = string.Empty
        };
        var serialized = ProtocolCodec.SerializeEnvelope(skeleton);
        if (!ProtocolCodec.TryParseEnvelope(serialized, out var normalized, out var error)) throw new InvalidDataException(error);
        return normalized.PayloadJson;
    }

    private static async Task<string> ReadWithTimeoutAsync(Stream stream, int timeoutMilliseconds)
    {
        using var timeout = new CancellationTokenSource(timeoutMilliseconds);
        return await PipeFrameIO.ReadFrameAsync(stream, timeout.Token).ConfigureAwait(false);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

internal sealed class FrameBuildResult
{
    internal FrameBuildResult(PipeEnvelope envelope, string rawJson)
    {
        Envelope = envelope;
        RawJson = rawJson;
    }

    internal PipeEnvelope Envelope { get; }
    internal string RawJson { get; }
}
