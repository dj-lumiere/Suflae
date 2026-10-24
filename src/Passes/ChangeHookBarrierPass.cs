using SyntaxTree;
using TypeModel.Enums;

namespace Suflae.Passes;

/// <summary>
/// Lets ObjectHacker's <c>when_changed</c> hooks see every change of a member variable, on the user programs before
/// analysis. Only a build that imports ObjectHacker somewhere is changed, and then every file of it is (an object
/// made in one file can be hooked in another), each file importing ObjectHacker for the routines written here. Each
/// statement <c>x.f = v</c> becomes:
/// <code>
/// if hooks_on()
///     x.changing_member(memvar: "f")
/// x.f = v
/// if hooks_on()
///     x.changed_member(memvar: "f")
/// </code>
/// in a block of its own (<c>x.f += v</c> the same way). <c>hooks_on()</c> reads how many objects have hooks, so with
/// none a change costs two loads and two branches more. It is read twice rather than kept in a local, since every
/// local gets a cleanup entry. <c>changing_member</c> keeps the old value and <c>changed_member</c> runs the hooks. For
/// a record both do nothing. Only a target written as a name or a chain of member variables is changed this way, so
/// the target is read again without running anything twice.
/// </summary>
internal static class ChangeHookBarrierPass
{
    private const string ModuleName = "ObjectHacker";
    private const string TargetPlaceholder = "objecthacker_hook_target";
    private const string StorePlaceholder = "objecthacker_hook_store";

    /// <summary>ObjectHacker's routines the written statements call, which a program cannot name itself.</summary>
    private static readonly HashSet<string> HelperNames = ["hooks_on", "changing_member", "changed_member"];

    internal static void Run(List<(Program Program, string FilePath)> files)
    {
        if (!files.Any(predicate: f => f.Program.Declarations.OfType<ImportDeclaration>()
                                         .Any(predicate: i => i.ModulePath == ModuleName)))
        {
            return;
        }

        foreach ((Program program, string filePath) in files)
        {
            EnsureImport(program: program);
            var barrier = new Barrier(filePath: filePath);
            for (int i = 0; i < program.Declarations.Count; i++)
            {
                if (program.Declarations[index: i] is RoutineDeclaration { Body: { } body } routine)
                {
                    Statement rewritten = barrier.VisitStatement(stmt: body);
                    if (!ReferenceEquals(objA: rewritten, objB: body))
                    {
                        program.Declarations[index: i] = routine with { Body = rewritten };
                    }
                }
            }
        }
    }

    private static void EnsureImport(Program program)
    {
        if (program.Declarations.OfType<ImportDeclaration>().Any(predicate: i => i.ModulePath == ModuleName))
        {
            return;
        }

        int at = program.Declarations.FindIndex(match: d => d is ImportDeclaration);
        if (at < 0)
        {
            at = program.Declarations.FindIndex(match: d => d is ModuleDeclaration) + 1;
        }

        program.Declarations.Insert(index: at,
            item: new ImportDeclaration(ModulePath: ModuleName, Alias: null, SpecificImports: null,
                Location: program.Location));
    }

    private sealed class Barrier(string filePath) : AstRewriter
    {
        // The parser writes `x.f = v` (and `x.f += v`) as an expression statement, analysis makes it an assignment.
        protected override Statement VisitExpressionStatement(ExpressionStatement s)
        {
            Expression? changed = s.Expression switch
            {
                BinaryExpression { Operator: BinaryOperator.Assign, Left: var left } => left,
                CompoundAssignmentExpression { Target: var compound } => compound,
                _ => null
            };
            if (changed is not MemberExpression { Object: var target, MemberName: var memvar } ||
                !IsPlainPath(expr: target))
            {
                return s;
            }

            string source =
                "routine reflected_written()\n" +
                "    if hooks_on()\n" +
                $"        {TargetPlaceholder}.changing_member(memvar: \"{memvar}\")\n" +
                $"    {StorePlaceholder} = 0\n" +
                "    if hooks_on()\n" +
                $"        {TargetPlaceholder}.changed_member(memvar: \"{memvar}\")\n";
            string text = new string(c: '\n', count: Math.Max(val1: 0, val2: s.Location.Line - 1)) + source;
            List<Builder.Tokenizer.Token> tokens =
                Builder.Tokenizer.Lexers.Tokenize(source: text, fileName: filePath, language: Language.Suflae);
            var parser = new Builder.Parser.Parser(tokens: tokens, language: Language.Suflae, fileName: filePath)
            {
                PrintsErrors = false
            };
            Program parsed = parser.Parse();
            if (parser.HasErrors ||
                parsed.Declarations.OfType<RoutineDeclaration>().FirstOrDefault()?.Body is not BlockStatement block)
            {
                return s;
            }

            var placed = new List<Statement>();
            foreach (Statement statement in block.Statements)
            {
                placed.Add(item: statement is ExpressionStatement
                {
                    Expression: BinaryExpression { Left: IdentifierExpression { Name: StorePlaceholder } }
                }
                    ? s
                    : new TargetFill(target: target).VisitStatement(stmt: statement));
            }

            return new BlockStatement(Statements: placed, Location: s.Location);
        }

        /// <summary>A name or a chain of member variables (<c>hero</c>, <c>me</c>, <c>game.hero</c>).</summary>
        private static bool IsPlainPath(Expression expr)
        {
            return expr switch
            {
                IdentifierExpression => true,
                MemberExpression { Object: var inner } => IsPlainPath(expr: inner),
                _ => false
            };
        }
    }

    /// <summary>Puts a copy of the change's target where the written text names the placeholder, and marks the calls
    /// of ObjectHacker's helpers as the builder's.</summary>
    private sealed class TargetFill(Expression target) : AstRewriter
    {
        public override Expression VisitExpression(Expression expr)
        {
            if (expr is IdentifierExpression { Name: TargetPlaceholder })
            {
                return Copy(expr: target);
            }

            Expression visited = base.VisitExpression(expr: expr);
            if (visited is CallExpression { Callee: var callee } call &&
                HelperNames.Contains(item: callee switch
                {
                    IdentifierExpression id => id.Name,
                    MemberExpression member => member.MemberName,
                    _ => ""
                }))
            {
                call.IsSynthesizedLowering = true;
            }

            return visited;
        }

        private static Expression Copy(Expression expr)
        {
            return expr switch
            {
                IdentifierExpression id => id with { },
                MemberExpression member => member with { Object = Copy(expr: member.Object) },
                _ => expr
            };
        }
    }
}
