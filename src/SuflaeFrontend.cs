using Builder.Tokenizer;
using RazorForge;
using Suflae.Lexer;
using TypeModel.Enums;

namespace Suflae;

/// <summary>
/// The Suflae front end: registers the Suflae lexer with the builder core, together with the RazorForge
/// front end, whose standard library a Suflae build analyzes.
/// </summary>
public static class SuflaeFrontend
{
    /// <summary>Registers the Suflae and RazorForge lexers (idempotent).</summary>
    public static void Register()
    {
        RazorForgeFrontend.Register();
        Lexers.Register(language: Language.Suflae,
            tokenize: (source, fileName) => new SuflaeLexer(source: source, fileName: fileName).Tokenize());
    }
}
