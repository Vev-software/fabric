using System.Text.Json.Serialization;
using Vev.Fabric.Contracts.Entitlements;

namespace Vev.Fabric.Contracts.Taxonomy;

/// <summary>
/// High-level category for a taxonomy definition.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TaxonomyKind>))]
public enum TaxonomyKind
{
    Feature,
    Limit,
    Resource,
    Reason
}

/// <summary>
/// A registered capability or limit in the VEV taxonomy.
/// </summary>
public sealed record CapabilityDefinition(
    string Id,
    TaxonomyKind Kind,
    string Description,
    bool Reserved = false);

/// <summary>
/// A shared decision reason in the Fabric error model.
/// </summary>
public sealed record DecisionReasonDefinition(
    string Code,
    string Description,
    bool Deny = false);

/// <summary>
/// Public catalog document for the current Fabric taxonomy slice.
/// </summary>
public sealed record TaxonomyCatalogDocument(
    string ContractVersion,
    IReadOnlyList<CapabilityDefinition> Capabilities,
    IReadOnlyList<DecisionReasonDefinition> Reasons);

/// <summary>
/// Naming rules and seeded ids for the public taxonomy.
/// </summary>
public static class TaxonomyCatalog
{
    public const string ContractVersion = "1";

    public static bool IsWellFormedCapabilityId(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.All(ch => char.IsLower(ch) || char.IsDigit(ch) || ch is '.' or '-');

    public static bool IsWellFormedLimitKey(string value) =>
        IsWellFormedCapabilityId(value);

    public static bool IsWellFormedResourceId(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.All(ch => char.IsLower(ch) || char.IsDigit(ch) || ch is '.' or '-' or ':' or '/');

    public static TaxonomyCatalogDocument Document { get; } =
        new(
            ContractVersion,
            Capabilities.All,
            Reasons.All);
}

/// <summary>
/// Seeded capabilities and limits already consumed by Atlas and Fabric contracts.
/// </summary>
public static class Capabilities
{
    public static IReadOnlyList<CapabilityDefinition> All { get; } =
    [
        // Atlas free/community catalogue surface
        new(AtlasTaxonomy.CatalogueRead.Value, TaxonomyKind.Feature, "Read the Atlas catalogue."),
        new(AtlasTaxonomy.CatalogueWrite.Value, TaxonomyKind.Feature, "Create, edit or delete Atlas catalogue entries."),
        new(AtlasTaxonomy.ExportPortableBundle.Value, TaxonomyKind.Feature, "Export a portable Atlas bundle."),

        // Atlas commercial/hosted feature surface
        new(AtlasTaxonomy.AnalysisIntegrationMap.Value, TaxonomyKind.Feature, "Integration mapping across the landscape.", Reserved: true),
        new(AtlasTaxonomy.AnalysisEndOfLife.Value, TaxonomyKind.Feature, "End-of-life analysis over the landscape.", Reserved: true),
        new(AtlasTaxonomy.AnalysisApm.Value, TaxonomyKind.Feature, "Application portfolio management analysis.", Reserved: true),
        new(AtlasTaxonomy.AnalysisRoadmap.Value, TaxonomyKind.Feature, "Roadmap generation over the landscape.", Reserved: true),
        new(AtlasTaxonomy.AiReview.Value, TaxonomyKind.Feature, "AI-assisted architecture review.", Reserved: true),
        new(AtlasTaxonomy.DiscoveryIngestion.Value, TaxonomyKind.Feature, "Discovery ingestion into Atlas.", Reserved: true),
        new(AtlasTaxonomy.PortalReadonly.Value, TaxonomyKind.Feature, "Read-only Atlas portal surface."),

        // Fabric and module system anchors
        new(FabricTaxonomy.MarketplaceInstall.Value, TaxonomyKind.Feature, "Install a marketplace extension."),

        // Atlas operational limits
        new(AtlasTaxonomy.Entities.Value, TaxonomyKind.Limit, "Maximum number of Atlas entities in scope."),
        new(AtlasTaxonomy.Users.Value, TaxonomyKind.Limit, "Maximum number of Atlas users."),
        new(AtlasTaxonomy.Storage.Value, TaxonomyKind.Limit, "Maximum Atlas storage allotment."),
        new(AtlasTaxonomy.Workspaces.Value, TaxonomyKind.Limit, "Maximum number of Atlas workspaces."),
        new(AtlasTaxonomy.ImportJobs.Value, TaxonomyKind.Limit, "Maximum Atlas import jobs."),

        // Example Fabric/Portic anchors used in handbook and examples
        new(AtlasTaxonomy.ExportArchiMate.Value, TaxonomyKind.Feature, "Export Atlas content to ArchiMate."),
        new(AtlasTaxonomy.RepositoryApplicationMax.Value, TaxonomyKind.Limit, "Maximum number of application records in Atlas."),
        new(PorticTaxonomy.GovernancePolicyAdvanced.Value, TaxonomyKind.Feature, "Advanced Portic governance policy surface."),
        new(PorticTaxonomy.GatewayProvidersMax.Value, TaxonomyKind.Limit, "Maximum number of Portic providers.")
    ];
}

/// <summary>
/// Canonical Atlas capability and limit identifiers currently seeded in Fabric.
/// </summary>
public static class AtlasTaxonomy
{
    public static readonly CapabilityId CatalogueRead = new("atlas.catalogue.read");
    public static readonly CapabilityId CatalogueWrite = new("atlas.catalogue.write");
    public static readonly CapabilityId AnalysisIntegrationMap = new("atlas.analysis.integration-map");
    public static readonly CapabilityId AnalysisEndOfLife = new("atlas.analysis.eol");
    public static readonly CapabilityId AnalysisApm = new("atlas.analysis.apm");
    public static readonly CapabilityId AnalysisRoadmap = new("atlas.analysis.roadmap");
    public static readonly CapabilityId AiReview = new("atlas.ai.review");
    public static readonly CapabilityId DiscoveryIngestion = new("atlas.discovery.ingestion");
    public static readonly CapabilityId PortalReadonly = new("atlas.portal.readonly");
    public static readonly CapabilityId ExportPortableBundle = new("atlas.export.portable-bundle");
    public static readonly CapabilityId ExportArchiMate = new("atlas.export.archimate");

    public static readonly LimitKey Entities = new("atlas.entities");
    public static readonly LimitKey Users = new("atlas.users");
    public static readonly LimitKey Storage = new("atlas.storage");
    public static readonly LimitKey Workspaces = new("atlas.workspaces");
    public static readonly LimitKey ImportJobs = new("atlas.import.jobs");
    public static readonly LimitKey RepositoryApplicationMax = new("atlas.repository.application.max");
}

public static class FabricTaxonomy
{
    public static readonly CapabilityId MarketplaceInstall = new("fabric.marketplace.install");
}

public static class PorticTaxonomy
{
    public static readonly CapabilityId GovernancePolicyAdvanced = new("portic.governance.policy.advanced");
    public static readonly LimitKey GatewayProvidersMax = new("portic.gateway.providers.max");
}

/// <summary>
/// Shared allow/deny reasons used across entitlement and authorization decisions.
/// </summary>
public static class Reasons
{
    public static IReadOnlyList<DecisionReasonDefinition> All { get; } =
    [
        new(ReasonCodes.Allow, "The action or capability is allowed."),
        new(ReasonCodes.RoleMissing, "The principal lacks a required role.", Deny: true),
        new(ReasonCodes.EntitlementGranted, "The capability is granted by the active entitlement source."),
        new(ReasonCodes.EntitlementDenied, "The tenant does not hold the capability.", Deny: true),
        new(ReasonCodes.EntitlementUnavailable, "No current entitlement snapshot is available.", Deny: true),
        new(ReasonCodes.EntitlementSnapshotInvalid, "The signed entitlement snapshot failed validation.", Deny: true),
        new(ReasonCodes.EntitlementSnapshotStale, "The entitlement snapshot expired beyond grace.", Deny: true),
        new(ReasonCodes.EntitlementSnapshotTenantMismatch, "The entitlement snapshot belongs to a different tenant.", Deny: true),
        new(ReasonCodes.LifecycleTrialExpired, "Trial access has expired and the tenant is in retention mode.", Deny: true),
        new(ReasonCodes.LifecycleReadOnly, "The tenant is in read-only lifecycle mode.", Deny: true),
        new(ReasonCodes.LifecycleLocked, "The tenant is locked pending purge or upgrade.", Deny: true),
        new(ReasonCodes.LifecycleRetention, "The tenant is inside the retention window.", Deny: true),
        new(ReasonCodes.LifecyclePurged, "The tenant data has been purged.", Deny: true)
    ];
}
