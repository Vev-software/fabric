using System.Text.Json.Serialization;

namespace Vev.Fabric.Contracts.Discovery;

/// <summary>
/// Public Atlas discovery lifecycle event emitted when discovery-driven catalogue apply creates
/// or updates one Atlas asset. This is intentionally narrow: it gives Atlas one public event seam
/// for discovery effects without turning Fabric into a generic Atlas domain model host.
/// </summary>
public sealed record AtlasDiscoveryAssetLifecycleEvent(
    string EventId,
    DateTimeOffset OccurredAt,
    TenantContext Tenant,
    string EnrollmentId,
    string PrincipalId,
    string Source,
    string EventType,
    string AssetId,
    string SourceAgentId,
    string ObservedId,
    string CorrelationId,
    CapabilityId Capability,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string>? Metadata = null);

/// <summary>
/// Shared event-type vocabulary for Atlas discovery lifecycle effects. Atlas owns when these
/// events are emitted; Fabric owns the public names and the contract shape.
/// </summary>
public static class AtlasDiscoveryEventVocabulary
{
    public const string ServerCreatedType = "eu.vev.atlas.server.created.v1";
    public const string ServerUpdatedType = "eu.vev.atlas.server.updated.v1";
    public const string ApplicationCreatedType = "eu.vev.atlas.application.created.v1";
    public const string ApplicationUpdatedType = "eu.vev.atlas.application.updated.v1";

    public static IReadOnlyList<string> EventTypes { get; } =
    [
        ServerCreatedType,
        ServerUpdatedType,
        ApplicationCreatedType,
        ApplicationUpdatedType
    ];

    public static bool IsSupported(string eventType) =>
        EventTypes.Contains(eventType, StringComparer.Ordinal);
}
