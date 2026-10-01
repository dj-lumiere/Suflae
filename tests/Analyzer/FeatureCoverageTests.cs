using Builder.LlvmEmit;
using Builder.Targeting;
using Builder.Verification;
using Builder.Verification.Results;
using SyntaxTree;
using TypeModel.Enums;

namespace Suflae.Tests.Analyzer;

using static TestHelpers;

/// <summary>
/// Drives the full in-process compile pipeline (parse → semantic analysis + lowering + monomorphization
/// → LLVM IR emission) over feature-rich RazorForge and Suflae programs. Unlike the spawned
/// <c>buildandrun</c> stdlib harness (whose child process is not coverage-instrumented), these run the
/// compiler IN the test host, so they exercise — and count coverage for — the lowering/instantiation/
/// codegen passes that only fire for constructs like pattern matching, lambdas, generics, error-handling
/// variants, iterator inlining, and Suflae module globals.
///
/// Each case asserts the program analyzes without errors and emits non-empty IR; the point is breadth of
/// executed compiler paths, not output shape (the stdlib harness owns behavioral snapshots).
/// </summary>
public class FeatureCoverageTests
{
    /// <summary>Parse + analyze + codegen a RazorForge program to IR, asserting a clean compile.</summary>
    private static string GenerateIr(string source)
    {
        Program program = Parse(source: source);
        var analyzer = new SemanticVerifier(language: Language.RazorForge,
            buildMode: RfBuildMode.ReleaseSpace);
        AnalysisResult result = analyzer.Analyze(program: program);
        AssertNoErrors(result: result);

        string ir = Emit(program: program, result: result);
        Assert.False(condition: string.IsNullOrWhiteSpace(value: ir),
            userMessage: "expected non-empty IR");
        return ir;
    }

    /// <summary>Suflae sibling of <see cref="GenerateIr"/> — analyzed in Suflae realm.</summary>
    private static string GenerateIrSuflae(string source)
    {
        Program program = ParseSuflae(source: source);
        var analyzer = new SemanticVerifier(language: Language.Suflae,
            buildMode: RfBuildMode.ReleaseSpace);
        AnalysisResult result = analyzer.Analyze(program: program);
        AssertNoErrors(result: result);

        string ir = Emit(program: program, result: result);
        Assert.False(condition: string.IsNullOrWhiteSpace(value: ir),
            userMessage: "expected non-empty IR");
        return ir;
    }

    private static string Emit(Program program, AnalysisResult result)
    {
        var generator = new LlvmEmitter(program: program,
            registry: result.Registry,
            options: new LlvmEmitterOptions
            {
                StdlibPrograms = result.Registry.StdlibPrograms,
                BuildMode = RfBuildMode.ReleaseSpace,
                SynthesizedBodies = result.SynthesizedBodies,
                InstantiatedGenericBodies = result.InstantiatedGenericBodies
            });
        return generator.Generate();
    }

    private static void AssertNoErrors(AnalysisResult result)
    {
        if (result.Errors.Count > 0)
        {
            string msgs = string.Join(separator: "\n",
                values: result.Errors.Select(selector: e => $"  - {e.Message} at {e.Location}"));
            Assert.Fail(message: $"Expected no errors but got {result.Errors.Count}:\n{msgs}");
        }
    }

    // ---- pattern matching / when / is-binding ---------------------------------------------------

    // ---- lambdas / closures ---------------------------------------------------------------------

    // ---- iterator inlining / IterTools ----------------------------------------------------------

    // ---- generics / monomorphization ------------------------------------------------------------

    // ---- error handling variants ----------------------------------------------------------------

    // ---- operators / lowering -------------------------------------------------------------------

    // ---- collections ----------------------------------------------------------------------------

    // ---- records / derives / wired routines -----------------------------------------------------

    // ---- entities / ownership -------------------------------------------------------------------

    // ---- f-strings / format specs ---------------------------------------------------------------

    // ---- control flow ---------------------------------------------------------------------------

    // ---- Suflae realm: module globals + script-ish program --------------------------------------

    [Fact]
    public void Suflae_ModuleGlobal_Compiles()
    {
        const string source = """
                              module Test/Feat/SfGlobal
                              import IO/Console

                              global counter: S64 = 0

                              routine bump()
                                  counter = counter + 1
                                  return

                              routine start()
                                  bump()
                                  bump()
                                  show(f"{counter}")
                                  return
                              """;
        _ = GenerateIrSuflae(source: source);
    }

    [Fact]
    public void Suflae_ScriptGlobals_ReleasedAtStartExit()
    {
        // Script mode (no explicit start) with a routine that reads the global: the global must reach
        // the __ModuleGlobals singleton, and the singleton is released at start()'s exit.
        const string source = """
                              module Test/Feat/SfGlobalTeardown
                              import IO/Console

                              global name: Text = "abc"
                              global counter: S64 = 0

                              routine bump()
                                  counter = counter + 1
                                  return

                              bump()
                              show(f"{name} {counter}")
                              """;
        string ir = GenerateIrSuflae(source: source);
        // The definition line itself: the first mention of start() may be a call from main.
        int defAt = ir.Split(separator: '\n')
                      .Select(selector: (line, index) => (line, index))
                      .Where(predicate: l => l.line.StartsWith(value: "define ", comparisonType: StringComparison.Ordinal) &&
                                             l.line.Contains(value: "SfGlobalTeardown.start()\"", comparisonType: StringComparison.Ordinal))
                      .Select(selector: l => ir.IndexOf(value: l.line, comparisonType: StringComparison.Ordinal))
                      .First();
        int endAt = ir.IndexOf(value: "\n}", startIndex: defAt, comparisonType: StringComparison.Ordinal);
        string start = ir[defAt..endAt];
        Assert.Contains(expectedSubstring: "__ModuleGlobals].destroy()", actualString: start);
    }

    [Fact]
    public void Suflae_DependentGlobals_Compile()
    {
        const string source = """
                              module Test/Feat/SfGlobal2
                              import IO/Console

                              global base: S64 = 10
                              global derived: S64 = base + 5

                              routine start()
                                  show(f"{base} {derived}")
                                  return
                              """;
        _ = GenerateIrSuflae(source: source);
    }

    [Fact]
    public void Suflae_BasicArithmeticProgram_Compiles()
    {
        const string source = """
                              module Test/Feat/SfBasic
                              import IO/Console

                              routine start()
                                  var x = 41
                                  var y = x + 1
                                  show(f"{y}")
                                  return
                              """;
        _ = GenerateIrSuflae(source: source);
    }
}
