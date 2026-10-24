using Builder.Declaration;
using Builder.Diagnostics;
using SyntaxTree;
using TypeModel.Enums;

namespace Suflae.Passes;

/// <summary>
/// ObjectHacker's <c>implant</c>, on the user programs before analysis, in a build that imports ObjectHacker. A member
/// routine of an entity is named by its type and name, as <c>Hero.attack</c>:
/// <list type="bullet">
/// <item><c>implant(target: hero, replacing: Hero.attack, with: f)</c> becomes
/// <c>var objecthacker_implant: Callable[(Hero, S64), S64] = f</c> (so a replacement of another signature is a build
/// error there) and <c>add_implant(key: ..., routine_key: "Hero.attack(power:)", replacement: Unknown(from: ...))</c>;</item>
/// <item><c>remove_implant(target: hero, replacing: Hero.attack)</c> becomes <c>drop_implant(...)</c>;</item>
/// <item>every member routine of an entity starts with a test: when the object has a replacement for it, the
/// replacement runs instead, given the object and the arguments. With no object hooked the test is one load and one
/// branch.</item>
/// </list>
/// A routine is named by its type, its name and its parameters' names, so overloads are told apart by the names.
/// </summary>
internal static class ImplantPass
{
    private const string ModuleName = "ObjectHacker";
    private const string Implant = "implant";
    private const string RemoveImplant = "remove_implant";
    private const string TargetPlaceholder = "objecthacker_implant_target";
    private const string ValuePlaceholder = "objecthacker_implant_value";
    private const string TypePlaceholder = "Reflected_Implant_Type";

    /// <summary>ObjectHacker's routines the written statements call, which a program cannot name itself.</summary>
    private static readonly HashSet<string> HelperNames =
    [
        "hooks_on", "own_hook_key", "hook_key", "has_implant", "implant_of", "add_implant", "drop_implant", "kept_as"
    ];

    internal static bool Run(List<(Program Program, string FilePath)> files,
        Action<SemanticDiagnosticCode, string, SourceLocation> report)
    {
        if (!files.Any(predicate: f => f.Program.Declarations.OfType<ImportDeclaration>()
                                         .Any(predicate: i => i.ModulePath == ModuleName)))
        {
            return true;
        }

        // An entity is known by its module and name: two modules of one build may each have a type of the same name.
        var entities = new HashSet<(string Module, string Name)>(collection: files.SelectMany(selector: f =>
            f.Program.Declarations.OfType<EntityDeclaration>()
             .Select(selector: e => (ModuleOf(program: f.Program, filePath: f.FilePath), e.Name))));
        var routines = new Dictionary<(string Owner, string Name), List<(RoutineDeclaration Routine, string FilePath)>>();
        foreach ((Program program, string declaringFile) in files)
        foreach (RoutineDeclaration routine in program.Declarations.OfType<RoutineDeclaration>())
        {
            if (Replaceable(routine: routine, module: ModuleOf(program: program, filePath: declaringFile), entities: entities))
            {
                (string, string) key = (routine.OwnerName!, routine.MemberRoutineName!);
                if (!routines.TryGetValue(key: key, value: out List<(RoutineDeclaration, string)>? list))
                {
                    routines[key: key] = list = [];
                }

                list.Add(item: (routine, declaringFile));
            }
        }

        bool ok = true;
        foreach ((Program program, string filePath) in files)
        {
            var calls = new CallRewriter(routines: routines, filePath: filePath, report: report);
            for (int i = 0; i < program.Declarations.Count; i++)
            {
                if (program.Declarations[index: i] is not RoutineDeclaration { Body: BlockStatement body } routine)
                {
                    continue;
                }

                Statement rewritten = calls.VisitStatement(stmt: body);
                if (Replaceable(routine: routine, module: ModuleOf(program: program, filePath: filePath), entities: entities) &&
                    Prologue(routine: routine, filePath: filePath) is { } prologue)
                {
                    var statements = new List<Statement> { prologue };
                    statements.AddRange(collection: rewritten is BlockStatement block ? block.Statements : [rewritten]);
                    rewritten = new BlockStatement(Statements: statements, Location: body.Location);
                }

                if (!ReferenceEquals(objA: rewritten, objB: body))
                {
                    program.Declarations[index: i] = routine with { Body = rewritten };
                }
            }

            ok &= calls.Ok;
        }

        return ok;
    }

