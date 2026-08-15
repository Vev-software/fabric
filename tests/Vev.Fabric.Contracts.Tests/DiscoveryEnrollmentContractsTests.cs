using System.Text.Json;
using Vev.Fabric.Contracts.Discovery;
using Vev.Fabric.Contracts.Entitlements;

namespace Vev.Fabric.Contracts.Tests;

public sealed class DiscoveryEnrollmentContractsTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly DateTimeOffset EnrolledAt = new(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Evaluate_New_Enrollment_Is_Pending()
    {
        var status = DiscoveryEnrollmentStateMachine.Evaluate(
            "disc-1",
            new TenantContext("tenant-a"),
            CreatePrincipal(),
            new CapabilityId("atlas.discovery.ingestion"),
            new DiscoveryEnrollmentTimeline(EnrolledAt),
            EnrolledAt);

        Assert.Equal(DiscoveryEnrollmentState.Pending, status.State);
        Assert.Equal(ReasonCodes.DiscoveryEnrollmentPending, status.ReasonCode);
    }

    [Fact]
    public void Activate_Transitions_To_Active()
    {
        var result = DiscoveryEnrollmentStateMachine.Apply(
            new DiscoveryEnrollmentTransitionRequest(
                "disc-1",
                new TenantContext("tenant-a"),
                CreatePrincipal(),
                new CapabilityId("atlas.discovery.ingestion"),
                DiscoveryEnrollmentTransition.Activate,
                EnrolledAt,
                new DiscoveryEnrollmentTimeline(EnrolledAt),
                EnrolledAt.AddDays(30)));

        Assert.True(result.Accepted);
        Assert.Equal(DiscoveryEnrollmentState.Active, result.Enrollment.State);
        Assert.Equal(ReasonCodes.Allow, result.ReasonCode);
    }

    [Fact]
    public void RotateCredential_Updates_Expiry_And_Rotation_Timestamp()
    {
        var timeline = new DiscoveryEnrollmentTimeline(
            EnrolledAt,
            CredentialExpiresAt: EnrolledAt.AddDays(30),
            ActivatedAt: EnrolledAt);

        var result = DiscoveryEnrollmentStateMachine.Apply(
            new DiscoveryEnrollmentTransitionRequest(
                "disc-1",
                new TenantContext("tenant-a"),
                CreatePrincipal(),
                new CapabilityId("atlas.discovery.ingestion"),
                DiscoveryEnrollmentTransition.RotateCredential,
                EnrolledAt.AddDays(7),
                timeline,
                EnrolledAt.AddDays(37)));

        Assert.True(result.Accepted);
        Assert.Equal(EnrolledAt.AddDays(37), result.Enrollment.Timeline.CredentialExpiresAt);
        Assert.Equal(EnrolledAt.AddDays(7), result.Enrollment.Timeline.LastRotatedAt);
    }

    [Fact]
    public void Suspend_Transitions_To_Suspended()
    {
        var timeline = new DiscoveryEnrollmentTimeline(
            EnrolledAt,
            CredentialExpiresAt: EnrolledAt.AddDays(30),
            ActivatedAt: EnrolledAt);

        var result = DiscoveryEnrollmentStateMachine.Apply(
            new DiscoveryEnrollmentTransitionRequest(
                "disc-1",
                new TenantContext("tenant-a"),
                CreatePrincipal(),
                new CapabilityId("atlas.discovery.ingestion"),
                DiscoveryEnrollmentTransition.Suspend,
                EnrolledAt.AddDays(1),
                timeline));

        Assert.True(result.Accepted);
        Assert.Equal(DiscoveryEnrollmentState.Suspended, result.Enrollment.State);
        Assert.Equal(ReasonCodes.DiscoveryEnrollmentSuspended, result.ReasonCode);
    }

    [Fact]
    public void Expired_Credential_Evaluates_Fail_Static()
    {
        var status = DiscoveryEnrollmentStateMachine.Evaluate(
            "disc-1",
            new TenantContext("tenant-a"),
            CreatePrincipal(),
            new CapabilityId("atlas.discovery.ingestion"),
            new DiscoveryEnrollmentTimeline(
                EnrolledAt,
                CredentialExpiresAt: EnrolledAt.AddDays(1),
                ActivatedAt: EnrolledAt),
            EnrolledAt.AddDays(1));

        Assert.Equal(DiscoveryEnrollmentState.Expired, status.State);
        Assert.Equal(ReasonCodes.DiscoveryCredentialExpired, status.ReasonCode);
    }

    [Fact]
    public void Revoke_Transitions_To_Revoked_From_Suspended()
    {
        var timeline = new DiscoveryEnrollmentTimeline(
            EnrolledAt,
            CredentialExpiresAt: EnrolledAt.AddDays(30),
            ActivatedAt: EnrolledAt,
            SuspendedAt: EnrolledAt.AddDays(5));

        var result = DiscoveryEnrollmentStateMachine.Apply(
            new DiscoveryEnrollmentTransitionRequest(
                "disc-1",
                new TenantContext("tenant-a"),
                CreatePrincipal(),
                new CapabilityId("atlas.discovery.ingestion"),
                DiscoveryEnrollmentTransition.Revoke,
                EnrolledAt.AddDays(6),
                timeline));

        Assert.True(result.Accepted);
        Assert.Equal(DiscoveryEnrollmentState.Revoked, result.Enrollment.State);
        Assert.Equal(ReasonCodes.DiscoveryEnrollmentRevoked, result.ReasonCode);
    }

    [Fact]
    public void Invalid_Transition_Is_Rejected()
    {
        var result = DiscoveryEnrollmentStateMachine.Apply(
            new DiscoveryEnrollmentTransitionRequest(
                "disc-1",
                new TenantContext("tenant-a"),
                CreatePrincipal(),
                new CapabilityId("atlas.discovery.ingestion"),
                DiscoveryEnrollmentTransition.RotateCredential,
                EnrolledAt,
                new DiscoveryEnrollmentTimeline(EnrolledAt),
                EnrolledAt.AddDays(30)));

        Assert.False(result.Accepted);
        Assert.Equal(ReasonCodes.DiscoveryLifecycleTransitionInvalid, result.ReasonCode);
        Assert.Equal(DiscoveryEnrollmentState.Pending, result.Enrollment.State);
    }

    [Fact]
    public void DiscoveryLifecycleEvent_RoundTrips()
    {
        var lifecycleEvent = new DiscoveryLifecycleEvent(
            EventId: "disc-evt-1",
            OccurredAt: EnrolledAt,
            Tenant: new TenantContext("tenant-a"),
            EnrollmentId: "disc-1",
            PrincipalId: "scanner-1",
            Source: "fabric.control-plane",
            EventType: DiscoveryLifecycleEventType.EnrollmentActivated,
            ReasonCode: ReasonCodes.Allow,
            Capability: new CapabilityId("atlas.discovery.ingestion"),
            CorrelationId: "req-1",
            Metadata: new Dictionary<string, string> { ["credentialVersion"] = "2" });

        var json = JsonSerializer.Serialize(lifecycleEvent, SerializerOptions);
        var roundTripped = JsonSerializer.Deserialize<DiscoveryLifecycleEvent>(json, SerializerOptions);

        Assert.NotNull(roundTripped);
        Assert.Equal(DiscoveryLifecycleEventType.EnrollmentActivated, roundTripped!.EventType);
        Assert.Equal("2", roundTripped.Metadata!["credentialVersion"]);
    }

    [Fact]
    public void DiscoveryAuditVocabulary_Produces_Expected_Resources()
    {
        var enrollment = DiscoveryAuditVocabulary.EnrollmentResource("disc-1");
        var ingestion = DiscoveryAuditVocabulary.IngestionResource("tenant-a", "disc-1");

        Assert.Equal("fabric:discovery/enrollments/disc-1", enrollment.Value);
        Assert.Equal("discovery-enrollment", enrollment.Type);
        Assert.Equal("atlas:discovery/ingestion/tenant-a/disc-1", ingestion.Value);
        Assert.Equal("discovery-ingestion", ingestion.Type);
    }

    private static PrincipalContext CreatePrincipal() =>
        new("scanner-1", "Atlas Discovery Scanner", ["Machine"], new Dictionary<string, string> { ["kind"] = "discovery-scanner" });
}
