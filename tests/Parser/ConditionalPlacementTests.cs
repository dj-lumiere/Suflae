using Builder.Diagnostics;
using SyntaxTree;

namespace Suflae.Tests.Parser;

using static TestHelpers;

/// <summary>
/// A conditional value <c>if c then a else b</c> stands only as the whole value of <c>var x =</c>, <c>x =</c> or
/// <c>return</c>. Every other position (inside parentheses, an argument, an operand, a subscript, an f-text slot, a
/// branch of another conditional, after <c>try</c>/<c>check</c>) is SF-G112 with a message that points at a named
/// value or <c>when</c>.
/// </summary>
public sealed class ConditionalPlacementTests
{
    [Theory]
    [InlineData("var x = (if c then 4 else 3) + 1")]
    [InlineData("var x = 1 + if c then 4 else 3")]
    [InlineData("var x = (if c then 4 else 3)")]
    [InlineData("var x = pick(a: if c then 4 else 3, b: 1)")]
    [InlineData("var x = Point(x: if c then 4 else 3, y: 1)")]
    [InlineData("var x = xs[if c then 0 else 1]")]
    [InlineData("var x = f\"{if c then 4 else 3}\"")]
    [InlineData("var x = if c then (if c then 1 else 2) else 3")]
    [InlineData("var x = if c then 1 else if c then 2 else 3")]
    [InlineData("var x = not if c then true else false")]
    [InlineData("x += if c then 1 else 2")]
    [InlineData("show(if c then 1 else 2)")]
    public void Parse_NestedConditional_ReportsG112(string line)
    {
        AssertRejected(source: Wrap(line: line), recovery: false);
    }

    [Theory]
    [InlineData("var x = try if c then f() else g()")]
    [InlineData("var x = check if c then f() else g()")]
    public void Parse_ConditionalAfterRecovery_ReportsG112(string line)
    {
        AssertRejected(source: Wrap(line: line), recovery: true);
    }

    [Theory]
    [InlineData("var x = if c then 4 else 3")]
    [InlineData("var x = 0\n    x = if c then 4 else 3")]
    [InlineData("return if c then 4 else 3")]
    [InlineData("var x = if c then try f() else 3")]
    public void Parse_TopLevelConditional_Parses(string line)
    {
        Program program = AssertParsesSuflae(source: Wrap(line: line));
        Assert.NotNull(@object: program);
    }

    [Fact]
    public void Parse_GlobalConditional_Parses()
    {
        AssertParsesSuflae(source: "global limit: S64 = if true then 4 else 3\n");
    }

    private static string Wrap(string line)
    {
        return "routine probe(c: Bool) -> S64\n    " + line + "\n    return 0\n";
    }

    private static void AssertRejected(string source, bool recovery)
    {
        (Program _, Builder.Parser.Parser parser) = ParseSuflaeWithErrors(source: source);
        GrammarException? error = parser.GetStructuredErrors()
                                        .FirstOrDefault(predicate: e => e.RawMessage.Contains(value: "conditional value"));
        Assert.NotNull(@object: error);
        Assert.Equal(expected: GrammarDiagnosticCode.ExpectedExpression, actual: error.Code);
        Assert.Contains(expectedSubstring: "'when'", actualString: error.RawMessage);
        if (recovery)
        {
            Assert.Contains(expectedSubstring: "in front of a conditional value", actualString: error.RawMessage);
        }
    }
}

/// <summary>
/// <c>@noinline</c> is a routine annotation in Suflae too, and together with <c>@inline</c> it is SF-G227.
/// </summary>
public sealed class NoinlineAnnotationTests
{
    [Fact]
    public void Parse_Noinline_IsKept()
    {
        Program program = AssertParsesSuflae(source: "@noinline\nroutine cold(x: S64) -> S64\n    return x * 3\n");
        RoutineDeclaration routine = program.Declarations.OfType<RoutineDeclaration>().Single();
        Assert.Contains(expected: "noinline", collection: routine.Annotations);
    }

    [Fact]
    public void Parse_InlineAndNoinline_IsG227()
    {
        (_, Builder.Parser.Parser parser) =
            ParseSuflaeWithErrors(source: "@inline\n@noinline\nroutine cold(x: S64) -> S64\n    return x * 3\n");
        Assert.Contains(collection: parser.GetErrors(), filter: e => e.Contains(value: "SF-G227"));
    }
}
