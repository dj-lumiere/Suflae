using Builder.Declaration;
using Builder.Desugaring;
using Builder.Diagnostics;
using Builder.Tokenizer;
using SyntaxTree;

namespace Suflae.Passes;

/// <summary>
/// Suflae <c>global</c> storage synthesis, run on the user programs at the start of semantic analysis so
/// every entry point (build, check, codegen, tests) sees the same program shape.
/// </summary>
internal static class ModuleGlobalsSynthesisPass
{
    /// <summary>The synthesized entity holding every global as a field.</summary>
    internal const string ModuleGlobalsEntityName = RuntimeContract.ModuleGlobals.EntityName;

    /// <summary>The hidden singleton holding the one <see cref="ModuleGlobalsEntityName"/> instance. Every
    /// module-level global access <c>g</c> is rewritten to <c>__globals__.g</c> by GlobalEntityRewritePass.</summary>
    internal const string ModuleGlobalsSingletonName = RuntimeContract.ModuleGlobals.SingletonName;

    /// <summary>
    /// Suflae <c>global</c> eager initialization + thread-safe storage synthesis. Each module-level
    /// <c>global name: T = init</c> becomes a FIELD of one hidden per-program entity
    /// <see cref="ModuleGlobalsEntityName"/>, stored behind a single <c>Roamed</c> singleton
    /// <see cref="ModuleGlobalsSingletonName"/> (constructed + promoted at the top of <c>start()</c>).
    /// Field initializers run in dependency order (a global's init may read another global; a cycle —
    /// including self-reference, including through a free-routine call — is RF-S436). Initializers that
    /// touch no other global are folded straight into the singleton constructor; a dependent initializer
    /// runs as an ordered field assignment AFTER construction so its read sees the already-initialized
    /// field. The original <c>global</c> declarations are kept (init stripped) ONLY so semantic analysis
    /// registers them and stamps <c>IdentifierExpression.IsModuleGlobal</c> on every reference;
    /// GlobalEntityRewritePass then deletes them and rewrites each stamped reference to
    /// <c>__globals__.field</c>.
    /// Returns false after reporting through <paramref name="report"/> when the globals cannot be
    /// synthesized (RF-S436 / RF-S437 / RF-S438).
    /// </summary>
    internal static bool Run(List<(SyntaxTree.Program Program, string FilePath)> orderedFiles,
        Action<SemanticDiagnosticCode, string, SourceLocation> report)
    {
        if (!ModuleGlobalOrder.TryOrder(orderedFiles: orderedFiles, report: report,
                out List<ModuleGlobal> globals,
                out List<HashSet<int>> deps, out List<int> order))
        {
            return false;
        }

        if (globals.Count == 0)
        {
            return true;
        }

        // The synthesized declarations and the singleton's construction sit in the file of `start()` and carry
        // its location, so their names resolve in that file, which declares them, even when the first global is
        // in a module it imports.
        if (ModuleGlobalOrder.FindStart(orderedFiles: orderedFiles, report: report, at: globals[index: 0].Loc) is not
            ({ } startProgram, { } startBlock))
        {
            return false;
        }

        if (!TryBuildModuleGlobalsSynthesis(globals: globals,
                deps: deps,
                order: order,
                anchor: startBlock.Location,
                report: report,
                entityDecl: out EntityDeclaration entityDecl,
                singletonDecl: out VariableDeclaration singletonDecl,
                initStmts: out List<Statement> initStmts))
        {
            return false;
        }

        // Append (NOT prepend) the synthesized declarations: imports must stay at the top of the
        // file (RF-S114). Declaration order does not matter for type collection.
        startProgram.Declarations.Add(item: entityDecl);
        startProgram.Declarations.Add(item: singletonDecl);
        startBlock.Statements.InsertRange(index: 0, collection: initStmts);
        return true;
    }

