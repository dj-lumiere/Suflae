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
        "    var total = add(a: 1_s32, b: 2_s32)\n" +
        "    var p = Point(x: 3_s32, y: 4_s32)\n" +
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

    private static (int Line, int Character) Pos(string needle, int occurrence = 1)
    {
        return LspTestHarness.PositionOf(text: Source, needle: needle, occurrence: occurrence);
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
