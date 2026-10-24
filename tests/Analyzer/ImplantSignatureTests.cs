using Builder.Verification.Results;

namespace Suflae.Tests.Analyzer;

using static TestHelpers;

/// <summary>
/// ObjectHacker's <c>implant</c>: a replacement of another signature than the member routine it replaces is a build
/// error that names <c>implant</c>, the routine and both signatures.
/// </summary>
public sealed class ImplantSignatureTests
{
    [Fact]
    public void Analyze_ImplantOfWrongSignature_NamesRoutineAndBothSignatures()
    {
        AnalysisResult result = AnalyzeSuflae(fileName: "implant_signature.sf", source: """
                                     import IO/Console
                                     import ObjectHacker

                                     entity Hero
                                         name: Text
                                         hp: S64

                                     routine Hero.attack(power: S64) -> S64
                                         me.hp = me.hp - power
                                         return me.hp

                                     routine start()
                                         var ada = Hero(name: "Ada", hp: 10)
                                         implant(target: ada, replacing: Hero.attack, with: (hero: Hero, power: Text) => hero.hp)
                                         show(f"{ada.attack(power: 3)}")
                                     """);
        // The message as a Suflae file shows it.
        Assert.Contains(collection: result.Errors, filter: e => e.SurfaceMessage.Contains(
            value: "You `implant` a `Callable[(Hero, Text), S64]` as the replacement for `Hero.attack`, but " +
                   "`Hero.attack` (line 8) needs a `Callable[(Hero, S64), S64]`",
            comparisonType: StringComparison.Ordinal));
    }
}
