using Builder.Diagnostics;
using Builder.Verification.Results;

namespace Suflae.Tests.Analyzer;

using static TestHelpers;

/// <summary>
/// Forms found while verifying the Suflae documentation that once passed analysis and then failed in the LLVM
/// emitter or at link time. Each is a build error now, reported where it is written.
/// </summary>
public class DocFindingsTests
{
    /// <summary><c>Integer</c> has no <c>/</c>: an integer quotient is <c>//</c>.</summary>
    [Fact]
    public void Analyze_IntegerTrueDivision_ReportsMissingOperator()
    {
        string source = """
                        routine start()
                            var a = 7
                            var b = 2
                            show(a / b)
                            return
                        """;

        AnalysisResult result = AnalyzeSaSuflae(source: source);
        Assert.Contains(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.BinaryOperatorNotFound &&
                         e.Message.Contains(value: "'/'"));
    }

    /// <summary>A memberwise construction whose values the member variables cannot hold is reported (no creator
    /// of <c>Complex</c> takes two <c>Integer</c>s).</summary>
    [Fact]
    public void Analyze_MemberwiseConstructionWithWrongValueType_ReportsMismatch()
    {
        string source = """
                        import Numerics

                        routine start()
                            var c = Complex(real: 3, imag: 4)
                            show(c)
                            return
                        """;

        AnalysisResult result = AnalyzeSaSuflae(source: source);
        Assert.Equal(expected: 2,
            actual: result.Errors.Count(predicate: e => e.Code == SemanticDiagnosticCode.MemberVariableTypeMismatch));
    }

    /// <summary>A routine's parameters always state their types.</summary>
    [Fact]
    public void Analyze_RoutineParameterWithoutType_ReportsError()
    {
        string source = """
                        routine f(x)
                            return

                        routine start()
                            f(x: 1)
                            return
                        """;

        AnalysisResult result = AnalyzeSaSuflae(source: source);
        Assert.Contains(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.RoutineParameterWithoutType);
    }

    /// <summary>A <c>@crash_only</c> routine is only called bare: <c>try</c> over it is reported.</summary>
    [Fact]
    public void Analyze_TryOverCrashOnlyRoutine_ReportsError()
    {
        string source = """
                        import IO/Console
                        import Numerics

                        crashable BadError
                            what: Text

                        routine BadError.crash_message() -> Text
                            return me.what

                        @crash_only
                        routine boom!(n: Integer) -> Integer
                            if n < 0
                                throw BadError(what: "negative")
                            return n

                        routine start()
                            var m = try boom(n: -1)
                            show(m ?? 0)
                            return
                        """;

        AnalysisResult result = AnalyzeSaSuflae(source: source);
        Assert.Single(collection: result.Errors,
            predicate: e => e.Code == SemanticDiagnosticCode.CrashOnlyRecovered);
    }

    /// <summary>The fallback after <c>??</c> gives a value of the right type, or is a <c>@crash_only</c> call
    /// such as <c>stop()</c>: a call that gives no value and comes back is reported.</summary>
    [Fact]
    public void Analyze_NoneCoalesceFallbackWithoutValue_ReportsError()
    {
        string source = """
                        import IO/Console
                        import Numerics

                        routine find!(n: Integer) -> Integer
                            if n < 0
                                absent
                            return n

                        routine start()
                            var a = try find(n: 3) ?? show("missing")
                            var b = try find(n: 3) ?? stop()
                            show(a + b)
                            return
                        """;

        AnalysisResult result = AnalyzeSaSuflae(source: source);
        Assert.Single(collection: result.Errors,
            predicate: e => e.Code == SemanticDiagnosticCode.ArgumentTypeMismatch &&
                            e.Message.Contains(value: "'??'"));
    }

    /// <summary>A type obeying a protocol with a creator requirement must have a matching creator.</summary>
    [Fact]
    public void Analyze_ProtocolCreatorRequirementNotMet_ReportsError()
    {
        string source = """
                        protocol Parsable
                            routine Me!(text: Text)

                        record Num
                        obeys Parsable
                            n: Integer

                        routine start()
                            show(Num(n: 1).n)
                            return
                        """;

        AnalysisResult result = AnalyzeSaSuflae(source: source);
        Assert.Contains(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.MissingProtocolMemberRoutine &&
                         e.Message.Contains(value: "creator"));
    }
}
