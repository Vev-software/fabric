using System.Text.Json.Serialization;
using Vev.Fabric.Contracts.Taxonomy;

namespace Vev.Fabric.Contracts.Entitlements;

[JsonConverter(typeof(JsonStringEnumConverter<EntitlementOffer>))]
/// <summary>
/// Commercial/source profile resolved into concrete capabilities and limits.
/// </summary>
public enum EntitlementOffer
{
    CommunitySelfHosted,
    HostedTrial,
    HostedStarter,
    Pro,
    Enterprise,
    SelfHostedEnterprise
}

[JsonConverter(typeof(JsonStringEnumConverter<EntitlementLifecycleState>))]
/// <summary>
/// Lifecycle state used as a policy input during bundle translation.
/// The full canonical lifecycle contract is tracked separately in fabric#8.
/// </summary>
public enum EntitlementLifecycleState
{
    Active,
    TrialActive,
    TrialExpired,
    ReadOnly,
    Locked,
    RetentionPeriod,
    DataPurged
}

/// <summary>
/// Declarative request to translate a commercial offer and lifecycle state into grants and limits.
/// </summary>
public sealed record EntitlementBundleRequest(
    string Tenant,
    EntitlementOffer Offer,
    EntitlementLifecycleState LifecycleState,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset GraceUntil);

/// <summary>
/// Result of translating an entitlement bundle into runtime grants.
/// </summary>
public sealed record EntitlementBundleResolution(
    EntitlementSnapshot Snapshot,
    string ResolutionReasonCode);

/// <summary>
/// Translates product offers and lifecycle state into explicit capabilities and limits.
/// Products consume the result as data rather than branching on plan names.
/// </summary>
public sealed class EntitlementBundleResolver
{
    private static readonly EntitlementGrant CatalogueRead =
        new(AtlasTaxonomy.CatalogueRead.Value, "bundle");

    private static readonly EntitlementGrant CatalogueWrite =
        new(AtlasTaxonomy.CatalogueWrite.Value, "bundle");

    private static readonly EntitlementGrant ExportPortableBundle =
        new(AtlasTaxonomy.ExportPortableBundle.Value, "bundle");

    private static readonly EntitlementGrant IntegrationMap =
        new(AtlasTaxonomy.AnalysisIntegrationMap.Value, "bundle");

    private static readonly EntitlementGrant EndOfLife =
        new(AtlasTaxonomy.AnalysisEndOfLife.Value, "bundle");

    private static readonly EntitlementGrant ApplicationPortfolio =
        new(AtlasTaxonomy.AnalysisApm.Value, "bundle");

    private static readonly EntitlementGrant Roadmap =
        new(AtlasTaxonomy.AnalysisRoadmap.Value, "bundle");

    private static readonly EntitlementGrant AiReview =
        new(AtlasTaxonomy.AiReview.Value, "bundle");

    private static readonly EntitlementGrant DiscoveryIngestion =
        new(AtlasTaxonomy.DiscoveryIngestion.Value, "bundle");

    // Database schema introspection into the data catalogue: a paid data-layer capability that unlocks
    // from the Starter tier up (discovery as a calibrated Starter hook).
    private static readonly EntitlementGrant DataIntrospection =
        new(AtlasTaxonomy.DataIntrospection.Value, "bundle");

    // Data-quality, provenance and classification profiling over the data catalogue: a paid data-layer
    // capability that unlocks from the Starter tier up, alongside schema introspection.
    private static readonly EntitlementGrant DataQuality =
        new(AtlasTaxonomy.DataQuality.Value, "bundle");

    private static readonly EntitlementGrant PortalReadOnly =
        new(AtlasTaxonomy.PortalReadonly.Value, "bundle");

    public EntitlementBundleResolution Resolve(EntitlementBundleRequest request)
    {
        var grants = ResolveBaseOffer(request.Offer);
        var lifecycleReason = ReasonCodes.EntitlementGranted;

        grants = request.LifecycleState switch
        {
            EntitlementLifecycleState.Active or EntitlementLifecycleState.TrialActive => grants,
            EntitlementLifecycleState.TrialExpired => RestrictToReadOnlyRetentionSurface(grants),
            EntitlementLifecycleState.ReadOnly => RestrictToReadOnlyRetentionSurface(grants),
            EntitlementLifecycleState.Locked => RestrictToExportOnly(grants),
            EntitlementLifecycleState.RetentionPeriod => RestrictToExportOnly(grants),
            EntitlementLifecycleState.DataPurged => [],
            _ => grants
        };

        lifecycleReason = request.LifecycleState switch
        {
            EntitlementLifecycleState.TrialExpired => ReasonCodes.LifecycleTrialExpired,
            EntitlementLifecycleState.ReadOnly => ReasonCodes.LifecycleReadOnly,
            EntitlementLifecycleState.Locked => ReasonCodes.LifecycleLocked,
            EntitlementLifecycleState.RetentionPeriod => ReasonCodes.LifecycleRetention,
            EntitlementLifecycleState.DataPurged => ReasonCodes.LifecyclePurged,
            _ => ReasonCodes.EntitlementGranted
        };

        var snapshot = new EntitlementSnapshot(
            request.Tenant,
            request.IssuedAt,
            request.ExpiresAt,
            request.GraceUntil,
            grants);

        return new EntitlementBundleResolution(snapshot, lifecycleReason);
    }

