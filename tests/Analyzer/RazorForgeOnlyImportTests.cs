using Builder.Declaration;
using Builder.Diagnostics;
using Builder.Verification.Results;
using TypeModel.Enums;

namespace Suflae.Tests.Analyzer;

/// <summary>
/// A Suflae file may import only the modules the Suflae standard library declares: a module that only the RazorForge
/// standard library has (BuilderQuery, Simd) is SF-S116 at the import, through the real build driver.
/// </summary>
public class RazorForgeOnlyImportTests
{
    /// <summary>The errors of building <paramref name="source"/> as a Suflae entry file.</summary>
    private static List<SemanticError> BuildErrors(string source)
    {
        string dir = Path.Combine(path1: Path.GetTempPath(), path2: "sf-rf-only-" + Guid.NewGuid().ToString(format: "N"));
        Directory.CreateDirectory(path: dir);
        try
        {
            string entry = Path.Combine(path1: dir, path2: "main.sf");
            File.WriteAllText(path: entry, contents: source);
            var driver = new BuildDriver(projectRoot: dir, stdlibRoot: StdlibLoader.GetDefaultStdlibPath(),
                language: Language.Suflae);
            return driver.CompileFile(entryFile: entry).Errors;
        }
        finally
        {
            Directory.Delete(path: dir, recursive: true);
        }
    }

    /// <summary><c>import BuilderQuery</c> and <c>import Simd</c> in a Suflae file are each an SF-S116 that names the module.</summary>
    [Theory]
    [InlineData("BuilderQuery")]
    [InlineData("Simd")]
    public void Build_SuflaeImportsRazorForgeOnlyModule_ReportsS116(string module)
    {
        List<SemanticError> errors = BuildErrors(source: $"""
                                                          import {module}
                                                          import IO/Console

                                                          routine start()
                                                              show("hi")
                                                          """);
        SemanticError error = Assert.Single(collection: errors,
            predicate: e => e.Code == SemanticDiagnosticCode.ModuleNotInLanguageStandardLibrary);
        Assert.Contains(expectedSubstring: $"'{module}' isn't a module of the Suflae standard library", actualString: error.Message);
    }

    /// <summary>A module the Suflae standard library declares imports as before.</summary>
    [Fact]
    public void Build_SuflaeImportsItsOwnModule_HasNoS116()
    {
        List<SemanticError> errors = BuildErrors(source: """
                                                         import Collections
                                                         import IO/Console

                                                         routine start()
                                                             show("hi")
                                                         """);
        Assert.DoesNotContain(collection: errors,
            filter: e => e.Code == SemanticDiagnosticCode.ModuleNotInLanguageStandardLibrary);
    }
}
