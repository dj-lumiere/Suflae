using System.Text.Json;

namespace Suflae.Tests.Execution;

/// <summary>
/// End-to-end coverage for the LSP server (<c>src/Execution/LspServer.cs</c>): every request handler
/// is driven in-process over real, analyzed RazorForge/Suflae documents via <see cref="LspTestHarness"/>.
/// Positions are computed from the source text so the tests stay robust to edits.
///
/// All tests live in one class so they share xUnit's per-class sequential execution — the server keeps
/// open documents in a process-wide static dictionary, and each <see cref="LspTestHarness.Run"/> clears
/// it at the start, so overlapping sessions from parallel classes would race.
/// </summary>
public sealed class LspServerTests
{
    private const string Uri = "file:///test/Sample.rf";

    // A symbol-rich RazorForge document: free routines, a record with a member routine, a generic
    // container use, variables, calls, an f-string, and control flow — enough surface to exercise
    // hover / definition / references / completion / signature-help / symbols / semantic tokens.
    private const string Source =
        "module Test/Lsp\n" +
        "import IO/Console\n" +
        "\n" +
        "record Point\n" +
        "    x: S32\n" +
        "    y: S32\n" +
        "\n" +
        "    routine magnitude(me) -> S32\n" +
        "        return me.x * me.x + me.y * me.y\n" +
        "\n" +
        "routine add(a: S32, b: S32) -> S32\n" +
        "    return a + b\n" +
        "\n" +
        "routine start()\n" +
        "    var total = add(a: 1, b: 2)\n" +
        "    var p = Point(x: 3, y: 4)\n" +
        "    var m = p.magnitude()\n" +
        "    show(f\"total={total} mag={m}\")\n" +
        "    return\n";

    private static string Initialize(int id = 0)
    {
        return JsonSerializer.Serialize(value: new
        {
            jsonrpc = "2.0",
            id,
            method = "initialize",
            @params = new { capabilities = new { } }
        });
    }

    private const string Shutdown = "{\"jsonrpc\":\"2.0\",\"id\":999,\"method\":\"shutdown\"}";
    private const string Exit = "{\"jsonrpc\":\"2.0\",\"method\":\"exit\"}";

    private static (int Line, int Char) Pos(string needle, int occurrence = 1)
    {
        return LspTestHarness.PositionOf(text: Source, needle: needle, occurrence: occurrence);
    }

    /// <summary>The labels of a <c>textDocument/completion</c> reply.</summary>
    private static List<string> CompletionLabels(JsonElement? reply)
    {
        return reply!.Value.GetProperty(propertyName: "result")
                     .GetProperty(propertyName: "items")
                     .EnumerateArray()
                     .Select(selector: item => item.GetProperty(propertyName: "label").GetString() ?? "")
                     .ToList();
    }

    /// <summary>The <c>serverInfo.name</c> of an <c>initialize</c> reply.</summary>
    private static string? ServerName(JsonElement? reply)
    {
        return reply!.Value.GetProperty(propertyName: "result")
                     .GetProperty(propertyName: "serverInfo")
                     .GetProperty(propertyName: "name")
                     .GetString();
    }

    [Fact]
    public void Initialize_NamesTheSuflaeServer()
    {
        IReadOnlyList<JsonDocument> replies = LspTestHarness.Run(Initialize(id: 1), Shutdown, Exit);

        Assert.Equal(expected: "suflae-lsp", actual: ServerName(reply: LspTestHarness.ReplyWithId(replies: replies, id: 1)));
    }

    [Fact]
    public void RazorForgeDocument_IsLeftToTheRazorForgeServer()
    {
        IReadOnlyList<JsonDocument> replies = LspTestHarness.Run(
            LspTestHarness.DidOpen(uri: Uri, text: Source),
            Exit);

        // A .rf file is the RazorForge server's: this one publishes nothing for it.
        Assert.Null(@object: LspTestHarness.Diagnostics(replies: replies, uri: Uri));
    }

    [Fact]
    public void Completion_OffersOnlySuflaeKeywords()
    {
        const string sfUri = "file:///test/Keywords.sf";
        const string sf =
            "routine start()\n" +
            "    var total = 1\n" +
            "    return\n";
        (int line, int character) = LspTestHarness.PositionOf(text: sf, needle: "total");
        IReadOnlyList<JsonDocument> replies = LspTestHarness.Run(
            LspTestHarness.DidOpen(uri: sfUri, text: sf, languageId: "suflae"),
            LspTestHarness.Positional(id: 40, method: "textDocument/completion", uri: sfUri, line: line,
                character: character + 1),
            Exit);

        List<string> labels = CompletionLabels(reply: LspTestHarness.ReplyWithId(replies: replies, id: 40));
        Assert.Contains(expected: "routine", collection: labels);
        // RazorForge-only keywords are not Suflae's.
        Assert.DoesNotContain(expected: "steal", collection: labels);
    }

    [Theory]
    [InlineData("routine start()\n    var m = Moment.epoch()\n    return\n", "Moment", "absolute point in time")]
    [InlineData("routine start()\n    var d = Duration.zero()\n    return\n", "Duration", "span of time")]
    [InlineData("### Two numbers side by side.\nrecord Pair\n    a: S64\n    b: S64\n\nroutine start()\n    var p = Pair(a: 1, b: 2)\n    return\n", "Pair(", "side by side")]
    public void Hover_OverType_ShowsItsDocComment(string source, string needle, string doc)
    {
        // A Suflae standard library type is shown with `@rf(...)` between its doc comment and its header.
        string dir = Directory.CreateTempSubdirectory(prefix: "lsp_hover_").FullName;
        string path = Path.Combine(path1: dir, path2: "main.sf");
        File.WriteAllText(path: path, contents: source);
        string uri = new System.Uri(uriString: path).AbsoluteUri;
        (int line, int character) = LspTestHarness.PositionOf(text: source, needle: needle);
        IReadOnlyList<JsonDocument> replies = LspTestHarness.Run(
            LspTestHarness.DidOpen(uri: uri, text: source, languageId: "suflae"),
            LspTestHarness.Positional(id: 9, method: "textDocument/hover", uri: uri, line: line, character: character),
            Exit);

        JsonElement? reply = LspTestHarness.ReplyWithId(replies: replies, id: 9);
        Assert.NotNull(@object: reply);
        Assert.True(condition: reply!.Value.GetRawText().Contains(value: doc),
            userMessage: string.Join("\n", replies.Select(r => r.RootElement.GetRawText())));
    }

    [Fact]
    public void SuflaeDocument_Analyzes()
    {
        const string sf =
            "module Test/LspSf\n" +
            "import IO/Console\n" +
            "\n" +
            "routine start()\n" +
            "    var x = 41\n" +
            "    var y = x + 1\n" +
            "    show(f\"{y}\")\n" +
            "    return\n";

        IReadOnlyList<JsonDocument> replies = LspTestHarness.Run(
            LspTestHarness.DidOpen(uri: "file:///test/Sample.sf", text: sf, languageId: "suflae"),
            LspTestHarness.DocRequest(id: 21, method: "textDocument/documentSymbol", uri: "file:///test/Sample.sf"),
            Exit);

        Assert.NotNull(@object: LspTestHarness.ReplyWithId(replies: replies, id: 21));
    }
}
