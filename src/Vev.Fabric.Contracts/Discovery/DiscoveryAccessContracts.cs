using Vev.Fabric.Contracts.Entitlements;
using Vev.Fabric.Contracts.Lifecycle;
using Vev.Fabric.Contracts.Taxonomy;

namespace Vev.Fabric.Contracts.Discovery;

/// <summary>
/// Public request to decide whether a tenant-scoped machine principal may perform discovery
/// ingestion right now. This contract is intentionally narrow: it is for the
/// <c>atlas.discovery.ingestion</c> capability only.
/// </summary>
public sealed record DiscoveryIngestionAccessRequest(
    TenantContext Tenant,
    PrincipalContext Principal,
    string EnrollmentId,
    CapabilityId Capability,
    DiscoveryEnrollmentTimeline EnrollmentTimeline,
    TenantLifecycleTimeline TenantLifecycleTimeline,
    EntitlementDecision Entitlement,
    DateTimeOffset? AsOf = null);

/// <summary>
/// Canonical Fabric decision for discovery ingestion access after composing tenant lifecycle,
/// discovery enrollment state and entitlement.
/// </summary>
public sealed record DiscoveryIngestionAccessDecision(
    bool Allowed,
    string EnrollmentId,
    CapabilityId Capability,
    string ReasonCode,
    string Source,
    DateTimeOffset EvaluatedAt,
    DiscoveryEnrollmentState EnrollmentState,
    TenantLifecycleState TenantLifecycleState,
    DateTimeOffset? ValidUntil = null)
{
    public static DiscoveryIngestionAccessDecision Allow(
        string enrollmentId,
        CapabilityId capability,
        string source,
        DateTimeOffset evaluatedAt,
        DiscoveryEnrollmentState enrollmentState,
        TenantLifecycleState tenantLifecycleState,
        DateTimeOffset? validUntil = null) =>
        new(true, enrollmentId, capability, ReasonCodes.Allow, source, evaluatedAt, enrollmentState, tenantLifecycleState, validUntil);

    public static DiscoveryIngestionAccessDecision Deny(
        string enrollmentId,
        CapabilityId capability,
        string reasonCode,
        string source,
        DateTimeOffset evaluatedAt,
        DiscoveryEnrollmentState enrollmentState,
        TenantLifecycleState tenantLifecycleState,
        DateTimeOffset? validUntil = null) =>
        new(false, enrollmentId, capability, reasonCode, source, evaluatedAt, enrollmentState, tenantLifecycleState, validUntil);
}

/// <summary>
/// Fabric-owned access-decision mechanism for discovery ingestion.
/// </summary>
public interface IDiscoveryIngestionAccessEvaluator
{
    DiscoveryIngestionAccessDecision Evaluate(DiscoveryIngestionAccessRequest request);
}

/// <summary>
/// Reference evaluator for development and tests. Deny precedence is canonical and fail-static:
/// tenant lifecycle denies first, then discovery enrollment state, then entitlement.
/// </summary>
public sealed class LocalDiscoveryIngestionAccessEvaluator(TimeProvider? timeProvider = null) : IDiscoveryIngestionAccessEvaluator
{
    public const string DefaultSource = "discovery:local-access-evaluator";
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public DiscoveryIngestionAccessDecision Evaluate(DiscoveryIngestionAccessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Tenant.IsPresent)
        {
            throw new ArgumentException("Discovery access requires a tenant.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.EnrollmentId))
        {
            throw new ArgumentException("Discovery access requires a non-empty enrollmentId.", nameof(request));
        }

        if (!string.Equals(request.Capability.Value, AtlasTaxonomy.DiscoveryIngestion.Value, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Discovery access only supports capability '{AtlasTaxonomy.DiscoveryIngestion.Value}'.", nameof(request));
        }

        if (!string.Equals(request.Entitlement.Capability.Value, request.Capability.Value, StringComparison.Ordinal))
        {
            throw new ArgumentException("Entitlement decision capability must match the requested capability.", nameof(request));
        }

        var asOf = request.AsOf ?? timeProvider.GetUtcNow();
        var lifecycle = TenantLifecycleStateMachine.Evaluate(request.Tenant.TenantId, request.TenantLifecycleTimeline, asOf);
        var enrollment = DiscoveryEnrollmentStateMachine.Evaluate(
            request.EnrollmentId,
            request.Tenant,
            request.Principal,
            request.Capability,
            request.EnrollmentTimeline,
            asOf);

        if (lifecycle.State != TenantLifecycleState.TrialActive)
        {
            return DiscoveryIngestionAccessDecision.Deny(
                request.EnrollmentId,
                request.Capability,
                lifecycle.ReasonCode,
                DefaultSource,
                asOf,
                enrollment.State,
                lifecycle.State);
        }

        if (enrollment.State != DiscoveryEnrollmentState.Active)
        {
            return DiscoveryIngestionAccessDecision.Deny(
                request.EnrollmentId,
                request.Capability,
                enrollment.ReasonCode,
                DefaultSource,
                asOf,
                enrollment.State,
                lifecycle.State,
                enrollment.Timeline.CredentialExpiresAt);
        }

        if (!request.Entitlement.Allowed)
        {
            return DiscoveryIngestionAccessDecision.Deny(
                request.EnrollmentId,
                request.Capability,
                request.Entitlement.ReasonCode,
                DefaultSource,
                asOf,
                enrollment.State,
                lifecycle.State,
                request.Entitlement.ValidUntil);
        }

        return DiscoveryIngestionAccessDecision.Allow(
            request.EnrollmentId,
            request.Capability,
            DefaultSource,
            asOf,
            enrollment.State,
            lifecycle.State,
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
