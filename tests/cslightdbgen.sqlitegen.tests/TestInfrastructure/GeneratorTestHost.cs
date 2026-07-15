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

    public IEnumerable<Diagnostic> CompilationErrors => OutputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);
}

internal static class GeneratorTestHost
{
    public static GeneratorRunResult Run(params string[] sourceTexts) =>
        RunInternal([], sourceTexts);

    /// <summary>
    /// Runs the generator against <paramref name="sourceTexts"/> with an additional referenced
    /// assembly compiled from <paramref name="referencedSource"/>. The referenced assembly is
    /// exposed only as metadata (no syntax), so a base type declared there exercises the
    /// referenced-assembly (symbol-only) code path. The generator is run over the referenced source
    /// first so it gains the post-initialization attribute types and can legally apply
    /// <c>[LdgSQLiteBaseClass]</c>.
    /// </summary>
    public static GeneratorRunResult RunWithReference(string referencedSource, params string[] sourceTexts)
    {
        var referencedCompilation = CSharpCompilation.Create(
            assemblyName: "ReferencedAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(referencedSource)],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        CSharpGeneratorDriver.Create(new LightSQLiteGenerator())
            .RunGeneratorsAndUpdateCompilation(referencedCompilation, out var referencedOutput, out _);

        return RunInternal([((CSharpCompilation)referencedOutput).ToMetadataReference()], sourceTexts);
    }

    private static GeneratorRunResult RunInternal(IReadOnlyList<MetadataReference> additionalReferences, string[] sourceTexts)
    {
        if (sourceTexts.Length == 0)
        {
            throw new ArgumentException("At least one source text is required.", nameof(sourceTexts));
        }

        var syntaxTrees = sourceTexts
            .Select(static text => CSharpSyntaxTree.ParseText(text))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorTestsAssembly",
            syntaxTrees: syntaxTrees,
            references: GetMetadataReferences().Concat(additionalReferences),
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

    /// <summary>
    /// Runs the generator with incremental-step tracking enabled, then re-runs the SAME driver after
    /// appending an unrelated syntax tree (one that declares no generator targets). Because the
    /// original sources are unchanged, a correctly-cached incremental pipeline reuses each model's
    /// transform output on the second run. Returns the second run's result so a caching test can
    /// assert the reuse via <see cref="GeneratorDriverRunResult.Results"/> tracked steps.
    /// </summary>
    public static GeneratorDriverRunResult RunAndReRunWithUnrelatedTree(string unrelatedSource, params string[] sourceTexts)
    {
        if (sourceTexts.Length == 0)
        {
            throw new ArgumentException("At least one source text is required.", nameof(sourceTexts));
        }

        SyntaxTree[] syntaxTrees = sourceTexts
            .Select(static text => CSharpSyntaxTree.ParseText(text))
            .ToArray();

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorCachingTestsAssembly",
            syntaxTrees: syntaxTrees,
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new LightSQLiteGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

        driver = driver.RunGenerators(compilation);

        Compilation updatedCompilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(unrelatedSource));
        driver = driver.RunGenerators(updatedCompilation);

        return driver.GetRunResult();
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
