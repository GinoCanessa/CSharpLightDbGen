using System.Collections.Immutable;
using CsLightDbGen.SQLiteGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace cslightdbgen.sqlitegen.tests.TestInfrastructure;

internal sealed record GeneratorRunResult(
    CSharpCompilation InputCompilation,
    CSharpCompilation OutputCompilation,
    GeneratorDriverRunResult DriverRunResult,
    ImmutableArray<Diagnostic> OutputDiagnostics,
    IReadOnlyDictionary<string, string> GeneratedSources)
{
    public IEnumerable<Diagnostic> Errors => OutputDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
}

internal static class GeneratorTestHost
{
    public static GeneratorRunResult Run(params string[] sourceTexts)
    {
        if (sourceTexts.Length == 0)
        {
            throw new ArgumentException("At least one source text is required.", nameof(sourceTexts));
        }

        var allSources = new[]
        {
            "global using System;\nglobal using System.Threading;"
        }.Concat(sourceTexts);

        var syntaxTrees = allSources
            .Select(static text => CSharpSyntaxTree.ParseText(text))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorTestsAssembly",
            syntaxTrees: syntaxTrees,
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new LightSQLiteGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var outputDiagnostics);
        var runResult = driver.GetRunResult();

        var generated = runResult.Results
            .SelectMany(static r => r.GeneratedSources)
            .GroupBy(static g => g.HintName)
            .ToDictionary(static g => g.Key, static g => g.Last().SourceText.ToString());

        return new GeneratorRunResult(
            compilation,
            (CSharpCompilation)outputCompilation,
            runResult,
            outputDiagnostics,
            generated);
    }

    public static string GetGeneratedSourceByHintSuffix(GeneratorRunResult runResult, string suffix)
    {
        var match = runResult.GeneratedSources
            .FirstOrDefault(kvp => kvp.Key.EndsWith(suffix, StringComparison.Ordinal));

        if (string.IsNullOrEmpty(match.Key))
        {
            throw new InvalidOperationException($"Could not find generated source ending with '{suffix}'.");
        }

        return match.Value;
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        var tpa = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string)
            ?.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            ?? [];

        return tpa.Select(static p => MetadataReference.CreateFromFile(p));
    }
}
