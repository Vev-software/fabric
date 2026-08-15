using System.Text.Json;
using Vev.Fabric.Contracts.Entitlements;
using Vev.Fabric.Contracts.Extensions;

namespace Vev.Fabric.Contracts.Tests;

public sealed class ExtensionContractsTests
{
    private static readonly string SamplesDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "conformance", "samples"));

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static ExtensionManifest LoadManifest(string file) =>
        JsonSerializer.Deserialize<ExtensionManifest>(
            File.ReadAllText(Path.Combine(SamplesDirectory, file)), SerializerOptions)!;

    [Fact]
    public void Sample_ExtensionManifest_Deserializes()
    {
        var manifest = LoadManifest("extension-manifest.sample.json");

        Assert.Equal("com.acme.archimate-importer", manifest.Id);
        Assert.Equal(ExtensionType.ImporterExporter, manifest.Type);
        Assert.Equal("^1.0", manifest.CompatibleWith.FabricApi);
        Assert.Contains(manifest.DeclaredPermissions, p => p.Value == "atlas.catalogue.read");
    }

    [Fact]
    public void Valid_manifest_passes_validation()
    {
        var result = ExtensionManifestValidator.Validate(LoadManifest("extension-manifest.sample.json"));

        Assert.True(result.Valid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Manifest_declaring_a_reserved_capability_is_rejected()
    {
        // The hard guard: a module may not claim a reserved paid capability (atlas.ai.review).
        var result = ExtensionManifestValidator.Validate(LoadManifest("extension-manifest-reserved.sample.json"));

        Assert.False(result.Valid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ExtensionValidationReasonCodes.ReservedCapabilityDeclared, error.Code);
        Assert.Contains("atlas.ai.review", error.Message);
    }

    [Fact]
    public void Missing_id_is_rejected()
    {
        var manifest = new ExtensionManifest(
            Id: "",
            Version: "1.0.0",
            Publisher: "Acme",
            Type: ExtensionType.Connector,
            CompatibleWith: new ExtensionCompatibility("^1.0"));

        var result = ExtensionManifestValidator.Validate(manifest);

        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Code == ExtensionValidationReasonCodes.MissingId);
    }

    [Fact]
    public void Install_raises_a_marketplace_install_entitlement_request()
    {
        var manifest = LoadManifest("extension-manifest.sample.json");

        var request = ExtensionInstall.InstallEntitlementRequest(
            manifest,
            new TenantContext("tenant-a"),
            new PrincipalContext("principal-1", "Admin", ["AtlasArchitect"]));

        Assert.Equal("fabric.marketplace.install", request.Capability.Value);
        Assert.Equal("tenant-a", request.Tenant.TenantId);
        Assert.NotNull(request.Resource);
        Assert.Equal(manifest.Id, request.Resource!.Value.Value);
    }
}
