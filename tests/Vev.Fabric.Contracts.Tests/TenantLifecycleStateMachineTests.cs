using Vev.Fabric.Contracts.Entitlements;
using Vev.Fabric.Contracts.Lifecycle;

namespace Vev.Fabric.Contracts.Tests;

public sealed class TenantLifecycleStateMachineTests
{
    private static readonly DateTimeOffset TrialStartedAt = new(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset TrialExpiresAt = new(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Evaluate_Before_TrialExpiry_Is_TrialActive()
    {
        var lifecycle = TenantLifecycleStateMachine.Evaluate(
            "tenant-a",
            new TenantLifecycleTimeline(TrialStartedAt, TrialExpiresAt),
            TrialExpiresAt.AddMinutes(-1));

        Assert.Equal(TenantLifecycleState.TrialActive, lifecycle.State);
        Assert.Equal(ReasonCodes.Allow, lifecycle.ReasonCode);
    }

    [Fact]
    public void Evaluate_After_TrialExpiry_Is_TrialExpired()
    {
        var lifecycle = TenantLifecycleStateMachine.Evaluate(
            "tenant-a",
            new TenantLifecycleTimeline(TrialStartedAt, TrialExpiresAt),
            TrialExpiresAt);

        Assert.Equal(TenantLifecycleState.TrialExpired, lifecycle.State);
        Assert.Equal(ReasonCodes.LifecycleTrialExpired, lifecycle.ReasonCode);
    }

    [Fact]
    public void EnterReadOnly_Transitions_From_TrialExpired()
    {
        var result = TenantLifecycleStateMachine.Apply(
            new TenantLifecycleTransitionRequest(
                "tenant-a",
                TenantLifecycleTransition.EnterReadOnly,
                TrialExpiresAt,
                new TenantLifecycleTimeline(TrialStartedAt, TrialExpiresAt),
                TrialExpiresAt.AddDays(7)));

        Assert.True(result.Accepted);
        Assert.Equal(TenantLifecycleState.ReadOnly, result.Lifecycle.State);
        Assert.Equal(ReasonCodes.LifecycleReadOnly, result.ReasonCode);
    }

    [Fact]
    public void Lock_Transitions_From_ReadOnly()
    {
        var timeline = new TenantLifecycleTimeline(
            TrialStartedAt,
            TrialExpiresAt,
            ReadOnlyUntil: TrialExpiresAt.AddDays(7));

        var result = TenantLifecycleStateMachine.Apply(
            new TenantLifecycleTransitionRequest(
                "tenant-a",
                TenantLifecycleTransition.Lock,
                TrialExpiresAt.AddDays(7),
                timeline));

        Assert.True(result.Accepted);
        Assert.Equal(TenantLifecycleState.Locked, result.Lifecycle.State);
        Assert.Equal(ReasonCodes.LifecycleLocked, result.ReasonCode);
    }

    [Fact]
    public void StartRetention_Transitions_From_Locked()
    {
        var timeline = new TenantLifecycleTimeline(
            TrialStartedAt,
            TrialExpiresAt,
            ReadOnlyUntil: TrialExpiresAt.AddDays(7),
            LockedAt: TrialExpiresAt.AddDays(7));

        var result = TenantLifecycleStateMachine.Apply(
            new TenantLifecycleTransitionRequest(
                "tenant-a",
                TenantLifecycleTransition.StartRetention,
                TrialExpiresAt.AddDays(8),
                timeline,
                TrialExpiresAt.AddDays(38)));

        Assert.True(result.Accepted);
        Assert.Equal(TenantLifecycleState.RetentionPeriod, result.Lifecycle.State);
        Assert.Equal(ReasonCodes.LifecycleRetention, result.ReasonCode);
    }

    [Fact]
    public void PurgeData_Transitions_From_RetentionPeriod()
    {
        var retentionUntil = TrialExpiresAt.AddDays(38);
        var timeline = new TenantLifecycleTimeline(
            TrialStartedAt,
            TrialExpiresAt,
            ReadOnlyUntil: TrialExpiresAt.AddDays(7),
            LockedAt: TrialExpiresAt.AddDays(7),
            RetentionUntil: retentionUntil);

        var result = TenantLifecycleStateMachine.Apply(
            new TenantLifecycleTransitionRequest(
                "tenant-a",
                TenantLifecycleTransition.PurgeData,
                retentionUntil,
                timeline));

        Assert.True(result.Accepted);
        Assert.Equal(TenantLifecycleState.DataPurged, result.Lifecycle.State);
        Assert.Equal(ReasonCodes.LifecyclePurged, result.ReasonCode);
    }

    [Fact]
    public void Invalid_Transition_Is_Rejected()
    {
        var result = TenantLifecycleStateMachine.Apply(
            new TenantLifecycleTransitionRequest(
                "tenant-a",
                TenantLifecycleTransition.Lock,
                TrialExpiresAt,
                new TenantLifecycleTimeline(TrialStartedAt, TrialExpiresAt)));

        Assert.False(result.Accepted);
        Assert.Equal(ReasonCodes.LifecycleTransitionInvalid, result.ReasonCode);
        Assert.Equal(TenantLifecycleState.TrialExpired, result.Lifecycle.State);
    }

    [Theory]
    [InlineData(TenantLifecycleState.TrialActive, EntitlementLifecycleState.TrialActive)]
    [InlineData(TenantLifecycleState.TrialExpired, EntitlementLifecycleState.TrialExpired)]
    [InlineData(TenantLifecycleState.ReadOnly, EntitlementLifecycleState.ReadOnly)]
    [InlineData(TenantLifecycleState.Locked, EntitlementLifecycleState.Locked)]
    [InlineData(TenantLifecycleState.RetentionPeriod, EntitlementLifecycleState.RetentionPeriod)]
    [InlineData(TenantLifecycleState.DataPurged, EntitlementLifecycleState.DataPurged)]
    public void Canonical_lifecycle_maps_to_entitlement_policy_state(
        TenantLifecycleState lifecycleState,
        EntitlementLifecycleState expected)
    {
        var actual = EntitlementLifecycleStateMapper.From(lifecycleState);

        Assert.Equal(expected, actual);
    }
}
