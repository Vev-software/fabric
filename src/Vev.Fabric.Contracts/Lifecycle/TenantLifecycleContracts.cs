using System.Text.Json.Serialization;
using Vev.Fabric.Contracts.Entitlements;

namespace Vev.Fabric.Contracts.Lifecycle;

[JsonConverter(typeof(JsonStringEnumConverter<TenantLifecycleState>))]
/// <summary>
/// Canonical hosted-tenant lifecycle states exposed by Fabric.
/// </summary>
public enum TenantLifecycleState
{
    TrialActive,
    TrialExpired,
    ReadOnly,
    Locked,
    RetentionPeriod,
    DataPurged
}

[JsonConverter(typeof(JsonStringEnumConverter<TenantLifecycleTransition>))]
/// <summary>
/// Explicit lifecycle transitions the control plane may apply after evaluating policy.
/// </summary>
public enum TenantLifecycleTransition
{
    EnterReadOnly,
    Lock,
    StartRetention,
    PurgeData
}

/// <summary>
/// Timeline carried by the public lifecycle contract. The trial timestamps are always present; later
/// timestamps are populated only once a tenant enters that lifecycle phase.
/// </summary>
public sealed record TenantLifecycleTimeline(
    DateTimeOffset TrialStartedAt,
    DateTimeOffset TrialExpiresAt,
    DateTimeOffset? ReadOnlyUntil = null,
    DateTimeOffset? LockedAt = null,
    DateTimeOffset? RetentionUntil = null,
    DateTimeOffset? PurgedAt = null);

/// <summary>
/// Query the lifecycle state for a tenant at a particular instant.
/// </summary>
public sealed record TenantLifecycleQuery(
    string Tenant,
    DateTimeOffset? AsOf = null);

/// <summary>
/// The current lifecycle state and timestamps a product runtime consumes.
/// </summary>
public sealed record TenantLifecycleStatus(
    string Tenant,
    TenantLifecycleState State,
    string ReasonCode,
    DateTimeOffset EvaluatedAt,
    TenantLifecycleTimeline Timeline);

/// <summary>
/// Apply one explicit lifecycle transition to the current timeline.
/// </summary>
public sealed record TenantLifecycleTransitionRequest(
    string Tenant,
    TenantLifecycleTransition Transition,
    DateTimeOffset OccurredAt,
    TenantLifecycleTimeline Timeline,
    DateTimeOffset? PhaseUntil = null);

/// <summary>
/// Result of attempting one lifecycle transition.
/// </summary>
public sealed record TenantLifecycleTransitionResult(
    bool Accepted,
    string ReasonCode,
    TenantLifecycleStatus Lifecycle);

/// <summary>
/// Shared evaluation and transition validation for the hosted-tenant lifecycle state machine.
/// </summary>
public static class TenantLifecycleStateMachine
{
    public static TenantLifecycleStatus Evaluate(TenantLifecycleQuery query, TenantLifecycleTimeline timeline) =>
        Evaluate(query.Tenant, timeline, query.AsOf ?? DateTimeOffset.UtcNow);

    public static TenantLifecycleStatus Evaluate(string tenant, TenantLifecycleTimeline timeline, DateTimeOffset asOf)
    {
        ValidateTimeline(timeline);

        return timeline switch
        {
            { PurgedAt: not null } when asOf >= timeline.PurgedAt.Value =>
                new TenantLifecycleStatus(tenant, TenantLifecycleState.DataPurged, ReasonCodes.LifecyclePurged, asOf, timeline),

            { RetentionUntil: not null } =>
                new TenantLifecycleStatus(tenant, TenantLifecycleState.RetentionPeriod, ReasonCodes.LifecycleRetention, asOf, timeline),

            { LockedAt: not null } when asOf >= timeline.LockedAt.Value =>
                new TenantLifecycleStatus(tenant, TenantLifecycleState.Locked, ReasonCodes.LifecycleLocked, asOf, timeline),

            { ReadOnlyUntil: not null } =>
                new TenantLifecycleStatus(tenant, TenantLifecycleState.ReadOnly, ReasonCodes.LifecycleReadOnly, asOf, timeline),

            _ when asOf >= timeline.TrialExpiresAt =>
                new TenantLifecycleStatus(tenant, TenantLifecycleState.TrialExpired, ReasonCodes.LifecycleTrialExpired, asOf, timeline),

            _ =>
                new TenantLifecycleStatus(tenant, TenantLifecycleState.TrialActive, ReasonCodes.Allow, asOf, timeline)
        };
    }

