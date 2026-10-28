using Builder.Diagnostics;
using SyntaxTree;
using TypeModel.Enums;

namespace Suflae.Passes;

/// <summary>
/// Walking an emitter goes on from where it is (<c>for n in steps</c> takes the values <c>steps</c> has not handed out
/// yet). An entity a Suflae program writes that obeys <c>Emitter[T]</c> and writes no <c>emitter()</c> gets one that
/// gives the entity itself: <c>routine Steps.emitter() -> Steps</c> with <c>return me</c>. It is the entity's own
/// routine, so it takes and gives the entity's handle as the entity's other routines do (its <c>emit</c> among them),
/// and the walk calls <c>emit</c> on that handle. Run on the user programs before analysis.
/// </summary>
internal static class EmitterSelfSynthesisPass
{
    private const string EmitterProtocolName = "Emitter";
    private const string EmitterRoutineName = "emitter";

    internal static void Run(List<(Program Program, string FilePath)> files)
    {
        var written = new HashSet<string>(comparer: StringComparer.Ordinal);
        foreach ((Program program, _) in files)
        {
            foreach (RoutineDeclaration routine in program.Declarations.OfType<RoutineDeclaration>())
            {
                if (routine is { MemberRoutineName: EmitterRoutineName, OwnerName: { } owner })
                {
                    written.Add(item: owner);
                }
            }
        }

        foreach ((Program program, string filePath) in files)
        {
            var added = new List<ISyntaxTreeNode>();
            foreach (EntityDeclaration entity in program.Declarations.OfType<EntityDeclaration>())
            {
                if (written.Contains(item: entity.Name) ||
                    !entity.Protocols.Any(predicate: p => p.Name == EmitterProtocolName))
                {
                    continue;
                }

                string self = entity.GenericParameters is { Count: > 0 } parameters
                    ? $"{entity.Name}[{string.Join(separator: ", ", values: parameters)}]"
                    : entity.Name;
                string source = $"routine {self}.{EmitterRoutineName}() -> {self}\n    return me\n";
                List<Builder.Tokenizer.Token> tokens = Builder.Tokenizer.Lexers.Tokenize(source: source,
                    fileName: filePath, language: Language.Suflae);
                var parser = new Builder.Parser.Parser(tokens: tokens, language: Language.Suflae, fileName: filePath)
                {
                    PrintsErrors = false
                };
                Program parsed = parser.Parse();
                if (parser.HasErrors)
                {
                    continue;
                }

                added.AddRange(collection: parsed.Declarations.OfType<RoutineDeclaration>());
            }

            program.Declarations.AddRange(collection: added);
        }
    }
}
