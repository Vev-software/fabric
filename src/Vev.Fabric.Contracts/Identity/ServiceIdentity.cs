using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vev.Fabric.Contracts.Identity;

/// <summary>
/// Machine-to-machine identity for service callers (a product backend calling a sibling product's API).
/// Where <see cref="PrincipalContext"/> models an authenticated <em>user</em>, this models an
/// authenticated <em>service</em>: a short-lived, asymmetrically-signed assertion the caller mints and the
/// callee verifies, so no long-lived shared secret is transmitted or held by the verifier — the verifier
/// holds only the caller's <b>public</b> key.
/// <para>
/// The wire form is a standard compact JWS (<c>header.payload.signature</c>, base64url) signed with
/// <see cref="Algorithm"/> (ECDSA P-256 / SHA-256). It carries the calling tenant and the service
/// principal's coarse roles, so the callee can bind a per-request identity scoped to one tenant rather
/// than treating every call as an all-tenants superuser. This mirrors the substrate's existing
/// signed-document convention (see <c>SignedEntitlementSnapshot</c>) but is asymmetric, not HMAC.
/// </para>
/// </summary>
public static class ServiceIdentity
{
    /// <summary>HTTP header the compact assertion travels in.</summary>
    public const string AssertionHeaderName = "X-Fabric-Service-Assertion";

    /// <summary>The one accepted signing algorithm. Pinned: a verifier never honours any other <c>alg</c>.</summary>
    public const string Algorithm = "ES256";

    /// <summary>The JWS <c>typ</c> header value.</summary>
    public const string TokenType = "JWT";

    /// <summary>Registered/custom claim names carried in the assertion payload.</summary>
    public static class Claims
    {
        public const string Issuer = "iss";
        public const string Audience = "aud";
        public const string Subject = "sub";
        public const string Tenant = "tenant";
        public const string Roles = "roles";
        public const string IssuedAt = "iat";
        public const string NotBefore = "nbf";
        public const string Expiry = "exp";
        public const string TokenId = "jti";
    }
}

/// <summary>Machine-readable outcomes of verifying a service assertion.</summary>
public static class ServiceAssertionReasonCodes
{
    public const string Valid = "service_assertion_valid";
    public const string Malformed = "service_assertion_malformed";
    public const string UnsupportedAlgorithm = "service_assertion_unsupported_algorithm";
    public const string UnknownKey = "service_assertion_unknown_key";
    public const string BadSignature = "service_assertion_bad_signature";
    public const string Expired = "service_assertion_expired";
    public const string NotYetValid = "service_assertion_not_yet_valid";
    public const string WrongIssuer = "service_assertion_wrong_issuer";
    public const string WrongAudience = "service_assertion_wrong_audience";
    public const string MissingTenant = "service_assertion_missing_tenant";
}

/// <summary>
/// The verified identity a service assertion asserts: which service principal (<see cref="Subject"/>),
/// acting for which tenant (<see cref="TenantId"/>), with which coarse roles, minted by which issuer.
/// </summary>
public sealed record ServiceAssertion(
    string Issuer,
    string Audience,
    string Subject,
    string TenantId,
    IReadOnlyList<string> Roles,
    DateTimeOffset IssuedAt,
    DateTimeOffset NotBefore,
    DateTimeOffset ExpiresAt,
    string TokenId)
{
    /// <summary>The tenant this call acts for.</summary>
    public TenantContext Tenant => new(TenantId);

    /// <summary>Projects the verified assertion into a provider-neutral principal for the request pipeline.</summary>
    public PrincipalContext ToPrincipalContext(string? displayName = null) =>
        new(Subject, displayName ?? Subject, Roles);
}

/// <summary>Result of verifying a compact service assertion.</summary>
public sealed record ServiceAssertionValidationResult(bool IsValid, ServiceAssertion? Assertion, string ReasonCode)
{
    public static ServiceAssertionValidationResult Valid(ServiceAssertion assertion) =>
        new(true, assertion, ServiceAssertionReasonCodes.Valid);

    public static ServiceAssertionValidationResult Invalid(string reasonCode) =>
        new(false, null, reasonCode);
}

