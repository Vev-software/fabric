using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Vev.Fabric.Contracts.Entitlements;

/// <summary>
/// The signed snapshot document distributed by the control plane.
/// </summary>
public sealed record SignedEntitlementSnapshot(
    string KeyId,
    string Algorithm,
    string PayloadJson,
    string Signature);

/// <summary>
/// Signature verification for signed snapshot documents.
/// </summary>
public interface IEntitlementSignatureVerifier
{
    bool Verify(SignedEntitlementSnapshot snapshot);
}

/// <summary>
/// Verifies signed snapshots with HMAC-SHA256 using configured trust anchors.
/// </summary>
public sealed class HmacSha256SignatureVerifier(IReadOnlyDictionary<string, byte[]> keys) : IEntitlementSignatureVerifier
{
    public bool Verify(SignedEntitlementSnapshot snapshot)
    {
        if (!string.Equals(snapshot.Algorithm, "HS256", StringComparison.Ordinal))
        {
            return false;
        }

        if (!keys.TryGetValue(snapshot.KeyId, out var key))
        {
            return false;
        }

        using var hmac = new HMACSHA256(key);
        var payloadBytes = Encoding.UTF8.GetBytes(snapshot.PayloadJson);
        var computed = hmac.ComputeHash(payloadBytes);

        byte[] signatureBytes;

        try
        {
            signatureBytes = Convert.FromBase64String(snapshot.Signature);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(computed, signatureBytes);
    }
}

/// <summary>
/// Result of verifying and decoding a signed snapshot document.
/// </summary>
public sealed record SnapshotVerificationResult(
    bool IsValid,
    EntitlementSnapshot? Snapshot,
    string ReasonCode)
{
    public static SnapshotVerificationResult Valid(EntitlementSnapshot snapshot) =>
        new(true, snapshot, ReasonCodes.EntitlementGranted);

    public static SnapshotVerificationResult Invalid(string reasonCode) =>
        new(false, null, reasonCode);
}

/// <summary>
/// Parses the JSON payload and validates its signature before the evaluator accepts it.
/// </summary>
public sealed class JsonSignedEntitlementSnapshotVerifier(IEntitlementSignatureVerifier signatureVerifier)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SnapshotVerificationResult Verify(SignedEntitlementSnapshot snapshot)
    {
        if (!signatureVerifier.Verify(snapshot))
        {
            return SnapshotVerificationResult.Invalid(ReasonCodes.EntitlementSnapshotInvalid);
        }

        try
        {
            var decoded = JsonSerializer.Deserialize<EntitlementSnapshot>(snapshot.PayloadJson, SerializerOptions);
            return decoded is null
                ? SnapshotVerificationResult.Invalid(ReasonCodes.EntitlementSnapshotInvalid)
                : SnapshotVerificationResult.Valid(decoded);
        }
        catch (JsonException)
        {
            return SnapshotVerificationResult.Invalid(ReasonCodes.EntitlementSnapshotInvalid);
        }
    }
}
