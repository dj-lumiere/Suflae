using System.Diagnostics;
using System.Text;

namespace Suflae.Tests.Meta;

/// <summary>
/// End-to-end check of <c>set_exit_code</c>: a program that returns from <c>start</c> normally ends with the status
/// it set last, which the builder's <c>main</c> returns after <c>start</c>. Builds and runs
/// <c>tests/Fixtures/ExitCode/set_exit_code.sf</c> through <c>buildandrun</c>.
/// </summary>
public sealed class ExitCodeTests
{
    private static readonly string RepoRoot = LocateRepoRoot();

    [Fact]
    public void SetExitCode_LastCallIsTheExitStatus()
    {
        (int exit, string stdout, string stderr) = RunFixture(fixture: "set_exit_code.sf");

        Assert.True(condition: exit == 42,
            userMessage: $"expected exit 42, got {exit}\n--- stdout ---\n{stdout}\n--- stderr ---\n{stderr}");
        Assert.Contains(expectedSubstring: "returning", actualString: stdout);
    }

    /// <summary>A recovery keyword recovers every failure beneath it except <c>pierce</c>: a <c>pierce</c> two
    /// calls under <c>try</c> still crashes with status 82, after the same call recovered a plain throw.</summary>
    [Fact]
    public void PierceUnderTry_CrashesLoudly()
    {
        (int exit, string stdout, string stderr) = RunFixture(fixture: "pierce_under_try.sf");

        Assert.True(condition: exit == 82,
            userMessage: $"expected exit 82, got {exit}\n--- stdout ---\n{stdout}\n--- stderr ---\n{stderr}");
        Assert.Contains(expectedSubstring: "recovered throw: none", actualString: stdout);
        Assert.Contains(expectedSubstring: "value: 6", actualString: stdout);
        Assert.Contains(expectedSubstring: "piercing", actualString: stdout);
        Assert.DoesNotContain(expectedSubstring: "not reached", actualString: stdout);
        Assert.Contains(expectedSubstring: "Broken invariant error: the ledger no longer balances",
            actualString: stdout + stderr);
    }

    /// <summary>A lambda's failure is recovered only beneath a recovery keyword: called with no keyword above
    /// the call (here by an <c>each</c> loop over a <c>select</c>) it crashes with status 82, and the loop does
    /// not quietly end early.</summary>
    [Fact]
    public void LambdaFailureWithoutKeyword_CrashesLoudly()
    {
        (int exit, string stdout, string stderr) = RunFixture(fixture: "lambda_bare_crash.sf");

        Assert.True(condition: exit == 82,
            userMessage: $"expected exit 82, got {exit}\n--- stdout ---\n{stdout}\n--- stderr ---\n{stderr}");
        Assert.Contains(expectedSubstring: "recovered: none", actualString: stdout);
        Assert.Contains(expectedSubstring: "step 2", actualString: stdout);
        Assert.DoesNotContain(expectedSubstring: "step 5", actualString: stdout);
        Assert.DoesNotContain(expectedSubstring: "not reached", actualString: stdout);
        Assert.Contains(expectedSubstring: "Division by zero error", actualString: stdout + stderr);
    }

    /// <summary>A <c>pierce</c> inside a lambda another routine calls still crashes with status 82 beneath
    /// <c>try</c>, after the same call recovered the lambda's ordinary failure.</summary>
    [Fact]
    public void PierceInLambdaUnderTry_CrashesLoudly()
    {
        (int exit, string stdout, string stderr) = RunFixture(fixture: "pierce_in_lambda.sf");

        Assert.True(condition: exit == 82,
            userMessage: $"expected exit 82, got {exit}\n--- stdout ---\n{stdout}\n--- stderr ---\n{stderr}");
        Assert.Contains(expectedSubstring: "recovered: none", actualString: stdout);
        Assert.Contains(expectedSubstring: "value: 25", actualString: stdout);
        Assert.Contains(expectedSubstring: "piercing", actualString: stdout);
        Assert.DoesNotContain(expectedSubstring: "not reached", actualString: stdout);
        Assert.Contains(expectedSubstring: "Broken invariant error: the ledger no longer balances",
            actualString: stdout + stderr);
    }

    /// <summary>A range step held in a variable is checked when the range is iterated: a negative step counts down,
    /// and a step of zero crashes with status 82 instead of looping forever.</summary>
    [Fact]
    public void RangeStepZero_CrashesLoudly()
    {
        (int exit, string stdout, string stderr) = RunFixture(fixture: "range_step_zero.sf");

        Assert.True(condition: exit == 82,
            userMessage: $"expected exit 82, got {exit}\n--- stdout ---\n{stdout}\n--- stderr ---\n{stderr}");
        Assert.Contains(expectedSubstring: "step 10", actualString: stdout);
        Assert.Contains(expectedSubstring: "step 1", actualString: stdout);
        Assert.DoesNotContain(expectedSubstring: "not reached", actualString: stdout);
        Assert.Contains(expectedSubstring: "Range step zero error", actualString: stdout + stderr);
    }

    /// <summary>An entity from an imported `.rf` module works as a local and as a member variable of a Suflae entity:
    /// its routines are built as RazorForge code and it is released once.</summary>
    [Fact]
    public void RazorForgeEntity_UsedFromSuflae_RunsAndIsReleasedOnce()
    {
        (int exit, string stdout, string stderr) = RunFixture(fixture: "rf_entity_use.sf", folder: "RfInterop");

        Assert.True(condition: exit == 0,
            userMessage: $"expected exit 0, got {exit}\n--- stdout ---\n{stdout}\n--- stderr ---\n{stderr}");
        Assert.Equal(expected: "2\n12\n2", actual: stdout.Replace(oldValue: "\r\n", newValue: "\n").Trim());
    }

    /// <summary>A parameter written as a bare `.rf` entity name is SF-S439, as `RF::Counter` is.</summary>
    [Fact]
    public void RazorForgeEntity_BareNameParameter_IsS439()
    {
        (int exit, string stdout, string stderr) =
            RunFixture(fixture: "rf_entity_param.sf", folder: "RfInterop", verb: "check");

        Assert.NotEqual(expected: 0, actual: exit);
        Assert.Contains(expectedSubstring: "SF-S439", actualString: stdout + stderr);
    }

    /// <summary>Keeping a `.rf` entity under a second name is SF-S413: both names would release it.</summary>
    [Fact]
    public void RazorForgeEntity_SecondName_IsS413()
    {
        (int exit, string stdout, string stderr) =
            RunFixture(fixture: "rf_entity_alias.sf", folder: "RfInterop", verb: "check");

        Assert.NotEqual(expected: 0, actual: exit);
        Assert.Contains(expectedSubstring: "SF-S413", actualString: stdout + stderr);
    }

    private static (int Exit, string Stdout, string Stderr) RunFixture(string fixture, string folder = "ExitCode",
        string verb = "buildandrun")
    {
        string sfPath = Path.Combine(paths: [RepoRoot, "tests", "Fixtures", folder, fixture]);
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { TestHelpers.ToolchainDll(sourcePath: sfPath), verb, sfPath },
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
        if (!p.WaitForExit(milliseconds: 300_000))
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