internal static class ServiceAssertionWire
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    internal sealed record Header(
        [property: JsonPropertyName("alg")] string Alg,
        [property: JsonPropertyName("typ")] string Typ,
        [property: JsonPropertyName("kid")] string Kid);

    internal sealed record Payload(
        [property: JsonPropertyName("iss")] string Iss,
        [property: JsonPropertyName("aud")] string Aud,
        [property: JsonPropertyName("sub")] string Sub,
        [property: JsonPropertyName("tenant")] string Tenant,
        [property: JsonPropertyName("roles")] IReadOnlyList<string> Roles,
        [property: JsonPropertyName("iat")] long Iat,
        [property: JsonPropertyName("nbf")] long Nbf,
        [property: JsonPropertyName("exp")] long Exp,
        [property: JsonPropertyName("jti")] string Jti);

    internal static string Encode(ReadOnlySpan<byte> bytes) => Base64Url.EncodeToString(bytes);

    internal static byte[] Decode(string value) => Base64Url.DecodeFromChars(value);
}

/// <summary>
/// Mints short-lived, ECDSA-signed service assertions. Holds the caller's <b>private</b> key; only the
/// caller can mint. The matching <see cref="ServiceAssertionValidator"/> on the callee holds only the
/// public key. The supplied <see cref="ECDsa"/> is owned by the caller — this type does not dispose it.
/// </summary>
public sealed class ServiceAssertionIssuer(ECDsa signingKey, string issuer, string keyId, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>Builds an issuer from a PEM-encoded EC private key (the helper then owns that imported key).</summary>
    public static ServiceAssertionIssuer FromPem(string issuer, string keyId, string privateKeyPem, TimeProvider? clock = null)
    {
        var key = ECDsa.Create();
        key.ImportFromPem(privateKeyPem);
        return new ServiceAssertionIssuer(key, issuer, keyId, clock);
    }

    /// <summary>Mints a compact assertion for one call: this service acting for <paramref name="tenantId"/>.</summary>
    public string Issue(
        string audience, string subject, string tenantId, IEnumerable<string> roles, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Assertion lifetime must be positive.");
        }

        var now = _clock.GetUtcNow();
        var header = new ServiceAssertionWire.Header(ServiceIdentity.Algorithm, ServiceIdentity.TokenType, keyId);
        var payload = new ServiceAssertionWire.Payload(
            issuer, audience, subject, tenantId,
            roles is null ? [] : [.. roles],
            now.ToUnixTimeSeconds(),
            now.ToUnixTimeSeconds(),
            now.Add(lifetime).ToUnixTimeSeconds(),
            Guid.NewGuid().ToString("N"));

        var headerSegment = ServiceAssertionWire.Encode(JsonSerializer.SerializeToUtf8Bytes(header, ServiceAssertionWire.Json));
        var payloadSegment = ServiceAssertionWire.Encode(JsonSerializer.SerializeToUtf8Bytes(payload, ServiceAssertionWire.Json));
        var signingInput = Encoding.ASCII.GetBytes($"{headerSegment}.{payloadSegment}");
        var signature = signingKey.SignData(signingInput, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return $"{headerSegment}.{payloadSegment}.{ServiceAssertionWire.Encode(signature)}";
    }
}

/// <summary>
/// Verifies compact service assertions against a set of trusted public keys (by <c>kid</c>), a fixed
/// expected issuer and audience, and the clock. Fail-closed: any parse, signature, algorithm, key,
/// issuer, audience, tenant, or expiry problem yields <see cref="ServiceAssertionValidationResult.Invalid"/>
/// with a specific reason code — there is no default-accept path. Supplied keys are owned by the caller.
/// </summary>
public sealed class ServiceAssertionValidator
{
    private readonly IReadOnlyDictionary<string, ECDsa> _trustedKeys;
    private readonly string _expectedIssuer;
    private readonly string _expectedAudience;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _clockSkew;

    public ServiceAssertionValidator(
        IReadOnlyDictionary<string, ECDsa> trustedKeysByKeyId,
        string expectedIssuer,
        string expectedAudience,
        TimeProvider? clock = null,
        TimeSpan? clockSkew = null)
    {
        ArgumentNullException.ThrowIfNull(trustedKeysByKeyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedIssuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedAudience);

        _trustedKeys = trustedKeysByKeyId;
        _expectedIssuer = expectedIssuer;
        _expectedAudience = expectedAudience;
        _clock = clock ?? TimeProvider.System;
        _clockSkew = clockSkew ?? TimeSpan.FromSeconds(60);
    }

