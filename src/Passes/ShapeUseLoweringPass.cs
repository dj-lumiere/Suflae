using Builder.Declaration;
using Builder.Lowering;
using Builder.Lowering.Passes;
using SyntaxTree;
using TypeModel.Symbols;
using TypeModel.Types;

namespace Suflae.Passes;

/// <summary>
/// Opens a SHAPE USE of a container while its element positions are in use, so a change that could move the
/// elements crashes instead of leaving the user of those positions pointing at the wrong place (the
/// IterGuard). The shape is the element count and positions; element values may still change. Two uses:
/// <list type="bullet">
/// <item>an <c>each</c> loop over a named container (a <see cref="LoopStatement"/> whose
/// <see cref="LoopStatement.IterationSourceName"/> is set): <c>xs.begin_shape_use()</c> before the loop,
/// <c>xs.end_shape_use()</c> after it and before every <c>return</c> inside it;</item>
/// <item>a statement that reaches an entity element through a <c>modify_at</c>/<c>view_at</c> token
/// (built by OperatorLoweringPass): <c>begin_shape_use</c> before the statement and <c>end_shape_use</c>
/// after it. A <c>return</c> value or an <c>if</c> condition holding such a token is first bound to a
/// temporary, so the use closes right after its evaluation.</item>
/// </list>
/// The count lives on the entity's <c>Roamed</c> controller, which every name of the entity shares. Every call
/// through a handle to a <c>@reshaping</c> routine (one that can add, remove or move elements) is preceded by
/// <c>handle.require_shape_free()</c>, which crashes with ReshapingWhileInUseError while a use is open. A
/// Suflae entity is reached only through handles, so this catches the change even when it is hidden behind
/// other calls, where the build-time checks (RF-S625, RF-S639) cannot see it. Uses are counted, so nested
/// loops and element calls on one entity stack. A failure inside an open use ends the program, so that use
/// never needs closing.
/// <para>Runs on user programs after OperatorLoweringPass. The begin/end calls are spliced into the
/// enclosing block rather than wrapped in a new one, so a declaration stays visible to the statements after
/// it.</para>
/// </summary>
internal sealed class ShapeUseLoweringPass(PostprocessingContext ctx)
{
    private const string ModifyAt = "modify_at";
    private const string ViewAt = "view_at";

    /// <summary>The containers whose shape the enclosing <c>each</c> loops use, outermost first.</summary>
    private readonly List<Expression> _loopShapeUses = [];

    private int _tempCount;

    public void Run(Program program)
    {
        BodyDispatch.RunOnProgram(registry: ctx.Registry, program: program, lower: r => LowerNested(stmt: r.Body));
    }

    /// <summary>Lowers a statement in a nested position (a body or a branch), wrapping a spliced result.</summary>
    private Statement LowerNested(Statement stmt)
    {
        List<Statement> lowered = LowerStatement(stmt: stmt, preceding: []);
        return lowered.Count == 1
            ? lowered[index: 0]
            : new BlockStatement(Statements: lowered, Location: stmt.Location);
    }

    private BlockStatement LowerBlock(BlockStatement block)
    {
        var rewritten = new List<Statement>(capacity: block.Statements.Count);
        foreach (Statement stmt in block.Statements)
        {
            rewritten.AddRange(collection: LowerStatement(stmt: stmt, preceding: rewritten));
        }

        block.Statements.Clear();
        block.Statements.AddRange(collection: rewritten);
        return block;
    }

    /// <summary>
    /// Lowers one statement to the statements that replace it. <paramref name="preceding"/> holds the
    /// already-lowered statements before it in the same block, where an <c>each</c> loop's iterator
    /// declaration (and so its source's type) is found.
    /// </summary>
    private List<Statement> LowerStatement(Statement stmt, List<Statement> preceding)
    {
        switch (stmt)
        {
            case BlockStatement block:
                return [LowerBlock(block: block)];

            case DangerStatement danger:
                return [danger with { Body = LowerBlock(block: danger.Body) }];

            case LoopStatement loop:
                return LowerLoop(loop: loop, preceding: preceding);

            case IfStatement ifs:
            {
                IfStatement rebuilt = ifs with
                {
                    ThenStatement = LowerNested(stmt: ifs.ThenStatement),
                    ElseStatement = ifs.ElseStatement is { } alt
                        ? LowerNested(stmt: alt)
                        : null
                };
                return BindHead(head: ifs.Condition,
                    rebuild: condition => rebuilt with { Condition = condition },
                    whole: rebuilt);
            }

            case WhenStatement whenStmt:
                return
                [
                    whenStmt with
                    {
                        Clauses = whenStmt.Clauses
                                      .Select(selector: c => c with { Body = LowerNested(stmt: c.Body) })
                                      .ToList()
                    }
                ];

            case UsingStatement usingStmt:
                return
                [
                    usingStmt with
                    {
                        Body = LowerNested(stmt: usingStmt.Body),
                        FallbackBody = usingStmt.FallbackBody is { } fallback
                            ? LowerNested(stmt: fallback)
                            : null
                    }
                ];

            case ReturnStatement ret:
                return LowerReturn(ret: ret);

            default:
            {
                List<Expression> containers = TokenContainers(root: stmt);
                List<Statement> guards = ReshapeGuards(root: stmt);
                if (containers.Count == 0)
                {
                    return [.. guards, stmt];
                }

                var result = new List<Statement>(collection: guards);
                result.AddRange(collection: ShapeUseCalls(containers: containers, verb: RuntimeContract.ShapeUse.Begin));
                result.Add(item: stmt);
                result.AddRange(collection: ShapeUseCalls(containers: containers,
                    verb: RuntimeContract.ShapeUse.End));
                return result;
            }
        }
    }

