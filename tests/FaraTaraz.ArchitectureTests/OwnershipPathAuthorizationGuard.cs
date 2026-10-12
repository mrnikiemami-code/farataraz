namespace FaraTaraz.ArchitectureTests;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

/// <summary>
/// FT-SEC-007 durable guard (redesigned, M2) for the Ingestion persistence write path.
///
/// <b>Rule 1 — boundary.</b> The W2 persistence write primitives
/// (<c>SyncRunRepository.StartAsync</c>, <c>SourceRecordRepository.InsertOrUpdateAsync</c>,
/// <c>SyncCheckpointRepository.SaveAsync</c>, <c>IngestionUnitOfWork.ExecuteAsync</c>) may be
/// called ONLY from the approved Infrastructure boundary
/// (<c>src/Modules/Ingestion/Ingestion.Infrastructure</c>: adapters, repositories, unit of work).
/// Any direct call from Application/Host/Adapters/elsewhere is a write-path bypass. There is no
/// interface-name exemption and no allowlist.
///
/// <b>Rule 2 — admission before reaching persistence through the port.</b> Application orchestration
/// reaches persistence through the write port, whose transactional entry is
/// <c>ExecuteInTransactionAsync</c> (NOT a write primitive, so it is not part of Rule 1). A file
/// outside the boundary that invokes that entry must also make REAL CALLS to tenant admission
/// (<c>AssertTrusted(</c>) and fail-closed ownership admission (<c>IsOwnedByAsync(</c>). Comments,
/// string literals, unused fields and a bare interface name are NOT evidence (the source is
/// comment/string-stripped before matching, and a call is required, not a reference).
///
/// <b>Documented limit — ordering is not proven lexically.</b> A lexical scan cannot prove that the
/// admission calls happen BEFORE the port entry, nor that their results gate it. Rule 2 is a
/// NECESSARY condition only. The ordering/gating invariant (trusted tenant + owned source before
/// any provider call or persistence) is enforced by executable real-PostgreSQL negative integration
/// tests (<c>IngestionRunCoordinatorTests</c>: untrusted tenant / not-owned source / ownership lookup
/// failure each assert ZERO provider calls and ZERO writes). Never treat this guard as a full
/// authorization proof.
///
/// The source is stripped of line comments, block comments, string/char literals (line counts
/// preserved) before matching, so a comment or XML doc cannot satisfy the guard.
/// </summary>
public class OwnershipPathAuthorizationGuard
{
    /// <summary>
    /// Only the Ingestion Infrastructure layer owns its write primitives.
    /// </summary>
    public static readonly string ApprovedBoundaryFolder = "src/Modules/Ingestion/Ingestion.Infrastructure";

    /// <summary>
    /// Application write-port transactional entry. Invoking it is the approved way for Application
    /// orchestration to reach persistence; it is NOT a persistence write primitive, so a caller
    /// outside the boundary that invokes it must also perform tenant + ownership admission (rule 2).
    /// </summary>
    public static readonly string PortTransactionalEntry = "ExecuteInTransactionAsync";

    /// <summary>The trusted execution-context admission call a write-port consumer must perform.</summary>
    public static readonly string TenantAdmissionCall = "AssertTrusted(";

    /// <summary>The fail-closed ownership-admission call a write-port consumer must perform.</summary>
    public static readonly string OwnershipAdmissionCall = "IsOwnedByAsync(";

    private static readonly Regex Call = new(
        @"(?<receiver>[A-Za-z_][A-Za-z0-9_.]*)\.\s?(?<method>StartAsync|InsertOrUpdateAsync|SaveAsync|ExecuteAsync)\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex PortEntry = new(
        @"\.\s?ExecuteInTransactionAsync\s*\(", RegexOptions.Compiled);

