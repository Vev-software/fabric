using System.Text.Json.Serialization;
using Vev.Fabric.Contracts.Entitlements;

namespace Vev.Fabric.Contracts.Ai;

[JsonConverter(typeof(JsonStringEnumConverter<AiDataResidency>))]
public enum AiDataResidency
{
    EuropeanEconomicArea
}

[JsonConverter(typeof(JsonStringEnumConverter<AiPromptInjectionHandling>))]
public enum AiPromptInjectionHandling
{
    Required
}

[JsonConverter(typeof(JsonStringEnumConverter<AiAssistOutcome>))]
public enum AiAssistOutcome
{
    Completed,
    Denied,
    Unavailable,
    Failed
}

/// <summary>Mandatory handling requirements carried with every Fabric AI request.</summary>
public sealed record AiSafetyPolicy(
    bool NoTraining,
    AiDataResidency RequiredDataResidency,
    bool RedactionRequired,
    AiPromptInjectionHandling PromptInjectionHandling)
{
    public static AiSafetyPolicy Required { get; } = new(true, AiDataResidency.EuropeanEconomicArea, true, AiPromptInjectionHandling.Required);
}

/// <summary>Provider-neutral request for advisory AI assistance. Products retain all mutation authority.</summary>
public sealed record AiAssistRequest(
    TenantContext Tenant,
    PrincipalContext Principal,
    CapabilityId Capability,
    string Purpose,
    string Input,
    AiSafetyPolicy Policy,
    string CorrelationId,
    IReadOnlyDictionary<string, string>? Grounding = null);

/// <summary>Usage record emitted by the provider route for product-side metering and reconciliation.</summary>
public sealed record AiUsage(
    string UsageId,
    decimal InputUnits,
    decimal OutputUnits,
    DateTimeOffset MeteredAt);

/// <summary>Provider assertion that its route honored the policy attached to the request.</summary>
public sealed record AiPolicyAttestation(
    bool NoTraining,
    AiDataResidency DataResidency,
    bool RedactionApplied,
    AiPromptInjectionHandling PromptInjectionHandling);

/// <summary>Outcome of one AI-assist request. A result is advisory and never a product-state mutation.</summary>
public sealed record AiAssistResult(
    AiAssistOutcome Outcome,
    string ReasonCode,
    AiPolicyAttestation Policy,
    string? Output = null,
    string? Provider = null,
    AiUsage? Usage = null);

/// <summary>Fabric seam for provider routing. Portic and other adapters implement this interface.</summary>
public interface IAiAssistService
{
    Task<AiAssistResult> AssistAsync(AiAssistRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Fail-closed reference implementation for deployments without a configured AI provider.</summary>
public sealed class UnavailableAiAssistService : IAiAssistService
{
    public Task<AiAssistResult> AssistAsync(AiAssistRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!AiSafetyPolicyValidator.IsRequired(request.Policy))
        {
            return Task.FromResult(new AiAssistResult(
                AiAssistOutcome.Denied,
                ReasonCodes.AiPolicyRequired,
                ToAttestation(request.Policy)));
        }

        return Task.FromResult(new AiAssistResult(
            AiAssistOutcome.Unavailable,
            ReasonCodes.AiProviderUnavailable,
            ToAttestation(request.Policy)));
    }

    private static AiPolicyAttestation ToAttestation(AiSafetyPolicy policy) => new(
        policy.NoTraining,
        policy.RequiredDataResidency,
        policy.RedactionRequired,
        policy.PromptInjectionHandling);
}

/// <summary>Shared fail-closed policy check for implementations and product integration tests.</summary>
public static class AiSafetyPolicyValidator
{
    public static bool IsRequired(AiSafetyPolicy? policy) =>
        policy is
        {
            NoTraining: true,
            RequiredDataResidency: AiDataResidency.EuropeanEconomicArea,
            RedactionRequired: true,
            PromptInjectionHandling: AiPromptInjectionHandling.Required
        };
}
