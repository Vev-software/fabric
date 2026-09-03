using System.Text.Json;
using Vev.Fabric.Contracts.Taxonomy;

namespace Vev.Fabric.Contracts.Tests;

public sealed class TaxonomyCatalogTests
{
    [Fact]
    public void CapabilityIds_Are_Unique_And_WellFormed()
    {
        var ids = Capabilities.All.Select(capability => capability.Id).ToArray();

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ids, id => Assert.True(TaxonomyCatalog.IsWellFormedCapabilityId(id), id));
    }

    [Fact]
    public void LimitKeys_Are_WellFormed()
    {
        var limitIds = Capabilities.All
            .Where(capability => capability.Kind == TaxonomyKind.Limit)
            .Select(capability => capability.Id);

        Assert.All(limitIds, id => Assert.True(TaxonomyCatalog.IsWellFormedLimitKey(id), id));
    }

    [Fact]
    public void Reserved_Capabilities_Are_The_Canonical_Atlas_Commercial_Seams()
    {
        // Fabric is the single source of truth for the reserved (paid) capability ids. Downstream
        // editions (atlas-community AtlasCapabilities.ReservedPaid, atlas-enterprise) key their
        // gates on exactly these strings, so pin the set here and let it fail closed on drift.
        var reserved = Capabilities.All
            .Where(capability => capability is { Kind: TaxonomyKind.Feature, Reserved: true })
            .Select(capability => capability.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        string[] expected =
        [
            "atlas.ai.generate",
            "atlas.ai.review",
            "atlas.analysis.apm",
            "atlas.analysis.eol",
            "atlas.analysis.integration-map",
            "atlas.analysis.roadmap",
            "atlas.data.introspection",
            "atlas.data.overlap",
            "atlas.data.quality",
            "atlas.discovery.ingestion",
            "atlas.export.archimate",
        ];

        Assert.Equal(expected.OrderBy(id => id, StringComparer.Ordinal), reserved);
    }

    [Fact]
    public void ArchiMate_Export_Is_A_Reserved_Commercial_Seam()
    {
        // atlas.export.archimate is granted by Starter-or-higher bundles (issue #44), so it is a paid
        // seam, not a free portability escape hatch — the portable bundle stays the free exit.
        var archimate = Assert.Single(
            Capabilities.All, capability => capability.Id == AtlasTaxonomy.ExportArchiMate.Value);

        Assert.True(archimate.Reserved, "atlas.export.archimate must be reserved (paid) in the canonical taxonomy.");
    }

    [Fact]
    public void ReasonCodes_Are_Unique()
    {
        var codes = Reasons.All.Select(reason => reason.Code).ToArray();

        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("atlas:asset/app-checkout", true)]
    [InlineData("portic:provider/openai-primary", true)]
    [InlineData("Atlas:Asset", false)]
    [InlineData("atlas asset", false)]
    public void ResourceIds_Follow_Naming_Rules(string value, bool expected)
    {
        Assert.Equal(expected, TaxonomyCatalog.IsWellFormedResourceId(value));
    }

    [Fact]
    public void CatalogDocument_Deserializes_From_Sample()
    {
        var samplesDirectory = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "conformance", "samples"));

        var json = File.ReadAllText(Path.Combine(samplesDirectory, "taxonomy-catalog.sample.json"));
        var document = JsonSerializer.Deserialize<TaxonomyCatalogDocument>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(document);
        Assert.NotEmpty(document!.Capabilities);
        Assert.NotEmpty(document.Reasons);
    }
}
