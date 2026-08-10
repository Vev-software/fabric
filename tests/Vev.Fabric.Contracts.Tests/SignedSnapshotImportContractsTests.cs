using Vev.Fabric.Contracts.Entitlements;

namespace Vev.Fabric.Contracts.Tests;

public sealed class SignedSnapshotImportContractsTests
{
    [Fact]
    public void ImportRequest_Carries_Document_And_ValidateOnly_Flag()
    {
        var document = new SignedEntitlementSnapshot("dev-key", "HS256", "{}", "sig");
        var request = new ImportSignedEntitlementSnapshotRequest(document, ValidateOnly: true);

        Assert.True(request.ValidateOnly);
        Assert.Equal(document, request.Document);
    }

    [Fact]
    public void ImportResponse_Carries_Accepted_Snapshot_When_Valid()
    {
        var snapshot = new EntitlementSnapshot(
            "tenant-a",
            new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero),
            []);

        var response = new ImportSignedEntitlementSnapshotResponse(
            Accepted: true,
            ReasonCode: ReasonCodes.EntitlementGranted,
            Snapshot: snapshot);

        Assert.True(response.Accepted);
        Assert.Equal(snapshot, response.Snapshot);
    }
}
