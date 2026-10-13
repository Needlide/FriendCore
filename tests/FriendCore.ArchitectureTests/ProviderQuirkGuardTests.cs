using System.Text.RegularExpressions;

namespace FriendCore.ArchitectureTests;

/// <summary>
///     Guards a provider quirk that cost real debugging time and is invisible until it silently
///     returns the wrong rows. A comment in the repository warns the person who reads it; this
///     fails the build for the person who does not, which is most of them.
/// </summary>
public class ProviderQuirkGuardTests
{
    /// <summary>
    ///     Walks up from the test assembly to the solution root, then into src/.
    /// </summary>
    private static string SourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FriendCore.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "src");
    }

    private static IEnumerable<string> SourceFiles() =>
        Directory.EnumerateFiles(SourceRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                           && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

    /// <summary>
    ///     Strips line comments before scanning. Both guards are ABOUT a name that has to appear in
    ///     the very comments warning people off it, so without this the guards would fail on their
    ///     own documentation and be disabled within a week as "just a false positive".
    /// </summary>
    private static string CodeOnly(string path) =>
        Regex.Replace(File.ReadAllText(path), @"(?m)//.*$", string.Empty);

    /// <summary>
    ///     <c>EF.Functions.ILike</c> cannot express a literal substring match on this provider.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         EF/Npgsql translates it to <c>content ILIKE @term ESCAPE ''</c> — with an EMPTY
    ///         escape character. That is the whole problem, and it has two edges:
    ///     </para>
    ///     <list type="number">
    ///         <item>
    ///             Escaping by hand does nothing. <c>term.Replace("_", "\\_")</c> leaves a literal
    ///             backslash in the pattern, so it matches nothing — PostgreSQL's normal backslash
    ///             default has been switched off.
    ///         </item>
    ///         <item>
    ///             And with no escape character there is no way to make <c>%</c> or <c>_</c>
    ///             literal at all, so a user-supplied term containing them becomes a wildcard.
    ///             Observed in production code: a keyword filter built on this helper let
    ///             <c>snake_case</c> hide <c>snakeXcase</c>.
    ///         </item>
    ///     </list>
    ///     <para>
    ///         Use <c>string.Contains</c>. EF compiles that to
    ///         <c>lower(content) LIKE @param</c> with the pattern bound and its LIKE metacharacters
    ///         escaped, which is the correct literal behaviour.
    ///     </para>
    /// </remarks>
    [Fact]
    public void Source_Should_Not_Use_EF_Functions_ILike()
    {
        var offenders = SourceFiles()
            .Where(file => CodeOnly(file).Contains("EF.Functions.ILike", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "EF.Functions.ILike compiles to `content ILIKE @term ESCAPE ''` — an EMPTY escape "
            + "character. Escaping by hand therefore does nothing, and % and _ cannot be made "
            + "literal at all, so a user-supplied term containing them becomes a wildcard. Use "
            + "string.Contains, which EF compiles to a properly escaped LIKE against a bound "
            + "parameter. Found in:\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>
    ///     The same trap reached by hand-written SQL. A raw <c>ILIKE</c> is legitimate SQL, but
    ///     only if the pattern is built with an explicit escape character — otherwise a user typing
    ///     <c>50%</c> gets a wildcard scan over the whole table.
    /// </summary>
    [Fact]
    public void Raw_ILIKE_Should_Carry_An_Explicit_Escape_Clause()
    {
        var ilike = new Regex(@"ILIKE", RegexOptions.IgnoreCase);
        var escapeClause = new Regex(@"ILIKE[^;]*?ESCAPE", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var offenders = new List<string>();
        foreach (var file in SourceFiles())
        {
            var text = CodeOnly(file);
            if (!ilike.IsMatch(text))
                continue;

            if (!escapeClause.IsMatch(text))
                offenders.Add(file);
        }

        Assert.True(
            offenders.Count == 0,
            "Raw ILIKE without an explicit ESCAPE clause: % and _ stay wildcards, so a search term "
            + "containing them scans as a pattern instead of matching literally. Add "
            + "\"ESCAPE '\\'\", or use string.Contains. Found in:\n  "
            + string.Join("\n  ", offenders));
    }
}