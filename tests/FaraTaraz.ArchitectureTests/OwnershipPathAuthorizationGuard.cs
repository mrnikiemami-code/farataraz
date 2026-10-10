namespace FaraTaraz.ArchitectureTests;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

/// <summary>
/// FT-SEC-007 durable guard: the Ingestion persistence write primitives
/// <c>SyncRunRepository.StartAsync</c> and <c>SourceRecordRepository.InsertOrUpdateAsync</c> must
/// be reached only through the approved ownership-authorized orchestration boundary.
///
/// <b>What it checks.</b> It reads the ACTUAL production source and flags any call to one of the
/// target write methods whose file is outside the owning layer
/// (<c>FaraTaraz.Modules.Ingestion.Infrastructure</c>). The Application use case that performs
/// the fail-closed <c>IAccountingSourceOwnership</c> check is the only approved orchestration
/// boundary; the write primitives themselves live in the Infrastructure layer. Any
/// Application/Host/Adapter file that invokes these writes is a write-path authorization bypass.
///
/// <b>Why real source, not naming/comments.</b> Before matching, the source is stripped of line
/// comments, block comments, string literals and char literals (line counts preserved) so a
/// comment or an XML doc cannot satisfy the guard. A match therefore reflects a REAL call edge in
/// the code, and a drift that renames a comment or moves a file still fails the guard. The target
/// method names (<c>StartAsync</c>/<c>InsertOrUpdateAsync</c>) are unique to the Ingestion
/// Infrastructure write primitives, so a call to them in a forbidden layer is unambiguous.
/// </summary>
public class OwnershipPathAuthorizationGuard
{
    /// <summary>
    /// Only the Ingestion Infrastructure layer owns its write primitives.
    /// </summary>
    public static readonly string ApprovedBoundaryFolder = "src/Modules/Ingestion/Ingestion.Infrastructure";

    private static readonly Regex Call = new(
        @"(?<receiver>[A-Za-z_][A-Za-z0-9_.]*)\.\s?(?<method>StartAsync|InsertOrUpdateAsync)\s*\(",
        RegexOptions.Compiled);

    /// <summary>
    /// Scans the shipped production source for forbidden callers of the ownership-write methods.
    /// Empty means every call originates inside the approved Infrastructure boundary.
    /// </summary>
    public static IReadOnlyList<string> DetectForbiddenWriteCalls(string repoRoot)
    {
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(repoRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(file) || IsTestSource(file, repoRoot) || IsApprovedCaller(file, repoRoot))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            var stripped = StripCommentsAndStrings(text);

            foreach (Match match in Call.Matches(stripped))
            {
                var relative = MakeRelative(file, repoRoot).Replace("\\", "/");
                var line = LineOf(text, (int)match.Index);
                violations.Add($"{relative}:{line} -> {match.Groups["method"].Value}");
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

    private static bool IsBuildOutput(string path)
        => path.Contains("\\bin\\", StringComparison.Ordinal) ||
           path.Contains("\\obj\\", StringComparison.Ordinal) ||
           path.Contains("/bin/", StringComparison.Ordinal) ||
           path.Contains("/obj/", StringComparison.Ordinal);

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

        // Approved: an Infrastructure-layer file that owns the write primitive.
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
