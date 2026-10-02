using Builder.Frontends;
using Suflae.Lexer;
using TypeModel.Enums;

namespace Suflae;

/// <summary>
/// The Suflae Language Server: the shared engine (<c>Builder.Execution.LspServer</c>) run by <c>suflae lsp</c>,
/// serving <c>.sf</c> files with Suflae's own keywords and names.
/// </summary>
internal sealed class SuflaeLanguageServer : LanguageServerProfile
{
    /// <inheritdoc/>
    public override Language Language => Language.Suflae;

    /// <inheritdoc/>
    public override string ServerName => "suflae-lsp";

    /// <inheritdoc/>
    public override string CodeBlockLanguage => "suflae";

    /// <inheritdoc/>
    public override IReadOnlyList<string> Keywords => SuflaeLexer.KeywordSpellings;

    /// <inheritdoc/>
    public override string? Format(string text, string fileName)
    {
        return Builder.Formatting.SourceFormatter.Format(source: text, fileName: fileName, language: Language)
                      .Output;
    }
}
