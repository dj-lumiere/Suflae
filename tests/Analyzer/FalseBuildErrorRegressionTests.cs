using Builder.Diagnostics;
using Builder.Verification.Results;

namespace Suflae.Tests.Analyzer;

using static TestHelpers;

/// <summary>
/// Programs the analyzer once rejected although they are legal: a variant declared after a record that holds it, and
/// a <c>Watched</c> of an entity written as a type.
/// </summary>
public class FalseBuildErrorRegressionTests
{
    /// <summary>Declaration order does not matter: the record holding the variant sees the variant's arms.</summary>
    [Fact]
    public void Analyze_VariantDeclaredAfterHolder_ArmsMatch()
    {
        AnalysisResult result = AnalyzeSaSuflae(source: """
                                       record Holder
                                           inner: AnyService

                                       record Slow
                                           delay: Duration

                                       variant AnyService
                                           Slow
                                           Text

                                       routine start()
                                           var b: AnyService = Slow(delay: 2s)
                                           var h = Holder(inner: b)
                                           when h.inner
                                               is Slow as slow => show(f"slow {slow.delay}")
                                               is Text as t => show(t)
                                       """);
        Assert.DoesNotContain(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.PatternTypeMismatch);
    }

    /// <summary><c>Watched[Session]</c> watches the entity itself, the type <c>watch(target:)</c> gives back.</summary>
    [Fact]
    public void Analyze_WatchedOfEntity_Builds()
    {
        AnalysisResult result = AnalyzeSaSuflae(source: """
                                       import ObjectHacker

                                       entity Session
                                           n: Int

                                       routine start()
                                           var s = Session(n: 1)
                                           var w: Watched[Session] = watch(target: s)
                                           show(w.is_alive())
                                       """);
        Assert.DoesNotContain(collection: result.Errors,
            filter: e => e.Code is SemanticDiagnosticCode.ReferenceTypeConstraintViolation
                or SemanticDiagnosticCode.VariableInitializerTypeMismatch);
    }

    /// <summary>A <c>Watched</c> comes from <c>watch(target:)</c>: building one from an entity names an argument it
    /// has nowhere.</summary>
    [Fact]
    public void Analyze_WatchedBuiltFromEntity_Errors()
    {
        AnalysisResult result = AnalyzeSaSuflae(source: """
                                                        import ObjectHacker

                                                        entity Session
                                                            n: Int

                                                        routine start()
                                                            var w: Watched[Session] = Watched(from: Session(n: 1))
                                                        """);
        Assert.Contains(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.UnknownNamedArgument && e.Message.Contains(value: "'from'"));
        Assert.DoesNotContain(collection: result.Errors,
            filter: e => e.Code is SemanticDiagnosticCode.ReferenceTypeConstraintViolation
                or SemanticDiagnosticCode.VariableInitializerTypeMismatch);
    }
}
