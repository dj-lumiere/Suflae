using Builder.Declaration;
using Builder.Diagnostics;
using Builder.Desugaring;
using SyntaxTree;
using TypeModel.Enums;

namespace Suflae.Passes;

/// <summary>
/// Gives a Suflae program's own annotations their effect, on the user programs before analysis. An annotation is a
/// record that obeys <c>Annotation</c> (ObjectHacker's), written on a routine, a record or an entity with the
/// record's name: <c>@Memoize(limit: 100)</c>. Two routines of the record do the work, and either may be missing:
/// <list type="bullet">
/// <item><c>apply(to:)</c> runs once for each declaration the annotation is on, when the program starts: the pass
/// adds <c>Memoize(limit: 100).apply(to: info)</c> to the top of <c>start()</c>, in file order (a module after the
/// modules it imports) and source order, before the globals are set up there.</item>
/// <item><c>around(call:)</c> runs instead of each call of the routine it is on. The routine keeps its name and
/// signature, and its body moves to a routine of its own (<c>fib__around_body</c>). A third routine
/// (<c>fib__around_unknown</c>) runs that body and keeps the result in an <c>Unknown</c>. The routine itself builds an
/// <c>Invocation</c> whose <c>proceed()</c> calls the third routine, hands it to <c>around</c>, and gives back what
/// <c>around</c> returns as the routine's return type. A call inside the body (recursion) goes through
/// <c>around</c> as well.</item>
/// </list>
/// The calls written here that reach ObjectHacker's own helpers are marked as the builder's.
/// </summary>
internal static class AnnotationSynthesisPass
{
    /// <summary>ObjectHacker's helpers the written bodies call, which a program cannot name itself.</summary>
    private static readonly HashSet<string> HelperNames =
    [
        "new_invocation", "annotated_routine", "annotated_member", "annotated_type", "unknown_nothing",
        "type_fact_maker"
    ];

    private sealed record AnnotationKind(string Name, bool HasApply, bool HasAround);

    internal static bool Run(List<(Program Program, string FilePath)> files,
        Action<SemanticDiagnosticCode, string, SourceLocation> report)
    {
        Dictionary<string, AnnotationKind> kinds = CollectKinds(files: files);
        var applies = new List<Statement>();
        bool ok = true;
        foreach ((Program program, string filePath) in files)
        {
            string moduleName = program.Declarations.OfType<ModuleDeclaration>().FirstOrDefault()?.Path ?? "";
            var replaced = new Dictionary<RoutineDeclaration, List<RoutineDeclaration>>();
            foreach (ISyntaxTreeNode node in program.Declarations)
            {
                switch (node)
                {
                    case RoutineDeclaration routine:
                        ok &= HandleRoutine(routine: routine, kinds: kinds, moduleName: moduleName,
                            filePath: filePath, report: report, applies: applies, replaced: replaced);
                        break;
                    case RecordDeclaration record:
                        ok &= HandleType(name: record.Name, generic: record.GenericParameters,
                            annotations: record.Annotations, at: record.Location, kinds: kinds,
                            filePath: filePath, report: report, applies: applies);
                        break;
                    case EntityDeclaration entity:
                        ok &= HandleType(name: entity.Name, generic: entity.GenericParameters,
                            annotations: entity.LeadingAnnotations, at: entity.Location, kinds: kinds,
                            filePath: filePath, report: report, applies: applies);
                        break;
                }
            }

            foreach ((RoutineDeclaration original, List<RoutineDeclaration> written) in replaced)
            {
                int at = program.Declarations.IndexOf(item: original);
                program.Declarations.RemoveAt(index: at);
                program.Declarations.InsertRange(index: at, collection: written);
            }
        }

        if (applies.Count == 0)
        {
            return ok;
        }

        foreach ((Program program, _) in Enumerable.Reverse(source: files))
        {
            if (program.Declarations.OfType<RoutineDeclaration>()
                       .FirstOrDefault(predicate: r => r is { Name: "start", Body: BlockStatement }) is
                { Body: BlockStatement start })
            {
                start.Statements.InsertRange(index: 0, collection: applies);
                return ok;
            }
        }

        report(SemanticDiagnosticCode.InvalidAnnotation,
            "An annotation's 'apply' runs when the program starts, but no file in the build has a 'routine start()' " +
            "or top-level statements. Add one to the program's entry file.",
            applies[index: 0].Location);
        return false;
    }

