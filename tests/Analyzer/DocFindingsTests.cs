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
    /// <summary><c>Int</c> has no <c>/</c>: an integer quotient is <c>//</c>.</summary>
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

    /// <summary><c>/</c> on a fixed-width integer is an error whose message points to <c>//</c> and to the
    /// <c>Decimal</c> quotient.</summary>
    [Fact]
    public void Analyze_FixedWidthTrueDivision_PointsToFloorAndDecimal()
    {
        string source = """
                        routine start()
                            var a: S64 = 7
                            var b: S64 = 2
                            show(a / b)
                        """;

        AnalysisResult result = AnalyzeSaSuflae(source: source);
        Assert.Contains(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.BinaryOperatorNotFound &&
                         e.Message.Contains(value: "a // b") && e.Message.Contains(value: "to[Decimal]()"));
    }

    /// <summary>A creator argument of the wrong type names the creator and the argument, not the member variables
    /// a memberwise construction would need.</summary>
    [Fact]
    public void Analyze_CreatorArgumentOfWrongType_NamesTheCreator()
    {
        string source = """
                        routine start()
                            var x: S64 = 4
                            var xs = List[S64](capacity: x)
                            show(xs)
                        """;

        AnalysisResult result = AnalyzeSaSuflae(source: source);
        Assert.Contains(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.ArgumentTypeMismatch &&
                         e.Message.Contains(value: "'capacity'"));
        Assert.DoesNotContain(collection: result.Errors,
            filter: e => e.Code is SemanticDiagnosticCode.MissingRequiredMemberVariable
                or SemanticDiagnosticCode.MemberVariableNotFound);
    }

    /// <summary>A memberwise construction whose values the member variables cannot hold is reported (no creator
    /// of <c>Label</c> takes two <c>Int</c>s).</summary>
    [Fact]
    public void Analyze_MemberwiseConstructionWithWrongValueType_ReportsMismatch()
    {
        string source = """
                        record Label
                            head: Text
                            tail: Text

                        routine start()
                            var c = Label(head: 3, tail: 4)
                            show(c.head)
                            return
                        """;

        AnalysisResult result = AnalyzeSaSuflae(source: source);
        Assert.Equal(expected: 2,
            actual: result.Errors.Count(predicate: e => e.Code == SemanticDiagnosticCode.MemberVariableTypeMismatch));
    }

    /// <summary>A parameter written without a type takes any value: the routine is made for each type it is used
    /// with, so writing it is not an error.</summary>
    [Fact]
    public void Analyze_RoutineParameterWithoutType_IsAccepted()
    {
        string source = """
                        routine f(x)
                            return

                        routine start()
                            f(x: 1)
                            return
                        """;

        AnalysisResult result = AnalyzeSaSuflae(source: source);
        Assert.DoesNotContain(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.RoutineParameterWithoutType);
    }

    /// <summary>A <c>@crash_only</c> routine is only called bare: <c>try</c> over it is reported.</summary>
    [Fact]
    public void Analyze_TryOverCrashOnlyRoutine_ReportsError()
    {
        string source = """
                        import IO/Console

                        crashable BadError
                            what: Text

                        routine BadError.crash_message() -> Text
                            return me.what

                        @crash_only
                        failable routine boom(n: Int) -> Int
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

                        failable routine find(n: Int) -> Int
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
                            failable routine Me(text: Text)

                        record Num
                        obeys Parsable
                            n: Int

                        routine start()
                            show(Num(n: 1).n)
                            return
                        """;

        AnalysisResult result = AnalyzeSaSuflae(source: source);
        Assert.Contains(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.MissingProtocolMemberRoutine &&
                         e.Message.Contains(value: "creator"));
    }

    /// <summary>A missing protocol routine is named in the types a Suflae program writes: the record itself, no token
    /// and no module.</summary>
    [Fact]
    public void Analyze_MissingProtocolRoutine_NamesTheWrittenType()
    {
        string source = """
                        entity Player
                            name: Text

                        record Team
                        obeys Comparable
                            lead: Player
                            size: Int

                        routine start()
                            show(Team(lead: Player(name: "Ada"), size: 3).size)
                            return
                        """;

        AnalysisResult result = AnalyzeSaSuflae(source: source);
        Assert.Contains(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.MissingProtocolMemberRoutine &&
                         e.Message.Contains(value: "`routine Team.compare(you: Team) -> ComparisonSign`"));
        Assert.DoesNotContain(collection: result.Errors,
            filter: e => e.Message.Contains(value: "Viewing") || e.Message.Contains(value: "Roamed"));
    }

    /// <summary>The builder writes a record's `compare` and `hash` only when every member variable has them: an entity
    /// whose declaration doesn't ask for `Comparable` or `Hashable` has neither, so the record's `obeys` line is
    /// SF-S702 for each.</summary>
    [Fact]
    public void Analyze_RecordHoldingEntity_ComparableAndHashableNeedTheEntitysOwn()
    {
        string source = """
                        entity Player
                        obeys Equatable
                            name: Text

                        record Ranked
                        obeys Comparable
                            lead: Player

                        record Keyed
                        obeys Equatable, Hashable
                            lead: Player

                        routine start()
                            show(Ranked(lead: Player(name: "Ada")).lead.name)
                            show(Keyed(lead: Player(name: "Ada")).lead.name)
                        """;

        AnalysisResult result = AnalyzeSaSuflae(source: source);
        Assert.Contains(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.MissingProtocolMemberRoutine &&
                         e.Message.Contains(value: "`routine Ranked.compare(you: Ranked) -> ComparisonSign`"));
        Assert.Contains(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.MissingProtocolMemberRoutine &&
                         e.Message.Contains(value: "`routine Keyed.hash(k0: U64, k1: U64) -> U64`"));
        Assert.DoesNotContain(collection: result.Errors,
            filter: e => e.Message.Contains(value: "Keyed.eq"));
    }

    /// <summary>Adding to the list an `each` loop goes through, written in the loop's body, doesn't build.</summary>
    [Fact]
    public void Analyze_ListChangedInItsOwnLoop_ReportsError()
    {
        string source = """
                        routine start()
                            var xs = [1, 2, 3]
                            each x in xs
                                xs.add_last(value: 9)
                        """;

        AnalysisResult result = AnalyzeSaSuflae(source: source);
        Assert.Contains(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.ReshapingDuringIteration);
    }
}
