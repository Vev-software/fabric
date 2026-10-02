using Vev.Fabric.Contracts.Entitlements;
using Vev.Fabric.Contracts.Lifecycle;
using Vev.Fabric.Contracts.Taxonomy;

namespace Vev.Fabric.Contracts.Sharing;

/// <summary>
/// What a push claims about itself, taken from its payload or envelope. These are never trusted: the enrollment's own
/// <see cref="DataSharingBinding"/> decides the tenant and the consumer account, and a claim that differs is denied.
/// </summary>
public sealed record DataSharingClaim(string? TenantId = null, string? ConsumerAccountId = null);

/// <summary>
/// Public request to decide whether a push may be accepted right now. Narrow on purpose: it is for the
/// <c>atlas.landscape.share</c> capability only.
/// </summary>
public sealed record DataSharingPushAccessRequest(
    DataSharingBinding Binding,
    PrincipalContext Principal,
    string EnrollmentId,
    CapabilityId Capability,
    DataSharingEnrollmentTimeline EnrollmentTimeline,
    TenantLifecycleTimeline TenantLifecycleTimeline,
    EntitlementDecision Entitlement,
    DataSharingClaim? Claimed = null,
    DateTimeOffset? AsOf = null);

/// <summary>The canonical decision after composing tenant lifecycle, enrollment state, the binding and entitlement.</summary>
public sealed record DataSharingPushAccessDecision(
    bool Allowed,
    string EnrollmentId,
    CapabilityId Capability,
    string ReasonCode,
    string Source,
    DateTimeOffset EvaluatedAt,
    DataSharingEnrollmentState EnrollmentState,
    TenantLifecycleState TenantLifecycleState,
    DateTimeOffset? ValidUntil = null)
{
    public static DataSharingPushAccessDecision Allow(
        string enrollmentId, CapabilityId capability, string source, DateTimeOffset evaluatedAt,
        DataSharingEnrollmentState enrollmentState, TenantLifecycleState tenantLifecycleState, DateTimeOffset? validUntil = null) =>
        new(true, enrollmentId, capability, ReasonCodes.Allow, source, evaluatedAt, enrollmentState, tenantLifecycleState, validUntil);

    public static DataSharingPushAccessDecision Deny(
        string enrollmentId, CapabilityId capability, string reasonCode, string source, DateTimeOffset evaluatedAt,
        DataSharingEnrollmentState enrollmentState, TenantLifecycleState tenantLifecycleState, DateTimeOffset? validUntil = null) =>
        new(false, enrollmentId, capability, reasonCode, source, evaluatedAt, enrollmentState, tenantLifecycleState, validUntil);
}

/// <summary>Fabric-owned access-decision mechanism for pushes of a shared landscape digest.</summary>
public interface IDataSharingPushAccessEvaluator
{
    DataSharingPushAccessDecision Evaluate(DataSharingPushAccessRequest request);
}

/// <summary>
/// Reference evaluator. Deny precedence is canonical and fail-static: tenant lifecycle first, then enrollment state, then the
/// binding (a push that names another tenant or account), then entitlement. Revoking from either side denies the very next push.
/// </summary>
public sealed class LocalDataSharingPushAccessEvaluator(TimeProvider? timeProvider = null) : IDataSharingPushAccessEvaluator
{
    public const string DefaultSource = "sharing:local-access-evaluator";
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public DataSharingPushAccessDecision Evaluate(DataSharingPushAccessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Binding.SourceTenant.IsPresent) throw new ArgumentException("Data sharing requires a source tenant.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Binding.ConsumerAccountId)) throw new ArgumentException("Data sharing requires a consumer account.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.EnrollmentId)) throw new ArgumentException("Data sharing requires a non-empty enrollmentId.", nameof(request));
        if (!string.Equals(request.Capability.Value, AtlasTaxonomy.LandscapeShare.Value, StringComparison.Ordinal))
            throw new ArgumentException($"Data sharing only supports capability '{AtlasTaxonomy.LandscapeShare.Value}'.", nameof(request));
        if (!string.Equals(request.Entitlement.Capability.Value, request.Capability.Value, StringComparison.Ordinal))
            throw new ArgumentException("Entitlement decision capability must match the requested capability.", nameof(request));

        var asOf = request.AsOf ?? timeProvider.GetUtcNow();
        var lifecycle = TenantLifecycleStateMachine.Evaluate(request.Binding.SourceTenant.TenantId, request.TenantLifecycleTimeline, asOf);
        var enrollment = DataSharingEnrollmentStateMachine.Evaluate(
            request.EnrollmentId, request.Binding, request.Principal, request.Capability, request.EnrollmentTimeline, asOf);

        DataSharingPushAccessDecision Deny(string reason, DateTimeOffset? validUntil = null) =>
            DataSharingPushAccessDecision.Deny(request.EnrollmentId, request.Capability, reason, DefaultSource, asOf, enrollment.State, lifecycle.State, validUntil);

        if (lifecycle.State != TenantLifecycleState.TrialActive) return Deny(lifecycle.ReasonCode);
        if (enrollment.State != DataSharingEnrollmentState.Active) return Deny(enrollment.ReasonCode, enrollment.Timeline.CredentialExpiresAt);

        // The binding is the enrollment's, never the payload's: a claim that differs is a push for someone else.
        if (request.Claimed is { } claimed
            && ((claimed.TenantId is not null && !string.Equals(claimed.TenantId, request.Binding.SourceTenant.TenantId, StringComparison.Ordinal))
                || (claimed.ConsumerAccountId is not null && !string.Equals(claimed.ConsumerAccountId, request.Binding.ConsumerAccountId, StringComparison.Ordinal))))
        {
            return Deny(ReasonCodes.SharingBindingMismatch);
        }

        if (!request.Entitlement.Allowed) return Deny(request.Entitlement.ReasonCode, request.Entitlement.ValidUntil);

        return DataSharingPushAccessDecision.Allow(request.EnrollmentId, request.Capability, DefaultSource, asOf, enrollment.State, lifecycle.State,
            Min(request.Entitlement.ValidUntil, enrollment.Timeline.CredentialExpiresAt));
    }

    private static DateTimeOffset? Min(DateTimeOffset? left, DateTimeOffset? right) =>
        left switch
        {
            null => right,
            _ when right is null => left,
            _ => left <= right ? left : right
        };
}
