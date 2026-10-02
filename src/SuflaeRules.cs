using Builder.Declaration;
using Builder.Diagnostics;
using Builder.Frontends;
using Builder.Lowering;
using Builder.Tokenizer;
using RazorForge;
using Suflae.Lexer;
using Suflae.Passes;
using SyntaxTree;
using TypeModel.Enums;

namespace Suflae;

/// <summary>
/// Suflae's rules: entities are shared, reference-counted handles (<c>Roamed[E]</c>) that may be none,
/// module-level <c>global</c>s live on one locked singleton, a container reshaped while it is looped over
/// crashes at run time, the memory-unsafe surface is hidden, and unsuffixed numbers default to arbitrary
/// precision. Suflae reads its own standard library on top of RazorForge's.
/// </summary>
public sealed class SuflaeRules : LanguageRules
{
    /// <summary>The one instance the front end registers.</summary>
    public static readonly SuflaeRules Instance = new();

    private SuflaeRules()
    {
    }

    public override Language Language => Language.Suflae;
    public override string Name => "Suflae";
    public override string ToolName => "suflae";
    public override string Version => RazorForgeRules.VersionOf(assembly: typeof(SuflaeRules).Assembly);
    public override string FileExtension => ".sf";
    public override string ShortName => "SF";
    public override bool BareSourceRuns => true;
    public override bool HasOwnStandardLibrary => true;

    /// <summary>
    /// <c>Numerics</c> brings in only <c>Integer</c>: Suflae's bare numeric vocabulary is
    /// <c>Integer</c>/<c>Decimal</c> (an unsuffixed <c>6</c> is an <c>Integer</c>, which lives in
    /// <c>Numerics</c>, not Core; <c>Decimal</c> is in Core), the fixed-width scalars come with the Core
    /// prelude, and the arbitrary-precision <c>Real</c>/<c>Complex</c> still need an explicit
    /// <c>import Numerics</c>. Console and file I/O are always available, so <c>show(...)</c> needs no
    /// import.
    /// </summary>
    public override IReadOnlyList<(string Module, IReadOnlyList<string>? Symbols)> PreludeImports { get; } =
        [("Numerics", ["Integer"]), ("IO/Console", null), ("IO/File", null)];

    public override List<Token> Tokenize(string source, string fileName)
    {
        return new SuflaeLexer(source: source, fileName: fileName).Tokenize();
    }

    /// <inheritdoc/>
    public override LanguageServerProfile LanguageServer { get; } = new SuflaeLanguageServer();

    /// <inheritdoc/>
    public override (List<Token> Tokens, List<CommentTrivia> Comments) TokenizeWithComments(string source,
        string fileName)
    {
        var lexer = new SuflaeLexer(source: source, fileName: fileName);
        return (lexer.Tokenize(), lexer.Comments);
    }

    public override bool AllowsUnsafeCode => false;
    public override bool HasPassStatement => false;
    public override bool HasThreadedRoutines => false;
    public override bool HasModuleGlobals => true;
    public override bool HasTargetDirectives => false;
    public override bool DefaultsToArbitraryPrecision => true;
    public override bool HasDataSizeQuery => false;

    public override bool EntitiesAreShared => true;
    public override bool ChecksOwnership => false;
    public override bool ChecksAccessTokens => false;
    public override bool ChecksReadonly => false;
    public override bool ChecksShapeAtRunTime => true;

    public override bool RequiresLateinitForDeferredInit => false;
    public override bool RequiresInferableLambdaParameters => false;
    public override bool RequiresEnterableForUsing => false;
    public override bool RewritesDisplayWrapperArguments => false;

    public override string ScriptVariableAdvice(string name)
    {
        return $"Pass it in as an argument, or declare it as 'global {name}: <Type> = ...' to share it " +
               "across routines.";
    }

    public override bool SynthesizeBeforeAnalysis(List<(Program Program, string FilePath)> files,
        Action<SemanticDiagnosticCode, string, SourceLocation> report)
    {
        return ModuleGlobalsSynthesisPass.Run(orderedFiles: files, report: report);
    }

    public override void SynthesizeStandardLibrary(
        IReadOnlyList<(Program Program, string FilePath, string Module)> programs)
    {
        WrapperForwarderSynthesis.Run(programs: programs);
    }

    public override void LowerEntities(Program program, TypeRegistry registry)
    {
        new EntityLoweringPass(registry: registry).Run(program: program);
    }

    public override void LowerFirst(PostprocessingContext ctx, Program program)
    {
        new GlobalEntityRewritePass(ctx: ctx).Run(program: program);
    }

    public override void LowerFirstInVariantBodies(PostprocessingContext ctx)
    {
        new GlobalEntityRewritePass(ctx: ctx).RunOnVariantBodies();
    }

    public override void LowerShapeUse(PostprocessingContext ctx, Program program)
    {
        new ShapeUseLoweringPass(ctx: ctx).Run(program: program);
    }
}
