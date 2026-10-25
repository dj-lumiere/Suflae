using Builder.Diagnostics;
using Builder.Verification.Results;

namespace Suflae.Tests.Analyzer;

using static TestHelpers;

/// <summary>
/// Failures rise only through failable routines: a <c>throw</c> in a routine that is not failable crashes where it
/// stands, <c>try</c>/<c>check</c> over an expression that reaches no failable call is SF-S760, a routine value that
/// may fail goes only to a <c>failable Callable[...]</c> (SF-S761), and <c>pierce</c> is an ordinary name.
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

    /// <summary><c>pierce</c> is not a keyword: it names a variable, a routine and a field like any other
    /// name.</summary>
    [Fact]
    public void Pierce_IsAnOrdinaryName()
    {
        AssertAnalyzesSaSuflae(source: """
                                       record Arrow
                                           pierce: S64
                                       routine pierce(n: S64) -> S64
                                           return n + 1
                                       routine aim(pierce: S64) -> S64
                                           var a = Arrow(pierce: pierce)
                                           return a.pierce
                                       routine start()
                                           var hit = pierce(n: 1)
                                           var pierced = aim(pierce: hit)
                                           return
                                       """);
    }
}
