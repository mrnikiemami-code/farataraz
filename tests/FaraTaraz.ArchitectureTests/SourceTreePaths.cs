namespace FaraTaraz.ArchitectureTests;

using System;
using System.Linq;

/// <summary>
/// Shared source-tree path classification for the architecture guards (FT-CI-003).
///
/// Build output (any <c>bin</c> or <c>obj</c> directory segment) must be excluded from every
/// production source scan, on both Windows (<c>\</c>) and Linux (<c>/</c>) separators, so an
/// equivalent source tree is handled identically on the local Windows worktree and the Ubuntu
/// CI runner. Keeping one implementation here avoids the drift that let three guards exclude
/// build output using Windows-only path fragments.
/// </summary>
internal static class SourceTreePaths
{
    /// <summary>
    /// True when the path contains a <c>bin</c> or <c>obj</c> directory segment, independent of
    /// the OS path separator. A segment is compared exactly, so <c>mybin</c> / <c>objo</c> are not
    /// treated as build output.
    /// </summary>
    internal static bool IsBuildOutput(string path)
        => path
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment == "bin" || segment == "obj");
}