    /// <summary>The program's annotation records, with whether each has an <c>apply</c> and an <c>around</c>.</summary>
    private static Dictionary<string, AnnotationKind> CollectKinds(List<(Program Program, string FilePath)> files)
    {
        var names = new HashSet<string>(comparer: StringComparer.Ordinal);
        foreach ((Program program, _) in files)
        foreach (RecordDeclaration record in program.Declarations.OfType<RecordDeclaration>())
        {
            if (record.Protocols.Any(predicate: p => p.Name == RuntimeContract.ProgramAnnotation.Protocol))
            {
                names.Add(item: record.Name);
            }
        }

        var applies = new HashSet<string>(comparer: StringComparer.Ordinal);
        var arounds = new HashSet<string>(comparer: StringComparer.Ordinal);
        foreach ((Program program, _) in files)
        foreach (RoutineDeclaration routine in program.Declarations.OfType<RoutineDeclaration>())
        {
            if (routine.OwnerName is not { } owner || !names.Contains(item: owner))
            {
                continue;
            }

            if (routine.MemberRoutineName == RuntimeContract.ProgramAnnotation.Apply)
            {
                applies.Add(item: owner);
            }
            else if (routine.MemberRoutineName == RuntimeContract.ProgramAnnotation.Around)
            {
                arounds.Add(item: owner);
            }
        }

        return names.ToDictionary(keySelector: n => n,
            elementSelector: n => new AnnotationKind(Name: n, HasApply: applies.Contains(item: n),
                HasAround: arounds.Contains(item: n)),
            comparer: StringComparer.Ordinal);
    }

    /// <summary>The program's annotations among a declaration's, each with its record, or false after reporting one
    /// that names no annotation record.</summary>
    private static bool ProgramAnnotations(IEnumerable<string>? annotations, Dictionary<string, AnnotationKind> kinds,
        SourceLocation at, Action<SemanticDiagnosticCode, string, SourceLocation> report,
        out List<(string Text, AnnotationKind Kind)> found)
    {
        found = [];
        bool ok = true;
        foreach (string annotation in annotations ?? [])
        {
            string name = Builder.Parser.Parser.AnnotationName(annotation: annotation);
            if (!Builder.Parser.Parser.IsProgramAnnotation(name: name, language: Language.Suflae))
            {
                continue;
            }

            if (!kinds.TryGetValue(key: name, value: out AnnotationKind? kind))
            {
                report(SemanticDiagnosticCode.InvalidAnnotation,
                    $"There is no annotation '@{name}'. An annotation is a record that obeys 'Annotation' " +
                    $"(import ObjectHacker): declare 'record {name}' with 'obeys Annotation', or remove '@{name}'.",
                    at);
                ok = false;
                continue;
            }

            found.Add(item: (annotation, kind));
        }

        return ok;
    }

    private static string Construct(string annotation, AnnotationKind kind)
    {
        return $"{kind.Name}({Builder.Parser.Parser.ProgramAnnotationArguments(annotation: annotation)})";
    }

    private static string Quote(string text)
    {
        return "\"" + text.Replace(oldValue: "\\", newValue: "\\\\").Replace(oldValue: "\"", newValue: "\\\"")
                          .Replace(oldValue: "{", newValue: "{{").Replace(oldValue: "}", newValue: "}}") + "\"";
    }

    private static bool HandleType(string name, List<string>? generic, List<string>? annotations, SourceLocation at,
        Dictionary<string, AnnotationKind> kinds, string filePath,
        Action<SemanticDiagnosticCode, string, SourceLocation> report, List<Statement> applies)
    {
        if (!ProgramAnnotations(annotations: annotations, kinds: kinds, at: at, report: report,
                found: out List<(string Text, AnnotationKind Kind)> found))
        {
            return false;
        }

        if (found.Count == 0)
        {
            return true;
        }

        if (generic is { Count: > 0 })
        {
            report(SemanticDiagnosticCode.InvalidAnnotation,
                $"'{name}' has type parameters, and an annotation goes only on a type without them. Remove " +
                $"'@{found[index: 0].Kind.Name}'.", at);
            return false;
        }

        foreach ((string text, AnnotationKind kind) in found)
        {
            if (kind.HasApply)
            {
                applies.AddRange(collection: ParseStatements(
                    source: $"    {Construct(annotation: text, kind: kind)}.apply(to: annotated_type(maker: {name}.type_fact_maker()))\n",
                    filePath: filePath, line: at.Line));
            }
        }

        return true;
    }

