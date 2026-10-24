using Builder.Verification.Results;

namespace Suflae.Tests.Analyzer;

using static TestHelpers;

/// <summary>
/// `==` and a hash come from the member variables: a record or entity whose member variable has no `==` has none, a
/// record whose member variable has no hash has none, and an entity hashes only when it declares `obeys Hashable`.
/// Each missing one is a build error where it is used, naming the member variable.
/// </summary>
public class ValueEqualityTests
{
    /// <summary>`==` on a record holding a routine value names that member variable.</summary>
    [Fact]
    public void Analyze_RecordWithRoutineValue_HasNoEquality()
    {
        string source = """
                        record Button
                            label: Text
                            on_click: Callable[(), None]

                        routine noop()
                            return

                        routine start()
                            var a = Button("ok", noop)
                            var same = a == a
                            return
                        """;

        AnalysisResult result = AnalyzeSaSuflae(source: source);
        Assert.Contains(collection: result.Errors,
            filter: e => e.Message.Contains(value: "'Button' has no '=='") &&
                         e.Message.Contains(value: "member variable 'on_click'"));
    }

    /// <summary>A generic `needs T obeys Equatable` takes a plain record and an entity, and rejects a record whose
    /// member variable has no `==`.</summary>
    [Fact]
    public void Analyze_EquatableConstraint_TakesEveryTypeWithEquality()
    {
        string source = """
                        entity Player
                            name: Text

                        record Point
                            x: Integer

                        record Button
                            on_click: Callable[(), None]

                        routine noop()
                            return

                        routine same[T](a: T, b: T) -> Bool
                        needs T obeys Equatable
                            return a == b

                        routine start()
                            var first = same(Point(1), Point(1))
                            var second = same(Player("Ada"), Player("Ada"))
                            var third = same(Button(noop), Button(noop))
                            return
                        """;

        AnalysisResult result = AnalyzeSaSuflae(source: source);
        Assert.DoesNotContain(collection: result.Errors,
            filter: e => e.Message.Contains(value: "'Point'") || e.Message.Contains(value: "'Player'"));
        Assert.Contains(collection: result.Errors,
            filter: e => e.Message.Contains(value: "'Button' has no '=='") &&
                         e.Message.Contains(value: "member variable 'on_click'"));
    }

    /// <summary>An entity without `obeys Hashable` is no Set element, and a record holding one has no hash.</summary>
    [Fact]
    public void Analyze_EntityWithoutHashable_IsNoSetElement()
    {
        string source = """
                        entity Player
                            name: Text

                        record Seat
                            holder: Player
                            row: Integer

                        record Button
                            on_click: Callable[(), None]

                        routine start()
                            var players = Set[Player]()
                            var seats = Set[Seat]()
                            var buttons = Set[Button]()
                            return
                        """;

        AnalysisResult result = AnalyzeSaSuflae(source: source);
        Assert.Contains(collection: result.Errors,
            filter: e => e.Message.Contains(value: "'Player' is an entity without 'obeys Hashable'"));
        Assert.Contains(collection: result.Errors,
            filter: e => e.Message.Contains(value: "'Seat' has no hash") &&
                         e.Message.Contains(value: "member variable 'holder'"));
        Assert.Contains(collection: result.Errors,
            filter: e => e.Message.Contains(value: "'Button' has no hash") &&
                         e.Message.Contains(value: "member variable 'on_click'"));
    }

    /// <summary>With `obeys Hashable` an entity is a Set element, and a record holding it hashes.</summary>
    [Fact]
    public void Analyze_EntityWithHashable_IsSetElement()
    {
        string source = """
                        entity Player
                        obeys Hashable
                            name: Text

                        record Seat
                            holder: Player
                            row: Integer

                        routine start()
                            var players = Set[Player]()
                            players.add(Player("Ada"))
                            var seats = Set[Seat]()
                            seats.add(Seat(Player("Ada"), 1))
                            return
                        """;

        AnalysisResult result = AnalyzeSaSuflae(source: source);
        Assert.DoesNotContain(collection: result.Errors,
            filter: e => e.Message.Contains(value: "Hashable") || e.Message.Contains(value: "hash"));
    }
}
