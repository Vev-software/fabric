using System.Text.Json;
using Vev.Fabric.Contracts.Authorization;
using Vev.Fabric.Contracts.Entitlements;

namespace Vev.Fabric.Contracts.Tests;

public sealed class AuthorizationContractsTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void AuthorizationRequest_RoundTrips()
    {
        var request = new AuthorizationRequest(
            new TenantContext("tenant-a"),
            new PrincipalContext(
                "principal-1",
                "Atlas Architect",
                ["AtlasArchitect"],
                new Dictionary<string, string> { ["sub"] = "principal-1" }),
            "atlas.catalogue.write",
            new ResourceId("atlas:catalogue/main"));

        var json = JsonSerializer.Serialize(request, SerializerOptions);
        var roundTripped = JsonSerializer.Deserialize<AuthorizationRequest>(json, SerializerOptions);

        Assert.Equal(request.Tenant, roundTripped.Tenant);
        Assert.Equal(request.Action, roundTripped.Action);
        Assert.Equal(request.Resource, roundTripped.Resource);
        Assert.Equal(request.Principal.PrincipalId, roundTripped.Principal.PrincipalId);
        Assert.Equal(request.Principal.DisplayName, roundTripped.Principal.DisplayName);
        Assert.Equal(request.Principal.Roles, roundTripped.Principal.Roles);
        Assert.NotNull(roundTripped.Principal.Claims);
        Assert.Equal("principal-1", roundTripped.Principal.Claims!["sub"]);
    }

    [Fact]
    public void LocalAuthorizer_Allows_When_Principal_Holds_Required_Role()
    {
        var authorizer = new LocalAuthorizer(
            new AuthorizationPolicyRegistry()
                .Require("atlas.catalogue.write", "AtlasArchitect"));

        var decision = authorizer.Authorize(CreateRequest(["AtlasArchitect"]));

        Assert.True(decision.Allowed);
        Assert.Equal(ReasonCodes.Allow, decision.ReasonCode);
        Assert.Equal(LocalAuthorizer.DefaultSource, decision.Source);
    }

    [Fact]
    public void LocalAuthorizer_Denies_When_Principal_Misses_Required_Role()
    {
        var authorizer = new LocalAuthorizer(
            new AuthorizationPolicyRegistry()
                .Require("atlas.catalogue.write", "AtlasArchitect"));

        var decision = authorizer.Authorize(CreateRequest(["AtlasCustomer"]));

        Assert.False(decision.Allowed);
        Assert.Equal(ReasonCodes.RoleMissing, decision.ReasonCode);
        Assert.Equal(LocalAuthorizer.DefaultSource, decision.Source);
    }

    [Fact]
    public void LocalAuthorizer_Allows_Unregistered_Actions()
    {
        var authorizer = new LocalAuthorizer(new AuthorizationPolicyRegistry());

        var decision = authorizer.Authorize(CreateRequest(["AtlasCustomer"], "atlas.catalogue.read"));

        Assert.True(decision.Allowed);
        Assert.Equal(ReasonCodes.Allow, decision.ReasonCode);
    }

    private static AuthorizationRequest CreateRequest(IReadOnlyCollection<string> roles, string action = "atlas.catalogue.write") =>
        new(
            new TenantContext("tenant-a"),
            new PrincipalContext("principal-1", "Test User", roles, new Dictionary<string, string>()),
            action,
            new ResourceId("atlas:catalogue/main"));
}
