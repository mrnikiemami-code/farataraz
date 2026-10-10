namespace FaraTaraz.ArchitectureTests;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

/// <summary>
/// Durable trust-boundary guards (FT-SEC-006 / FT-GUARD-002).
///
/// These enforce that tenant authority cannot be reached by bypassing repository enforcement:
///   A. concrete EF Core contexts (<c>IngestionDbContext</c> / <c>AccountingSourcesDbContext</c>)
///      are referenced only inside one of the two real Infrastructure project roots
///      (<c>Modules/Ingestion/Ingestion.Infrastructure</c>,
///      <c>Modules/AccountingSources/AccountingSources.Infrastructure</c>) and their descendants
///      (repository/oracle/design-time tooling/migrations). No other production project may name
///      them, so non-Infrastructure code cannot construct or query a tenant-owned context directly.
///   B. trusted-context minting (<c>TenantContext.FromAuthenticatedPrincipal</c> /
///      <c>new TenantContext</c>) happens only inside <c>src/BuildingBlocks</c>, the trusted
///      foundation. No other production project can establish an authoritative tenant.
///
/// The guards scan the actual <c>src/**</c> tree (excluding bin/obj); tests/ is outside src and
/// is excluded automatically. The core logic is a pure function of (relative path, source) so the
/// same code path is exercised by synthetic negative/positive cases. Matching is performed on a
/// lexical scan that strips comments and string/char literals first, so a comment or string that
/// merely mentions these identifiers does not trip the guard. Interpolated strings are handled so
/// that literal text is stripped while expression content inside <c>{ ... }</c> is preserved as
/// code, and a missing closing delimiter never swallows the rest of the file.
/// </summary>
public sealed class TenantBoundaryGuards
{
    private const string DbContextTypeIngestion = "IngestionDbContext";
    private const string DbContextTypeAccounting = "AccountingSourcesDbContext";
    private const string TenantContextName = "TenantContext";
    private const string MintFactoryName = "FromAuthenticatedPrincipal";

    private static readonly string[] DbContextTypeNames =
    {
        DbContextTypeIngestion,
        DbContextTypeAccounting,
    };

    // Only the two real production Infrastructure project roots (and their descendants) are allowed
    // to name concrete DbContext types. A nested folder whose name ends in ".Infrastructure" under
    // another project (e.g. Application/Escape.Infrastructure) is NOT an allowed root and stays
    // forbidden.
    private static readonly string[] InfrastructureProjectRoots =
    {
        "Modules/Ingestion/Ingestion.Infrastructure",
        "Modules/AccountingSources/AccountingSources.Infrastructure",
    };

    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string SrcRoot = Path.Combine(RepoRoot, "src");

    /// <summary>
    /// Pure guard entry point: given (relativeDirectory, source) inputs, return the list of
    /// violations. relativeDirectory is the production path under <c>src/</c> using forward slashes.
    /// This is shared by the real-production-tree fact and the synthetic proof tests.
    /// </summary>
    public static IReadOnlyList<string> FindViolations(
        IEnumerable<(string RelativeDir, string Source)> inputs)
    {
        var violations = new List<string>();

        foreach (var (relativeDir, source) in inputs)
        {
            var identifiers = ExtractIdentifiers(StripCommentsAndStrings(source));

            // Concrete DbContext types are allowed only inside the two real Infrastructure roots.
            if (!IsInfrastructureDirectory(relativeDir))
            {
                var dbContext = identifiers
                    .Where(id => DbContextTypeNames.Contains(id))
                    .Select(id => $"{relativeDir}: concrete DbContext '{id}' referenced outside a .Infrastructure project");

                violations.AddRange(dbContext);
            }

            // Trusted-context minting is allowed only inside BuildingBlocks.
            if (!IsBuildingBlocksDirectory(relativeDir))
            {
                var minting = identifiers
                    .SelectMany((id, index) => MintingMatch(id, index, identifiers))
                    .Select(_ => $"{relativeDir}: trusted TenantContext mint outside BuildingBlocks");

                violations.AddRange(minting);
            }
        }

        return violations;
    }

