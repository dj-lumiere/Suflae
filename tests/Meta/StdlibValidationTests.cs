using TypeModel.Enums;
using Builder.Verification;
using Builder.Verification.Results;

namespace Suflae.Tests.Meta;

/// <summary>
/// Validates that all stdlib routine bodies pass semantic verification.
/// Mirrors the `validate-stdlib` CLI command.
/// </summary>
public sealed class StdlibValidationTests
{
    /// <summary>Verifies that all Suflae stdlib bodies pass semantic analysis.</summary>
    [Fact]
    public void Suflae_Stdlib_Validates()
    {
        var analyzer = new SemanticVerifier(language: Language.Suflae);
        List<SemanticError> errors = analyzer.ValidateStdlibBodies();
        Assert.Empty(collection: errors);
    }
}
