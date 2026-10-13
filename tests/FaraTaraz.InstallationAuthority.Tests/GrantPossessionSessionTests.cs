namespace FaraTaraz.InstallationAuthority.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants;

/// <summary>
/// Stage-B proof-of-possession tests: fresh challenge, canonical message binding, single-use atomic
/// session, P-256/ES256 verification against the claims' installation key, and failure/cancellation
/// behaviour. Success here is a job-bound possession proof only — never authorization.
/// </summary>
public sealed class GrantPossessionSessionTests
{
    private static GrantJobRequest Request(
        string installationId = "install-1", string sourceId = "src-1", string capability = "Customers")
        => new(installationId, new AccountingSourceId(sourceId), capability);

    [Fact]
    public void Correct_installation_key_succeeds_and_binds_the_exact_job()
    {
        var (claims, installation) = TestGrantFactory.Create();
        using (installation)
        {
            var session = GrantPossessionSession.Begin(claims, Request(), default);
            var signature = TestGrantFactory.Sign(installation, session.CopySignedMessage());

            var result = session.Complete(signature, default);

            Assert.True(result.IsSucceeded);
            Assert.Equal("tenant-1", result.TenantId.Value);
            Assert.Equal("grant-1", result.GrantId);
            Assert.Equal("install-1", result.InstallationId);
            Assert.Equal("src-1", result.SourceId.Value);
            Assert.Equal("Customers", result.Capability);
        }
    }

    [Fact]
    public void Wrong_private_key_fails()
    {
        var (claims, installation) = TestGrantFactory.Create();
        using (installation)
        using (var other = TestGrantSigner.NewCompanyKey())
        {
            var session = GrantPossessionSession.Begin(claims, Request(), default);
            var signature = TestGrantFactory.Sign(other, session.CopySignedMessage());

            Assert.Equal(GrantPossessionFailure.InvalidSignature, session.Complete(signature, default).Failure);
        }
    }

    [Theory]
    [InlineData(63)]
    [InlineData(65)]
    [InlineData(0)]
    public void Malformed_signature_length_fails(int length)
    {
        var (claims, installation) = TestGrantFactory.Create();
        using (installation)
        {
            var session = GrantPossessionSession.Begin(claims, Request(), default);

            Assert.Equal(GrantPossessionFailure.MalformedSignature,
                session.Complete(new byte[length], default).Failure);
        }
    }

    [Fact]
    public void A_session_accepts_at_most_one_completion_attempt()
    {
        var (claims, installation) = TestGrantFactory.Create();
        using (installation)
        {
            var session = GrantPossessionSession.Begin(claims, Request(), default);
            var signature = TestGrantFactory.Sign(installation, session.CopySignedMessage());

            Assert.True(session.Complete(signature, default).IsSucceeded);
            Assert.Equal(GrantPossessionFailure.SessionConsumed, session.Complete(signature, default).Failure);
        }
    }

    [Fact]
    public void A_failed_attempt_also_consumes_the_session()
    {
        var (claims, installation) = TestGrantFactory.Create();
        using (installation)
        using (var other = TestGrantSigner.NewCompanyKey())
        {
            var session = GrantPossessionSession.Begin(claims, Request(), default);
            var good = TestGrantFactory.Sign(installation, session.CopySignedMessage());
            var bad = TestGrantFactory.Sign(other, session.CopySignedMessage());

            Assert.Equal(GrantPossessionFailure.InvalidSignature, session.Complete(bad, default).Failure);
            Assert.Equal(GrantPossessionFailure.SessionConsumed, session.Complete(good, default).Failure);
        }
    }

    [Fact]
    public void Proof_from_another_session_cannot_complete_this_one()
    {
        var (claims, installation) = TestGrantFactory.Create();
        using (installation)
        {
            var sessionA = GrantPossessionSession.Begin(claims, Request(), default);
            var sessionB = GrantPossessionSession.Begin(claims, Request(), default);

            var proofForA = TestGrantFactory.Sign(installation, sessionA.CopySignedMessage());

            Assert.Equal(GrantPossessionFailure.InvalidSignature, sessionB.Complete(proofForA, default).Failure);
            Assert.True(sessionA.Complete(proofForA, default).IsSucceeded);
        }
    }

