using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcusAIFramework.Api;
using MarcusAIFramework.Sdk.FakeHost;
using MarcusAIFramework.Sdk.TestKit;
using TaleWorlds.CampaignSystem;

namespace Awake.SdkSmoke;

internal static class Program
{
	private static async Task<int> Main()
	{
		try
		{
			AwakeLog.Enabled = false;
			return await RunAsync();
		}
		catch (Exception ex)
		{
			Console.WriteLine("FAIL_TYPE " + ex.GetType().FullName);
			Console.WriteLine("FAIL_MSG " + ex.Message);
			if (ex.InnerException != null)
			{
				Console.WriteLine("INNER_MSG " + ex.InnerException.Message);
			}
			return 1;
		}
	}

	private static async Task<int> RunAsync()
	{
		await RunEchoAndProbeAsync();
		await RunUiDispatcherMainThreadSmokeAsync();
		RunNpcTargetStableIdSmoke();
		RunUnnamedProfileSmoke();
		Console.WriteLine("PASS ALL Awake.SdkSmoke");
		return 0;
	}

	private static void RunUnnamedProfileSmoke()
	{
		if (!StringComparer.Ordinal.Equals(AwakeUnnamedProfileService.RoleLabel(Occupation.Villager), "村民")
			|| !StringComparer.Ordinal.Equals(AwakeUnnamedProfileService.RoleLabel(Occupation.Tavernkeeper), "酒馆老板")
			|| !StringComparer.Ordinal.Equals(AwakeUnnamedProfileService.RoleLabel(Occupation.Soldier), "士兵"))
		{
			throw new InvalidOperationException("unnamed profile role label mismatch.");
		}
		if (!AwakeUnnamedProfileService.BuildStateConstraint(null).Contains("内容包"))
		{
			throw new InvalidOperationException("unnamed profile null target should use the generic state block.");
		}
		Console.WriteLine("PASS unnamed profile role labels");
	}

	private static void RunNpcTargetStableIdSmoke()
	{
		string kind;
		string characterId;
		int agentIndex;

		if (!AwakeNpcTarget.TryParseStableId("hero:lord_swadian", out kind, out characterId, out agentIndex)
			|| !StringComparer.Ordinal.Equals(kind, "hero")
			|| !StringComparer.Ordinal.Equals(characterId, "lord_swadian")
			|| agentIndex != -1)
		{
			throw new InvalidOperationException("hero stable id parsing mismatch.");
		}

		if (!AwakeNpcTarget.TryParseStableId("npc:townsman_empire:a3", out kind, out characterId, out agentIndex)
			|| !StringComparer.Ordinal.Equals(kind, "npc")
			|| !StringComparer.Ordinal.Equals(characterId, "townsman_empire")
			|| agentIndex != 3)
		{
			throw new InvalidOperationException("agent stable id parsing mismatch.");
		}

		if (!AwakeNpcTarget.TryParseStableId("npc:townsman_empire:static", out kind, out characterId, out agentIndex)
			|| agentIndex != -1)
		{
			throw new InvalidOperationException("static npc stable id parsing mismatch.");
		}

		if (AwakeNpcTarget.TryParseStableId("invalid", out kind, out characterId, out agentIndex))
		{
			throw new InvalidOperationException("invalid stable id should be rejected.");
		}

		Console.WriteLine("PASS npc target stable id parsing");
	}

	private static async Task RunEchoAndProbeAsync()
	{
		FakeClock clock = new FakeClock(DateTimeOffset.UtcNow);
		RequestContext valid = clock.Context("awake.smoke", null, "smoke-correlation");
		OperationResult<string> obj = await AwakeExtension.HandleEchoAsync("{}", valid, CancellationToken.None);
		MafAssertions.Succeeded(obj, "echo success expected");
		if (obj.Value.IndexOf("\"echo\":true", StringComparison.Ordinal) < 0)
		{
			throw new InvalidOperationException("echo payload did not contain the expected marker.");
		}
		Console.WriteLine("PASS echo success path");

		MafAssertions.Failed(await AwakeExtension.HandleEchoAsync("[]", valid, CancellationToken.None), FrameworkErrorCategory.InvalidRequest, "awake.invalid_payload", "invalid payload expected");
		MafAssertions.Failed(await AwakeExtension.HandleEchoAsync("{not-json", valid, CancellationToken.None), FrameworkErrorCategory.InvalidRequest, "awake.invalid_payload", "malformed object payload expected");
		Console.WriteLine("PASS echo invalid payload path");

		RequestContext expired = new RequestContext(new ExtensionId("awake.smoke"), new SessionRef("smoke-campaign", "smoke-timeline", "smoke-session"), "smoke-expired", DateTimeOffset.UtcNow.AddSeconds(-1.0));
		MafAssertions.Failed(await AwakeExtension.HandleEchoAsync("{}", expired, CancellationToken.None), FrameworkErrorCategory.Expired, "awake.context_expired", "expired context expected");
		Console.WriteLine("PASS echo expired context path");

		RequestContext providerContext = clock.Context("awake.smoke", null, "smoke-provider");
		OperationResult<IReadOnlyList<ContextContribution>> contributed = await new ProbeContextProvider().ContributeAsync(
			new ContextPlanRequest(Array.Empty<string>(), Array.Empty<string>(), new[] { "PlayerKnown" }, Array.Empty<string>(), 512),
			providerContext,
			CancellationToken.None);
		MafAssertions.Succeeded(contributed, "context contribution expected");
		if (contributed.Value.Count != 1 || !StringComparer.Ordinal.Equals(contributed.Value[0].ProviderId, "awake.probe.context"))
		{
			throw new InvalidOperationException("unexpected context contribution shape.");
		}
		Console.WriteLine("PASS context provider contribution path");
	}

	private static async Task RunUiDispatcherMainThreadSmokeAsync()
	{
		AwakeUiDispatcher.ResetGameThreadForTesting();
		AwakeUiDispatcher.Drain();

		ManualResetEventSlim gameReady = new ManualResetEventSlim(false);
		ManualResetEventSlim drainSignal = new ManualResetEventSlim(false);
		Task gameThreadTask = Task.Factory.StartNew(
			() =>
			{
				try
				{
					AwakeUiDispatcher.InitializeGameThread();
					gameReady.Set();
					drainSignal.Wait();
					AwakeUiDispatcher.Drain();
				}
				catch (Exception ex)
				{
					AwakeLog.Write("ui_dispatcher_smoke_game_thread_error error=" + ex.Message);
				}
			},
			CancellationToken.None,
			TaskCreationOptions.LongRunning,
			TaskScheduler.Default);

		if (!gameReady.Wait(TimeSpan.FromSeconds(5)))
		{
			throw new InvalidOperationException("game thread smoke did not start.");
		}

		Task<int> enqueued = AwakeUiDispatcher.RunOnGameThreadAsync(
			() => Task.FromResult(42),
			CancellationToken.None);
		drainSignal.Set();
		await gameThreadTask;
		int value = await enqueued;
		if (value != 42)
		{
			throw new InvalidOperationException("ui dispatcher main thread result mismatch.");
		}
		Console.WriteLine("PASS ui dispatcher main thread smoke");
		AwakeUiDispatcher.ResetGameThreadForTesting();
	}
}