    /// <summary>Whether a routine is a member routine of an entity of the program that an object can have replaced:
    /// not common, without type parameters, every parameter typed, and not one the builder wrote.</summary>
    private static bool Replaceable(RoutineDeclaration routine, string module, HashSet<(string Module, string Name)> entities)
    {
        return routine is { OwnerName: { } owner, MemberRoutineName: { } name, IsCommon: false, HasReceiverTypeArgs: false } &&
               entities.Contains(item: (module, owner)) && routine.GenericParameters is not { Count: > 0 } &&
               routine.Parameters.All(predicate: p => p.Type != null) &&
               !RuntimeContract.ProgramAnnotation.IsWritten(name: name);
    }

    /// <summary>The module a file belongs to: its <c>module</c> line, or its folder when it has none (a file without
    /// the line takes its module from where it is).</summary>
    private static string ModuleOf(Program program, string filePath)
    {
        return program.Declarations.OfType<ModuleDeclaration>().FirstOrDefault()?.Path ??
               Path.GetDirectoryName(path: filePath) ?? filePath;
    }

    /// <summary>The name a routine's replacement is kept under: its type, its name and its parameters' names.</summary>
    private static string KeyOf(RoutineDeclaration routine)
    {
        return $"{routine.OwnerName}.{routine.MemberRoutineName}(" +
               string.Concat(values: routine.Parameters.Select(selector: p => p.Name + ":")) + ")";
    }

    private static string Quote(string text)
    {
        return "\"" + text.Replace(oldValue: "\\", newValue: "\\\\").Replace(oldValue: "\"", newValue: "\\\"")
                          .Replace(oldValue: "{", newValue: "{{").Replace(oldValue: "}", newValue: "}}") + "\"";
    }

    /// <summary>The type of a replacement: the routine's object first, then its parameters.</summary>
    private static TypeExpression CallableOf(RoutineDeclaration routine, string filePath)
    {
        var placeholders = new Dictionary<string, TypeExpression>(comparer: StringComparer.Ordinal)
        {
            [key: "Reflected_P0"] = new TypeExpression(Name: routine.OwnerName!, GenericArguments: null,
                Location: routine.Location)
        };
        var names = new List<string> { "Reflected_P0" };
        for (int i = 0; i < routine.Parameters.Count; i++)
        {
            string name = $"Reflected_P{i + 1}";
            placeholders[key: name] = routine.Parameters[index: i].Type!;
            names.Add(item: name);
        }

        string returns = "None";
        if (routine.ReturnType is { } returnType)
        {
            placeholders[key: "Reflected_R"] = returnType;
            returns = "Reflected_R";
        }

        string tuple = names.Count == 1 ? $"({names[index: 0]},)" : $"({string.Join(separator: ", ", values: names)})";
        Statement parsed = Parse(source: $"    var objecthacker_type: Callable[{tuple}, {returns}] = 0\n",
            filePath: filePath, line: routine.Location.Line)!;
        TypeExpression written = ((parsed as BlockStatement)!.Statements[index: 0] as DeclarationStatement)!
                                .Declaration is VariableDeclaration { Type: { } type }
            ? type
            : throw new InvalidOperationException(message: "The builder's replacement type did not parse.");
        TypeExpression filled = Fill(type: written, placeholders: placeholders);
        return routine.IsFailable ? filled with { IsFailable = true } : filled;
    }

    private static TypeExpression Fill(TypeExpression type, Dictionary<string, TypeExpression> placeholders)
    {
        if (type.GenericArguments == null && placeholders.TryGetValue(key: type.Name, value: out TypeExpression? found))
        {
            return Copy(type: found);
        }

        return type.GenericArguments == null
            ? type
            : type with
            {
                GenericArguments = type.GenericArguments.Select(selector: a => Fill(type: a, placeholders: placeholders))
                                       .ToList()
            };
    }

    private static TypeExpression Copy(TypeExpression type)
    {
        return type with { GenericArguments = type.GenericArguments?.Select(selector: Copy).ToList() };
    }

    private static bool Returns(RoutineDeclaration routine)
    {
        return routine.ReturnType is { Name: not ("None" or "Blank") };
    }

