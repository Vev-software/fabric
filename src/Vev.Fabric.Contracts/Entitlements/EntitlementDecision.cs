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
}
