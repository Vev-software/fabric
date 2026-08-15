using Vev.Fabric.Contracts.Entitlements;
using Vev.Fabric.Contracts.Taxonomy;

namespace Vev.Fabric.Contracts.Extensions;

/// <summary>One machine-readable reason a manifest was rejected.</summary>
/// <param name="Code">Stable reason code (see <see cref="ExtensionValidationReasonCodes"/>).</param>
/// <param name="Message">Human-readable explanation.</param>
public sealed record ExtensionValidationError(string Code, string Message);

/// <summary>The result of validating an <see cref="ExtensionManifest"/>.</summary>
/// <param name="Valid">Whether the manifest passed.</param>
/// <param name="Errors">The reasons it failed; empty when valid.</param>
public sealed record ExtensionValidationResult(bool Valid, IReadOnlyList<ExtensionValidationError> Errors)
{
    /// <summary>A passing result.</summary>
    public static ExtensionValidationResult Ok { get; } = new(true, []);

    /// <summary>A failing result carrying the reasons.</summary>
    public static ExtensionValidationResult Invalid(IReadOnlyList<ExtensionValidationError> errors) => new(false, errors);
}

/// <summary>Stable reason codes emitted by the manifest validator.</summary>
public static class ExtensionValidationReasonCodes
{
    /// <summary>The manifest has no id.</summary>
    public const string MissingId = "extension_missing_id";

    /// <summary>The manifest has no version.</summary>
    public const string MissingVersion = "extension_missing_version";

    /// <summary>The manifest has no publisher.</summary>
    public const string MissingPublisher = "extension_missing_publisher";

    /// <summary>The manifest declares no Fabric API compatibility range.</summary>
    public const string MissingFabricApiCompatibility = "extension_missing_fabric_api_compatibility";

    /// <summary>The manifest declares a reserved paid capability — the hard guard.</summary>
    public const string ReservedCapabilityDeclared = "extension_reserved_capability";
}

/// <summary>
/// Validates an <see cref="ExtensionManifest"/>, enforcing the single hard guard that keeps the
/// module mechanism from becoming a back-door around the paid edition: a manifest may never declare
/// a <b>reserved paid capability</b> (09 §3). Reserved capabilities are marked in the taxonomy
/// (fabric#7, <see cref="CapabilityDefinition.Reserved"/>); a module extends the edges, it can never
/// flip a reserved capability to allowed — that is the entitlement decision's job.
/// </summary>
public static class ExtensionManifestValidator
{
    /// <summary>The reserved paid capability ids from the taxonomy that a manifest may not declare.</summary>
    public static IReadOnlySet<string> ReservedCapabilityIds { get; } =
        TaxonomyCatalog.Document.Capabilities
            .Where(capability => capability.Reserved)
            .Select(capability => capability.Id)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Validate a manifest at authoring/validation time (deny-by-default + reserved guard).</summary>
    public static ExtensionValidationResult Validate(ExtensionManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var errors = new List<ExtensionValidationError>();

        if (string.IsNullOrWhiteSpace(manifest.Id))
            errors.Add(new(ExtensionValidationReasonCodes.MissingId, "Manifest requires a non-empty id."));

        if (string.IsNullOrWhiteSpace(manifest.Version))
            errors.Add(new(ExtensionValidationReasonCodes.MissingVersion, "Manifest requires a non-empty version."));

        if (string.IsNullOrWhiteSpace(manifest.Publisher))
            errors.Add(new(ExtensionValidationReasonCodes.MissingPublisher, "Manifest requires a non-empty publisher."));

        if (manifest.CompatibleWith is null || string.IsNullOrWhiteSpace(manifest.CompatibleWith.FabricApi))
            errors.Add(new(ExtensionValidationReasonCodes.MissingFabricApiCompatibility, "Manifest requires a compatibleWith.fabricApi range."));

        // The hard guard: a module can never claim or satisfy a reserved paid capability.
        foreach (var permission in manifest.DeclaredPermissions)
        {
            if (ReservedCapabilityIds.Contains(permission.Value))
            {
                errors.Add(new(
                    ExtensionValidationReasonCodes.ReservedCapabilityDeclared,
                    $"Manifest declares reserved paid capability '{permission.Value}': a module may not claim or satisfy a reserved capability; that is the entitlement decision's job."));
            }
        }

        return errors.Count == 0 ? ExtensionValidationResult.Ok : ExtensionValidationResult.Invalid(errors);
    }
}

/// <summary>
/// The install lifecycle is entitlement-checked: installing an extension emits
/// <c>fabric.marketplace.install</c> as an entitlement decision (07 §6), so the commercial control
/// plane can govern who installs what without any <c>if (plan == …)</c>.
/// </summary>
public static class ExtensionInstall
{
    /// <summary>
    /// The entitlement request the install lifecycle raises for a given manifest, targeting the
    /// <c>fabric.marketplace.install</c> capability with the extension as the resource.
    /// </summary>
    public static EntitlementRequest InstallEntitlementRequest(
        ExtensionManifest manifest, TenantContext tenant, PrincipalContext principal)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return new EntitlementRequest(
            tenant,
            FabricTaxonomy.MarketplaceInstall,
            principal,
            new ResourceId(manifest.Id));
    }
}