    /// <summary>Builds a validator trusting a single PEM-encoded EC public key (the common one-caller case).</summary>
    public static ServiceAssertionValidator FromPem(
        string keyId, string publicKeyPem, string expectedIssuer, string expectedAudience,
        TimeProvider? clock = null, TimeSpan? clockSkew = null)
    {
        var key = ECDsa.Create();
        key.ImportFromPem(publicKeyPem);
        return new ServiceAssertionValidator(
            new Dictionary<string, ECDsa>(StringComparer.Ordinal) { [keyId] = key },
            expectedIssuer, expectedAudience, clock, clockSkew);
    }

    public ServiceAssertionValidationResult Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return ServiceAssertionValidationResult.Invalid(ServiceAssertionReasonCodes.Malformed);
        }

        var parts = token.Split('.');
        if (parts.Length != 3 || parts.Any(string.IsNullOrEmpty))
        {
            return ServiceAssertionValidationResult.Invalid(ServiceAssertionReasonCodes.Malformed);
        }

        ServiceAssertionWire.Header? header;
        ServiceAssertionWire.Payload? payload;
        byte[] signature;
        try
        {
            header = JsonSerializer.Deserialize<ServiceAssertionWire.Header>(
                ServiceAssertionWire.Decode(parts[0]), ServiceAssertionWire.Json);
            payload = JsonSerializer.Deserialize<ServiceAssertionWire.Payload>(
                ServiceAssertionWire.Decode(parts[1]), ServiceAssertionWire.Json);
            signature = ServiceAssertionWire.Decode(parts[2]);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return ServiceAssertionValidationResult.Invalid(ServiceAssertionReasonCodes.Malformed);
        }

        if (header is null || payload is null)
        {
            return ServiceAssertionValidationResult.Invalid(ServiceAssertionReasonCodes.Malformed);
        }

        // Pin the algorithm: never trust the header's alg to select a verification scheme (no alg confusion,
        // no "none"). Only ES256 is honoured.
        if (!string.Equals(header.Alg, ServiceIdentity.Algorithm, StringComparison.Ordinal))
        {
            return ServiceAssertionValidationResult.Invalid(ServiceAssertionReasonCodes.UnsupportedAlgorithm);
        }

        if (string.IsNullOrEmpty(header.Kid) || !_trustedKeys.TryGetValue(header.Kid, out var key))
        {
            return ServiceAssertionValidationResult.Invalid(ServiceAssertionReasonCodes.UnknownKey);
        }

        // Verify the signature before trusting a single claim in the payload.
        var signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
        if (!key.VerifyData(signingInput, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
        {
            return ServiceAssertionValidationResult.Invalid(ServiceAssertionReasonCodes.BadSignature);
        }

        if (!string.Equals(payload.Iss, _expectedIssuer, StringComparison.Ordinal))
        {
            return ServiceAssertionValidationResult.Invalid(ServiceAssertionReasonCodes.WrongIssuer);
        }

        if (!string.Equals(payload.Aud, _expectedAudience, StringComparison.Ordinal))
        {
            return ServiceAssertionValidationResult.Invalid(ServiceAssertionReasonCodes.WrongAudience);
        }

        var now = _clock.GetUtcNow();
        var expires = DateTimeOffset.FromUnixTimeSeconds(payload.Exp);
        var notBefore = DateTimeOffset.FromUnixTimeSeconds(payload.Nbf);
        if (now > expires + _clockSkew)
        {
            return ServiceAssertionValidationResult.Invalid(ServiceAssertionReasonCodes.Expired);
        }

        if (now < notBefore - _clockSkew)
        {
            return ServiceAssertionValidationResult.Invalid(ServiceAssertionReasonCodes.NotYetValid);
        }

        if (string.IsNullOrWhiteSpace(payload.Tenant))
        {
            return ServiceAssertionValidationResult.Invalid(ServiceAssertionReasonCodes.MissingTenant);
        }

        return ServiceAssertionValidationResult.Valid(new ServiceAssertion(
            payload.Iss,
            payload.Aud,
            payload.Sub,
            payload.Tenant,
            payload.Roles ?? [],
            DateTimeOffset.FromUnixTimeSeconds(payload.Iat),
            notBefore,
            expires,
            payload.Jti));
    }
}
