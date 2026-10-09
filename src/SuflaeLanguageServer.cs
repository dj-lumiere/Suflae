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
    /// <remarks>The language id the Rider plugin registers for highlighting code in hover.</remarks>
    public override string CodeBlockLanguage => "suflae";

    /// <inheritdoc/>
    public override IReadOnlyList<string> Keywords => SuflaeLexer.KeywordSpellings;

    /// <inheritdoc/>
    /// <remarks>From the improvement plan: `agent` in front of a call, `until` loops, `becomes` in an expression `when`,
    /// and the rename `grab` → `check`.</remarks>
    public override IReadOnlyList<string> PlannedKeywords { get; } = ["agent", "until", "becomes", "check"];

    /// <inheritdoc/>
    /// <remarks>From the improvement plan: `absent` / `lookup` go with their failure kinds, `onlyif` becomes `when`,
    /// `suspended` gives way to `agent` at the call, `unless` / `but` / `disobeys` / `discard` go (a result is
    /// dropped without a marker), and `grab` is renamed `check`.</remarks>
    public override IReadOnlyList<string> RetiredKeywords { get; } =
        ["absent", "lookup", "onlyif", "suspended", "unless", "but", "disobeys", "discard", "grab"];

    /// <inheritdoc/>
    public override string? Format(string text, string fileName)
    {
        return Builder.Formatting.SourceFormatter.Format(source: text, fileName: fileName, language: Language)
                      .Output;
    }
}
