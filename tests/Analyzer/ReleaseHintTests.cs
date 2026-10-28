using Builder.Verification.Results;

namespace Suflae.Tests.Analyzer;

using static TestHelpers;

/// <summary>
/// The errors a previous-release spelling meets name what a Suflae program writes now, with no word of access tokens
/// (a Suflae program has none).
/// </summary>
public class ReleaseHintTests
{
    /// <summary>The messages of the errors <paramref name="source"/> gives, one string.</summary>
    private static string Errors(string source, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        AnalysisResult result = AnalyzeSaSuflae(source: source, fileName: name);
        return string.Join(separator: "\n", values: result.Errors.Select(selector: e => e.Message));
    }

    [Fact]
    public void RetiredAccessType_SaysToUseTheValue()
    {
        string errors = Errors(source: """
                                       routine take(x: Accessing[S64]) -> S64
                                           return 0

                                       routine start()
                                           return
                                       """);
        Assert.Contains(expectedSubstring: "'Accessing' is gone: use the value itself.", actualString: errors);
    }

    [Fact]
    public void RecoveryPrefix_NamesTheKeyword()
    {
        string errors = Errors(source: """
                                       failable routine halve(n: S64) -> S64
                                           return n // 2

                                       routine start()
                                           var r = try_halve(n: 4)
                                           return
                                       """);
        Assert.Contains(expectedSubstring: "write 'try halve(...)'", actualString: errors);
    }

    [Fact]
    public void TypedDraw_NamesTheTypeArgument()
    {
        string errors = Errors(source: """
                                       import Random

                                       routine start()
                                           var r = make_random(seed: 1).next_b64()
                                       """);
        Assert.Contains(expectedSubstring: "It is 'next[B64]()' now", actualString: errors);
    }

    [Theory]
    [InlineData("Atomic[S64]", "A number every job changes is a 'global' or a member variable of an entity")]
    [InlineData("CircularList[S64]", "It is called 'RotatableList' now (migrate --step type-names rewrites it).")]
    [InlineData("Integer", "It is called 'Int' now (migrate --step type-names rewrites it).")]
    public void RenamedType_NamesTheNewName(string type, string hint)
    {
        string errors = Errors(source: $"""
                                        routine take(x: {type}) -> S64
                                            return 0

                                        routine start()
                                            return
                                        """);
        Assert.Contains(expectedSubstring: hint, actualString: errors);
    }

    [Fact]
    public void RenamedTypeCreated_NamesTheNewName()
    {
        string errors = Errors(source: """
                                       routine start()
                                           var hits = Atomic[S64](initial: 0)
                                       """);
        Assert.Contains(expectedSubstring: "A number every job changes is a 'global' or a member variable of an entity",
            actualString: errors);
    }
}