    /// <summary>
    /// Scans the shipped production source for forbidden callers of the ownership-write methods.
    /// Empty means every call originates inside the approved Infrastructure boundary.
    /// </summary>
    public static IReadOnlyList<string> DetectForbiddenWriteCalls(string repoRoot)
    {
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(repoRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(file) || IsTestSource(file, repoRoot))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            var stripped = StripCommentsAndStrings(text);
            var relative = MakeRelative(file, repoRoot).Replace("\\", "/");
            var insideBoundary = IsApprovedCaller(file, repoRoot);

            // Rule 1: the W2 write surface is callable only inside the approved Infrastructure
            // boundary. No interface-name exemption, no allowlist.
            if (!insideBoundary)
            {
                foreach (Match match in Call.Matches(stripped))
                {
                    var line = LineOf(text, (int)match.Index);
                    violations.Add(
                        $"{relative}:{line} -> {match.Groups["method"].Value} " +
                        "(direct persistence write outside the approved boundary)");
                }
            }

            // Rule 2: a write-port entry call outside the boundary requires REAL tenant-admission
            // and ownership-admission calls. Necessary condition only (ordering is enforced by the
            // real-PostgreSQL negative integration tests; see the type doc).
            if (!insideBoundary && PortEntry.IsMatch(stripped))
            {
                var hasTenantAdmission = stripped.Contains(TenantAdmissionCall, StringComparison.Ordinal);
                var hasOwnershipAdmission = stripped.Contains(OwnershipAdmissionCall, StringComparison.Ordinal);

                if (!hasTenantAdmission || !hasOwnershipAdmission)
                {
                    violations.Add(
                        $"{relative} -> {PortTransactionalEntry} invoked without complete admission " +
                        $"(tenant-admission={hasTenantAdmission}, ownership-admission={hasOwnershipAdmission})");
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// A file is an approved caller only when it lives inside the Ingestion Infrastructure layer
    /// that owns the write primitives.
    /// </summary>
    private static bool IsApprovedCaller(string file, string repoRoot)
    {
        var relative = MakeRelative(file, repoRoot);
        return relative.Replace("\\", "/").StartsWith(ApprovedBoundaryFolder.Replace("\\", "/"),
            StringComparison.Ordinal);
    }

    private static bool IsBuildOutput(string path) => SourceTreePaths.IsBuildOutput(path);

    /// <summary>
    /// File path relative to the repo root, without relying on <c>Path.GetRelative</c>.
    /// </summary>
    private static string MakeRelative(string file, string repoRoot)
    {
        var root = repoRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? repoRoot
            : repoRoot + Path.DirectorySeparatorChar;

        return file.StartsWith(root, StringComparison.Ordinal)
            ? file.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar)
            : file;
    }

    /// <summary>
    /// Test projects legitimately construct and exercise the repositories in negative tests, so
    /// production-source scanning excludes any file that lives under a <c>tests</c> directory.
    /// </summary>
    private static bool IsTestSource(string file, string repoRoot)
    {
        var relative = MakeRelative(file, repoRoot).Replace("\\", "/");
        var segments = relative.Split('/');
        return segments.Any(s =>
            s.Equals("tests", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("test", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Removes comments and string/char literals while preserving length and newlines, so call
    /// detection cannot be satisfied by textual noise and line numbers stay accurate.
    /// </summary>
    internal static string StripCommentsAndStrings(string source)
    {
        var builder = new StringBuilder(source.Length);
        var index = 0;
        var length = source.Length;

        while (index < length)
        {
            var c = source[index];

            // Line comment.
            if (c == '/' && index + 1 < length && source[index + 1] == '/')
            {
                index += 2;
                while (index < length && source[index] != '\n')
                {
                    index++;
                }
                continue;
            }

            // Block comment.
            if (c == '/' && index + 1 < length && source[index + 1] == '*')
            {
                index += 2;
                while (index + 1 < length && !(source[index] == '*' && source[index + 1] == '/'))
                {
                    if (source[index] != '\n')
                    {
                        builder.Append(' ');
                    }
                    else
                    {
                        builder.Append('\n');
                    }

                    index++;
                }

                index += 2;
                continue;
            }

            // String literal.
            if (c == '"')
            {
                index++;
                while (index < length)
                {
                    var s = source[index];
                    if (s == '\\')
                    {
                        index += 2;
                    }
                    else if (s == '"' || s == '\n')
                    {
                        break;
                    }
                    else
                    {
                        index++;
                    }
                }

                index++;
                continue;
            }

            // Char literal.
            if (c == '\'')
            {
                index++;
                while (index < length && source[index] != '\'')
                {
                    if (source[index] == '\\')
                    {
                        index += 2;
                    }
                    else
                    {
                        index++;
                    }
                }

                index++;
                continue;
            }

            builder.Append(c);
            index++;
        }

        return builder.ToString();
    }

    private static int LineOf(string source, int charIndex)
    {
        var line = 1;
        for (var i = 0; i < charIndex && i < source.Length; i++)
        {
            if (source[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    internal static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "FaraTaraz.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Repository root (FaraTaraz.sln) not found.");
    }
}

/// <summary>
/// Test surface for <see cref="OwnershipPathAuthorizationGuard"/>: GREEN on the shipped codebase,
/// RED via a synthetic violating source file, plus a comment-stripping proof.
/// </summary>
public class OwnershipPathAuthorizationGuardTests
{
    /// <summary>
    /// GREEN. The shipped production source has no forbidden caller of the ownership-write
    /// primitives.
    /// </summary>
    [Fact]
    public void Production_write_path_has_no_forbidden_caller()
    {
        var violations = OwnershipPathAuthorizationGuard.DetectForbiddenWriteCalls(
            OwnershipPathAuthorizationGuard.FindRepoRoot());

        Assert.Empty(violations);
    }

    /// <summary>
    /// RED mutation. A synthetic Application-layer file that calls
    /// <c>SyncRunRepository.StartAsync</c> must be reported as a bypass; a file inside the
    /// Infrastructure layer calling the same method is approved.
    /// </summary>
    [Fact]
    public void Guard_flags_forbidden_layer_call_but_approves_infrastructure_call()
    {
        var root = CreateTempRepoRoot();

        // Forbidden: an Application-layer caller that invokes the write primitive directly.
        WriteFile(root, "src/Modules/Ingestion/Ingestion.Application/BadCaller.cs",
            @"namespace FaraTaraz.Modules.Ingestion.Application;
              class BadCaller
              {
                  void Run() { _repo.StartAsync(default, default, ""c"", ""m"", default); }
              }");

        // Approved: an Infrastructure adapter/repository that owns the write primitive. Ownership
        // admission is NOT required here (rule 2 binds it to the Application port caller).
        WriteFile(root, "src/Modules/Ingestion/Ingestion.Infrastructure/GoodOwner.cs",
            @"namespace FaraTaraz.Modules.Ingestion.Infrastructure;
              class GoodOwner
              {
                  void Run() { _repo.InsertOrUpdateAsync(default, default, null, default); }
              }");

        var violations = OwnershipPathAuthorizationGuard.DetectForbiddenWriteCalls(root);

        Assert.Single(violations);
        Assert.Contains("Ingestion.Application/BadCaller.cs", violations[0]);
        Assert.Contains("StartAsync", violations[0]);

        DeleteTempRepoRoot(root);
    }

    /// <summary>
    /// A call mentioned only inside a comment or a string literal does NOT satisfy the guard.
    /// </summary>
    [Fact]
    public void Comment_or_string_does_not_satisfy_the_guard()
    {
        var stripped = OwnershipPathAuthorizationGuard.StripCommentsAndStrings(
            "// repo.StartAsync(x);\nvar s = \"repo.InsertOrUpdateAsync(x)\"; /* real */ 0;");

        Assert.DoesNotContain("StartAsync", stripped);
        Assert.DoesNotContain("InsertOrUpdateAsync", stripped);
    }

    /// <summary>
    /// The atomic unit of work and the checkpoint save are part of the same protected write
    /// surface: an Application-layer caller of <c>IngestionUnitOfWork.ExecuteAsync</c> (or a direct
    /// <c>SaveAsync</c>) is a write-path bypass and is flagged exactly like the record/run writes.
    /// </summary>
    [Fact]
    public void Guard_flags_a_forbidden_layer_caller_of_the_unit_of_work_or_checkpoint_save()
    {
        var root = CreateTempRepoRoot();

        WriteFile(root, "src/Modules/Ingestion/Ingestion.Application/Engine.cs",
            @"namespace FaraTaraz.Modules.Ingestion.Application;
              class Engine
              {
                  void Run() { _uow.ExecuteAsync(null); _checkpoints.SaveAsync(default); }
              }");

        var violations = OwnershipPathAuthorizationGuard.DetectForbiddenWriteCalls(root);

        Assert.Contains(violations, v => v.Contains("ExecuteAsync", StringComparison.Ordinal));
        Assert.Contains(violations, v => v.Contains("SaveAsync", StringComparison.Ordinal));

        DeleteTempRepoRoot(root);
    }

    /// <summary>
    /// Rule 1: an Infrastructure adapter that maps the write surface (including the unit of work)
    /// is APPROVED without referencing the ownership port — admission belongs to the Application
    /// port caller (rule 2), not to the adapter.
    /// </summary>
    [Fact]
    public void Guard_allows_an_approved_boundary_adapter_that_maps_the_write_surface()
    {
        var root = CreateTempRepoRoot();

        WriteFile(root, "src/Modules/Ingestion/Ingestion.Infrastructure/Persistence/Adapter.cs",
            @"namespace FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;
              class Adapter
              {
                  void Run()
                  {
                      _uow.ExecuteAsync(null);
                      _runs.StartAsync(default, default, ""c"", ""m"", default);
                      _records.InsertOrUpdateAsync(default, default, null, default);
                      _checkpoints.SaveAsync(default);
                  }
              }");

        var violations = OwnershipPathAuthorizationGuard.DetectForbiddenWriteCalls(root);

        Assert.Empty(violations);
        DeleteTempRepoRoot(root);
    }

    /// <summary>Rule 2: a write-port entry without tenant and ownership admission is flagged.</summary>
    [Fact]
    public void Guard_flags_a_write_port_entry_without_tenant_or_ownership_admission()
    {
        var root = CreateTempRepoRoot();

        WriteFile(root, "src/Modules/Ingestion/Ingestion.Application/Run.cs",
            @"namespace FaraTaraz.Modules.Ingestion.Application;
              class Run { void Go() { _store.ExecuteInTransactionAsync(null); } }");

        var violations = OwnershipPathAuthorizationGuard.DetectForbiddenWriteCalls(root);

        Assert.Single(violations);
        Assert.Contains("tenant-admission=False", violations[0]);
        Assert.Contains("ownership-admission=False", violations[0]);
        DeleteTempRepoRoot(root);
    }

    /// <summary>Rule 2: tenant admission alone is insufficient — ownership admission is required.</summary>
    [Fact]
    public void Guard_flags_a_write_port_entry_with_tenant_but_no_ownership_admission()
    {
        var root = CreateTempRepoRoot();

        WriteFile(root, "src/Modules/Ingestion/Ingestion.Application/Run.cs",
            @"namespace FaraTaraz.Modules.Ingestion.Application;
              class Run { void Go() { _tenant.AssertTrusted(); _store.ExecuteInTransactionAsync(null); } }");

        var violations = OwnershipPathAuthorizationGuard.DetectForbiddenWriteCalls(root);

        Assert.Single(violations);
        Assert.Contains("ownership-admission=False", violations[0]);
        DeleteTempRepoRoot(root);
    }

    /// <summary>Rule 2: a write-port entry with real tenant AND ownership admission is allowed.</summary>
    [Fact]
    public void Guard_allows_a_write_port_entry_with_tenant_and_ownership_admission()
    {
        var root = CreateTempRepoRoot();

        WriteFile(root, "src/Modules/Ingestion/Ingestion.Application/Run.cs",
            @"namespace FaraTaraz.Modules.Ingestion.Application;
              class Run
              {
                  async System.Threading.Tasks.Task Go()
                  {
                      _tenant.AssertTrusted();
                      await _ownership.IsOwnedByAsync(default, default, default);
                      await _store.ExecuteInTransactionAsync(null);
                  }
              }");

        var violations = OwnershipPathAuthorizationGuard.DetectForbiddenWriteCalls(root);

        Assert.Empty(violations);
        DeleteTempRepoRoot(root);
    }

    /// <summary>
    /// Rule 2 evidence must be a CALL: a comment, an unused field or a bare interface name is NOT
    /// admission.
    /// </summary>
    [Fact]
    public void Guard_does_not_treat_a_comment_field_or_interface_name_as_admission()
    {
        var root = CreateTempRepoRoot();

        WriteFile(root, "src/Modules/Ingestion/Ingestion.Application/Run.cs",
            @"namespace FaraTaraz.Modules.Ingestion.Application;
              class Run
              {
                  // _tenant.AssertTrusted(); await _ownership.IsOwnedByAsync(...) -- NOT evidence
                  private readonly IAccountingSourceOwnership _ownership = null!;
                  void Go() { _store.ExecuteInTransactionAsync(null); }
              }");

        var violations = OwnershipPathAuthorizationGuard.DetectForbiddenWriteCalls(root);

        Assert.Single(violations);
        Assert.Contains("tenant-admission=False", violations[0]);
        Assert.Contains("ownership-admission=False", violations[0]);
        DeleteTempRepoRoot(root);
    }

    private static string CreateTempRepoRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "owguard_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteFile(string root, string relative, string content)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static void DeleteTempRepoRoot(string root)
    {
        try
        {
            Directory.Delete(root, true);
        }
        catch (Exception)
        {
            // Best-effort cleanup of the temp mutation.
        }
    }
}