    private static bool HandleRoutine(RoutineDeclaration routine, Dictionary<string, AnnotationKind> kinds,
        string moduleName, string filePath, Action<SemanticDiagnosticCode, string, SourceLocation> report,
        List<Statement> applies, Dictionary<RoutineDeclaration, List<RoutineDeclaration>> replaced)
    {
        if (!ProgramAnnotations(annotations: routine.Annotations, kinds: kinds, at: routine.Location, report: report,
                found: out List<(string Text, AnnotationKind Kind)> found))
        {
            return false;
        }

        if (found.Count == 0)
        {
            return true;
        }

        string bare = routine.MemberRoutineName ?? routine.Name;
        string shown = routine.OwnerName is { } shownOwner ? $"{shownOwner}.{bare}" : bare;
        if (routine.GenericParameters is { Count: > 0 } || routine.HasReceiverTypeArgs)
        {
            report(SemanticDiagnosticCode.InvalidAnnotation,
                $"'{shown}' has type parameters, and an annotation goes only on a routine without them. Remove " +
                $"'@{found[index: 0].Kind.Name}'.", routine.Location);
            return false;
        }

        List<(string Text, AnnotationKind Kind)> arounds = found.Where(predicate: f => f.Kind.HasAround).ToList();
        if (arounds.Count > 1)
        {
            report(SemanticDiagnosticCode.InvalidAnnotation,
                $"'{shown}' has two annotations with an 'around' ('@{arounds[index: 0].Kind.Name}' and " +
                $"'@{arounds[index: 1].Kind.Name}'), and a routine takes one: the order would change what a call does. " +
                "Write one annotation whose 'around' does both.", routine.Location);
            return false;
        }

        // What finds the routine's description while the program runs.
        string count = routine.Parameters.Count.ToString(provider: System.Globalization.CultureInfo.InvariantCulture);
        string info = routine.OwnerName is { } owner
            ? $"annotated_member(maker: {owner}.type_fact_maker(), name: {Quote(text: bare)}, count: {count})"
            : $"annotated_routine(module_name: {Quote(text: moduleName)}, name: {Quote(text: bare)}, count: {count})";
        foreach ((string text, AnnotationKind kind) in found)
        {
            if (kind.HasApply)
            {
                applies.AddRange(collection: ParseStatements(
                    source: $"    {Construct(annotation: text, kind: kind)}.apply(to: {info})\n",
                    filePath: filePath, line: routine.Location.Line));
            }
        }

        if (arounds.Count == 1)
        {
            replaced[key: routine] = WrapAround(routine: routine, annotation: arounds[index: 0].Text,
                kind: arounds[index: 0].Kind, info: info, filePath: filePath);
        }

        return true;
    }

    /// <summary>The routine itself (calling <c>around</c>), the routine holding its body, and the routine keeping
    /// the body's result in an <c>Unknown</c>.</summary>
    private static List<RoutineDeclaration> WrapAround(RoutineDeclaration routine, string annotation,
        AnnotationKind kind, string info, string filePath)
    {
        string bare = routine.MemberRoutineName ?? routine.Name;
        string bodyName = bare + RuntimeContract.ProgramAnnotation.BodySuffix;
        string unknownName = bare + RuntimeContract.ProgramAnnotation.UnknownSuffix;
        string receiver = routine.OwnerName == null
            ? ""
            : routine.IsCommon
                ? $"{routine.OwnerName}."
                : "me.";
        string arguments = string.Join(separator: ", ",
            values: routine.Parameters.Select(selector: p => $"{p.Name}: {p.Name}"));
        string values = routine.Parameters.Count == 0
            ? "List[Unknown]()"
            : "[" + string.Join(separator: ", ",
                values: routine.Parameters.Select(selector: p => $"Unknown(from: {p.Name})")) + "]";
        bool returns = routine.ReturnType is { Name: not ("None" or "Blank") };

        // The body, under a name of its own and with no annotation.
        RoutineDeclaration body = Renamed(routine: routine, bare: bodyName) with
        {
            Annotations = routine.Annotations.Where(predicate: a =>
                                       !Builder.Parser.Parser.IsProgramAnnotation(
                                           name: Builder.Parser.Parser.AnnotationName(annotation: a),
                                           language: Language.Suflae))
                                  .ToList(),
            Visibility = VisibilityModifier.Secret
        };

        string unknownSource = returns
            ? $"    return Unknown(from: {receiver}{bodyName}({arguments}))\n"
            : $"    {receiver}{bodyName}({arguments})\n    return unknown_nothing()\n";
        RoutineDeclaration unknown = Renamed(routine: routine, bare: unknownName) with
        {
            Annotations = [],
            Visibility = VisibilityModifier.Secret,
            ReturnType = new TypeExpression(Name: "Unknown", GenericArguments: null, Location: routine.Location),
            Body = ParseBody(source: unknownSource, filePath: filePath, line: routine.Location.Line)
        };

        const string resultType = "Reflected_Annotation_Result";
        string call = $"{Construct(annotation: annotation, kind: kind)}.around(call: annotation_call)";
        // A lambda does not capture `me`, so the call reaches the object through a local.
        string lambdaReceiver = receiver == "me." ? "annotation_self." : receiver;
        string wrapperSource =
            (receiver == "me." ? "    var annotation_self = me\n" : "") +
            $"    var annotation_call = new_invocation(routine_info: {info}, args: {values}, run: () => {lambdaReceiver}{unknownName}({arguments}))\n" +
            (returns
                ? $"    return {call}.to[{resultType}]()\n"
                : $"    var annotation_result = {call}\n    return\n");
        Statement wrapperBody = ParseBody(source: wrapperSource, filePath: filePath, line: routine.Location.Line);
        if (returns)
        {
            // The result's type is the routine's own, put in where the text names a placeholder.
            AstWalker.Walk(root: wrapperBody, visit: node =>
            {
                List<TypeExpression>? typeArguments = node switch
                {
                    GenericMemberRoutineCallExpression g => g.TypeArguments,
                    GenericMemberExpression g => g.TypeArguments,
                    _ => null
                };
                if (typeArguments != null)
                {
                    for (int i = 0; i < typeArguments.Count; i++)
                    {
                        if (typeArguments[index: i].Name == resultType)
                        {
                            typeArguments[index: i] = CopyType(type: routine.ReturnType)!;
                        }
                    }
                }
            });
        }

        RoutineDeclaration wrapper = routine with { Body = wrapperBody };
        return [wrapper, body, unknown];
    }

