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

    /// <summary>A throw in a routine that is not failable crashes where it stands with its crashable's message (status
    /// 82), even when a failable routine calls it under <c>try</c>, after the same <c>try</c> received the failable
    /// routine's own throw.</summary>
    [Fact]
    public void ThrowInPlainRoutineUnderTry_CrashesWithItsMessage()
    {
        (int exit, string stdout, string stderr) = RunFixture(fixture: "throw_in_plain_under_try.sf");

        Assert.True(condition: exit == 82,
            userMessage: $"expected exit 82, got {exit}\n--- stdout ---\n{stdout}\n--- stderr ---\n{stderr}");
        Assert.Contains(expectedSubstring: "recovered throw: none", actualString: stdout);
        Assert.Contains(expectedSubstring: "value: 6", actualString: stdout);
        Assert.Contains(expectedSubstring: "breaking", actualString: stdout);
        Assert.DoesNotContain(expectedSubstring: "not reached", actualString: stdout);
        Assert.Contains(expectedSubstring: "Broken invariant error: the ledger no longer balances",
            actualString: stdout + stderr);
    }

    /// <summary>A failure no keyword receives crashes with status 82 and its own message, after a <c>check</c> on the
    /// same flow kept an earlier failure: the failure mold holds the new failure, and the kept <c>Check</c> its own.</summary>
    [Fact]
    public void BareFailureAfterKeptCheck_CrashesWithItsOwnMessage()
    {
        (int exit, string stdout, string stderr) = RunFixture(fixture: "mold_bare_crash.sf");

        Assert.True(condition: exit == 82,
            userMessage: $"expected exit 82, got {exit}\n--- stdout ---\n{stdout}\n--- stderr ---\n{stderr}");
        Assert.Contains(expectedSubstring: "kept: the code was 1", actualString: stdout);
        Assert.DoesNotContain(expectedSubstring: "not reached", actualString: stdout);
        Assert.Contains(expectedSubstring: "the code was 9", actualString: stdout + stderr);
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

    /// <summary>Failures rise only through failable routines: <c>try</c> receives a lambda's failure handed through a
    /// <c>failable Callable</c> parameter and a failable routine's throw, but a division by zero inside a routine that
    /// is not failable crashes there (status 82), even under <c>try</c>.</summary>
    [Fact]
    public void FailureInsidePlainHelperUnderTry_CrashesLoudly()
    {
        (int exit, string stdout, string stderr) = RunFixture(fixture: "wall_under_try.sf");

        Assert.True(condition: exit == 82,
            userMessage: $"expected exit 82, got {exit}\n--- stdout ---\n{stdout}\n--- stderr ---\n{stderr}");
        Assert.Contains(expectedSubstring: "recovered lambda: none", actualString: stdout);
        Assert.Contains(expectedSubstring: "value: 25", actualString: stdout);
        Assert.Contains(expectedSubstring: "recovered throw: none", actualString: stdout);
        Assert.Contains(expectedSubstring: "split: 5", actualString: stdout);
        Assert.Contains(expectedSubstring: "dividing by zero", actualString: stdout);
        Assert.DoesNotContain(expectedSubstring: "not reached", actualString: stdout);
        Assert.Contains(expectedSubstring: "Division by zero error", actualString: stdout + stderr);
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

    /// <summary>Calls deeper than the memory for them holds crash with the out of memory report and status 82, never
    /// a segmentation fault.</summary>
    [Fact]
    public void DeepCalls_CrashLoudly()
    {
        (int exit, string stdout, string stderr) = RunFixture(fixture: "deep_calls.sf");

        Assert.True(condition: exit == 82,
            userMessage: $"expected exit 82, got {exit}\n--- stdout ---\n{stdout}\n--- stderr ---\n{stderr}");
        Assert.Contains(expectedSubstring: "shallow 100", actualString: stdout);
        Assert.DoesNotContain(expectedSubstring: "not reached", actualString: stdout);
        Assert.Contains(expectedSubstring: "Out of memory error", actualString: stdout + stderr);
        Assert.DoesNotContain(expectedSubstring: "SegmentationFault", actualString: stdout + stderr);
    }

    /// <summary>Two jobs that use two shared entities in opposite orders wait on each other: a debug build's
    /// deadlock check stops the program with status 82 and says which job waits for which, instead of hanging. The
    /// report speaks of jobs and entities, never of the runtime's access manager.</summary>
    [Fact]
    public void SharedEntityDeadlock_CrashesLoudly()
    {
        (int exit, string stdout, string stderr) = RunFixture(fixture: "shared_entity_deadlock.sf");

        Assert.True(condition: exit == 82,
            userMessage: $"expected exit 82, got {exit}\n--- stdout ---\n{stdout}\n--- stderr ---\n{stderr}");
        Assert.Contains(expectedSubstring: "starting", actualString: stdout);
        Assert.DoesNotContain(expectedSubstring: "not reached", actualString: stdout);
        Assert.Contains(expectedSubstring: "These pieces of work wait on each other for the entities they share",
            actualString: stdout + stderr);
        Assert.DoesNotContain(expectedSubstring: "AccessManager", actualString: stdout + stderr);
    }

    /// <summary>Two jobs whose statements use globals of two modules in opposite orders finish (every global is behind
    /// one lock), and two whose statements use the globals and a shared entity in opposite orders end with a deadlock
    /// error, in a debug build and in a release build, never hanging.</summary>
    [Theory]
    [InlineData("debug")]
    [InlineData("release")]
    public void GlobalsAndEntityDeadlock_CrashesLoudly_InEveryMode(string mode)
    {
        string dir = Path.Combine(path1: Path.GetTempPath(), path2: $"sf-global-deadlock-{mode}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: dir);
        try
        {
            foreach (string file in Directory.GetFiles(
                         path: Path.Combine(paths: [RepoRoot, "tests", "Fixtures", "GlobalDeadlock"]), searchPattern: "*.sf"))
            {
                File.Copy(sourceFileName: file, destFileName: Path.Combine(path1: dir, path2: Path.GetFileName(path: file)));
            }

            File.WriteAllText(path: Path.Combine(path1: dir, path2: "config.toml"),
                contents: "[package]\nname = \"global-deadlock\"\nversion = \"0.0.1\"\nsuflae-version = \"0.5.0\"\n\n" +
                          $"[target]\nmode = \"{mode}\"\n");
            (int exit, string stdout, string stderr) = RunSource(sfPath: Path.Combine(path1: dir, path2: "main.sf"),
                name: $"global deadlock ({mode})");

            Assert.True(condition: exit == 82,
                userMessage: $"expected exit 82, got {exit}\n--- stdout ---\n{stdout}\n--- stderr ---\n{stderr}");
            Assert.Contains(expectedSubstring: "modules done", actualString: stdout);
            Assert.DoesNotContain(expectedSubstring: "not reached", actualString: stdout);
            Assert.Contains(expectedSubstring: "Deadlock error", actualString: stdout + stderr);
        }
        finally
        {
            try { Directory.Delete(path: dir, recursive: true); }
            catch
            {
                /* best effort */
            }
        }
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

    /// <summary><c>Int(1)</c> builds an <c>Int</c> from a value that is one already: SF-S646, which says to use the
    /// value as it is, where the LLVM emitter used to stop.</summary>
    [Fact]
    public void IntBuiltFromAnInt_IsS646()
    {
        (int exit, string stdout, string stderr) = RunFixture(fixture: "int_from_int.sf", verb: "check");

        Assert.NotEqual(expected: 0, actual: exit);
        Assert.Contains(expectedSubstring: "SF-S646", actualString: stdout + stderr);
        Assert.Contains(expectedSubstring: "from a value that is an 'Int' already", actualString: stdout + stderr);
    }

    /// <summary><c>U8(x)</c> with an <c>S64</c> <c>x</c> would read the bits of <c>x</c> and change the number:
    /// SF-S655, which points to <c>x.to[U8]()</c>.</summary>
    [Fact]
    public void NumberBuiltFromBits_IsS655()
    {
        (int exit, string stdout, string stderr) = RunFixture(fixture: "number_from_bits.sf", verb: "check");

        Assert.NotEqual(expected: 0, actual: exit);
        Assert.Contains(expectedSubstring: "SF-S655", actualString: stdout + stderr);
        Assert.Contains(expectedSubstring: "'x.to[U8]()'", actualString: stdout + stderr);
    }

    /// <summary><c>T(x)</c> in a generic routine reads bits once <c>make[U8]</c> makes <c>T</c> a <c>U8</c>: SF-S655
    /// names the instance and points to <c>x.to[T]()</c>.</summary>
    [Fact]
    public void GenericNumberBuiltFromBits_IsS655()
    {
        (int exit, string stdout, string stderr) = RunFixture(fixture: "generic_number_from_bits.sf", verb: "check");

        Assert.NotEqual(expected: 0, actual: exit);
        Assert.Contains(expectedSubstring: "SF-S655", actualString: stdout + stderr);
        Assert.Contains(expectedSubstring: "'T(x)' in 'make[U8]'", actualString: stdout + stderr);
        Assert.Contains(expectedSubstring: "'x.to[T]()'", actualString: stdout + stderr);
    }

    /// <summary><c>Char(n)</c> and <c>Bool(x)</c> read bits too: SF-S655, pointing to <c>Char.from_codepoint</c>
    /// and to a comparison.</summary>
    [Fact]
    public void CharAndBoolBuiltFromBits_AreS655()
    {
        (int exit, string stdout, string stderr) = RunFixture(fixture: "char_bool_from_bits.sf", verb: "check");

        Assert.NotEqual(expected: 0, actual: exit);
        Assert.Contains(expectedSubstring: "'Char.from_codepoint(n: n.to[Int]())'", actualString: stdout + stderr);
        Assert.Contains(expectedSubstring: "'two != 0'", actualString: stdout + stderr);
    }

    private static (int Exit, string Stdout, string Stderr) RunFixture(string fixture, string folder = "ExitCode",
        string verb = "buildandrun")
    {
        return RunSource(sfPath: Path.Combine(paths: [RepoRoot, "tests", "Fixtures", folder, fixture]), name: fixture,
            verb: verb);
    }

    /// <summary>Builds and runs (or checks) <paramref name="sfPath"/>, failing the test when it doesn't finish in
    /// time (a hang).</summary>
    private static (int Exit, string Stdout, string Stderr) RunSource(string sfPath, string name,
        string verb = "buildandrun")
    {
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
        if (!p.WaitForExit(milliseconds: TestHelpers.ChildTimeoutMs(baseMs: 300_000)))
        {
            try { p.Kill(entireProcessTree: true); }
            catch
            {
                /* best effort */
            }

            Assert.Fail(message: $"{name} did not finish.");
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
