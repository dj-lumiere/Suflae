using Builder.Diagnostics;
using Builder.Verification.Results;

namespace Suflae.Tests.Analyzer;

using static TestHelpers;

/// <summary>Messages fixed together before 0.5: one error per cause.</summary>
public class BListMessageTests
{
    /// <summary>A module-level <c>var</c> is reported once. The routine that reads it does not add an unknown identifier for each use.</summary>
    [Fact]
    public void Analyze_ModuleLevelVar_ReportsOnceWithoutUnknownIdentifiers()
    {
        string source = """
                        var counter: S64 = 0

                        routine bump()
                            counter = counter + 1
                            return

                        routine start()
                            bump()
                            show(counter)
                            return
                        """;

        AnalysisResult result = AnalyzeSaSuflae(source: source);
        Assert.Single(collection: result.Errors.Where(predicate: e =>
            e.Code == SemanticDiagnosticCode.ModuleLevelVarNotAllowed));
        Assert.DoesNotContain(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.UnknownIdentifier && e.Message.Contains(value: "'counter'"));
    }
}
