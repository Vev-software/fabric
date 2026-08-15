using System.Text.Json.Serialization;

namespace Vev.Fabric.Contracts.Audit;

/// <summary>
/// Category of an audit event. The category decides immutability: <see cref="Admin"/> and
/// <see cref="Security"/> events are immutable and must survive data-subject erasure, while
/// <see cref="Data"/> events describe ordinary asset changes.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AuditCategory>))]
public enum AuditCategory
{
    /// <summary>An ordinary create/edit/delete on a product asset.</summary>
    Data,

    /// <summary>An administrative action (configuration, membership, lifecycle). Immutable.</summary>
    Admin,

    /// <summary>A security-relevant action (auth, access, key or policy change). Immutable.</summary>
    Security
}

/// <summary>
/// Outcome of the audited action, so a security reviewer can tell an attempt from an effect.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AuditOutcome>))]
public enum AuditOutcome
{
    /// <summary>The action completed.</summary>
    Success,

    /// <summary>The action was attempted but failed.</summary>
    Failure,

    /// <summary>The action was refused by authorization, entitlement or lifecycle policy.</summary>
    Denied
}

/// <summary>
/// Redaction-safe projection of the acting principal. The audit envelope deliberately does not
/// carry <see cref="PrincipalContext.Claims"/>: claims can hold email or other provider data, and
/// audit payloads must not carry secrets or customer content by default. Use
/// <see cref="FromPrincipal"/> to derive an actor from a principal without leaking claims.
/// </summary>
/// <param name="PrincipalId">Stable subject identifier (the OIDC <c>sub</c>), not an email.</param>
/// <param name="DisplayName">Human-readable label for the audit trail.</param>
/// <param name="Roles">Coarse role names held in the tenant at the time of the action.</param>
public sealed record AuditActor(
    string PrincipalId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? DisplayName = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyCollection<string>? Roles = null)
{
    /// <summary>
    /// Derives a redaction-safe actor from an authenticated principal, dropping opaque claims.
    /// </summary>
    public static AuditActor FromPrincipal(PrincipalContext principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return new AuditActor(principal.PrincipalId, principal.DisplayName, principal.Roles);
    }
}

/// <summary>
/// The resource an audited action targeted. Fabric owns the field; the product supplies the
/// <see cref="Value"/> and optional <see cref="Type"/> — never a product-domain schema.
/// </summary>
/// <param name="Value">Product-supplied resource identifier, e.g. <c>atlas:catalogue/main</c>.</param>
/// <param name="Type">Optional product-supplied resource type label, e.g. <c>catalogue-entry</c>.</param>
public sealed record AuditResource(
    string Value,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Type = null);

/// <summary>
/// The append-only audit event envelope shared across VEV products (fabric#6, 05 §2 Audit).
/// The envelope carries who did what to which resource, in which tenant, when, and under which
/// correlation id — never the product-domain schema behind those values, and never secrets or
/// customer content.
/// </summary>
/// <param name="EventId">Stable unique identifier for this record; the append-only primary key.</param>
/// <param name="OccurredAt">When the audited action occurred.</param>
/// <param name="Tenant">The tenant whose isolation boundary the action happened within.</param>
/// <param name="Actor">Redaction-safe projection of the acting principal.</param>
/// <param name="Source">Emitting component, e.g. <c>atlas</c> or <c>fabric.control-plane</c>, so product and substrate events stitch together.</param>
/// <param name="Action">Product-supplied action value, e.g. <c>atlas.catalogue.write</c>.</param>
/// <param name="Resource">The resource the action targeted.</param>
/// <param name="Category">Data, admin or security; decides immutability.</param>
/// <param name="Outcome">Whether the action succeeded, failed or was denied.</param>
/// <param name="CorrelationId">Correlates every event emitted while handling one request.</param>
/// <param name="CausationId">Optional id of the event that caused this one, for causal stitching.</param>
/// <param name="Metadata">
/// Optional product-supplied context, string-valued and opaque to the contract. Must not carry
/// secrets or customer content — see <see cref="AuditRedaction"/>.
/// </param>
public sealed record AuditEvent(
    string EventId,
    DateTimeOffset OccurredAt,
    TenantContext Tenant,
    AuditActor Actor,
    string Source,
    string Action,
    AuditResource Resource,
    AuditCategory Category,
    AuditOutcome Outcome,
    string CorrelationId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? CausationId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    /// <summary>
    /// Admin and security events are immutable: once recorded they must never be edited, redacted
    /// or tombstoned, even under later data-subject erasure requests.
    /// </summary>
    [JsonIgnore]
    public bool IsImmutable => Category is AuditCategory.Admin or AuditCategory.Security;
}

