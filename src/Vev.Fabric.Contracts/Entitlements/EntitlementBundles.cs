using System.Text.Json.Serialization;

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
        new("atlas.catalogue.read", "bundle");

    private static readonly EntitlementGrant CatalogueWrite =
        new("atlas.catalogue.write", "bundle");

    private static readonly EntitlementGrant ExportPortableBundle =
        new("atlas.export.portable-bundle", "bundle");

    private static readonly EntitlementGrant IntegrationMap =
        new("atlas.analysis.integration-map", "bundle");

    private static readonly EntitlementGrant EndOfLife =
        new("atlas.analysis.eol", "bundle");

    private static readonly EntitlementGrant ApplicationPortfolio =
        new("atlas.analysis.apm", "bundle");

    private static readonly EntitlementGrant Roadmap =
        new("atlas.analysis.roadmap", "bundle");

    private static readonly EntitlementGrant AiReview =
        new("atlas.ai.review", "bundle");

    private static readonly EntitlementGrant DiscoveryIngestion =
        new("atlas.discovery.ingestion", "bundle");

    private static readonly EntitlementGrant PortalReadOnly =
        new("atlas.portal.readonly", "bundle");

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
                GrantWithLimits("atlas.entities.max", 100000m)
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
                GrantWithLimits("atlas.entities.max", 10000m, "atlas.users.max", 50m, "atlas.workspaces.max", 5m, "atlas.import.jobs.max", 20m)
            ],

            EntitlementOffer.HostedStarter =>
            [
                CatalogueRead,
                CatalogueWrite,
                ExportPortableBundle,
                PortalReadOnly,
                GrantWithLimits("atlas.entities.max", 2000m, "atlas.users.max", 10m, "atlas.workspaces.max", 1m, "atlas.import.jobs.max", 5m)
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
                GrantWithLimits("atlas.entities.max", 50000m, "atlas.users.max", 100m, "atlas.workspaces.max", 10m, "atlas.import.jobs.max", 100m)
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
                GrantWithLimits("atlas.entities.max", 250000m, "atlas.users.max", 1000m, "atlas.workspaces.max", 100m, "atlas.import.jobs.max", 1000m)
            ],

            _ => [CatalogueRead, ExportPortableBundle]
        };

    private static IReadOnlyList<EntitlementGrant> RestrictToReadOnlyRetentionSurface(IEnumerable<EntitlementGrant> grants) =>
        grants.Where(grant =>
            grant.Capability is "atlas.catalogue.read" or "atlas.export.portable-bundle" or "atlas.portal.readonly")
            .ToArray();

    private static IReadOnlyList<EntitlementGrant> RestrictToExportOnly(IEnumerable<EntitlementGrant> grants) =>
        grants.Where(grant => grant.Capability is "atlas.export.portable-bundle").ToArray();

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
