using Builder.Diagnostics;
using Builder.Verification.Results;

namespace Suflae.Tests.Analyzer;

using static TestHelpers;

/// <summary>
/// A Suflae result left unused is a notice: the line builds as written, and check, lint and the language server ask
/// whether the result was meant.
/// </summary>
public class NoticeTests
{
    [Fact]
    public void Analyze_SuflaeUnusedResult_IsANoticeNotAWarning()
    {
        AnalysisResult result = AnalyzeSuflae(source: """
                                                        routine get_value() -> Text
                                                            return "x"

                                                        routine test()
                                                            get_value()
                                                            return
                                                        """, fileName: "unused_result.sf");
        Assert.Empty(collection: result.Errors);
        Assert.DoesNotContain(collection: result.Warnings,
            filter: w => w.Code == SemanticWarningCode.UnusedRoutineReturnValue);
        Assert.Contains(collection: result.Notices,
            filter: n => n.Code == SemanticWarningCode.UnusedRoutineReturnValue && n.IsNotice);
    }
}