    /// <summary>The test at the top of a member routine: when the object has a replacement, it runs instead.</summary>
    private static Statement? Prologue(RoutineDeclaration routine, string filePath)
    {
        string key = Quote(text: KeyOf(routine: routine));
        string arguments = string.Concat(values: routine.Parameters.Select(selector: p => ", " + p.Name));
        string call = $"objecthacker_replacement(objecthacker_self{arguments})";
        string source =
            $"    if hooks_on() and has_implant(key: me.own_hook_key(), routine_key: {key})\n" +
            "        var objecthacker_self = me\n" +
            $"        var objecthacker_replacement = implant_of(key: me.own_hook_key(), routine_key: {key}).kept_as[{TypePlaceholder}]()\n" +
            (Returns(routine: routine) ? $"        return {call}\n" : $"        {call}\n        return\n");
        if (Parse(source: source, filePath: filePath, line: routine.Location.Line) is not BlockStatement block)
        {
            return null;
        }

        return new Placeholders(target: null, value: null,
                types: new Dictionary<string, TypeExpression> { [key: TypePlaceholder] = CallableOf(routine: routine, filePath: filePath) })
           .VisitStatement(stmt: block.Statements[index: 0]);
    }

    private static Statement? Parse(string source, string filePath, int line)
    {
        string text = new string(c: '\n', count: Math.Max(val1: 0, val2: line - 2)) +
                      $"routine reflected_written()\n{source}";
        List<Builder.Tokenizer.Token> tokens =
            Builder.Tokenizer.Lexers.Tokenize(source: text, fileName: filePath, language: Language.Suflae);
        var parser = new Builder.Parser.Parser(tokens: tokens, language: Language.Suflae, fileName: filePath)
        {
            PrintsErrors = false
        };
        Program parsed = parser.Parse();
        return parser.HasErrors ? null : parsed.Declarations.OfType<RoutineDeclaration>().FirstOrDefault()?.Body;
    }

    /// <summary>Rewrites <c>implant(...)</c> and <c>remove_implant(...)</c>.</summary>
    private sealed class CallRewriter(
        Dictionary<(string Owner, string Name), List<(RoutineDeclaration Routine, string FilePath)>> routines,
        string filePath,
        Action<SemanticDiagnosticCode, string, SourceLocation> report) : AstRewriter
    {
        public bool Ok { get; private set; } = true;

        protected override Statement VisitExpressionStatement(ExpressionStatement s)
        {
            if (s.Expression is CallExpression { Callee: IdentifierExpression { Name: Implant } } call &&
                Named(call: call) is (Expression target, Expression routine, var with) && Find(routine: routine, at: call.Location) is { } found)
            {
                if (with == null)
                {
                    Fail(message: "'implant' takes the replacement as 'with:' (a routine value of the member routine's " +
                                  "signature, given the object first).", at: call.Location);
                    return s;
                }

                string source =
                    $"    var objecthacker_implant: {TypePlaceholder} = {ValuePlaceholder}\n" +
                    $"    add_implant(key: hook_key(target: {TargetPlaceholder}), target: Unknown(from: {TargetPlaceholder}), routine_key: {Quote(text: KeyOf(routine: found))}, replacement: Unknown(from: objecthacker_implant))\n";
                if (Parse(source: source, filePath: filePath, line: s.Location.Line) is not BlockStatement block)
                {
                    return s;
                }

                var fill = new Placeholders(target: target, value: with,
                    types: new Dictionary<string, TypeExpression> { [key: TypePlaceholder] = CallableOf(routine: found, filePath: filePath) });
                // The local that takes the replacement says which routine it replaces, so a replacement of another
                // signature is reported as one, naming the routine.
                List<Statement> statements = block.Statements.Select(selector: fill.VisitStatement)
                                                  .Select(selector: statement => statement is DeclarationStatement
                                                       {
                                                           Declaration: VariableDeclaration variable
                                                       } declaration
                                                       ? declaration with
                                                       {
                                                           Declaration = variable with
                                                           {
                                                               ReplacementOf = $"{found.OwnerName}.{found.MemberRoutineName}",
                                                               ReplacedRoutineAt = found.Location
                                                           }
                                                       }
                                                       : statement)
                                                  .ToList();
                return new BlockStatement(Statements: statements, Location: s.Location);
            }

            return base.VisitExpressionStatement(s: s);
        }

        public override Expression VisitExpression(Expression expr)
        {
            if (expr is CallExpression { Callee: IdentifierExpression { Name: RemoveImplant } } call &&
                Named(call: call) is (Expression target, Expression routine, _) && Find(routine: routine, at: call.Location) is { } found &&
                Parse(source: $"    return drop_implant(key: hook_key(target: {TargetPlaceholder}), routine_key: {Quote(text: KeyOf(routine: found))})\n",
                    filePath: filePath, line: call.Location.Line) is BlockStatement { Statements: [ReturnStatement { Value: { } value }] })
            {
                return new Placeholders(target: target, value: null, types: []).VisitExpression(expr: value);
            }

            return base.VisitExpression(expr: expr);
        }

        /// <summary>The <c>target:</c>, <c>routine:</c> and <c>with:</c> arguments of an implant call, or null when the
        /// call is not written with them.</summary>
        private (Expression Target, Expression Routine, Expression? With)? Named(CallExpression call)
        {
            Expression? target = null;
            Expression? routine = null;
            Expression? with = null;
            foreach (Expression argument in call.Arguments)
            {
                switch (argument)
                {
                    case NamedArgumentExpression { Name: "target", Value: var value }:
                        target = value;
                        break;
                    case NamedArgumentExpression { Name: "replacing", Value: var value }:
                        routine = value;
                        break;
                    case NamedArgumentExpression { Name: "with", Value: var value }:
                        with = value;
                        break;
                }
            }

            if (target == null || routine == null)
            {
                Fail(message: "'implant' and 'remove_implant' take the object as 'target:' and the member routine as " +
                              "'replacing:', written as its type and name ('replacing: Hero.attack').", at: call.Location);
                return null;
            }

            return (target, routine, with);
        }

        private RoutineDeclaration? Find(Expression routine, SourceLocation at)
        {
            if (routine is not MemberExpression { Object: IdentifierExpression { Name: var owner }, MemberName: var name })
            {
                Fail(message: "Name the member routine by its type and name, as 'replacing: Hero.attack'.", at: at);
                return null;
            }

            if (!routines.TryGetValue(key: (owner, name), value: out List<(RoutineDeclaration Routine, string FilePath)>? declared))
            {
                Fail(message: $"'{owner}.{name}' is not a member routine of an entity of the program that an object can " +
                              "have replaced (a common routine, a routine with type parameters or one with a parameter " +
                              "without a type cannot).", at: at);
                return null;
            }

            // A type of the call's own file is the one its name means there. Another file's type of the same name
            // (another module) is used only when the file declares none.
            List<RoutineDeclaration> found = declared.Where(predicate: d => d.FilePath == filePath)
                                                     .Select(selector: d => d.Routine).ToList();
            if (found.Count == 0)
            {
                found = declared.Select(selector: d => d.Routine).ToList();
            }

            if (found.Count > 1)
            {
                Fail(message: $"'{owner}.{name}' has {found.Count} overloads, and 'replacing: {owner}.{name}' names one. " +
                              "Give them different names to implant one.", at: at);
                return null;
            }

            return found[index: 0];
        }

        private void Fail(string message, SourceLocation at)
        {
            report(SemanticDiagnosticCode.InvalidAnnotation, message, at);
            Ok = false;
        }
    }

