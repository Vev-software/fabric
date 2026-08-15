using System.Text.Json;
using Vev.Fabric.Contracts.Authorization;
using Vev.Fabric.Contracts.Entitlements;

namespace Vev.Fabric.Contracts.Tests;

public sealed class ConformanceFixtureTests
{
    private static readonly string SamplesDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "conformance", "samples"));

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Sample_SignedSnapshot_Deserializes()
    {
        var json = File.ReadAllText(Path.Combine(SamplesDirectory, "signed-entitlement-snapshot.sample.json"));
        var document = JsonSerializer.Deserialize<SignedEntitlementSnapshot>(json, SerializerOptions);

        Assert.NotNull(document);
        Assert.Equal("HS256", document!.Algorithm);
    }

    [Fact]
    public void Sample_BatchRequest_Deserializes()
    {
        var json = File.ReadAllText(Path.Combine(SamplesDirectory, "evaluate-entitlements-request.sample.json"));
        var document = JsonSerializer.Deserialize<EvaluateEntitlementsRequest>(json, SerializerOptions);

        Assert.NotNull(document);
        Assert.NotEmpty(document!.Requests);
    }

    [Fact]
    public void Sample_ImportRequest_Deserializes()
    {
        var json = File.ReadAllText(Path.Combine(SamplesDirectory, "import-signed-entitlement-snapshot-request.sample.json"));
        var document = JsonSerializer.Deserialize<ImportSignedEntitlementSnapshotRequest>(json, SerializerOptions);

        Assert.NotNull(document);
        Assert.NotNull(document!.Document);
    }

    [Fact]
    public void Sample_BundleRequest_Deserializes()
    {
        var json = File.ReadAllText(Path.Combine(SamplesDirectory, "entitlement-bundle-request.sample.json"));
        var document = JsonSerializer.Deserialize<EntitlementBundleRequest>(json, SerializerOptions);

        Assert.NotNull(document);
        Assert.Equal(EntitlementOffer.HostedTrial, document!.Offer);
        Assert.Equal(EntitlementLifecycleState.TrialExpired, document.LifecycleState);
    }

    [Fact]
    public void Sample_TaxonomyCatalog_Deserializes()
    {
        var json = File.ReadAllText(Path.Combine(SamplesDirectory, "taxonomy-catalog.sample.json"));
        var document = JsonSerializer.Deserialize<Taxonomy.TaxonomyCatalogDocument>(json, SerializerOptions);

        Assert.NotNull(document);
        Assert.NotEmpty(document!.Capabilities);
        Assert.NotEmpty(document.Reasons);
    }

    [Fact]
    public void Sample_AuthorizationRequest_Deserializes()
    {
        var json = File.ReadAllText(Path.Combine(SamplesDirectory, "authorization-request.sample.json"));
        var document = JsonSerializer.Deserialize<AuthorizationRequest>(json, SerializerOptions);

        Assert.Equal("atlas.catalogue.write", document.Action);
    }

    [Fact]
    public void Sample_AuthorizationDecision_Deserializes()
    {
        var json = File.ReadAllText(Path.Combine(SamplesDirectory, "authorization-decision.sample.json"));
        var document = JsonSerializer.Deserialize<AuthorizationDecision>(json, SerializerOptions);

        Assert.NotNull(document);
        Assert.True(document!.Allowed);
        Assert.Equal("local-authorizer", document.Source);
    }
}