    /// <summary>
    /// An <c>each</c> loop over a named container that counts shape uses is bracketed by
    /// <c>begin_shape_use</c>/<c>end_shape_use</c>, and its body is lowered with the container on the
    /// loop stack so a <c>return</c> inside closes the use.
    /// </summary>
    private List<Statement> LowerLoop(LoopStatement loop, List<Statement> preceding)
    {
        Expression? source = loop.IterationSourceName is { } name
            ? LoopSource(name: name, preceding: preceding)
            : loop.IterationSourcePath is { } path
                ? LoopSourceByPath(path: path, preceding: preceding)
                : null;
        if (source == null || !HasShapeUse(container: source))
        {
            return [loop with { Body = LowerNested(stmt: loop.Body) }];
        }

        _loopShapeUses.Add(item: source);
        Statement body = LowerNested(stmt: loop.Body);
        _loopShapeUses.RemoveAt(index: _loopShapeUses.Count - 1);

        return
        [
            .. ShapeUseCalls(containers: [source], verb: RuntimeContract.ShapeUse.Begin),
            loop with { Body = body },
            .. ShapeUseCalls(containers: [source], verb: RuntimeContract.ShapeUse.End)
        ];
    }

    /// <summary>
    /// A <c>return</c> leaves every enclosing loop that opened a shape use, so it closes those uses
    /// (innermost first) after its value is computed. A value that reaches an element token is computed
    /// inside that token's own use.
    /// </summary>
    private List<Statement> LowerReturn(ReturnStatement ret)
    {
        List<Expression> containers = ret.Value is { } v
            ? TokenContainers(root: v)
            : [];
        if (containers.Count == 0 && _loopShapeUses.Count == 0)
        {
            return [ret];
        }

        var loopEnds = ShapeUseCalls(containers: Enumerable.Reverse(source: _loopShapeUses).ToList(),
            verb: RuntimeContract.ShapeUse.End);
        // A value that is only a name or a literal (or none) reads no element, so the loop uses can be
        // closed right before it. So can a value whose type is unknown, which cannot be bound.
        if (containers.Count == 0 &&
            ret.Value is null or IdentifierExpression or LiteralExpression ||
            ret.Value is not { ResolvedType: { } valueType } value)
        {
            return [.. loopEnds, ret];
        }

        (DeclarationStatement decl, IdentifierExpression tmp) =
            MakeTemporary(value: value, type: valueType);
        return
        [
            .. ShapeUseCalls(containers: containers, verb: RuntimeContract.ShapeUse.Begin),
            decl,
            .. ShapeUseCalls(containers: containers, verb: RuntimeContract.ShapeUse.End),
            .. loopEnds,
            ret with { Value = tmp }
        ];
    }

    /// <summary>
    /// When the head expression of a compound statement reaches an element token, computes it into a
    /// temporary inside the token's shape use, so the use does not stay open for the whole branch.
    /// </summary>
    private List<Statement> BindHead(Expression head, Func<Expression, Statement> rebuild,
        Statement whole)
    {
        List<Expression> containers = TokenContainers(root: head);
        if (containers.Count == 0 || head.ResolvedType is not { } headType)
        {
            return [whole];
        }

        (DeclarationStatement decl, IdentifierExpression tmp) =
            MakeTemporary(value: head, type: headType);
        return
        [
            .. ShapeUseCalls(containers: containers, verb: RuntimeContract.ShapeUse.Begin),
            decl,
            .. ShapeUseCalls(containers: containers, verb: RuntimeContract.ShapeUse.End),
            rebuild(arg: tmp)
        ];
    }

