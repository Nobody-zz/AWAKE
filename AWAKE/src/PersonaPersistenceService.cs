using System;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace Awake;

internal static class PersonaSubjectEligibility
{
    internal static bool TryGetEligibleHeroSubject(
        AwakeNpcTarget target,
        out Hero hero,
        out string subjectStableId)
    {
        hero = null;
        subjectStableId = string.Empty;
        try
        {
            return TryGetEligibleHeroSubject(target, Hero.MainHero, out hero, out subjectStableId);
        }
        catch
        {
            return false;
        }
    }

    internal static bool TryGetEligibleHeroSubject(
        AwakeNpcTarget target,
        Hero mainHero,
        out Hero hero,
        out string subjectStableId)
    {
        hero = target?.Hero;
        subjectStableId = string.Empty;
        if (target == null || hero == null || mainHero == null || ReferenceEquals(hero, mainHero)) return false;
        if (string.IsNullOrWhiteSpace(hero.StringId)) return false;
        subjectStableId = "hero:" + hero.StringId;
        if (!StringComparer.Ordinal.Equals(target.StableId, subjectStableId))
        {
            hero = null;
            subjectStableId = string.Empty;
            return false;
        }
        return true;
    }
}

internal sealed class PersonaSessionHydrationAdapter
{
    private readonly WorldStateStore _store;

    internal PersonaSessionHydrationAdapter(WorldStateStore store)
    {
        _store = store;
    }

    internal async Task<ContextSnapshot> HydrateAsync(
        ContextSnapshot input,
        AwakeNpcTarget target,
        RuntimeBundle bundle,
        int sessionGeneration,
        DateTimeOffset deadlineUtc,
        CancellationToken cancellationToken)
    {
        ContextSnapshot snapshot = input?.DeepClone() ?? new ContextSnapshot();
        Hero hero;
        string subjectStableId;
        if (_store == null
            || !PersonaSubjectEligibility.TryGetEligibleHeroSubject(target, out hero, out subjectStableId)
            || !AwakeRuntime.IsCurrentSession(sessionGeneration, _store))
        {
            return snapshot;
        }
        if (cancellationToken.IsCancellationRequested || DateTimeOffset.UtcNow >= deadlineUtc) return snapshot;

        PersonaTimelineIdentity timeline = _store.BuildPersonaTimeline();
        string key;
        string keyError;
        if (!PersonaStorageKey.TryBuild(timeline, subjectStableId, out key, out keyError)) return snapshot;
        PersonaRuntimeStateDocument document = await _store.GetPersonaStateAsync(
            key,
            null,
            cancellationToken).ConfigureAwait(false);
        if (document == null) return snapshot;
        if (cancellationToken.IsCancellationRequested || DateTimeOffset.UtcNow >= deadlineUtc) return snapshot;
        string validationError;
        if (!PersonaPersistenceValidator.TryValidateRuntimeState(
            document,
            timeline,
            subjectStableId,
            bundle,
            out validationError))
        {
            AwakeLog.Write("persona.persistence.rejected subject=" + subjectStableId + " reason=" + validationError);
            return snapshot;
        }
        if (cancellationToken.IsCancellationRequested
            || DateTimeOffset.UtcNow >= deadlineUtc
            || !AwakeRuntime.IsCurrentSession(sessionGeneration, _store)) return snapshot;

        snapshot.PersonaSubjectStableId = subjectStableId;
        snapshot.PersonaStateRevision = document.Revision;
        snapshot.PersonaStateDigest = document.PayloadDigest;
        snapshot.PersonaContinuity = document.Continuity.DeepClone();
        if (cancellationToken.IsCancellationRequested
            || DateTimeOffset.UtcNow >= deadlineUtc
            || !AwakeRuntime.IsCurrentSession(sessionGeneration, _store)) return input?.DeepClone() ?? new ContextSnapshot();
        AwakeLog.Write("persona.persistence.loaded subject=" + subjectStableId
            + " revision=" + document.Revision
            + " sequence=" + document.Sequence);
        return snapshot;
    }
}

internal static class PersonaPersistenceProducerContract
{
    internal static bool TryBuildDocument(
        ContextSnapshot snapshot,
        PersonaTimelineIdentity timeline,
        RuntimeBundle bundle,
        long revision,
        long sequence,
        PersonaProjectionWatermarks watermarks,
        out PersonaRuntimeStateDocument document,
        out string error)
    {
        document = null;
        error = string.Empty;
        if (snapshot == null || timeline == null || bundle == null) return Fail("persona.runtime_state.input_missing", out error);
        if (string.IsNullOrWhiteSpace(snapshot.PersonaSubjectStableId)) return Fail("persona.runtime_state.subject_missing", out error);
        document = new PersonaRuntimeStateDocument
        {
            PersonaSubjectStableId = snapshot.PersonaSubjectStableId,
            CharacterId = snapshot.CharacterId ?? string.Empty,
            Timeline = CloneTimeline(timeline),
            ActiveBundleId = bundle.BundleId ?? string.Empty,
            ActiveBundleRevision = bundle.Revision,
            ActiveBundleDigest = bundle.Digest ?? string.Empty,
            Revision = revision,
            Sequence = sequence,
            Watermarks = CloneWatermarks(watermarks),
            Continuity = snapshot.PersonaContinuity?.DeepClone() ?? new PersonaContinuityState()
        };
        document.PayloadDigest = document.ComputePayloadDigest();
        if (!PersonaPersistenceValidator.TryValidateRuntimeState(document, timeline, snapshot.PersonaSubjectStableId, bundle, out error))
        {
            document = null;
            return false;
        }
        return true;
    }

    private static bool Fail(string value, out string error)
    {
        error = value;
        return false;
    }

    private static PersonaTimelineIdentity CloneTimeline(PersonaTimelineIdentity value)
    {
        return new PersonaTimelineIdentity
        {
            CampaignId = value.CampaignId,
            SaveId = value.SaveId,
            TimelineId = value.TimelineId,
            BranchId = value.BranchId,
            ParentBranchId = value.ParentBranchId,
            ForkSequence = value.ForkSequence
        };
    }

    private static PersonaProjectionWatermarks CloneWatermarks(PersonaProjectionWatermarks value)
    {
        value = value ?? new PersonaProjectionWatermarks();
        return new PersonaProjectionWatermarks
        {
            TranscriptAcceptedSequence = value.TranscriptAcceptedSequence,
            EffectsAcceptedSequence = value.EffectsAcceptedSequence,
            MemoryAcceptedSequence = value.MemoryAcceptedSequence,
            PersonaAcceptedSequence = value.PersonaAcceptedSequence
        };
    }
}
