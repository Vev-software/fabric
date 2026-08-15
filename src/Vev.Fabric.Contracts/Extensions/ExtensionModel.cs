using System.Text.Json.Serialization;

namespace Vev.Fabric.Contracts.Extensions;

/// <summary>
/// The closed set of extension types Fabric supports (07 §2). It is deliberately closed — there
/// is no universal plug-in host (AGENTS.md §1.7); an extension is one of these known shapes.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ExtensionType>))]
public enum ExtensionType
{
    /// <summary>An adapter for an external provider (identity, PDP, storage, …).</summary>
    [JsonStringEnumMemberName("provider-adapter")]
    ProviderAdapter,

    /// <summary>An importer/exporter for an external format.</summary>
    [JsonStringEnumMemberName("importer-exporter")]
    ImporterExporter,

    /// <summary>A policy pack (rules/checks applied by the product).</summary>
    [JsonStringEnumMemberName("policy-pack")]
    PolicyPack,

    /// <summary>A workflow or action triggered in the product.</summary>
    [JsonStringEnumMemberName("workflow-action")]
    WorkflowAction,

    /// <summary>A connector to an external system.</summary>
    [JsonStringEnumMemberName("connector")]
    Connector,

    /// <summary>A UI extension surface.</summary>
    [JsonStringEnumMemberName("ui-extension")]
    UiExtension,

    /// <summary>A product domain module.</summary>
    [JsonStringEnumMemberName("domain-module")]
    DomainModule
}

/// <summary>
/// The versions an extension is compatible with, as SemVer ranges (07 §3). The Fabric contract API
/// range is required; a product range is present only for product-specific extensions.
/// </summary>
/// <param name="FabricApi">SemVer range of the Fabric contract API the extension targets, e.g. "^1.0".</param>
/// <param name="Product">Optional SemVer range of the host product, for product-specific extensions.</param>
public sealed record ExtensionCompatibility(
    string FabricApi,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Product = null);

/// <summary>
/// The Fabric-owned extension manifest (07 §3). It declares what an extension is and — deny by
/// default — exactly what it may touch: no permission, resource, network host or secret is granted
/// unless the manifest declares it. A manifest can never declare a reserved paid capability; that
/// is the entitlement decision's job, not the module's (see <see cref="ExtensionManifestValidator"/>,
/// 09 §3).
/// </summary>
/// <param name="Id">Stable extension identifier (reverse-DNS recommended), e.g. "com.acme.archimate-importer".</param>
/// <param name="Version">The extension's own SemVer version.</param>
/// <param name="Publisher">The publishing entity.</param>
/// <param name="Type">Which of the closed extension types this is.</param>
/// <param name="CompatibleWith">The Fabric/product versions the extension targets.</param>
/// <param name="Permissions">Declared capabilities the extension requests. Deny-by-default: absent = none.</param>
/// <param name="Resources">Declared resource scopes the extension needs. Deny-by-default.</param>
/// <param name="Network">Declared outbound network host patterns. Deny-by-default.</param>
/// <param name="Secrets">Declared secret references (names, never values). Deny-by-default.</param>
public sealed record ExtensionManifest(
    string Id,
    string Version,
    string Publisher,
    ExtensionType Type,
    ExtensionCompatibility CompatibleWith,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<CapabilityId>? Permissions = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<ResourceId>? Resources = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? Network = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? Secrets = null)
{
    /// <summary>The declared permissions, never null (absent = empty = deny-by-default).</summary>
    [JsonIgnore]
    public IReadOnlyList<CapabilityId> DeclaredPermissions => Permissions ?? [];
}
