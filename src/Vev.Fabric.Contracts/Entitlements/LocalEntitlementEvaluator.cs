namespace Vev.Fabric.Contracts.Entitlements;

/// <summary>
/// Fast local evaluator that reads the last verified snapshot and never calls the control plane on
/// the request path. It fails static: when no fresh snapshot can be loaded there is no explicit
/// grant and no access.
///
/// Hardened for offline / air-gapped / self-hosted hosts, where the clock is under the customer's
/// (or an attacker's) control (fabric#9, security#1):
/// <list type="bullet">
/// <item><b>Anti-rollback (T3)</b> — a snapshot older than the highest already seen (by
///   <see cref="EntitlementSnapshot.Counter"/>, else <see cref="EntitlementSnapshot.IssuedAt"/>) is
///   refused; downgrade only via a newer snapshot. Equal is accepted (idempotent re-fetch).</item>
/// <item><b>Anti-clock-manipulation (T4)</b> — observed time only moves forward. A verified snapshot
///   raises the floor to its <c>IssuedAt</c>; a request whose clock reads earlier than the last
///   observed time is denied rather than trusted.</item>
/// <item><b>Trial hard-stop (T7)</b> — a <c>trial</c> grant is denied the moment it expires, with no
///   grace: an outage can never freeze a trial open. The "never stop authorised production"
///   fail-static guarantee applies to purchased sources only.</item>
/// </list>
/// The signer and keys live in the control plane; this reference evaluator owns the semantics only.
/// </summary>
public sealed class LocalEntitlementEvaluator(
    JsonSignedEntitlementSnapshotVerifier verifier,
    TimeProvider? timeProvider = null) : IEntitlementService
{
    private const string EvaluatorSource = "entitlement:local-evaluator";
    private const string TrialSource = "trial";

    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private VerifiedSnapshot? currentSnapshot;

    private DateTimeOffset? highestIssuedAt;
    private long? highestCounter;
    private DateTimeOffset? lastObservedAt;

    /// <summary>The highest snapshot <c>issuedAt</c> ever accepted — the anti-rollback watermark.</summary>
    public DateTimeOffset? HighestObservedIssuedAt => highestIssuedAt;

    /// <summary>The highest snapshot counter ever accepted, when counters are used.</summary>
    public long? HighestObservedCounter => highestCounter;

    /// <summary>The furthest-forward time this evaluator has observed (never moves backwards).</summary>
    public DateTimeOffset? LastObservedAt => lastObservedAt;

    /// <summary>
    /// Verify and, if it is not a rollback, accept a signed snapshot. A rollback (older than the
    /// watermark) is refused with <see cref="ReasonCodes.EntitlementSnapshotRolledBack"/> and the
    /// current snapshot is left unchanged. Accepting a snapshot advances the anti-rollback watermark
    /// and raises the observed-time floor to the snapshot's <c>issuedAt</c>.
    /// </summary>
    public SnapshotVerificationResult LoadSnapshot(SignedEntitlementSnapshot snapshot)
    {
        var result = verifier.Verify(snapshot);
        if (!result.IsValid || result.Snapshot is null)
        {
            return result;
        }

        var payload = result.Snapshot;

        if (IsRollback(payload))
        {
            return SnapshotVerificationResult.Invalid(ReasonCodes.EntitlementSnapshotRolledBack);
        }

        currentSnapshot = new VerifiedSnapshot(snapshot, payload);
        highestIssuedAt = Max(highestIssuedAt, payload.IssuedAt);
        if (payload.Counter is { } counter)
        {
            highestCounter = highestCounter is { } seen ? Math.Max(seen, counter) : counter;
        }

        // A signed snapshot proves time was at least its issuance: raise the trusted-time floor.
        lastObservedAt = Max(lastObservedAt, payload.IssuedAt);

        return result;
    }

    /// <inheritdoc />
    public EntitlementDecision Evaluate(EntitlementRequest request)
    {
        var now = timeProvider.GetUtcNow();

        // Anti-clock-manipulation (T4): observed time only moves forward. A clock earlier than the
        // last observed time is an anomaly — deny, never trust it (R1).
        if (lastObservedAt is { } observed && now < observed)
        {
            return EntitlementDecision.Deny(request.Capability, ReasonCodes.EntitlementClockRegression, EvaluatorSource, now);
        }

        lastObservedAt = Max(lastObservedAt, now);

        var snapshot = currentSnapshot?.Payload;
        if (snapshot is null)
        {
            return EntitlementDecision.Deny(request.Capability, ReasonCodes.EntitlementUnavailable, EvaluatorSource, now);
        }

        if (!string.Equals(snapshot.Tenant, request.Tenant.TenantId, StringComparison.Ordinal))
        {
            return EntitlementDecision.Deny(
                request.Capability, ReasonCodes.EntitlementSnapshotTenantMismatch, EvaluatorSource, now, snapshot.GraceUntil);
        }

        var grant = snapshot.Entitlements.FirstOrDefault(candidate =>
            string.Equals(candidate.Capability, request.Capability.Value, StringComparison.Ordinal));

        // Trial hard-stop (T7): a trial denies on expiry with no grace; a purchased source fails
        // static within its grace window.
        var isTrial = grant is { Source: TrialSource };
        var staleBoundary = isTrial ? snapshot.ExpiresAt : snapshot.GraceUntil;
        if (now > staleBoundary)
        {
            return EntitlementDecision.Deny(
                request.Capability,
                isTrial ? ReasonCodes.TrialExpired : ReasonCodes.EntitlementSnapshotStale,
                EvaluatorSource,
                now,
                staleBoundary);
        }

        if (grant is null)
        {
            return EntitlementDecision.Deny(
                request.Capability, ReasonCodes.EntitlementDenied, SnapshotSource(snapshot, now > snapshot.ExpiresAt), now, snapshot.GraceUntil);
        }

        if (grant.ValidFrom is { } validFrom && now < validFrom)
        {
            return EntitlementDecision.Deny(
                request.Capability, ReasonCodes.EntitlementDenied, SnapshotSource(snapshot, now > snapshot.ExpiresAt), now, grant.ValidUntil ?? snapshot.GraceUntil);
        }

        if (grant.ValidUntil is { } validUntil && now > validUntil)
        {
            return EntitlementDecision.Deny(
                request.Capability, ReasonCodes.EntitlementDenied, SnapshotSource(snapshot, now > snapshot.ExpiresAt), now, validUntil);
        }

        return EntitlementDecision.Allow(
            request.Capability,
            SnapshotSource(snapshot, now > snapshot.ExpiresAt),
            now,
            Min(snapshot.GraceUntil, grant.ValidUntil),
            grant.Limits);
    }

    private bool IsRollback(EntitlementSnapshot payload)
    {
        // A monotonic counter, when present on both sides, is the stricter check.
        if (payload.Counter is { } counter && highestCounter is { } seenCounter)
        {
            return counter < seenCounter;
        }

        return highestIssuedAt is { } seenIssuedAt && payload.IssuedAt < seenIssuedAt;
    }

    private static DateTimeOffset Max(DateTimeOffset? left, DateTimeOffset right) =>
        left is { } value && value >= right ? value : right;

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