    private static IReadOnlyList<EntitlementGrant> ResolveBaseOffer(EntitlementOffer offer) =>
        offer switch
        {
            EntitlementOffer.CommunitySelfHosted =>
            [
                CatalogueRead,
                CatalogueWrite,
                ExportPortableBundle,
                GrantWithLimits(AtlasTaxonomy.Entities.Value, 100000m)
            ],

            EntitlementOffer.HostedTrial =>
            [
                CatalogueRead,
                CatalogueWrite,
                ExportPortableBundle,
                PortalReadOnly,
                IntegrationMap,
                EndOfLife,
                ApplicationPortfolio,
                Roadmap,
                AiReview,
                DiscoveryIngestion,
                DataIntrospection,
                DataQuality,
                GrantWithLimits(AtlasTaxonomy.Entities.Value, 10000m, AtlasTaxonomy.Users.Value, 50m, AtlasTaxonomy.Workspaces.Value, 5m, AtlasTaxonomy.ImportJobs.Value, 20m)
            ],

            EntitlementOffer.HostedStarter =>
            [
                CatalogueRead,
                CatalogueWrite,
                ExportPortableBundle,
                PortalReadOnly,
                DataIntrospection,
                DataQuality,
                GrantWithLimits(AtlasTaxonomy.Entities.Value, 2000m, AtlasTaxonomy.Users.Value, 10m, AtlasTaxonomy.Workspaces.Value, 1m, AtlasTaxonomy.ImportJobs.Value, 5m)
            ],

            EntitlementOffer.Pro =>
            [
                CatalogueRead,
                CatalogueWrite,
                ExportPortableBundle,
                PortalReadOnly,
                IntegrationMap,
                EndOfLife,
                ApplicationPortfolio,
                Roadmap,
                AiReview,
                DataIntrospection,
                DataQuality,
                GrantWithLimits(AtlasTaxonomy.Entities.Value, 50000m, AtlasTaxonomy.Users.Value, 100m, AtlasTaxonomy.Workspaces.Value, 10m, AtlasTaxonomy.ImportJobs.Value, 100m)
            ],

            EntitlementOffer.Enterprise or EntitlementOffer.SelfHostedEnterprise =>
            [
                CatalogueRead,
                CatalogueWrite,
                ExportPortableBundle,
                PortalReadOnly,
                IntegrationMap,
                EndOfLife,
                ApplicationPortfolio,
                Roadmap,
                AiReview,
                DiscoveryIngestion,
                DataIntrospection,
                DataQuality,
                GrantWithLimits(AtlasTaxonomy.Entities.Value, 250000m, AtlasTaxonomy.Users.Value, 1000m, AtlasTaxonomy.Workspaces.Value, 100m, AtlasTaxonomy.ImportJobs.Value, 1000m)
            ],

            _ => [CatalogueRead, ExportPortableBundle]
        };

    private static IReadOnlyList<EntitlementGrant> RestrictToReadOnlyRetentionSurface(IEnumerable<EntitlementGrant> grants) =>
        grants.Where(grant =>
            grant.Capability is "atlas.catalogue.read" or "atlas.export.portable-bundle" or "atlas.portal.readonly")
            .ToArray();

    private static IReadOnlyList<EntitlementGrant> RestrictToExportOnly(IEnumerable<EntitlementGrant> grants) =>
        grants.Where(grant => grant.Capability == AtlasTaxonomy.ExportPortableBundle.Value).ToArray();

    private static EntitlementGrant GrantWithLimits(params object[] values)
    {
        var limits = new Dictionary<string, decimal>(StringComparer.Ordinal);

        for (var i = 0; i < values.Length; i += 2)
        {
            limits[(string)values[i]] = (decimal)values[i + 1];
        }

        return new EntitlementGrant("fabric.bundle.limits", "bundle", limits);
    }
}
