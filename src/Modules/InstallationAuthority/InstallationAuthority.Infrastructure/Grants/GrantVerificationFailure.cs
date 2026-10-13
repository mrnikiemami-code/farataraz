namespace FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants;

/// <summary>
/// Why a grant was rejected during stage-A verification (signature + claim structure only).
///
/// These are verification failures, NOT authorization outcomes: a token can be a perfectly valid,
/// signed grant and still not be authorized for a job. Job authorization, possession proof,
/// expiry-at-admission and offline policy belong to stage B and are deliberately absent here.
/// Not public: stage-A verification is internal to the authority.
/// </summary>
internal enum GrantVerificationFailure
{
    /// <summary>The grant is structurally valid and its signature verified. Not an authorization.</summary>
    None = 0,

    /// <summary>Encoding/structure/size/depth/duplicate-property failure, or malformed JSON.</summary>
    Malformed = 1,

    /// <summary>The header key id does not resolve in the pinned trust store.</summary>
    UnknownKey = 2,

    /// <summary>The header declares an algorithm other than ES256, or a wrong type.</summary>
    InvalidAlgorithm = 3,

    /// <summary>The signature does not verify over the original header.payload bytes.</summary>
    InvalidSignature = 4,

    /// <summary>The payload is well-formed JSON but its claims are missing, mistyped or out of bounds.</summary>
    InvalidClaims = 5,
}