    /// <summary>
    /// The containers of the <c>modify_at</c>/<c>view_at</c> element tokens inside
    /// <paramref name="root"/> whose container is a plain named path (a fresh container value has no
    /// other reference that could change it) and counts shape uses.
    /// </summary>
    private List<Expression> TokenContainers(object root)
    {
        var containers = new List<Expression>();
        AstWalker.WalkExpressions(root: root,
            visit: e =>
            {
                if (e is CallExpression
                    {
                        Callee: MemberExpression { MemberName: ModifyAt or ViewAt, Object: var target }
                    } && Unprojected(expr: target) is var container && IsNamedPath(expr: container) &&
                    HasShapeUse(container: container))
                {
                    containers.Add(item: container);
                }
            });
        return containers;
    }

    /// <summary>
    /// The named source of an <c>each</c> loop, typed from its iterator declaration among the statements
    /// before the loop (`var _iter = xs.iter()`).
    /// </summary>
    private static Expression? LoopSource(string name, List<Statement> preceding)
    {
        for (int i = preceding.Count - 1; i >= 0; i--)
        {
            if (preceding[index: i] is not DeclarationStatement
                {
                    Declaration: VariableDeclaration { Initializer: { } init }
                })
            {
                continue;
            }

            TypeSymbol? sourceType = null;
            AstWalker.WalkExpressions(root: init,
                visit: e =>
                {
                    if (e is IdentifierExpression { ResolvedType: { } t } id && id.Name == name)
                    {
                        sourceType ??= t;
                    }
                });
            if (sourceType != null)
            {
                return new IdentifierExpression(Name: name, Location: init.Location)
                {
                    ResolvedType = sourceType
                };
            }
        }

        return null;
    }

    /// <summary>
    /// A field-chain or element source of an <c>each</c> loop (<c>me.items</c>, <c>grid[0]</c>): the analyzed
    /// receiver of its iterator declaration among the statements before the loop, copied so the shape-use
    /// calls read it again without sharing the declaration's nodes.
    /// </summary>
    private static Expression? LoopSourceByPath(string path, List<Statement> preceding)
    {
        for (int i = preceding.Count - 1; i >= 0; i--)
        {
            if (preceding[index: i] is not DeclarationStatement
                {
                    Declaration: VariableDeclaration { Initializer: { } init }
                })
            {
                continue;
            }

            Expression? found = null;
            AstWalker.WalkExpressions(root: init,
                visit: e =>
                {
                    if (found == null && e.ResolvedType != null && SourcePath(expr: e) == path)
                    {
                        found = e;
                    }
                });
            if (found != null)
            {
                return CopyReadPath(expr: found);
            }
        }

        return null;
    }

    /// <summary>The path of a name, field chain or element read (<c>grid[0]</c> is <c>grid[]</c>), as
    /// ControlFlowLoweringPass records it on the loop.</summary>
    private static string? SourcePath(Expression expr)
    {
        return expr switch
        {
            IdentifierExpression id => id.Name,
            MemberExpression { Object: var inner, MemberName: var field } =>
                SourcePath(expr: inner) is { } prefix
                    ? $"{prefix}.{field}"
                    : null,
            IndexExpression { Object: var container } =>
                SourcePath(expr: container) is { } owner
                    ? $"{owner}[]"
                    : null,
            _ => null
        };
    }

    /// <summary>A fresh copy of a name / field chain / element read, keeping every level's resolved type.</summary>
    private static Expression CopyReadPath(Expression expr)
    {
        return expr switch
        {
            MemberExpression member => member with { Object = CopyReadPath(expr: member.Object) },
            IndexExpression index => index with { Object = CopyReadPath(expr: index.Object) },
            _ => expr with { }
        };
    }

    /// <summary>Whether <paramref name="container"/> is a handle whose controller counts shape uses (a Suflae
    /// <c>Roamed</c>).</summary>
    private bool HasShapeUse(Expression container)
    {
        return container.ResolvedType is { } type && IsRoamed(type: type) &&
               ctx.Registry.LookupMemberRoutine(type: type, memberRoutineName: RuntimeContract.ShapeUse.Begin) != null;
    }

    /// <summary>The handle behind <c>handle.control()</c>, the projection RoamedProjectionLoweringPass puts on a call
    /// through a handle; any other expression as it is.</summary>
    private static Expression Unprojected(Expression expr)
    {
        return expr is CallExpression
        {
            Callee: MemberExpression { MemberName: RuntimeContract.Control, Object: var handle }
        } && handle.ResolvedType is { } type && IsRoamed(type: type)
            ? handle
            : expr;
    }

