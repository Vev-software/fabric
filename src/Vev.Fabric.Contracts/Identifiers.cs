using System.Text.Json.Serialization;

namespace Vev.Fabric.Contracts;

/// <summary>
/// Stable tenant identifier and isolation boundary for every product request.
/// </summary>
/// <param name="TenantId">Stable tenant identifier.</param>
public readonly record struct TenantContext(string TenantId)
{
    [JsonIgnore]
    public bool IsPresent => !string.IsNullOrWhiteSpace(TenantId);

    public override string ToString() => TenantId;
}

/// <summary>
/// Provider-neutral representation of the authenticated principal. Identity is a Fabric
/// concern (products never invent their own): a product resolves "which principal" from
/// this contract alone. OIDC/OAuth is adopted, not built — <see cref="Claims"/> carries
/// selected provider claims in a provider-neutral shape.
/// </summary>
/// <param name="PrincipalId">Stable subject identifier (the OIDC <c>sub</c>, not an email).</param>
/// <param name="DisplayName">Human-readable label for audit and UX.</param>
/// <param name="Roles">Coarse role names held in the current tenant.</param>
/// <param name="Claims">
/// Optional provider-neutral claims (e.g. selected OIDC claims), string-valued and opaque
/// to the contract. Never a place for secrets or tokens. Defaults to none.
/// </param>
public sealed record PrincipalContext(
    string PrincipalId,
    string? DisplayName,
    IReadOnlyCollection<string> Roles,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string>? Claims = null);

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