/// <summary>
/// Redaction rules baked into the audit contract: audit payloads carry no secrets and no customer
/// content by default (AGENTS.md §1.6, 03 · E4/E5). The check is a coarse structural guard on
/// metadata keys — it does not inspect values — so products still keep sensitive values out.
/// </summary>
public static class AuditRedaction
{
    /// <summary>
    /// Normalised tokens that must not appear in an audit metadata key. Matching is
    /// case-insensitive and ignores <c>-</c> and <c>_</c> separators.
    /// </summary>
    public static IReadOnlyCollection<string> ForbiddenMetadataKeyTokens { get; } =
    [
        "password",
        "passphrase",
        "secret",
        "token",
        "apikey",
        "accesskey",
        "credential",
        "authorization",
        "cookie",
        "privatekey",
        "sessionid"
    ];

    /// <summary>
    /// Returns <c>true</c> when the metadata is safe to persist in an audit record. When it is not,
    /// <paramref name="offendingKey"/> names the first key that looks like it carries a secret.
    /// </summary>
    public static bool IsRedactionSafe(IReadOnlyDictionary<string, string>? metadata, out string? offendingKey)
    {
        offendingKey = null;
        if (metadata is null)
        {
            return true;
        }

        foreach (var key in metadata.Keys)
        {
            var normalized = Normalize(key);
            if (ForbiddenMetadataKeyTokens.Any(token => normalized.Contains(token, StringComparison.Ordinal)))
            {
                offendingKey = key;
                return false;
            }
        }

        return true;
    }

    private static string Normalize(string key) =>
        key.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
}

/// <summary>
/// Raised when an audit event is rejected because its metadata looks like it carries a secret or
/// customer content.
/// </summary>
public sealed class AuditRedactionException(string offendingKey)
    : Exception($"Audit metadata key '{offendingKey}' is forbidden: audit payloads must not carry secrets or customer content.")
{
    /// <summary>The metadata key that triggered the rejection.</summary>
    public string OffendingKey { get; } = offendingKey;
}

/// <summary>
/// Fabric-owned append-only audit mechanism. A product emits asset-change events through this sink;
/// Fabric owns the envelope and the append-only, redaction-checked guarantee.
/// </summary>
public interface IAuditSink
{
    /// <summary>
    /// Appends one audit event. Implementations validate required fields and redaction safety and
    /// never expose an update or delete operation.
    /// </summary>
    void Append(AuditEvent auditEvent);
}

/// <summary>
/// Simple local reference sink for development and tests. It is append-only by construction — it
/// exposes only <see cref="Append"/> and a read-only view — and rejects events that are malformed
/// or whose metadata fails the redaction check.
/// </summary>
public sealed class InMemoryAuditLog : IAuditSink
{
    private readonly List<AuditEvent> _events = [];

    /// <summary>The recorded events, in append order.</summary>
    public IReadOnlyList<AuditEvent> Events => _events;

    /// <inheritdoc />
    public void Append(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        if (string.IsNullOrWhiteSpace(auditEvent.EventId))
        {
            throw new ArgumentException("Audit event requires a non-empty eventId.", nameof(auditEvent));
        }

        if (!auditEvent.Tenant.IsPresent)
        {
            throw new ArgumentException("Audit event requires a tenant.", nameof(auditEvent));
        }

        if (string.IsNullOrWhiteSpace(auditEvent.Source))
        {
            throw new ArgumentException("Audit event requires a source.", nameof(auditEvent));
        }

        if (string.IsNullOrWhiteSpace(auditEvent.Action))
        {
            throw new ArgumentException("Audit event requires an action.", nameof(auditEvent));
        }

        if (string.IsNullOrWhiteSpace(auditEvent.CorrelationId))
        {
            throw new ArgumentException("Audit event requires a correlationId.", nameof(auditEvent));
        }

        if (!AuditRedaction.IsRedactionSafe(auditEvent.Metadata, out var offendingKey))
        {
            throw new AuditRedactionException(offendingKey!);
        }

        _events.Add(auditEvent);
    }
}
