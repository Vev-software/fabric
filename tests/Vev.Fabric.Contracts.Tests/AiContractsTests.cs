using Vev.Fabric.Contracts.Ai;
using Vev.Fabric.Contracts.Entitlements;

namespace Vev.Fabric.Contracts.Tests;

public sealed class AiContractsTests
{
    [Fact]
    public async Task Unavailable_service_fails_closed_when_provider_is_not_configured()
    {
        var result = await new UnavailableAiAssistService().AssistAsync(CreateRequest(AiSafetyPolicy.Required));

        Assert.Equal(AiAssistOutcome.Unavailable, result.Outcome);
        Assert.Equal(ReasonCodes.AiProviderUnavailable, result.ReasonCode);
        Assert.True(result.Policy.NoTraining);
        Assert.Equal(AiDataResidency.EuropeanEconomicArea, result.Policy.DataResidency);
    }

    [Fact]
    public async Task Unavailable_service_denies_a_request_without_all_required_guardrails()
    {
        var policy = AiSafetyPolicy.Required with { NoTraining = false };
        var result = await new UnavailableAiAssistService().AssistAsync(CreateRequest(policy));

        Assert.Equal(AiAssistOutcome.Denied, result.Outcome);
        Assert.Equal(ReasonCodes.AiPolicyRequired, result.ReasonCode);
    }

    private static AiAssistRequest CreateRequest(AiSafetyPolicy policy) => new(
        new TenantContext("tenant-a"),
        new PrincipalContext("architect-1", "Atlas Architect", ["AtlasArchitect"]),
        new CapabilityId("atlas.ai.review"),
        "draft-target-version",
        "Redacted grounded context.",
        policy,
        "corr-ai-1");
}
