using Vev.Fabric.Contracts;
using Vev.Fabric.Contracts.Entitlements;

namespace Vev.Fabric.Contracts.Tests;

public sealed class EntitlementBatchContractsTests
{
    [Fact]
    public void BatchRequest_Preserves_RequestOrdering()
    {
        var request = new EvaluateEntitlementsRequest(
            [
                CreateRequest("atlas.catalogue.read"),
                CreateRequest("atlas.catalogue.write")
            ]);

        Assert.Collection(
            request.Requests,
            first => Assert.Equal("atlas.catalogue.read", first.Capability.Value),
            second => Assert.Equal("atlas.catalogue.write", second.Capability.Value));
    }

    [Fact]
    public void BatchResponse_Carries_All_Decisions()
    {
        var now = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var response = new EvaluateEntitlementsResponse(
            [
                EntitlementDecision.Allow(new CapabilityId("atlas.catalogue.read"), "snapshot", now),
                EntitlementDecision.Deny(new CapabilityId("atlas.catalogue.write"), ReasonCodes.EntitlementDenied, "snapshot", now)
            ]);

        Assert.Equal(2, response.Decisions.Count);
        Assert.True(response.Decisions[0].Allowed);
        Assert.False(response.Decisions[1].Allowed);
    }

    private static EntitlementRequest CreateRequest(string capability) =>
        new(
            new TenantContext("tenant-a"),
            new CapabilityId(capability),
            new PrincipalContext("principal-1", "Test User", Array.Empty<string>()));
}
