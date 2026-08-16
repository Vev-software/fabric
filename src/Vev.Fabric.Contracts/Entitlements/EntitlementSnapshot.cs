using System.Text.Json.Serialization;

namespace Vev.Fabric.Contracts.Entitlements;

/// <summary>
/// Signed snapshot payload consumed by the local evaluator.
/// </summary>
/// <param name="Tenant">The tenant the snapshot is scoped to.</param>
/// <param name="IssuedAt">When the control plane issued the snapshot; the anti-rollback lower bound and trusted-time floor.</param>
/// <param name="ExpiresAt">When the snapshot's entitlements expire.</param>
/// <param name="GraceUntil">How long a purchased snapshot fails static (freezes open) after expiry.</param>
/// <param name="Entitlements">The capability grants and limits.</param>
/// <param name="Counter">
/// Optional monotonic issue counter per tenant+deployment. When present it is a stricter
/// anti-rollback nonce than <see cref="IssuedAt"/>: a snapshot whose counter is lower than the
/// highest already seen is refused even if its timestamp looks newer (fabric#9, security#1 T3).
/// </param>
public sealed record EntitlementSnapshot(
    string Tenant,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset GraceUntil,
    IReadOnlyList<EntitlementGrant> Entitlements,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    long? Counter = null);

/// <summary>
/// One capability grant or limit included in a snapshot.
/// </summary>
public sealed record EntitlementGrant(
    string Capability,
    string Source,
    IReadOnlyDictionary<string, decimal>? Limits = null,
    DateTimeOffset? ValidFrom = null,
    DateTimeOffset? ValidUntil = null);
