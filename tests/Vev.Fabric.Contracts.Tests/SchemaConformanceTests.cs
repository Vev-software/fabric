using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Vev.Fabric.Contracts.Audit;
using Vev.Fabric.Contracts.Discovery;
using Vev.Fabric.Contracts.Authorization;
using Vev.Fabric.Contracts.Entitlements;
using Vev.Fabric.Contracts.Lifecycle;
using Vev.Fabric.Contracts.Taxonomy;

namespace Vev.Fabric.Contracts.Tests;

public sealed class SchemaConformanceTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static TheoryData<string, string, Type> SampleContracts =>
        new()
        {
            { "audit-event.sample.json", "audit-event.schema.json", typeof(AuditEvent) },
            { "authorization-decision.sample.json", "authorization-decision.schema.json", typeof(AuthorizationDecision) },
            { "authorization-request.sample.json", "authorization-request.schema.json", typeof(AuthorizationRequest) },
            { "discovery-enrollment-status.sample.json", "discovery-enrollment-status.schema.json", typeof(DiscoveryEnrollmentStatus) },
            { "discovery-enrollment-transition-request.sample.json", "discovery-enrollment-transition-request.schema.json", typeof(DiscoveryEnrollmentTransitionRequest) },
            { "discovery-enrollment-transition-result.sample.json", "discovery-enrollment-transition-result.schema.json", typeof(DiscoveryEnrollmentTransitionResult) },
            { "discovery-lifecycle-event.sample.json", "discovery-lifecycle-event.schema.json", typeof(DiscoveryLifecycleEvent) },
            { "entitlement-bundle-request.sample.json", "entitlement-bundle-request.schema.json", typeof(EntitlementBundleRequest) },
            { "evaluate-entitlements-request.sample.json", "evaluate-entitlements-request.schema.json", typeof(EvaluateEntitlementsRequest) },
            { "import-signed-entitlement-snapshot-request.sample.json", "import-signed-entitlement-snapshot-request.schema.json", typeof(ImportSignedEntitlementSnapshotRequest) },
            { "signed-entitlement-snapshot.sample.json", "signed-entitlement-snapshot.schema.json", typeof(SignedEntitlementSnapshot) },
            { "tenant-lifecycle-query.sample.json", "tenant-lifecycle-query.schema.json", typeof(TenantLifecycleQuery) },
            { "tenant-lifecycle-status.sample.json", "tenant-lifecycle-status.schema.json", typeof(TenantLifecycleStatus) },
            { "tenant-lifecycle-transition-request.sample.json", "tenant-lifecycle-transition-request.schema.json", typeof(TenantLifecycleTransitionRequest) },
            { "tenant-lifecycle-transition-result.sample.json", "tenant-lifecycle-transition-result.schema.json", typeof(TenantLifecycleTransitionResult) },
            { "taxonomy-catalog.sample.json", "taxonomy-catalog.schema.json", typeof(TaxonomyCatalogDocument) }
        };

    [Theory]
    [MemberData(nameof(SampleContracts))]
    public void Conformance_sample_matches_its_published_schema(string sampleFile, string schemaFile, Type contractType)
    {
        var instance = JsonNode.Parse(File.ReadAllText(Path.Combine(TestSchemas.SampleDir, sampleFile)));
        var results = Evaluate(schemaFile, instance);

        Assert.True(results.IsValid, JsonSchemaTestHelpers.Describe(results));

        var deserialized = JsonSerializer.Deserialize(instance!.ToJsonString(), contractType, SerializerOptions);
        Assert.NotNull(deserialized);

        var roundTripped = JsonNode.Parse(JsonSerializer.Serialize(deserialized, contractType, SerializerOptions));
        var roundTripResults = Evaluate(schemaFile, roundTripped);

        Assert.True(roundTripResults.IsValid, JsonSchemaTestHelpers.Describe(roundTripResults));
    }

    [Fact]
    public void Dotnet_sdk_serializes_bundle_resolution_to_a_schema_valid_document()
    {
        var resolver = new EntitlementBundleResolver();
        var resolution = resolver.Resolve(
            new EntitlementBundleRequest(
                "tenant-a",
                EntitlementOffer.Pro,
                EntitlementLifecycleState.Active,
                new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero)));

        var instance = JsonNode.Parse(JsonSerializer.Serialize(resolution, SerializerOptions));
        var results = Evaluate("entitlement-bundle-resolution.schema.json", instance);

        Assert.True(results.IsValid, JsonSchemaTestHelpers.Describe(results));
    }

    [Fact]
    public void Dotnet_sdk_serializes_import_response_to_a_schema_valid_document()
    {
        var response = new ImportSignedEntitlementSnapshotResponse(
            Accepted: true,
            ReasonCode: ReasonCodes.EntitlementGranted,
            Snapshot: new EntitlementSnapshot(
                "tenant-a",
                new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero),
                [
                    new EntitlementGrant(
                        "atlas.catalogue.read",
                        "snapshot")
                ]));

        var instance = JsonNode.Parse(JsonSerializer.Serialize(response, SerializerOptions));
        var results = Evaluate("import-signed-entitlement-snapshot-response.schema.json", instance);

        Assert.True(results.IsValid, JsonSchemaTestHelpers.Describe(results));
    }

    private static EvaluationResults Evaluate(string schemaFile, JsonNode? instance) =>
        JsonSchemaTestHelpers.Evaluate(TestSchemas.Load(schemaFile), instance);
}