    [Fact]
    public void Changed_source_capability_or_installation_changes_the_signed_message()
    {
        // Deterministic encoding comparison with a fixed nonce, so ONLY the named field differs.
        var nonce = new byte[32];

        var baseline = PossessionMessage.Encode("grant-1", "tenant-1", "install-1", "src-1", "Customers", nonce);
        var changedSource = PossessionMessage.Encode("grant-1", "tenant-1", "install-1", "src-2", "Customers", nonce);
        var changedCapability = PossessionMessage.Encode("grant-1", "tenant-1", "install-1", "src-1", "Products", nonce);
        var changedInstallation = PossessionMessage.Encode("grant-1", "tenant-1", "install-2", "src-1", "Customers", nonce);

        Assert.NotEqual(baseline, changedSource);
        Assert.NotEqual(baseline, changedCapability);
        Assert.NotEqual(baseline, changedInstallation);

        // Length prefixes prevent ambiguous concatenation of adjacent fields.
        var ambiguousA = PossessionMessage.Encode("grant-1", "tenant-1", "inst", "all-1", "Customers", nonce);
        var ambiguousB = PossessionMessage.Encode("grant-1", "tenant-1", "install-1", "", "Customers", nonce);
        Assert.NotEqual(ambiguousA, ambiguousB);
    }

    [Fact]
    public async Task Concurrent_completion_attempts_yield_at_most_one_success()
    {
        var (claims, installation) = TestGrantFactory.Create();
        using (installation)
        {
            var session = GrantPossessionSession.Begin(claims, Request(), default);
            var signature = TestGrantFactory.Sign(installation, session.CopySignedMessage());

            var results = await Task.WhenAll(Enumerable.Range(0, 8)
                .Select(_ => Task.Run(() => session.Complete(signature, default))));

            Assert.Equal(1, results.Count(r => r.IsSucceeded));
            Assert.Equal(7, results.Count(r => r.Failure == GrantPossessionFailure.SessionConsumed));
        }
    }

    [Fact]
    public void Mutating_returned_copies_does_not_alter_retained_session_state()
    {
        var (claims, installation) = TestGrantFactory.Create();
        using (installation)
        {
            var session = GrantPossessionSession.Begin(claims, Request(), default);

            var messageCopy = session.CopySignedMessage();
            var challengeCopy = session.CopyChallenge();
            for (var i = 0; i < messageCopy.Length; i++)
            {
                messageCopy[i] ^= 0xFF;
            }

            for (var i = 0; i < challengeCopy.Length; i++)
            {
                challengeCopy[i] ^= 0xFF;
            }

            // The retained message is unchanged: a signature over a FRESH copy verifies.
            var signature = TestGrantFactory.Sign(installation, session.CopySignedMessage());
            Assert.True(session.Complete(signature, default).IsSucceeded);
        }
    }

    [Fact]
    public void Cancellation_never_yields_a_successful_proof()
    {
        var (claims, installation) = TestGrantFactory.Create();
        using (installation)
        {
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();

            Assert.ThrowsAny<OperationCanceledException>(
                () => GrantPossessionSession.Begin(claims, Request(), cancelled.Token));

            var session = GrantPossessionSession.Begin(claims, Request(), default);
            Assert.ThrowsAny<OperationCanceledException>(
                () => session.Complete(new byte[64], cancelled.Token));
        }
    }

    [Fact]
    public void Stage_B_types_are_internal_and_expose_no_public_trust_api()
    {
        var assembly = typeof(GrantPossessionSession).Assembly;

        var names = new[]
        {
            typeof(GrantJobRequest), typeof(GrantAuthorityExpectations), typeof(GrantJobPolicy),
            typeof(GrantPossessionSession), typeof(GrantPossessionResult), typeof(PossessionMessage),
        };

        Assert.All(names, t =>
        {
            Assert.False(t.IsPublic);
            Assert.Equal("FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants", t.Namespace);
        });

        // The whole verification namespace still exposes no public type.
        Assert.Empty(assembly.GetTypes()
            .Where(t => t.Namespace == "FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants"
                        && (t.IsPublic || t.IsNestedPublic))
            .Select(t => t.FullName)
            .ToList());
    }
}
