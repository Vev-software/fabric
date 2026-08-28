using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vev.Fabric.Contracts.Identity;

namespace Vev.Fabric.Contracts.Tests;

public sealed class ServiceIdentityTests
{
    private const string KeyId = "caller-2026";
    private const string Issuer = "vev:service/caller";
    private const string Audience = "vev:service/callee";

    [Fact]
    public void A_minted_assertion_verifies_and_carries_the_tenant_and_roles()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var issuer = new ServiceAssertionIssuer(key, Issuer, KeyId);
        var validator = ValidatorFor(key);

        var token = issuer.Issue(Audience, "svc-worker", "tenant-a", ["catalogue.write"], TimeSpan.FromMinutes(5));
        var result = validator.Validate(token);

        Assert.True(result.IsValid);
        Assert.Equal(ServiceAssertionReasonCodes.Valid, result.ReasonCode);
        var assertion = result.Assertion!;
        Assert.Equal("svc-worker", assertion.Subject);
        Assert.Equal("tenant-a", assertion.TenantId);
        Assert.Equal(new TenantContext("tenant-a"), assertion.Tenant);
        Assert.Equal(["catalogue.write"], assertion.Roles);
        Assert.Equal(Issuer, assertion.Issuer);
        Assert.Equal(Audience, assertion.Audience);

