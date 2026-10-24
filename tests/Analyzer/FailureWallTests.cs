using Builder.Diagnostics;
using Builder.Verification.Results;

namespace Suflae.Tests.Analyzer;

using static TestHelpers;

/// <summary>
/// Failures rise only through failable routines: a <c>throw</c> in a routine that is not failable crashes where it
/// stands, <c>try</c>/<c>check</c> over an expression that reaches no failable call is SF-S760, a routine value that
/// may fail goes only to a <c>failable Callable[...]</c> (SF-S761), and <c>pierce</c> is not a keyword (SF-G227).
/// </summary>
public sealed class FailureWallTests
{
    [Fact]
    public void Throw_InPlainRoutine_Builds()
    {
        AssertAnalyzesSaSuflae(source: """
                                       routine require_positive(n: S64) -> S64
                                           if n <= 0
                                               throw LogicBreachedError()
                                           return n
                                       routine start()
                                           var a = require_positive(n: 1)
                                           return
                                       """);
    }

    [Fact]
    public void Try_OverPlainCall_IsError()
    {
        AnalysisResult result = AnalyzeSaSuflae(source: """
                                                        routine name_of(n: S64) -> Text
                                                            return f"{n}"
                                                        routine start()
                                                            var a = try name_of(n: 1)
                                                        """);
        Assert.Contains(collection: result.Errors, filter: e => e.Code == SemanticDiagnosticCode.NothingToRecover);
    }

    [Fact]
    public void FailableLambda_ToFailableParameter_IsRecoverable()
    {
        AssertAnalyzesSaSuflae(source: """
                                       routine apply(f: failable Callable[(S64,), S64], x: S64) -> S64
                                           return f(x)
                                       routine start()
                                           var a = try apply(f: x => 10 // x, x: 0)
                                           return
                                       """);
    }

    [Fact]
    public void FailableLambda_ToPlainParameter_IsError()
    {
        AnalysisResult result = AnalyzeSaSuflae(source: """
                                                        routine apply(f: Callable[(S64,), S64], x: S64) -> S64
                                                            return f(x)
                                                        routine start()
                                                            var a = apply(f: x => 10 // x, x: 2)
                                                        """);
        Assert.Contains(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.FailableValueToPlainParameter);
    }

    [Fact]
    public void Pierce_IsGrammarError()
    {
        (_, Builder.Parser.Parser parser) = ParseSuflaeWithErrors(source: """
                                                                       routine start()
                                                                           pierce LogicBreachedError()
                                                                       """);
        Assert.Contains(collection: parser.GetErrors(), filter: e => e.Contains(value: "pierce"));
    }
}
