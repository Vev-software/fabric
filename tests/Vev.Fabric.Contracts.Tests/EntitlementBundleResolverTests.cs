using Vev.Fabric.Contracts.Entitlements;
using Vev.Fabric.Contracts.Taxonomy;

namespace Vev.Fabric.Contracts.Tests;

public sealed class EntitlementBundleResolverTests
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ExpiresAt = IssuedAt.AddDays(3);
    private static readonly DateTimeOffset GraceUntil = IssuedAt.AddDays(7);

    [Fact]
    public void Resolve_CommunitySelfHosted_GrantsCatalogueSurface()
    {
        var resolver = new EntitlementBundleResolver();

        var result = resolver.Resolve(new EntitlementBundleRequest(
            "tenant-a",
            EntitlementOffer.CommunitySelfHosted,
            EntitlementLifecycleState.Active,
            IssuedAt,
            ExpiresAt,
            GraceUntil));

        Assert.Contains(result.Snapshot.Entitlements, grant => grant.Capability == "atlas.catalogue.read");
        Assert.Contains(result.Snapshot.Entitlements, grant => grant.Capability == "atlas.catalogue.write");
        Assert.DoesNotContain(result.Snapshot.Entitlements, grant => grant.Capability == "atlas.analysis.eol");
        // Data-layer introspection and quality are paid Starter+ capabilities; the free community offer never grants them.
        Assert.DoesNotContain(result.Snapshot.Entitlements, grant => grant.Capability == "atlas.data.introspection");
        Assert.DoesNotContain(result.Snapshot.Entitlements, grant => grant.Capability == "atlas.data.quality");
    }

    [Fact]
    public void Resolve_HostedStarter_GrantsDataIntrospection()
    {
        var resolver = new EntitlementBundleResolver();

        var result = resolver.Resolve(new EntitlementBundleRequest(
            "tenant-a",
            EntitlementOffer.HostedStarter,
            EntitlementLifecycleState.Active,
            IssuedAt,
            ExpiresAt,
            GraceUntil));

        // Schema introspection unlocks from the Starter tier up (a paid data-layer capability).
        Assert.Contains(result.Snapshot.Entitlements, grant => grant.Capability == "atlas.data.introspection");
    }

    [Fact]
    public void Resolve_HostedStarter_GrantsDataQuality()
    {
        var resolver = new EntitlementBundleResolver();

        var result = resolver.Resolve(new EntitlementBundleRequest(
            "tenant-a",
            EntitlementOffer.HostedStarter,
            EntitlementLifecycleState.Active,
            IssuedAt,
            ExpiresAt,
            GraceUntil));

        // Data-quality profiling unlocks from the Starter tier up, alongside schema introspection.
        Assert.Contains(result.Snapshot.Entitlements, grant => grant.Capability == "atlas.data.quality");
    }

    [Theory]
    [InlineData(EntitlementOffer.HostedTrial)]
    [InlineData(EntitlementOffer.HostedStarter)]
    [InlineData(EntitlementOffer.Pro)]
    [InlineData(EntitlementOffer.Enterprise)]
    [InlineData(EntitlementOffer.SelfHostedEnterprise)]
    public void Resolve_Starter_and_higher_grants_ArchiMate_export(EntitlementOffer offer)
    {
        var resolution = new EntitlementBundleResolver().Resolve(new EntitlementBundleRequest(
            "tenant-a", offer, EntitlementLifecycleState.Active, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30), DateTimeOffset.UtcNow.AddDays(37)));

        Assert.Contains(resolution.Snapshot.Entitlements, grant => grant.Capability == AtlasTaxonomy.ExportArchiMate.Value);
    }

    [Fact]
    public void Resolve_HostedTrial_GrantsPaidCapabilities_WhileTrialActive()
    {
        var resolver = new EntitlementBundleResolver();

        var result = resolver.Resolve(new EntitlementBundleRequest(
            "tenant-a",
            EntitlementOffer.HostedTrial,
            EntitlementLifecycleState.TrialActive,
            IssuedAt,
            ExpiresAt,
            GraceUntil));

        Assert.Contains(result.Snapshot.Entitlements, grant => grant.Capability == "atlas.analysis.integration-map");
        Assert.Contains(result.Snapshot.Entitlements, grant => grant.Capability == "atlas.discovery.ingestion");
        Assert.Equal(ReasonCodes.EntitlementGranted, result.ResolutionReasonCode);
    }

    [Fact]
    public void Resolve_TrialExpired_ReducesToReadOnlySurface()
    {
        var resolver = new EntitlementBundleResolver();

        var result = resolver.Resolve(new EntitlementBundleRequest(
            "tenant-a",
            EntitlementOffer.HostedTrial,
            EntitlementLifecycleState.TrialExpired,
            IssuedAt,
            ExpiresAt,
            GraceUntil));

        Assert.Equal(ReasonCodes.LifecycleTrialExpired, result.ResolutionReasonCode);
        Assert.Contains(result.Snapshot.Entitlements, grant => grant.Capability == "atlas.catalogue.read");
        Assert.Contains(result.Snapshot.Entitlements, grant => grant.Capability == "atlas.export.portable-bundle");
        Assert.DoesNotContain(result.Snapshot.Entitlements, grant => grant.Capability == "atlas.catalogue.write");
        Assert.DoesNotContain(result.Snapshot.Entitlements, grant => grant.Capability == "atlas.analysis.integration-map");
    }

    [Fact]
    public void Resolve_Locked_ReducesToExportOnly()
    {
        var resolver = new EntitlementBundleResolver();

        var result = resolver.Resolve(new EntitlementBundleRequest(
            "tenant-a",
            EntitlementOffer.Pro,
            EntitlementLifecycleState.Locked,
            IssuedAt,
            ExpiresAt,
            GraceUntil));

        Assert.Equal(ReasonCodes.LifecycleLocked, result.ResolutionReasonCode);
        Assert.Single(result.Snapshot.Entitlements);
        Assert.Equal("atlas.export.portable-bundle", result.Snapshot.Entitlements[0].Capability);
    }

    [Fact]
    public void Resolve_DataPurged_GrantsNothing()
    {
        var resolver = new EntitlementBundleResolver();

        var result = resolver.Resolve(new EntitlementBundleRequest(
            "tenant-a",
            EntitlementOffer.Enterprise,
            EntitlementLifecycleState.DataPurged,
            IssuedAt,
            ExpiresAt,
            GraceUntil));

        Assert.Equal(ReasonCodes.LifecyclePurged, result.ResolutionReasonCode);
        Assert.Empty(result.Snapshot.Entitlements);
    }
}
