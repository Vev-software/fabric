using System.Text.Json;
using Vev.Fabric.Contracts.Audit;
using Vev.Fabric.Contracts.Authorization;
using Vev.Fabric.Contracts.Entitlements;
using Vev.Fabric.Contracts.Lifecycle;

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
    public void Sample_AuditEvent_Deserializes()
    {
        var json = File.ReadAllText(Path.Combine(SamplesDirectory, "audit-event.sample.json"));
        var document = JsonSerializer.Deserialize<AuditEvent>(json, SerializerOptions);

        Assert.NotNull(document);
        Assert.Equal("atlas.catalogue.write", document!.Action);
        Assert.Equal(AuditCategory.Data, document.Category);
        Assert.Equal(AuditOutcome.Success, document.Outcome);
        Assert.Equal("tenant-a", document.Tenant.TenantId);
        Assert.Equal("principal-1", document.Actor.PrincipalId);
        Assert.DoesNotContain("claims", json);
    }

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

    [Fact]
    public void Sample_TenantLifecycleQuery_Deserializes()
    {
        var json = File.ReadAllText(Path.Combine(SamplesDirectory, "tenant-lifecycle-query.sample.json"));
        var document = JsonSerializer.Deserialize<TenantLifecycleQuery>(json, SerializerOptions);

        Assert.NotNull(document);
        Assert.Equal("tenant-a", document!.Tenant);
    }

    [Fact]
    public void Sample_TenantLifecycleStatus_Deserializes()
    {
        var json = File.ReadAllText(Path.Combine(SamplesDirectory, "tenant-lifecycle-status.sample.json"));
        var document = JsonSerializer.Deserialize<TenantLifecycleStatus>(json, SerializerOptions);

        Assert.NotNull(document);
        Assert.Equal(TenantLifecycleState.ReadOnly, document!.State);
    }

    [Fact]
    public void Sample_TenantLifecycleTransitionRequest_Deserializes()
    {
        var json = File.ReadAllText(Path.Combine(SamplesDirectory, "tenant-lifecycle-transition-request.sample.json"));
        var document = JsonSerializer.Deserialize<TenantLifecycleTransitionRequest>(json, SerializerOptions);

        Assert.NotNull(document);
        Assert.Equal(TenantLifecycleTransition.EnterReadOnly, document!.Transition);
    }

    [Fact]
    public void Sample_TenantLifecycleTransitionResult_Deserializes()
    {
        var json = File.ReadAllText(Path.Combine(SamplesDirectory, "tenant-lifecycle-transition-result.sample.json"));
        var document = JsonSerializer.Deserialize<TenantLifecycleTransitionResult>(json, SerializerOptions);

        Assert.NotNull(document);
        Assert.True(document!.Accepted);
    }

    [Fact]
    public void Sample_TenantContext_Deserializes()
    {
        var json = File.ReadAllText(Path.Combine(SamplesDirectory, "tenant-context.sample.json"));
        var context = JsonSerializer.Deserialize<TenantContext>(json, SerializerOptions);

        Assert.True(context.IsPresent);
        Assert.Equal("tenant-a", context.TenantId);
    }

    [Fact]
    public void Sample_PrincipalContext_Deserializes()
    {
        var json = File.ReadAllText(Path.Combine(SamplesDirectory, "principal-context.sample.json"));
        var principal = JsonSerializer.Deserialize<PrincipalContext>(json, SerializerOptions);

        Assert.NotNull(principal);
        Assert.Equal("principal-1", principal!.PrincipalId);
        Assert.Contains("AtlasArchitect", principal.Roles);
        Assert.NotNull(principal.Claims);
        Assert.Equal("auser", principal.Claims!["preferred_username"]);
    }
}
