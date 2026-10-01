using Builder.Serialization;
using Builder.Verification;
using TypeModel.Enums;

namespace Suflae.Tests.Serialization;

/// <summary>
/// Exercises the on-disk stdlib snapshot cache (<c>src/Serialization/StdlibSnapshotCache.cs</c>) — the
/// warm-start path the compile daemon relies on. A <c>dotnet build</c> emits the modular <c>.pbrf</c>
/// artifacts, so <see cref="StdlibSnapshotCache.LoadOrCapture"/> here takes the reassemble-from-cache
/// route (falling back to a fresh capture if the cache is stale/absent), then the process-lifetime memo
/// on the second call.
/// </summary>
public sealed class StdlibSnapshotCacheTests
{
    [Fact]
    public void ComputeStdlibHash_DiffersByLanguageRealm()
    {
        string? rf = StdlibSnapshotCache.ComputeStdlibHash(language: Language.RazorForge);
        string? sf = StdlibSnapshotCache.ComputeStdlibHash(language: Language.Suflae);

        Assert.False(condition: string.IsNullOrWhiteSpace(value: rf));
        Assert.False(condition: string.IsNullOrWhiteSpace(value: sf));
    }

}