    /// <summary>Builds the synthesized entity declaration, singleton declaration, and the init-statement
    /// list that gets prepended to <c>start()</c>. Returns false (after reporting RF-S437) if a dependent
    /// field has a type with no synthesizable default.</summary>
    private static bool TryBuildModuleGlobalsSynthesis(
        List<ModuleGlobal> globals,
        List<HashSet<int>> deps, List<int> order, SourceLocation anchor,
        Action<SemanticDiagnosticCode, string, SourceLocation> report, out EntityDeclaration entityDecl,
        out VariableDeclaration singletonDecl, out List<Statement> initStmts)
    {
        int n = globals.Count;
        SourceLocation loc0 = anchor;

        // 3) Build the __ModuleGlobals entity, one field per global (`name: Type`, no initializer). Two modules'
        //    globals of one name get a field each, named with their module.
        var fieldDecls = new List<SyntaxTree.Declaration>(capacity: n);
        for (int i = 0; i < n; i++)
        {
            fieldDecls.Add(item: new VariableDeclaration(Name: globals[index: i].StorageName,
                Type: globals[index: i].Type,
                Initializer: null,
                Visibility: VisibilityModifier.Open,
                Location: globals[index: i].Loc));
        }

        entityDecl = new EntityDeclaration(Name: ModuleGlobalsEntityName,
            GenericParameters: null,
            Protocols: new List<TypeExpression>(),
            Members: fieldDecls,
            Visibility: VisibilityModifier.Open,
            Location: loc0);

        // 4) Constructor arguments: independent fields use their real initializer; dependent fields
        //    are seeded with a type default and get their real value from a post-construction assignment.
        var ctorArgs = new List<(string Name, Expression Value)>(capacity: n);
        for (int i = 0; i < n; i++)
        {
            Expression value;
            if (deps[index: i].Count == 0)
            {
                value = globals[index: i].Init;
            }
            else
            {
                LiteralExpression? def = DefaultInitializerFor(type: globals[index: i].Type,
                    loc: globals[index: i].Loc);
                if (def == null)
                {
                    report(SemanticDiagnosticCode.ModuleGlobalDependentInitUnsupported,
                        $"The global '{globals[index: i].Name}: {globals[index: i].Type.Name}' has an " +
                        "initializer that reads another global, which only works for number, Text and Bool " +
                        "globals. Initialize it without reading other globals.",
                        globals[index: i].Loc);
                    singletonDecl = null!;
                    initStmts = null!;
                    return false;
                }

                value = def;
            }

            ctorArgs.Add(item: (globals[index: i].StorageName, value));
        }

        var construct = new CreatorExpression(TypeName: ModuleGlobalsEntityName,
            TypeArguments: null,
            MemberVariables: ctorArgs,
            Location: loc0);

        // 5) Init statements: construct + promote singleton, then ordered assignments for dependent globals.
        initStmts = new List<Statement>(capacity: n + 1)
        {
            new AssignmentStatement(
                Target: new IdentifierExpression(Name: ModuleGlobalsSingletonName,
                    Location: loc0),
                Value: construct,
                Location: loc0) { IsGlobalInit = true }
        };
        foreach (int i in order)
        {
            if (deps[index: i].Count == 0)
            {
                continue; // independent — already set by the constructor
            }

            // Marked as a global's initialization so analysis reads both sides in the global's own file and module:
            // the name assigned to is that module's global even when another module has one of the same name.
            initStmts.Add(item: new AssignmentStatement(
                Target: new IdentifierExpression(Name: globals[index: i].Name,
                    Location: globals[index: i].Loc),
                Value: globals[index: i].Init,
                Location: globals[index: i].Loc) { IsGlobalInit = true });
        }

        // 6) The singleton declaration — an entity `global` stored behind a promoted Roamed[E] handle.
        singletonDecl = new VariableDeclaration(Name: ModuleGlobalsSingletonName,
            Type: new TypeExpression(Name: ModuleGlobalsEntityName,
                GenericArguments: null,
                Location: loc0),
            Initializer: null,
            Visibility: VisibilityModifier.Open,
            Location: loc0,
            IsGlobal: true);
        return true;
    }

    /// <summary>A build-time default value for a <c>global</c> whose initializer depends on another
    /// global (so its real value is assigned after the singleton is constructed). A bare integer literal
    /// <c>0</c> conforms to any numeric type (int/float/decimal) via RF-S767; Text and Bool have their
    /// own empty/false defaults. Returns null for a type with no synthesizable default.</summary>
    private static LiteralExpression? DefaultInitializerFor(TypeExpression type,
        SourceLocation loc)
    {
        return type.Name switch
        {
            "Text" => new LiteralExpression(Value: "",
                LiteralType: TokenType.TextLiteral,
                Location: loc),
            "Bool" => new LiteralExpression(Value: false,
                LiteralType: TokenType.False,
                Location: loc),
            "S8" or "S16" or "S32" or "S64" or "S128" or "S256" or "U8" or "U16" or "U32" or "U64"
                or "U128" or "U256" or "B16" or "B32" or "B64" or "B128" or "F256" or "Decimal"
                or "D32" or "D64" or "D128" or "Integer" => new LiteralExpression(Value: "0",
                    LiteralType: TokenType.UndecidedInteger,
                    Location: loc),
            _ => null
        };
    }
}
