using System.Diagnostics;
using System.Text;

namespace Suflae.Tests.Meta;

/// <summary>
/// ObjectHacker's loud failures, each a program that ends with status 82 and the message of the failure: what an
/// <c>Unknown</c> keeps compared or hashed without the type having <c>==</c> or a hash.
/// </summary>
public sealed class ObjectHackerCrashTests
{
    private static readonly string RepoRoot = LocateRepoRoot();

    /// <summary>Comparing two Unknowns that keep a record with no <c>==</c> crashes with a message naming the type.</summary>
    [Fact]
    public void UnknownEquality_WithoutEq_CrashesNamingTheType()
    {
        (int exit, string stdout, string stderr) = RunFixture(fixture: "unknown_no_equality.sf");

        Assert.True(condition: exit == 82,
            userMessage: $"expected exit 82, got {exit}\n--- stdout ---\n{stdout}\n--- stderr ---\n{stderr}");
        Assert.Contains(expectedSubstring: "comparing", actualString: stdout);
        Assert.DoesNotContain(expectedSubstring: "not reached", actualString: stdout);
        Assert.Contains(expectedSubstring: "'Action' has no ==", actualString: stdout + stderr);
    }

    private static (int Exit, string Stdout, string Stderr) RunFixture(string fixture)
    {
        string sfPath = Path.Combine(paths: [RepoRoot, "tests", "Fixtures", "ExitCode", fixture]);
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { TestHelpers.ToolchainDll(sourcePath: sfPath), "buildandrun", sfPath },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            WorkingDirectory = RepoRoot,
            Environment =
            {
                [key: "DOTNET_gcServer"] = "0", [key: "DOTNET_GCConserveMemory"] = "9"
            }
        };
        using var p = Process.Start(startInfo: psi)!;
        Task<string> outTask = p.StandardOutput.ReadToEndAsync();
        Task<string> errTask = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(milliseconds: TestHelpers.ChildTimeoutMs(baseMs: 300_000)))
        {
            try { p.Kill(entireProcessTree: true); }
            catch
            {
                /* best effort */
            }

            Assert.Fail(message: $"{fixture} did not finish.");
        }

        return (p.ExitCode, outTask.Result, errTask.Result);
    }

    private static string LocateRepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(value: dir))
        {
            if (File.Exists(path: Path.Combine(path1: dir, path2: "Suflae.csproj")))
            {
                return dir;
            }

            string? parent = Path.GetDirectoryName(path: dir);
            if (parent == null || parent == dir)
            {
                break;
            }

            dir = parent;
        }

        throw new InvalidOperationException(
            message: "Could not locate Suflae.csproj walking up from test assembly directory.");
    }
}
