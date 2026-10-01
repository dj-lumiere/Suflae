using Builder.Targeting;

namespace Suflae.Tests.BuildSystem;

/// <summary>
/// Tests for <see cref="TargetGate"/> — file-granularity conditional compilation driven by a leading
/// <c>@target(...)</c> comment directive. A concrete non-host <see cref="TargetConfig"/> is passed so
/// the assertions are deterministic regardless of the machine running the tests.
/// </summary>
public sealed class TargetGateTests
{
    private static TargetConfig Target(string os, string arch)
    {
        return new TargetConfig(triple: "",
            dataLayout: "",
            pointerBitWidth: 64,
            pageSize: 4096,
            cacheLineSize: 64,
            targetOS: os,
            targetArch: arch);
    }

    [Fact]
    public void NonRfFile_NeverGated()
    {
        // A `.sf` file is Suflae — never subject to conditional compilation, even with a directive.
        string f = WriteTemp(name: "x.sf", body: "@target(os: \"windows\")\nmodule M\n");
        try
        {
            Assert.True(condition: TargetGate.ShouldCompile(filePath: f,
                target: Target(os: "linux", arch: "x86_64")));
        }
        finally { File.Delete(path: f); }
    }

    private static string WriteTemp(string name, string body)
    {
        string path = Path.Combine(path1: Path.GetTempPath(),
            path2: "rf_tgt_" + Guid.NewGuid()
                                   .ToString(format: "N") + "_" + name);
        File.WriteAllText(path: path, contents: body);
        return path;
    }
}
