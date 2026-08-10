namespace Vev.Fabric.Contracts.Entitlements;

/// <summary>
/// Fast local evaluator that reads the last verified snapshot and never calls the control plane
/// on the request path. When no fresh snapshot can be loaded it fails static: no explicit grant,
/// no access.
/// </summary>
public sealed class LocalEntitlementEvaluator(
    JsonSignedEntitlementSnapshotVerifier verifier,
    TimeProvider? timeProvider = null) : IEntitlementService
{
    private const string EvaluatorSource = "entitlement:local-evaluator";
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private VerifiedSnapshot? currentSnapshot;

    public SnapshotVerificationResult LoadSnapshot(SignedEntitlementSnapshot snapshot)
    {
        var result = verifier.Verify(snapshot);
        if (result.IsValid && result.Snapshot is not null)
        {
            currentSnapshot = new VerifiedSnapshot(snapshot, result.Snapshot);
        }

        return result;
    }

    public EntitlementDecision Evaluate(EntitlementRequest request)
    {
        var now = timeProvider.GetUtcNow();
        var snapshot = currentSnapshot?.Payload;

        if (snapshot is null)
        {
            return EntitlementDecision.Deny(
                request.Capability,
                ReasonCodes.EntitlementUnavailable,
                EvaluatorSource,
                now);
        }

        if (!string.Equals(snapshot.Tenant, request.Tenant.TenantId, StringComparison.Ordinal))
        {
            return EntitlementDecision.Deny(
                request.Capability,
                ReasonCodes.EntitlementSnapshotTenantMismatch,
                EvaluatorSource,
                now,
                snapshot.GraceUntil);
        }

        if (now > snapshot.GraceUntil)
        {
            return EntitlementDecision.Deny(
                request.Capability,
                ReasonCodes.EntitlementSnapshotStale,
                EvaluatorSource,
                now,
                snapshot.GraceUntil);
        }

        var grant = snapshot.Entitlements.FirstOrDefault(candidate =>
            string.Equals(candidate.Capability, request.Capability.Value, StringComparison.Ordinal));

        if (grant is null)
        {
            return EntitlementDecision.Deny(
                request.Capability,
                ReasonCodes.EntitlementDenied,
                SnapshotSource(snapshot, withinGrace: now > snapshot.ExpiresAt),
                now,
                snapshot.GraceUntil);
        }

        if (grant.ValidFrom is { } validFrom && now < validFrom)
        {
            return EntitlementDecision.Deny(
                request.Capability,
                ReasonCodes.EntitlementDenied,
                SnapshotSource(snapshot, withinGrace: now > snapshot.ExpiresAt),
                now,
                grant.ValidUntil ?? snapshot.GraceUntil);
        }

        if (grant.ValidUntil is { } validUntil && now > validUntil)
        {
            return EntitlementDecision.Deny(
                request.Capability,
                ReasonCodes.EntitlementDenied,
                SnapshotSource(snapshot, withinGrace: now > snapshot.ExpiresAt),
                now,
                validUntil);
        }

        return EntitlementDecision.Allow(
            request.Capability,
            SnapshotSource(snapshot, withinGrace: now > snapshot.ExpiresAt),
            now,
            Min(snapshot.GraceUntil, grant.ValidUntil),
            grant.Limits);
    }

    private static DateTimeOffset? Min(DateTimeOffset left, DateTimeOffset? right) =>
        right is null || left <= right ? left : right;

    private static string SnapshotSource(EntitlementSnapshot snapshot, bool withinGrace) =>
        withinGrace
            ? $"entitlement:snapshot:{snapshot.IssuedAt:O}:grace"
            : $"entitlement:snapshot:{snapshot.IssuedAt:O}";

    private sealed record VerifiedSnapshot(
        SignedEntitlementSnapshot Document,
        EntitlementSnapshot Payload);
}
