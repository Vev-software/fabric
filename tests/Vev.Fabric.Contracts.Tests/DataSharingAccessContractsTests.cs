using Vev.Fabric.Contracts.Entitlements;
using Vev.Fabric.Contracts.Lifecycle;
using Vev.Fabric.Contracts.Sharing;

namespace Vev.Fabric.Contracts.Tests;

public sealed class DataSharingAccessContractsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly CapabilityId Capability = new("atlas.landscape.share");
    private static readonly DataSharingBinding Binding = new(new TenantContext("tenant-a"), "account-42");

    private static PrincipalContext Principal() => new("atlas-pusher-1", "Atlas landscape sharing", ["Machine"]);

    private static DataSharingEnrollmentTimeline Active() => new(Now.AddHours(-1), CredentialExpiresAt: Now.AddDays(30), ActivatedAt: Now.AddMinutes(-50));

    private static TenantLifecycleTimeline Trial() => new(Now.AddDays(-1), Now.AddDays(10), null, null, null, null);

    private static EntitlementDecision Entitled(DateTimeOffset? validUntil = null) =>
        EntitlementDecision.Allow(Capability, "entitlement:test", Now, validUntil ?? Now.AddDays(60));

    private static EntitlementDecision NotEntitled() =>
        EntitlementDecision.Deny(Capability, ReasonCodes.EntitlementDenied, "entitlement:test", Now);

    private static DataSharingPushAccessRequest Request(DataSharingEnrollmentTimeline? enrollment = null, TenantLifecycleTimeline? lifecycle = null,
        EntitlementDecision? entitlement = null, DataSharingClaim? claimed = null) =>
        new(Binding, Principal(), "share-1", Capability, enrollment ?? Active(), lifecycle ?? Trial(), entitlement ?? Entitled(), claimed, Now);

    private static DataSharingPushAccessDecision Evaluate(DataSharingPushAccessRequest request) =>
        new LocalDataSharingPushAccessEvaluator().Evaluate(request);

    [Fact]
    public void Relations_require_both_grants_and_the_existing_enrollment_checks()
    {
        var evaluator = new LocalDataSharingPushAccessEvaluator();
        var relations = EntitlementDecision.Allow(Vev.Fabric.Contracts.Taxonomy.AtlasTaxonomy.LandscapeShareRelations, "test", Now, Now.AddDays(2));
        var relationsDenied = EntitlementDecision.Deny(relations.Capability, ReasonCodes.EntitlementDenied, "test", Now);

        var relationsOnly = evaluator.EvaluateRelations(Request(entitlement: NotEntitled()), relations);
        Assert.False(relationsOnly.Allowed);
        Assert.Equal(ReasonCodes.EntitlementDenied, relationsOnly.ReasonCode);
        Assert.False(evaluator.EvaluateRelations(Request(), relationsDenied).Allowed);
        Assert.False(evaluator.EvaluateRelations(Request(enrollment: new DataSharingEnrollmentTimeline(Now)), relations).Allowed);
        Assert.False(evaluator.EvaluateRelations(Request(claimed: new DataSharingClaim("tenant-b")), relations).Allowed);
        var allowed = evaluator.EvaluateRelations(Request(), relations);
        Assert.True(allowed.Allowed);
        Assert.Equal(relations.Capability, allowed.Capability);
        Assert.Equal(Now.AddDays(2), allowed.ValidUntil);
        Assert.Throws<ArgumentException>(() => evaluator.EvaluateRelations(Request(), Entitled()));
    }

    [Fact]
    public void An_active_enrollment_of_an_entitled_tenant_is_allowed_until_the_earlier_of_credential_and_entitlement()
    {
        var decision = Evaluate(Request());
        var shorterEntitlement = Evaluate(Request(entitlement: Entitled(Now.AddDays(5))));

        Assert.Equal((true, ReasonCodes.Allow, DataSharingEnrollmentState.Active, Now.AddDays(30)), (decision.Allowed, decision.ReasonCode, decision.EnrollmentState, decision.ValidUntil));
        Assert.Equal(Now.AddDays(5), shorterEntitlement.ValidUntil);
    }

    [Fact]
    public void Deny_precedence_is_tenant_lifecycle_then_enrollment_then_binding_then_entitlement()
    {
        var expiredTrial = new TenantLifecycleTimeline(Now.AddDays(-40), Now.AddDays(-10), null, null, null, null);
        var revoked = Active() with { RevokedAt = Now.AddMinutes(-1), RevokedBy = DataSharingParty.Consumer };
        var wrongClaim = new DataSharingClaim(TenantId: "tenant-b");

        // Everything is wrong at once: the tenant lifecycle speaks first.
        var everything = Evaluate(Request(revoked, expiredTrial, NotEntitled(), wrongClaim));
        // Lifecycle fine: the enrollment speaks before the binding and the entitlement.
        var enrollmentFirst = Evaluate(Request(revoked, null, NotEntitled(), wrongClaim));
        // Enrollment fine: the binding speaks before the entitlement.
        var bindingBeforeEntitlement = Evaluate(Request(claimed: wrongClaim, entitlement: NotEntitled()));
        var entitlementLast = Evaluate(Request(entitlement: NotEntitled()));

        Assert.Equal(ReasonCodes.LifecycleTrialExpired, everything.ReasonCode);
        Assert.Equal(ReasonCodes.SharingEnrollmentRevoked, enrollmentFirst.ReasonCode);
        Assert.Equal(ReasonCodes.SharingBindingMismatch, bindingBeforeEntitlement.ReasonCode);
        Assert.Equal(ReasonCodes.EntitlementDenied, entitlementLast.ReasonCode);
        Assert.All(new[] { everything, enrollmentFirst, bindingBeforeEntitlement, entitlementLast }, d => Assert.False(d.Allowed));
    }

    [Fact]
    public void A_push_that_claims_another_tenant_or_account_is_denied_whatever_else_is_true()
    {
        Assert.Equal(ReasonCodes.SharingBindingMismatch, Evaluate(Request(claimed: new DataSharingClaim(TenantId: "tenant-b"))).ReasonCode);
        Assert.Equal(ReasonCodes.SharingBindingMismatch, Evaluate(Request(claimed: new DataSharingClaim(ConsumerAccountId: "account-99"))).ReasonCode);
        // A claim that agrees, or no claim at all, changes nothing: the binding is the enrollment's.
        Assert.True(Evaluate(Request(claimed: new DataSharingClaim("tenant-a", "account-42"))).Allowed);
        Assert.True(Evaluate(Request(claimed: new DataSharingClaim())).Allowed);
        Assert.True(Evaluate(Request(claimed: null)).Allowed);
    }

    [Theory]
    [InlineData(DataSharingParty.Source)]
    [InlineData(DataSharingParty.Consumer)]
    public void Revoking_from_either_side_denies_the_very_next_push(DataSharingParty side)
    {
        Assert.True(Evaluate(Request()).Allowed);
        var revoke = DataSharingEnrollmentStateMachine.Apply(new DataSharingEnrollmentTransitionRequest("share-1", Binding, Principal(), Capability,
            DataSharingEnrollmentTransition.Revoke, Now, Active(), RequestedBy: side));

        var next = Evaluate(Request(enrollment: revoke.Enrollment.Timeline));

        Assert.True(revoke.Accepted);
        Assert.Equal((false, ReasonCodes.SharingEnrollmentRevoked, DataSharingEnrollmentState.Revoked), (next.Allowed, next.ReasonCode, next.EnrollmentState));
    }

    [Theory]
    [InlineData(0, ReasonCodes.SharingEnrollmentPending)]
    [InlineData(1, ReasonCodes.SharingEnrollmentSuspended)]
    [InlineData(2, ReasonCodes.SharingCredentialExpired)]
    public void Pending_suspended_and_expired_enrollments_deny(int kind, string reason)
    {
        var timeline = kind switch
        {
            0 => new DataSharingEnrollmentTimeline(Now.AddHours(-1)),
            1 => Active() with { SuspendedAt = Now.AddMinutes(-5) },
            _ => Active() with { CredentialExpiresAt = Now.AddMinutes(-5) },
        };

        var decision = Evaluate(Request(timeline));

        Assert.Equal((false, reason), (decision.Allowed, decision.ReasonCode));
    }

    [Fact]
    public void Only_the_share_capability_and_a_complete_request_are_accepted()
    {
        var other = new CapabilityId("atlas.discovery.ingestion");
        var wrongCapability = Request() with { Capability = other };
        var mismatchedEntitlement = Request() with { Entitlement = EntitlementDecision.Allow(other, "x", Now) };

        Assert.Throws<ArgumentException>(() => Evaluate(wrongCapability));
        Assert.Throws<ArgumentException>(() => Evaluate(mismatchedEntitlement));
        Assert.Throws<ArgumentException>(() => Evaluate(Request() with { EnrollmentId = " " }));
        Assert.Throws<ArgumentException>(() => Evaluate(Request() with { Binding = new DataSharingBinding(new TenantContext(""), "account-42") }));
        Assert.Throws<ArgumentException>(() => Evaluate(Request() with { Binding = new DataSharingBinding(new TenantContext("tenant-a"), "") }));
    }
}