        var principal = assertion.ToPrincipalContext();
        Assert.Equal("svc-worker", principal.PrincipalId);
        Assert.Equal(["catalogue.write"], principal.Roles);
    }

    [Fact]
    public void The_verifier_only_needs_the_public_key()
    {
        using var privateKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var publicOnly = ECDsa.Create();
        publicOnly.ImportSubjectPublicKeyInfo(privateKey.ExportSubjectPublicKeyInfo(), out _);

        var token = new ServiceAssertionIssuer(privateKey, Issuer, KeyId)
            .Issue(Audience, "svc", "tenant-a", [], TimeSpan.FromMinutes(5));
        var result = new ServiceAssertionValidator(
            new Dictionary<string, ECDsa>(StringComparer.Ordinal) { [KeyId] = publicOnly }, Issuer, Audience)
            .Validate(token);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void A_tampered_payload_fails_the_signature()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var token = new ServiceAssertionIssuer(key, Issuer, KeyId)
            .Issue(Audience, "svc", "tenant-a", [], TimeSpan.FromMinutes(5));

        // Re-sign nothing: swap in a different payload segment (a token for tenant-b).
        var forgedPayload = Encode("""{"iss":"vev:service/caller","aud":"vev:service/callee","sub":"svc","tenant":"tenant-b","roles":[],"iat":0,"nbf":0,"exp":9999999999,"jti":"x"}""");
        var parts = token.Split('.');
        var forged = $"{parts[0]}.{forgedPayload}.{parts[2]}";

        Assert.Equal(ServiceAssertionReasonCodes.BadSignature, ValidatorFor(key).Validate(forged).ReasonCode);
    }

    [Fact]
    public void A_signature_from_another_key_is_rejected()
    {
        using var minterKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var attackerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var token = new ServiceAssertionIssuer(attackerKey, Issuer, KeyId)
            .Issue(Audience, "svc", "tenant-a", [], TimeSpan.FromMinutes(5));

        // Same kid, but the trusted key is the real minter's — the attacker's signature must not verify.
        Assert.Equal(ServiceAssertionReasonCodes.BadSignature, ValidatorFor(minterKey).Validate(token).ReasonCode);
    }

    [Fact]
    public void An_unknown_key_id_is_rejected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var token = new ServiceAssertionIssuer(key, Issuer, "some-other-kid")
            .Issue(Audience, "svc", "tenant-a", [], TimeSpan.FromMinutes(5));

        Assert.Equal(ServiceAssertionReasonCodes.UnknownKey, ValidatorFor(key).Validate(token).ReasonCode);
    }

    [Fact]
    public void An_expired_assertion_is_rejected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var clock = new MutableClock(DateTimeOffset.UnixEpoch.AddYears(56));
        var token = new ServiceAssertionIssuer(key, Issuer, KeyId, clock)
            .Issue(Audience, "svc", "tenant-a", [], TimeSpan.FromMinutes(5));

        clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(61)); // past exp + the 60s skew
        var validator = new ServiceAssertionValidator(TrustedKeys(key), Issuer, Audience, clock);

        Assert.Equal(ServiceAssertionReasonCodes.Expired, validator.Validate(token).ReasonCode);
    }

    [Fact]
    public void An_assertion_not_yet_valid_is_rejected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var issuerClock = new MutableClock(DateTimeOffset.UnixEpoch.AddYears(56).AddHours(1)); // an hour ahead
        var token = new ServiceAssertionIssuer(key, Issuer, KeyId, issuerClock)
            .Issue(Audience, "svc", "tenant-a", [], TimeSpan.FromMinutes(5));

        var validatorClock = new MutableClock(DateTimeOffset.UnixEpoch.AddYears(56)); // "now" is before nbf
        var validator = new ServiceAssertionValidator(TrustedKeys(key), Issuer, Audience, validatorClock);

        Assert.Equal(ServiceAssertionReasonCodes.NotYetValid, validator.Validate(token).ReasonCode);
    }

    [Fact]
    public void A_wrong_issuer_is_rejected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var token = new ServiceAssertionIssuer(key, "vev:service/someone-else", KeyId)
            .Issue(Audience, "svc", "tenant-a", [], TimeSpan.FromMinutes(5));

        Assert.Equal(ServiceAssertionReasonCodes.WrongIssuer, ValidatorFor(key).Validate(token).ReasonCode);
    }

    [Fact]
    public void A_wrong_audience_is_rejected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var token = new ServiceAssertionIssuer(key, Issuer, KeyId)
            .Issue("vev:service/not-us", "svc", "tenant-a", [], TimeSpan.FromMinutes(5));

        Assert.Equal(ServiceAssertionReasonCodes.WrongAudience, ValidatorFor(key).Validate(token).ReasonCode);
    }

    [Fact]
    public void An_assertion_with_a_non_es256_algorithm_is_rejected_even_if_signed()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        // A validly-signed token whose header claims alg "none" — the pinned validator must refuse it
        // before ever looking at the signature (no alg confusion, no downgrade to "none").
        var token = SignRaw(
            $$"""{"alg":"none","typ":"JWT","kid":"{{KeyId}}"}""",
            $$"""{"iss":"{{Issuer}}","aud":"{{Audience}}","sub":"svc","tenant":"tenant-a","roles":[],"iat":0,"nbf":0,"exp":9999999999,"jti":"x"}""",
            key);

        Assert.Equal(ServiceAssertionReasonCodes.UnsupportedAlgorithm, ValidatorFor(key).Validate(token).ReasonCode);
    }

    [Fact]
    public void An_assertion_missing_the_tenant_is_rejected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var token = SignRaw(
            $$"""{"alg":"ES256","typ":"JWT","kid":"{{KeyId}}"}""",
            $$"""{"iss":"{{Issuer}}","aud":"{{Audience}}","sub":"svc","tenant":"","roles":[],"iat":0,"nbf":0,"exp":9999999999,"jti":"x"}""",
            key);

        Assert.Equal(ServiceAssertionReasonCodes.MissingTenant, ValidatorFor(key).Validate(token).ReasonCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("only.two")]
    [InlineData("a.b.c.d")]
    [InlineData("!!!.@@@.###")]
    public void Malformed_input_is_rejected(string token)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Assert.False(ValidatorFor(key).Validate(token).IsValid);
    }

    private static ServiceAssertionValidator ValidatorFor(ECDsa key) =>
        new(TrustedKeys(key), Issuer, Audience);

    private static Dictionary<string, ECDsa> TrustedKeys(ECDsa key) =>
        new(StringComparer.Ordinal) { [KeyId] = key };

    private static string Encode(string json) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json));

    // Signs an arbitrary header/payload with the ES256 scheme the contract uses, for crafting the
    // adversarial-but-well-signed tokens the happy-path issuer would never mint.
    private static string SignRaw(string headerJson, string payloadJson, ECDsa key)
    {
        var input = $"{Encode(headerJson)}.{Encode(payloadJson)}";
        var signature = key.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{input}.{Base64Url.EncodeToString(signature)}";
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
