namespace Vev.Fabric.Contracts.Entitlements;

/// <summary>
/// Signed snapshot payload consumed by the local evaluator.
/// </summary>
public sealed record EntitlementSnapshot(
    string Tenant,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset GraceUntil,
    IReadOnlyList<EntitlementGrant> Entitlements);

/// <summary>
/// One capability grant or limit included in a snapshot.
/// </summary>
public sealed record EntitlementGrant(
    string Capability,
    string Source,
    IReadOnlyDictionary<string, decimal>? Limits = null,
    DateTimeOffset? ValidFrom = null,
    DateTimeOffset? ValidUntil = null);