    /// <summary>
    /// <c>handle.require_shape_free()</c> for every call in <paramref name="root"/> made through a named handle
    /// to a <c>@reshaping</c> routine, so the change crashes while a loop or an element call uses the shape.
    /// </summary>
    private List<Statement> ReshapeGuards(object root)
    {
        var handles = new List<Expression>();
        AstWalker.WalkExpressions(root: root,
            visit: e =>
            {
                if (e is CallExpression { ResolvedRoutine: { } callee, Callee: MemberExpression { Object: var target } } &&
                    ChangesShape(routine: callee) &&
                    Unprojected(expr: target) is var handle && !ReferenceEquals(objA: handle, objB: target) &&
                    IsNamedPath(expr: handle) && HasShapeUse(container: handle))
                {
                    handles.Add(item: handle);
                }
            });
        return ShapeUseCalls(containers: handles, verb: RuntimeContract.ShapeUse.RequireFree);
    }

    /// <summary>The library routines already judged by <see cref="ChangesShape"/>, by declaration site. A routine
    /// being judged counts as not changing the shape while its own body is walked, so recursion ends.</summary>
    private readonly Dictionary<(string File, int Line), bool> _changesShape = [];

    private Dictionary<(string File, int Line), RoutineDeclaration>? _libraryRoutines;

    /// <summary>
    /// Whether a call of <paramref name="routine"/> can change the shape of its receiver: a RazorForge
    /// <c>@reshaping</c> routine, or a library routine whose body writes a member variable of <c>me</c> (a
    /// container's count, capacity or buffer), directly or through another routine it calls on <c>me</c>. Writing
    /// an element's value goes into the container's buffer and leaves its member variables alone. A routine of the
    /// program itself guards its own writes (its <c>me</c> is the handle, see RoamedLockBracketLoweringPass).
    /// </summary>
    private bool ChangesShape(RoutineInfo routine)
    {
        if (routine.IsReshaping)
        {
            return true;
        }

        if ((routine.GenericDefinition ?? routine).Location is not { } site ||
            LibraryRoutine(file: site.FileName, line: site.Line) is not { } declaration)
        {
            return false;
        }

        (string, int) key = (site.FileName, site.Line);
        if (_changesShape.TryGetValue(key: key, value: out bool known))
        {
            return known;
        }

        _changesShape[key: key] = false;
        bool changes = false;
        AstWalker.Walk(root: declaration.Body,
            visit: node =>
            {
                changes |= node switch
                {
                    AssignmentStatement { Target: var target } => IsOwnMemberVariable(expr: target),
                    BinaryExpression { Operator: BinaryOperator.Assign, Left: var left } => IsOwnMemberVariable(expr: left),
                    CallExpression { Callee: MemberExpression { Object: IdentifierExpression { Name: "me" }, MemberName: var name } } =>
                        routine.OwnerType is { } owner && OwnRoutinesNamed(owner: owner, name: name).Any(predicate: ChangesShape),
                    // A call on something `me` holds (`me.items.add_last(...)`, a list keeping its elements in a
                    // buffer) changes this container's shape when it changes the shape of what it is called on.
                    CallExpression { Callee: MemberExpression { Object: var held, MemberName: var heldName } } heldCall
                        when IsHeldByMe(expr: held) =>
                        heldCall.ResolvedRoutine is { } called
                            ? ChangesShape(routine: called)
                            : routine.OwnerType is { } holder &&
                              HeldType(expr: held, owner: holder) is { } heldType &&
                              OwnRoutinesNamed(owner: heldType, name: heldName).Any(predicate: ChangesShape),
                    _ => false
                };
            });
        _changesShape[key: key] = changes;
        return changes;
    }

    /// <summary>Whether <paramref name="expr"/> names a member variable of <c>me</c> (<c>me.count</c>).</summary>
    private static bool IsOwnMemberVariable(Expression expr)
    {
        return expr is MemberExpression { Object: IdentifierExpression { Name: "me" } };
    }