    public static TenantLifecycleTransitionResult Apply(TenantLifecycleTransitionRequest request)
    {
        var current = Evaluate(request.Tenant, request.Timeline, request.OccurredAt);
        var updatedTimeline = request.Timeline;

        switch (request.Transition)
        {
            case TenantLifecycleTransition.EnterReadOnly:
                if (current.State != TenantLifecycleState.TrialExpired ||
                    request.PhaseUntil is null ||
                    request.PhaseUntil <= request.OccurredAt)
                {
                    return Reject(request, current);
                }

                updatedTimeline = request.Timeline with { ReadOnlyUntil = request.PhaseUntil };
                break;

            case TenantLifecycleTransition.Lock:
                if (current.State != TenantLifecycleState.ReadOnly)
                {
                    return Reject(request, current);
                }

                updatedTimeline = request.Timeline with { LockedAt = request.OccurredAt };
                break;

            case TenantLifecycleTransition.StartRetention:
                if (current.State != TenantLifecycleState.Locked ||
                    request.PhaseUntil is null ||
                    request.PhaseUntil <= request.OccurredAt)
                {
                    return Reject(request, current);
                }

                updatedTimeline = request.Timeline with { RetentionUntil = request.PhaseUntil };
                break;

            case TenantLifecycleTransition.PurgeData:
                if (current.State != TenantLifecycleState.RetentionPeriod ||
                    (request.Timeline.RetentionUntil is not null && request.OccurredAt < request.Timeline.RetentionUntil.Value))
                {
                    return Reject(request, current);
                }

                updatedTimeline = request.Timeline with { PurgedAt = request.OccurredAt };
                break;

            default:
                return Reject(request, current);
        }

        var resultingLifecycle = Evaluate(request.Tenant, updatedTimeline, request.OccurredAt);
        return new TenantLifecycleTransitionResult(true, resultingLifecycle.ReasonCode, resultingLifecycle);
    }

    private static TenantLifecycleTransitionResult Reject(
        TenantLifecycleTransitionRequest request,
        TenantLifecycleStatus current) =>
        new(false, ReasonCodes.LifecycleTransitionInvalid, current);

    private static void ValidateTimeline(TenantLifecycleTimeline timeline)
    {
        if (timeline.TrialExpiresAt < timeline.TrialStartedAt)
        {
            throw new ArgumentException("trialExpiresAt must be on or after trialStartedAt.");
        }

        if (timeline.ReadOnlyUntil is not null && timeline.ReadOnlyUntil < timeline.TrialExpiresAt)
        {
            throw new ArgumentException("readOnlyUntil must be on or after trialExpiresAt.");
        }

        if (timeline.LockedAt is not null && timeline.ReadOnlyUntil is not null && timeline.LockedAt < timeline.TrialExpiresAt)
        {
            throw new ArgumentException("lockedAt must be on or after trialExpiresAt.");
        }

        if (timeline.RetentionUntil is not null && timeline.LockedAt is not null && timeline.RetentionUntil < timeline.LockedAt)
        {
            throw new ArgumentException("retentionUntil must be on or after lockedAt.");
        }

        if (timeline.PurgedAt is not null && timeline.RetentionUntil is not null && timeline.PurgedAt < timeline.RetentionUntil)
        {
            throw new ArgumentException("purgedAt must be on or after retentionUntil.");
        }
    }
}
