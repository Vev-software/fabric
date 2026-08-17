using System.Text.Json;
using Vev.Fabric.Contracts.Discovery;
using Vev.Fabric.Contracts.Taxonomy;

namespace Vev.Fabric.Contracts.Tests;

public sealed class AtlasDiscoveryLifecycleEventContractsTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void AtlasDiscoveryAssetLifecycleEvent_RoundTrips()
    {
        var lifecycleEvent = new AtlasDiscoveryAssetLifecycleEvent(
            EventId: "atlas-disc-evt-1",
            OccurredAt: new DateTimeOffset(2026, 8, 17, 10, 0, 0, TimeSpan.Zero),
            Tenant: new TenantContext("tenant-a"),
            EnrollmentId: "disc-1",
            PrincipalId: "scanner-1",
            Source: "atlas.enterprise.discovery",
            EventType: AtlasDiscoveryEventVocabulary.ServerCreatedType,
            AssetId: "srv-payments-01",
            SourceAgentId: "scanner-a",
            ObservedId: "host:payments-01",
            CorrelationId: "req-1",
            Capability: AtlasTaxonomy.DiscoveryIngestion,
            Metadata: new Dictionary<string, string>
            {
                ["assetKind"] = "server",
                ["change"] = "created"
            });

        var json = JsonSerializer.Serialize(lifecycleEvent, SerializerOptions);
        var roundTripped = JsonSerializer.Deserialize<AtlasDiscoveryAssetLifecycleEvent>(json, SerializerOptions);

        Assert.NotNull(roundTripped);
        Assert.Equal(AtlasDiscoveryEventVocabulary.ServerCreatedType, roundTripped!.EventType);
        Assert.Equal("scanner-a", roundTripped.SourceAgentId);
        Assert.Equal("created", roundTripped.Metadata!["change"]);
    }

    [Fact]
    public void AtlasDiscoveryEventVocabulary_Exposes_Supported_Event_Types()
    {
        Assert.True(AtlasDiscoveryEventVocabulary.IsSupported(AtlasDiscoveryEventVocabulary.ServerCreatedType));
        Assert.True(AtlasDiscoveryEventVocabulary.IsSupported(AtlasDiscoveryEventVocabulary.ServerUpdatedType));
        Assert.True(AtlasDiscoveryEventVocabulary.IsSupported(AtlasDiscoveryEventVocabulary.ApplicationCreatedType));
        Assert.True(AtlasDiscoveryEventVocabulary.IsSupported(AtlasDiscoveryEventVocabulary.ApplicationUpdatedType));
        Assert.False(AtlasDiscoveryEventVocabulary.IsSupported("eu.vev.atlas.database.created.v1"));
    }
}
