using Builder.Diagnostics;
using Builder.Verification.Results;

namespace Suflae.Tests.Analyzer;

using static TestHelpers;

/// <summary>
/// Tests for in-place compound assignment dispatch (#40).
/// Compound assignments (+=, -=, etc.) dispatch to in-place wired memberRoutines ($iadd, etc.)
/// first, then fall back to create-and-assign ($add) for non-entity types.
/// Entities require in-place wired memberRoutines (no fallback, since bare entity assignment is prohibited).
/// </summary>
public class CompoundAssignmentTests
{
    #region In-Place Dispatch (type defines $iadd)

    #endregion

    #region Fallback Dispatch (record with only $add)

    #endregion

    #region Entity Without In-Place Wired (no fallback)

    #endregion

    #region Neither Wired Exists

    #endregion

    #region Mutability Checks

    #endregion

    #region Choice Type Prohibition

    #endregion

    #region Multiple Compound Operators

    #endregion

    #region Additional Operator Coverage

    #endregion

    #region Primitive Types (existing behavior preserved)

    #endregion

    #region Operand type checking

    /// <summary>
    /// A Suflae <c>Integer += S64</c> needs an explicit conversion. Unchecked, the emitter passed the raw
    /// <c>i64</c> where <c>Integer.add</c> takes an <c>Integer</c> record and the program crashed.
    /// </summary>
    [Fact]
    public void Analyze_SuflaeIntegerPlusFixedWidthCompoundAssignment_ReportsError()
    {
        string source = """
                        import Numerics

                        routine accumulate(start_value: Integer, v: S64)
                            var t = start_value
                            t += v
                            return
                        """;

        AnalysisResult result = AnalyzeSaSuflae(source: source);
        Assert.Single(collection: result.Errors,
            predicate: e => e.Code == SemanticDiagnosticCode.ArgumentTypeMismatch);
    }

    #endregion
}