    private static IEnumerable<string> MintingMatch(
        string id, int index, IReadOnlyList<string> identifiers)
    {
        if (id != TenantContextName)
        {
            yield break;
        }

        var previous = index > 0 ? identifiers[index - 1] : string.Empty;
        var next = index + 1 < identifiers.Count ? identifiers[index + 1] : string.Empty;

        // "new TenantContext" or "TenantContext.FromAuthenticatedPrincipal"
        if (previous == "new" || next == MintFactoryName)
        {
            yield return string.Empty;
        }
    }

    [Fact]
    public void Production_tree_never_references_concrete_dbcontext_outside_infrastructure()
    {
        var inputs = ProductionInputs().ToList();

        Assert.Empty(FindViolations(inputs));
    }

    [Fact]
    public void Production_tree_never_mints_trusted_context_outside_building_blocks()
    {
        var inputs = ProductionInputs().ToList();

        Assert.Empty(FindViolations(inputs));
    }

    [Fact]
    public void Synthetic_non_infrastructure_dbcontext_reference_is_rejected()
    {
        var violations = FindViolations(new[]
        {
            ("Modules/Ingestion/Ingestion.Application",
                "namespace X;\nclass B {\n    void M(IngestionDbContext ctx) { var _ = ctx; }\n}"),
        });

        Assert.NotEmpty(violations);
    }

    [Fact]
    public void Synthetic_non_building_blocks_trusted_mint_is_rejected()
    {
        var factory = FindViolations(new[]
        {
            ("Modules/Ingestion/Ingestion.Application",
                "namespace X;\nclass B {\n    void M(TenantId t) { var c = TenantContext.FromAuthenticatedPrincipal(t); }\n}"),
        });

        Assert.NotEmpty(factory);

        var constructor = FindViolations(new[]
        {
            ("Modules/Ingestion/Ingestion.Application",
                "namespace X;\nclass B {\n    void M() { var c = new TenantContext(default, TenantContextOrigin.AuthenticatedPrincipal); }\n}"),
        });

        Assert.NotEmpty(constructor);
    }

    [Fact]
    public void Synthetic_comment_and_string_occurrences_do_not_trigger_the_guard()
    {
        var violations = FindViolations(new[]
        {
            ("Modules/Ingestion/Ingestion.Application",
                "// IngestionDbContext must not appear here\n" +
                "/* TenantContext.FromAuthenticatedPrincipal(t) is only text */\n" +
                "namespace X;\nclass B {\n" +
                "    string a = \"new TenantContext(x)\";\n" +
                "    string b = @\"TenantContext.FromAuthenticatedPrincipal(t)\";\n" +
                "    char c = 'T';\n" +
                "    string raw = \"\"\"IngestionDbContext in a raw string\"\"\";\n" +
                "    void M() { var _ = a + b + c; }\n" +
                "}"),
        });

        Assert.Empty(violations);
    }

    [Fact]
    public void Synthetic_forbidden_identifier_after_interpolated_string_is_detected()
    {
        // The interpolated string must close on its own delimiter; the identifier after it is code.
        var violations = FindViolations(new[]
        {
            ("Modules/Ingestion/Ingestion.Application",
                "namespace X;\nclass B {\n" +
                "    void M() {\n" +
                "        var s = $\"literal text here\";\n" +
                "        IngestionDbContext ctx = null;\n" +
                "    }\n" +
                "}"),
        });

        Assert.NotEmpty(violations);
    }