    /// <summary>A copy of the routine under another name, with parameters of its own: analysis changes a
    /// declaration's parameters in place, and each routine is analyzed apart.</summary>
    private static RoutineDeclaration Renamed(RoutineDeclaration routine, string bare)
    {
        RoutineDeclaration copy = routine with
        {
            Parameters = routine.Parameters.Select(selector: p => p with { Type = CopyType(type: p.Type) }).ToList(),
            ReturnType = CopyType(type: routine.ReturnType)
        };
        return routine.RenderedReceiver is { } receiver
            ? copy with { Name = $"{receiver}.{bare}", MemberRoutineName = bare }
            : copy with { Name = bare };
    }

    private static TypeExpression? CopyType(TypeExpression? type)
    {
        return type == null
            ? null
            : type with { GenericArguments = type.GenericArguments?.Select(selector: a => CopyType(type: a)!).ToList() };
    }

    private static List<Statement> ParseStatements(string source, string filePath, int line)
    {
        return ParseBody(source: source, filePath: filePath, line: line) is BlockStatement block
            ? block.Statements
            : [];
    }

    /// <summary>Parses a body the pass writes, and marks each call in it as the builder's (it may reach
    /// ObjectHacker's own helpers).</summary>
    /// <summary>Parses a body the pass writes as if it stood right under line <paramref name="line"/> of the file,
    /// the line of the declaration it is written for: an error in it points there, and a lambda in it, named by
    /// where it stands, is told apart from the lambda written for another declaration.</summary>
    private static Statement ParseBody(string source, string filePath, int line)
    {
        string text = new string(c: '\n', count: Math.Max(val1: 0, val2: line - 1)) +
                      $"routine reflected_written()\n{source}";
        List<Builder.Tokenizer.Token> tokens =
            Builder.Tokenizer.Lexers.Tokenize(source: text, fileName: filePath, language: Language.Suflae);
        var parser = new Builder.Parser.Parser(tokens: tokens, language: Language.Suflae, fileName: filePath)
        {
            PrintsErrors = false
        };
        Program parsed = parser.Parse();
        if (parser.HasErrors)
        {
            throw new InvalidOperationException(
                message: $"The builder wrote an annotation body that does not parse:\n{text}");
        }

        Statement body = parsed.Declarations.OfType<RoutineDeclaration>().Single().Body;
        // Only the calls of ObjectHacker's own helpers are marked: a marked call is also taken as one that copies
        // nothing, which would leave a captured Text or entity argument without the holder it needs.
        AstWalker.Walk(root: body, visit: node =>
        {
            if (node is CallExpression { Callee: var callee } call &&
                HelperNames.Contains(item: callee switch
                {
                    IdentifierExpression id => id.Name,
                    MemberExpression member => member.MemberName,
                    _ => ""
                }))
            {
                call.IsSynthesizedLowering = true;
            }
        });
        return body;
    }
}
