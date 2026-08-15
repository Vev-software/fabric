using System.Text.Json;
using Vev.Fabric.Contracts.Discovery;
using Vev.Fabric.Contracts.Entitlements;
using Vev.Fabric.Contracts.Lifecycle;
using Vev.Fabric.Contracts.Taxonomy;

namespace Vev.Fabric.Contracts.Tests;

public sealed class DiscoveryAccessContractsTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly DateTimeOffset TrialStartedAt = new(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset TrialExpiresAt = new(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset EvaluatedAt = new(2026, 8, 10, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Access_Is_Allowed_When_Lifecycle_Enrollment_And_Entitlement_Are_All_Valid()
    {
        var evaluator = new LocalDiscoveryIngestionAccessEvaluator();
        var request = CreateRequest(
            entitlement: EntitlementDecision.Allow(
                AtlasTaxonomy.DiscoveryIngestion,
                "entitlement:test",
                EvaluatedAt,
                EvaluatedAt.AddDays(30)),
            enrollmentTimeline: new DiscoveryEnrollmentTimeline(
                EvaluatedAt.AddDays(-1),
                CredentialExpiresAt: EvaluatedAt.AddDays(7),
                ActivatedAt: EvaluatedAt.AddDays(-1)),
            tenantLifecycleTimeline: new TenantLifecycleTimeline(TrialStartedAt, TrialExpiresAt),
            asOf: EvaluatedAt);

        var decision = evaluator.Evaluate(request);

        Assert.True(decision.Allowed);
        Assert.Equal(ReasonCodes.Allow, decision.ReasonCode);
        Assert.Equal(DiscoveryEnrollmentState.Active, decision.EnrollmentState);
        Assert.Equal(TenantLifecycleState.TrialActive, decision.TenantLifecycleState);
        Assert.Equal(EvaluatedAt.AddDays(7), decision.ValidUntil);
    }

    [Fact]
    public void Tenant_Lifecycle_Denial_Takes_Precedence_Over_Enrollment_And_Entitlement()
    {
        var evaluator = new LocalDiscoveryIngestionAccessEvaluator();
        var purgedAt = TrialExpiresAt.AddDays(31);
        var request = CreateRequest(
            entitlement: EntitlementDecision.Deny(
                AtlasTaxonomy.DiscoveryIngestion,
                ReasonCodes.EntitlementDenied,
                "entitlement:test",
                EvaluatedAt),
            enrollmentTimeline: new DiscoveryEnrollmentTimeline(
                EvaluatedAt.AddDays(-2),
                CredentialExpiresAt: EvaluatedAt.AddDays(-1),
                ActivatedAt: EvaluatedAt.AddDays(-2),
                RevokedAt: EvaluatedAt.AddDays(-1)),
            tenantLifecycleTimeline: new TenantLifecycleTimeline(
                TrialStartedAt,
                TrialExpiresAt,
                ReadOnlyUntil: TrialExpiresAt.AddDays(7),
                LockedAt: TrialExpiresAt.AddDays(7),
                RetentionUntil: TrialExpiresAt.AddDays(30),
                PurgedAt: purgedAt),
            asOf: purgedAt);

        var decision = evaluator.Evaluate(request);

        Assert.False(decision.Allowed);
        Assert.Equal(ReasonCodes.LifecyclePurged, decision.ReasonCode);
        Assert.Equal(TenantLifecycleState.DataPurged, decision.TenantLifecycleState);
        Assert.Equal(DiscoveryEnrollmentState.Revoked, decision.EnrollmentState);
    }

    [Fact]
    public void Enrollment_Denial_Takes_Precedence_Over_Entitlement_Denial()
    {
        var evaluator = new LocalDiscoveryIngestionAccessEvaluator();
        var request = CreateRequest(
            entitlement: EntitlementDecision.Deny(
                AtlasTaxonomy.DiscoveryIngestion,
                ReasonCodes.EntitlementDenied,
                "entitlement:test",
                EvaluatedAt),
            enrollmentTimeline: new DiscoveryEnrollmentTimeline(
                EvaluatedAt.AddDays(-2),
                CredentialExpiresAt: EvaluatedAt.AddDays(10),
                ActivatedAt: EvaluatedAt.AddDays(-2),
                SuspendedAt: EvaluatedAt.AddDays(-1)),
            tenantLifecycleTimeline: new TenantLifecycleTimeline(TrialStartedAt, TrialExpiresAt),
            asOf: EvaluatedAt);

        var decision = evaluator.Evaluate(request);

        Assert.False(decision.Allowed);
        Assert.Equal(ReasonCodes.DiscoveryEnrollmentSuspended, decision.ReasonCode);
        Assert.Equal(TenantLifecycleState.TrialActive, decision.TenantLifecycleState);
        Assert.Equal(DiscoveryEnrollmentState.Suspended, decision.EnrollmentState);
    }

    [Fact]
    public void Entitlement_Denial_Is_Returned_When_Lifecycle_And_Enrollment_Are_Valid()
    {
        var evaluator = new LocalDiscoveryIngestionAccessEvaluator();
        var request = CreateRequest(
            entitlement: EntitlementDecision.Deny(
                AtlasTaxonomy.DiscoveryIngestion,
                ReasonCodes.EntitlementSnapshotStale,
                "entitlement:test",
                EvaluatedAt,
                EvaluatedAt.AddHours(1)),
            enrollmentTimeline: new DiscoveryEnrollmentTimeline(
                EvaluatedAt.AddDays(-1),
                CredentialExpiresAt: EvaluatedAt.AddDays(7),
                ActivatedAt: EvaluatedAt.AddDays(-1)),
            tenantLifecycleTimeline: new TenantLifecycleTimeline(TrialStartedAt, TrialExpiresAt),
            asOf: EvaluatedAt);

        var decision = evaluator.Evaluate(request);

        Assert.False(decision.Allowed);
        Assert.Equal(ReasonCodes.EntitlementSnapshotStale, decision.ReasonCode);
        Assert.Equal(EvaluatedAt.AddHours(1), decision.ValidUntil);
    }

    [Fact]
    public void Round_Trip_Serializes_Request_And_Decision()
    {
        var request = CreateRequest(
            entitlement: EntitlementDecision.Allow(
                AtlasTaxonomy.DiscoveryIngestion,
                "entitlement:test",
                EvaluatedAt,
                EvaluatedAt.AddDays(30)),
            enrollmentTimeline: new DiscoveryEnrollmentTimeline(
                EvaluatedAt.AddDays(-1),
                CredentialExpiresAt: EvaluatedAt.AddDays(7),
                ActivatedAt: EvaluatedAt.AddDays(-1)),
            tenantLifecycleTimeline: new TenantLifecycleTimeline(TrialStartedAt, TrialExpiresAt),
            asOf: EvaluatedAt);

        var requestJson = JsonSerializer.Serialize(request, SerializerOptions);
        var roundTrippedRequest = JsonSerializer.Deserialize<DiscoveryIngestionAccessRequest>(requestJson, SerializerOptions);

        Assert.NotNull(roundTrippedRequest);
        Assert.Equal(request.EnrollmentId, roundTrippedRequest!.EnrollmentId);

        var decision = DiscoveryIngestionAccessDecision.Allow(
            request.EnrollmentId,
            request.Capability,
            "discovery:local-access-evaluator",
            EvaluatedAt,
            DiscoveryEnrollmentState.Active,
            TenantLifecycleState.TrialActive,
            EvaluatedAt.AddDays(7));

        var decisionJson = JsonSerializer.Serialize(decision, SerializerOptions);
        var roundTrippedDecision = JsonSerializer.Deserialize<DiscoveryIngestionAccessDecision>(decisionJson, SerializerOptions);

        Assert.NotNull(roundTrippedDecision);
        Assert.True(roundTrippedDecision!.Allowed);
        Assert.Equal(DiscoveryEnrollmentState.Active, roundTrippedDecision.EnrollmentState);
    }

    private static DiscoveryIngestionAccessRequest CreateRequest(
        EntitlementDecision entitlement,
        DiscoveryEnrollmentTimeline enrollmentTimeline,
        TenantLifecycleTimeline tenantLifecycleTimeline,
        DateTimeOffset asOf) =>
        new(
            new TenantContext("tenant-a"),
            new PrincipalContext("scanner-1", "Atlas Discovery Scanner", ["Machine"]),
            "disc-1",
            AtlasTaxonomy.DiscoveryIngestion,
            enrollmentTimeline,
            tenantLifecycleTimeline,
            entitlement,
            asOf);
}