    [Fact]
    public void Synthetic_forbidden_identifier_inside_expression_detected_but_literal_text_ignored()
    {
        // Identifier inside { ... } is expression code and must be detected.
        var expression = FindViolations(new[]
        {
            ("Modules/Ingestion/Ingestion.Application",
                "namespace X;\nclass B {\n" +
                "    void M() { var s = $\"value: {IngestionDbContext} end\"; }\n" +
                "}"),
        });

        Assert.NotEmpty(expression);

        // The same words as literal text are ignored.
        var literal = FindViolations(new[]
        {
            ("Modules/Ingestion/Ingestion.Application",
                "namespace X;\nclass B {\n" +
                "    void M() { var s = $\"IngestionDbContext is the type\"; }\n" +
                "}"),
        });

        Assert.Empty(literal);
    }

    [Fact]
    public void Synthetic_application_escape_infrastructure_is_forbidden()
    {
        // A nested folder that only *looks* like Infrastructure is not an allowed root.
        var violations = FindViolations(new[]
        {
            ("Modules/Ingestion/Ingestion.Application/Escape.Infrastructure",
                "namespace X;\nclass B {\n" +
                "    void M(IngestionDbContext ctx) { var _ = ctx; }\n" +
                "}"),
        });

        Assert.NotEmpty(violations);
    }

    [Fact]
    public void Real_infrastructure_roots_are_allowed()
    {
        var violations = FindViolations(new[]
        {
            ("Modules/Ingestion/Ingestion.Infrastructure/Persistence",
                "namespace X;\nclass B {\n" +
                "    void M(IngestionDbContext ctx) { var _ = ctx; }\n" +
                "}"),
            ("Modules/AccountingSources/AccountingSources.Infrastructure/Authorization",
                "namespace X;\nclass B {\n" +
                "    void M(AccountingSourcesDbContext ctx) { var _ = ctx; }\n" +
                "}"),
        });

        Assert.Empty(violations);
    }

    private static IEnumerable<(string, string)> ProductionInputs()
    {
        foreach (var file in ProductionSourceFiles())
        {
            var relative = Path.GetRelativePath(SrcRoot, file).Replace('\\', '/');
            var lastSlash = relative.LastIndexOf('/');
            var relativeDir = lastSlash < 0 ? string.Empty : relative.Substring(0, lastSlash);

            yield return (relativeDir, File.ReadAllText(file));
        }
    }

    private static bool IsInfrastructureDirectory(string relativeDir)
    {
        if (string.IsNullOrEmpty(relativeDir))
        {
            return false;
        }

        return InfrastructureProjectRoots.Any(root =>
            relativeDir == root ||
            relativeDir.StartsWith(root + "/", StringComparison.Ordinal));
    }

    private static bool IsBuildingBlocksDirectory(string relativeDir)
        => relativeDir == "BuildingBlocks"
           || relativeDir.StartsWith("BuildingBlocks/", StringComparison.Ordinal);

