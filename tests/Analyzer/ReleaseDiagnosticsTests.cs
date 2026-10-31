using Builder.Diagnostics;
using Builder.Verification.Results;

namespace Suflae.Tests.Analyzer;

using static TestHelpers;

/// <summary>
/// Diagnostics fixed before the 0.5 release, as a Suflae program meets them: a <c>posted</c> routine, a repeated
/// <c>== 1</c> arm and a conversion error that names Suflae types only.
/// </summary>
public class ReleaseDiagnosticsTests
{
    [Fact]
    public void Parse_PostedRoutine_IsRejected()
    {
        (_, Builder.Parser.Parser parser) = ParseSuflaeWithErrors(source: """
                                                                          posted routine foo() -> Int
                                                                              return 1
                                                                          """);
        Assert.True(condition: parser.HasErrors);
    }

    [Fact]
    public void Analyze_RepeatedComparisonArm_IsADuplicatePattern()
    {
        AnalysisResult result = AnalyzeSaSuflae(source: """
                                                        routine pick(x: Int) -> Int
                                                            return when x
                                                                == 1 => 10
                                                                == 1 => 20
                                                                else => 0
                                                        """);
        Assert.Contains(collection: result.Errors, filter: e => e.Code == SemanticDiagnosticCode.DuplicatePattern);
    }

    [Fact]
    public void Analyze_MissingConversion_NamesSuflaeTypesOnly()
    {
        AnalysisResult result = AssertHasErrorSaSuflae(source: """
                                                               routine start()
                                                                   var nums = [1, 2]
                                                                   var s = nums.to[Set]()
                                                                   return
                                                               """,
            expectedErrorSubstring: "no conversion from 'List[");
        Assert.DoesNotContain(collection: result.Errors, filter: e => e.Message.Contains(value: "SF::"));
    }
}