    /// <summary>Puts the call's own target and value, and the routine's types, where the written text names their
    /// placeholders, and marks the calls of ObjectHacker's helpers as the builder's.</summary>
    private sealed class Placeholders(Expression? target, Expression? value, Dictionary<string, TypeExpression> types)
        : AstRewriter
    {
        public override Statement VisitStatement(Statement stmt)
        {
            if (stmt is DeclarationStatement { Declaration: VariableDeclaration { Type: { } written } variable } declaration &&
                types.TryGetValue(key: written.Name, value: out TypeExpression? type))
            {
                stmt = declaration with { Declaration = variable with { Type = Copy(type: type) } };
            }

            return base.VisitStatement(stmt: stmt);
        }

        public override Expression VisitExpression(Expression expr)
        {
            switch (expr)
            {
                case IdentifierExpression { Name: TargetPlaceholder } when target != null:
                    return target;
                case IdentifierExpression { Name: ValuePlaceholder } when value != null:
                    return value;
                // The base rewriter leaves a lambda's body alone, and the written replacement is a lambda.
                case LambdaExpression lambda:
                    return lambda with { Body = VisitExpression(expr: lambda.Body) };
            }

            Expression visited = base.VisitExpression(expr: expr);
            List<TypeExpression>? typeArguments = visited switch
            {
                GenericMemberRoutineCallExpression g => g.TypeArguments,
                GenericMemberExpression g => g.TypeArguments,
                _ => null
            };
            if (typeArguments != null)
            {
                for (int i = 0; i < typeArguments.Count; i++)
                {
                    if (types.TryGetValue(key: typeArguments[index: i].Name, value: out TypeExpression? type))
                    {
                        typeArguments[index: i] = Copy(type: type);
                    }
                }
            }

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
    }
}