    private static IEnumerable<string> ProductionSourceFiles()
    {
        foreach (var dir in Directory.EnumerateDirectories(SrcRoot))
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                if (IsInBuildOutput(file))
                {
                    continue;
                }

                yield return file;
            }
        }
    }

    // bin/obj exclusion is separator-independent (Windows \ and Linux /) so generated files are
    // skipped on both platforms.
    private static bool IsInBuildOutput(string file)
    {
        var segments = file.Replace('\\', '/').Split('/');
        return segments.Any(seg => seg == "bin" || seg == "obj");
    }

    /// <summary>
    /// Strip // line comments, /* */ block comments, regular/verbatim/raw string literals, and
    /// char literals. Each skipped region becomes a single space so adjacent identifiers never
    /// merge. No full C# parser is used; this deterministic lexical state machine is sufficient for
    /// the identifier matches this guard performs. Interpolated strings strip literal text but
    /// preserve expression content inside <c>{ ... }</c> as code, and always stop at the closing
    /// delimiter even if it is missing.
    /// </summary>
    public static string StripCommentsAndStrings(string source)
    {
        var sb = new StringBuilder(source.Length);
        var i = 0;
        var n = source.Length;

        while (i < n)
        {
            var c = source[i];

            // Line comment // ...
            if (c == '/' && i + 1 < n && source[i + 1] == '/')
            {
                while (i < n && source[i] != '\n') i++;
                sb.Append(' ');
                continue;
            }

            // Block comment /* ... */
            if (c == '/' && i + 1 < n && source[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < n && !(source[i] == '*' && source[i + 1] == '/')) i++;
                i = Math.Min(i + 2, n);
                sb.Append(' ');
                continue;
            }

            // Raw string literal """ ... """
            if (c == '"' && i + 2 < n && source[i + 1] == '"' && source[i + 2] == '"')
            {
                i += 3;
                while (i + 2 < n && !(source[i] == '"' && source[i + 1] == '"' && source[i + 2] == '"')) i++;
                i = Math.Min(i + 3, n);
                sb.Append(' ');
                continue;
            }

            // Interpolated string: $ / $$ / @$/@$ / $@, then optional @, then "
            if (c == '$')
            {
                var p = i + 1;
                if (p < n && source[p] == '$') p++;
                if (p < n && source[p] == '@') p++;
                if (p < n && source[p] == '"')
                {
                    i = p + 1;
                    var depth = 0;
                    while (i < n)
                    {
                        var d = source[i];
                        if (d == '{') { depth++; i++; continue; }
                        if (d == '}') { if (depth > 0) depth--; i++; continue; }
                        if (d == '"')
                        {
                            // A quote at depth 0 closes the interpolated string; a quote at
                            // depth > 0 opens a nested literal whose content is stripped.
                            if (depth == 0)
                            {
                                i++;
                                break;
                            }

                            sb.Append(' ');
                            i++;
                            while (i < n && source[i] != '"') { if (source[i] == '\\') i += 2; else i++; }
                            i++;
                            continue;
                        }
                        if (d == '\'')
                        {
                            sb.Append(' ');
                            i++;
                            while (i < n && source[i] != '\'') { if (source[i] == '\\') i += 2; else i++; }
                            i++;
                            continue;
                        }
                        // Expression content (depth > 0) is preserved as code.
                        if (depth > 0) sb.Append(d);
                        i++;
                    }

                    sb.Append(' ');
                    continue;
                }
            }

            // Verbatim string @" ..."
            if (c == '@' && i + 1 < n && source[i + 1] == '"')
            {
                i += 2;
                while (i < n)
                {
                    if (source[i] == '"')
                    {
                        if (i + 1 < n && source[i + 1] == '"') { i += 2; continue; }
                        i++;
                        break;
                    }

                    i++;
                }

                sb.Append(' ');
                continue;
            }

            // Regular string " ..."
            if (c == '"')
            {
                i++;
                while (i < n)
                {
                    if (source[i] == '\\') { i += 2; continue; }
                    if (source[i] == '"') { i++; break; }
                    i++;
                }

                sb.Append(' ');
                continue;
            }

            // Char literal ' ...'
            if (c == '\'')
            {
                i++;
                while (i < n && source[i] != '\'')
                {
                    if (source[i] == '\\') { i += 2; continue; }
                    i++;
                }

                i++;
                sb.Append(' ');
                continue;
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Extract maximal identifier runs ([A-Za-z_][A-Za-z0-9_]*) from already-cleaned source.
    /// Punctuation (including '.', '(', ')') becomes a token boundary, so "TenantContext." and
    /// "new TenantContext" are matched by comparing adjacent identifiers.
    /// </summary>
    public static IReadOnlyList<string> ExtractIdentifiers(string cleaned)
    {
        var identifiers = new List<string>();
        var i = 0;
        var n = cleaned.Length;

        while (i < n)
        {
            var c = cleaned[i];
            if (c == '_' || char.IsLetter(c))
            {
                var start = i;
                while (i < n && (cleaned[i] == '_' || char.IsLetterOrDigit(cleaned[i]))) i++;
                identifiers.Add(cleaned.Substring(start, i - start));
            }
            else
            {
                i++;
            }
        }

        return identifiers;
    }

    private static string FindRepoRoot()
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
