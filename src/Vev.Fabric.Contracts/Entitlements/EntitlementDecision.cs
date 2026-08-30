namespace Vev.Fabric.Contracts.Entitlements;

/// <summary>
/// The result of an entitlement evaluation, with machine-readable reason codes and limits.
/// </summary>
public sealed record EntitlementDecision(
    bool Allowed,
    CapabilityId Capability,
    string ReasonCode,
    string Source,
    DateTimeOffset EvaluatedAt,
    DateTimeOffset? ValidUntil = null,
    IReadOnlyDictionary<string, decimal>? Limits = null)
{
    public static EntitlementDecision Allow(
        CapabilityId capability,
        string source,
        DateTimeOffset evaluatedAt,
        DateTimeOffset? validUntil = null,
        IReadOnlyDictionary<string, decimal>? limits = null) =>
        new(true, capability, ReasonCodes.EntitlementGranted, source, evaluatedAt, validUntil, limits);

    public static EntitlementDecision Deny(
        CapabilityId capability,
        string reasonCode,
        string source,
        DateTimeOffset evaluatedAt,
        DateTimeOffset? validUntil = null) =>
        new(false, capability, reasonCode, source, evaluatedAt, validUntil);
}

/// <summary>
/// Stable reason codes for the entitlement surface.
/// </summary>
public static class ReasonCodes
{
    public const string Allow = "allow";
    public const string RoleMissing = "role_missing";
    public const string EntitlementGranted = "entitlement_granted";
    public const string EntitlementDenied = "entitlement_denied";
    public const string EntitlementUnavailable = "entitlement_unavailable";
    public const string EntitlementSnapshotInvalid = "entitlement_snapshot_invalid";
    public const string EntitlementSnapshotStale = "entitlement_snapshot_stale";
    public const string EntitlementSnapshotTenantMismatch = "entitlement_snapshot_tenant_mismatch";
    public const string LifecycleTrialExpired = "lifecycle_trial_expired";
    public const string LifecycleReadOnly = "lifecycle_read_only";
    public const string LifecycleLocked = "lifecycle_locked";
    public const string LifecycleRetention = "lifecycle_retention";
    public const string LifecyclePurged = "lifecycle_purged";
    public const string LifecycleTransitionInvalid = "lifecycle_transition_invalid";
    public const string DiscoveryEnrollmentPending = "discovery_enrollment_pending";
    public const string DiscoveryEnrollmentSuspended = "discovery_enrollment_suspended";
    public const string DiscoveryEnrollmentRevoked = "discovery_enrollment_revoked";
    public const string DiscoveryCredentialExpired = "discovery_credential_expired";
    public const string DiscoveryLifecycleTransitionInvalid = "discovery_lifecycle_transition_invalid";

    // Offline evaluator hardening (fabric#9, security#1 T3/T4/T7).

    /// <summary>A snapshot older than the highest already seen was presented (anti-rollback).</summary>
    public const string EntitlementSnapshotRolledBack = "entitlement_snapshot_rolled_back";

    /// <summary>The wall clock moved backwards past the last observed time (anti-clock-manipulation).</summary>
    public const string EntitlementClockRegression = "entitlement_clock_regression";

    /// <summary>A trial entitlement expired: trials hard-stop and are never frozen open by an outage.</summary>
    public const string TrialExpired = "trial_expired";
    public const string AiPolicyRequired = "ai_policy_required";
    public const string AiProviderUnavailable = "ai_provider_unavailable";
}