    /// <summary>
    /// Whether <paramref name="expr"/> reaches something <c>me</c> holds, through member variables and calls
    /// (<c>me.items</c>, <c>me.storage.control()</c>), but is not <c>me</c> itself.
    /// </summary>
    private static bool IsHeldByMe(Expression expr)
    {
        while (true)
        {
            switch (expr)
            {
                case MemberExpression { Object: IdentifierExpression { Name: "me" } }:
                    return true;
                case MemberExpression member:
                    expr = member.Object;
                    break;
                case CallExpression { Callee: MemberExpression callee }:
                    expr = callee.Object;
                    break;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// The type of something <c>me</c> holds, read from the declarations when the library body is not analyzed
    /// yet: a member variable's declared type, a routine's return type, and the entity behind a handle's
    /// <c>control()</c>/<c>access()</c>.
    /// </summary>
    private TypeSymbol? HeldType(Expression expr, TypeSymbol owner)
    {
        if (expr.ResolvedType is { } known)
        {
            return known;
        }

        switch (expr)
        {
            case IdentifierExpression { Name: "me" }:
                return owner;
            case MemberExpression member when HeldType(expr: member.Object, owner: owner) is { } holder:
                List<MemberVariableInfo>? fields = holder switch
                {
                    EntityTypeSymbol entity => entity.MemberVariables,
                    RecordTypeSymbol record => record.MemberVariables,
                    _ => null
                };
                return fields?.FirstOrDefault(predicate: f => f.Name == member.MemberName)?.Type;
            case CallExpression { Callee: MemberExpression callee }
                when HeldType(expr: callee.Object, owner: owner) is { } receiver:
                if (RuntimeContract.ViewVerbs.Contains(item: callee.MemberName) && IsRoamed(type: receiver) &&
                    receiver.TypeArguments is [var inner])
                {
                    return inner;
                }

                return OwnRoutinesNamed(owner: receiver, name: callee.MemberName)
                      .Select(selector: r => r.ReturnType)
                      .FirstOrDefault(predicate: t => t is not null and not GenericParameterTypeSymbol);
            default:
                return null;
        }
    }

    /// <summary>Every routine named <paramref name="name"/> on <paramref name="owner"/>.</summary>
    private IEnumerable<RoutineInfo> OwnRoutinesNamed(TypeSymbol owner, string name)
    {
        var overloads = new List<RoutineInfo>();
        ctx.Registry.CollectMemberRoutineCandidates(type: owner, memberRoutineName: name, candidates: overloads);
        return overloads;
    }

    /// <summary>The library routine declared at <paramref name="file"/>:<paramref name="line"/>, or null.</summary>
    private RoutineDeclaration? LibraryRoutine(string file, int line)
    {
        if (_libraryRoutines == null)
        {
            _libraryRoutines = [];
            foreach ((Program program, _, _) in ctx.Registry.StdlibPrograms)
            {
                AstWalker.Walk(root: program,
                    visit: node =>
                    {
                        if (node is RoutineDeclaration { Body: not null } declaration)
                        {
                            _libraryRoutines.TryAdd(key: (declaration.Location.FileName, declaration.Location.Line),
                                value: declaration);
                        }
                    });
            }
        }

        return _libraryRoutines.GetValueOrDefault(key: (file, line));
    }

    private static bool IsRoamed(TypeSymbol type)
    {
        return TypeRegistry.GetRcWrapperBaseName(type: type) == RuntimeContract.Roamed;
    }

    private static bool IsNamedPath(Expression expr)
    {
        return expr switch
        {
            IdentifierExpression => true,
            MemberExpression member => IsNamedPath(expr: member.Object),
            _ => false
        };
    }

    private List<Statement> ShapeUseCalls(List<Expression> containers, string verb)
    {
        var calls = new List<Statement>(capacity: containers.Count);
        foreach (Expression container in containers)
        {
            TypeSymbol type = container.ResolvedType!;
            RoutineInfo routine =
                ctx.Registry.LookupMemberRoutine(type: type, memberRoutineName: verb)!;
            var callee = new MemberExpression(Object: CopyReadPath(expr: container),
                MemberName: verb,
                Location: container.Location) { ResolvedType = type };
            var call = new CallExpression(Callee: callee, Arguments: [], Location: container.Location)
            {
                ResolvedRoutine = routine,
                ResolvedType = routine.ReturnType,
                LoweringKind = Builder.Verification.CallClassifier.ClassifyMemberRoutineCall(memberRoutine: routine)
            };
            calls.Add(item: new ExpressionStatement(Expression: call, Location: container.Location));
        }

        return calls;
    }

    private (DeclarationStatement Declaration, IdentifierExpression Reference) MakeTemporary(
        Expression value, TypeSymbol type)
    {
        string name = $"_shape_use_{_tempCount++}";
        var declaration = new VariableDeclaration(Name: name,
            Type: ExpressionLoweringPass.TypeInfoToExpr(type: type, loc: value.Location),
            Initializer: value,
            Visibility: VisibilityModifier.Secret,
            Location: value.Location);
        return (new DeclarationStatement(Declaration: declaration, Location: value.Location),
            new IdentifierExpression(Name: name, Location: value.Location) { ResolvedType = type });
    }
}
