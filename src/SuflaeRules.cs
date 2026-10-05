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
    public override bool HasOwnStandardLibrary => true;

    /// <summary>
    /// <c>Numerics</c> brings in only <c>Integer</c>: Suflae's bare numeric vocabulary is
    /// <c>Integer</c>/<c>Decimal</c> (an unsuffixed <c>6</c> is an <c>Integer</c>, which lives in
    /// <c>Numerics</c>, not Core; <c>Decimal</c> is in Core), the fixed-width scalars come with the Core
    /// prelude, and the rest of <c>Numerics</c> (the <c>Integer</c>-argument forms of the integer
    /// routines) still needs an explicit <c>import Numerics</c>. Console and file I/O are always available,
    /// so <c>show(...)</c> needs no import.
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

    public override bool AllowsUnsafeCode => true;
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
    public override bool ZeroIsEmptyPlace => true;

    /// <summary>A Suflae user writes and reads <c>Account</c>, never the <c>Roamed[Account]</c> handle the
    /// builder carries an entity in.</summary>
    public override TypeModel.Types.TypeSymbol SurfaceType(TypeModel.Types.TypeSymbol type)
    {
        return type is TypeModel.Types.RecordTypeSymbol
        {
            GenericDefinition.Name: RuntimeContract.Roamed, TypeArguments: [var entity]
        }
            ? entity
            : type;
    }

    /// <summary>Drops every <c>Roamed[...]</c> wrapper from the text, keeping what it wraps, at any depth
    /// (<c>List[Roamed[Account]]</c> reads <c>List[Account]</c>).</summary>
    public override string SurfaceText(string text)
    {
        const string wrapper = RuntimeContract.Roamed + "[";
        int at = FindWrapper(text: text, wrapper: wrapper, from: 0);
        while (at >= 0)
        {
            int close = MatchingBracket(text: text, open: at + wrapper.Length - 1);
            if (close < 0)
            {
                break;
            }

            text = text[..at] + text[(at + wrapper.Length)..close] + text[(close + 1)..];
            at = FindWrapper(text: text, wrapper: wrapper, from: at);
        }

        return text;
    }

    /// <summary>The next <paramref name="wrapper"/> that starts a name (not the tail of a longer one), or -1.</summary>
    private static int FindWrapper(string text, string wrapper, int from)
    {
        for (int at = text.IndexOf(value: wrapper, startIndex: from, comparisonType: StringComparison.Ordinal);
             at >= 0;
             at = text.IndexOf(value: wrapper, startIndex: at + 1, comparisonType: StringComparison.Ordinal))
        {
            if (at == 0 || !(char.IsLetterOrDigit(c: text[index: at - 1]) || text[index: at - 1] == '_'))
            {
                return at;
            }
        }

        return -1;
    }

    /// <summary>The <c>]</c> that closes the <c>[</c> at <paramref name="open"/>, or -1 when it is unbalanced.</summary>
    private static int MatchingBracket(string text, int open)
    {
        int depth = 0;
        for (int i = open; i < text.Length; i++)
        {
            if (text[index: i] == '[')
            {
                depth++;
            }
            else if (text[index: i] == ']' && --depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

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

    /// <summary>The standard types a Suflae program never sees: the split (struct-of-arrays) collections choose a
    /// memory layout, which a Suflae program does not, and RazorForge's `CircularList` is Suflae's `List`, which
    /// adds and removes at both ends.</summary>
    public override IReadOnlySet<string> HiddenStandardTypes { get; } =
        new HashSet<string>(collection: ["Collections.SplitList", "Collections.SplitArray", "Collections.CircularList"],
            comparer: StringComparer.Ordinal);

    public override void SynthesizeStandardLibrary(
        IReadOnlyList<(Program Program, string FilePath, string Module)> programs)
    {
        WrapperForwarderSynthesis.Run(programs: programs);
    }

    public override void LowerEntities(Program program, TypeRegistry registry)
    {
        new EntityLoweringPass(registry: registry).Run(program: program);
    }

    public override RoutineDeclaration LowerEntitiesInRoutine(RoutineDeclaration routine, TypeRegistry registry)
    {
        return new EntityLoweringPass(registry: registry).LowerRoutine(r: routine);
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
