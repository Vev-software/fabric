namespace Vev.Fabric.Contracts;

/// <summary>
/// Stable tenant identifier and isolation boundary for every product request.
/// </summary>
/// <param name="TenantId">Stable tenant identifier.</param>
public readonly record struct TenantContext(string TenantId)
{
    public bool IsPresent => !string.IsNullOrWhiteSpace(TenantId);

    public override string ToString() => TenantId;
}

/// <summary>
/// Provider-neutral representation of the authenticated principal.
/// </summary>
/// <param name="PrincipalId">Stable subject identifier.</param>
/// <param name="DisplayName">Human-readable label for audit and UX.</param>
/// <param name="Roles">Coarse role names held in the current tenant.</param>
public sealed record PrincipalContext(
    string PrincipalId,
    string? DisplayName,
    IReadOnlyCollection<string> Roles);

/// <summary>
/// Stable capability identifier from the Fabric-owned taxonomy.
/// </summary>
/// <param name="Value">Namespaced capability id, e.g. <c>atlas.catalogue.read</c>.</param>
public readonly record struct CapabilityId(string Value)
{
    public override string ToString() => Value;
}

/// <summary>
/// Stable hosted-plan or operational limit key from the Fabric-owned taxonomy.
/// </summary>
/// <param name="Value">Namespaced or product-scoped limit key, e.g. <c>atlas.users</c>.</param>
public readonly record struct LimitKey(string Value)
{
    public override string ToString() => Value;
}

/// <summary>
/// Stable resource identifier used in entitlement and authorization context.
/// </summary>
/// <param name="Value">Resource identifier value.</param>
public readonly record struct ResourceId(string Value)
{
    public override string ToString() => Value;
}
