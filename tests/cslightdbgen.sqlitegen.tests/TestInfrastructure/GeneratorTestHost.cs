using System.Collections.Immutable;
using CsLightDbGen.SQLiteGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace cslightdbgen.sqlitegen.tests.TestInfrastructure;

internal sealed record GeneratorTestOptions
{
    public string AssemblyName { get; init; } = "GeneratorTestsAssembly";

    public CSharpParseOptions ParseOptions { get; init; } = CSharpParseOptions.Default;

    public IReadOnlyList<MetadataReference> AdditionalReferences { get; init; } = [];

    public bool TrackIncrementalSteps { get; init; }
}

internal sealed record GeneratorRunResult(
    CSharpCompilation InputCompilation,
    CSharpCompilation OutputCompilation,
    GeneratorDriverRunResult DriverRunResult,
    ImmutableArray<Diagnostic> OutputDiagnostics,
    IReadOnlyDictionary<string, string> GeneratedSources)
{
    internal GeneratorDriver? Driver { get; init; }

    public IEnumerable<Diagnostic> Diagnostics => DriverRunResult.Diagnostics
        .Concat(OutputDiagnostics)
        .Concat(OutputCompilation.GetDiagnostics())
        .Distinct();

    public IEnumerable<Diagnostic> Errors => OutputDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);

    public IEnumerable<Diagnostic> CompilationErrors => OutputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);
}

internal sealed record EmittedReferenceResult(
    PortableExecutableReference Reference,
    ImmutableArray<Diagnostic> Diagnostics);

internal static class GeneratorTestHost
{
    public static GeneratorRunResult Run(params string[] sourceTexts) =>
        Run(new GeneratorTestOptions(), sourceTexts);

    public static GeneratorRunResult Run(GeneratorTestOptions options, params string[] sourceTexts)
    {
        CSharpCompilation compilation = CreateCompilation(options, sourceTexts);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new LightSQLiteGenerator().AsSourceGenerator()],
            parseOptions: options.ParseOptions,
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: options.TrackIncrementalSteps));

        return RunInternal(driver, compilation);
    }

    /// <summary>
    /// Creates an ordinary compilation without running the analyzer. Explicit assembly names let
    /// image-composition tests keep each host and consumer's metadata identity distinct and stable.
    /// </summary>
    public static CSharpCompilation CreateCompilation(GeneratorTestOptions options, params string[] sourceTexts)
    {
        if (sourceTexts.Length == 0)
        {
            throw new ArgumentException("At least one source text is required.", nameof(sourceTexts));
        }

        SyntaxTree[] syntaxTrees = sourceTexts
            .Select(text => CSharpSyntaxTree.ParseText(text, options.ParseOptions))
            .ToArray();

        return CSharpCompilation.Create(
            assemblyName: options.AssemblyName,
            syntaxTrees: syntaxTrees,
            references: GetMetadataReferences().Concat(options.AdditionalReferences),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    /// <summary>
    /// Emits a real assembly image entirely in memory. Report every emit diagnostic on failure,
    /// including warnings; successful callers can inspect the same diagnostics alongside compilation
    /// and generator diagnostics rather than treating a generator run as proof of a valid image.
    /// </summary>
    public static EmittedReferenceResult EmitToReference(CSharpCompilation compilation)
    {
        using MemoryStream image = new MemoryStream();
        EmitResult result = compilation.Emit(image);
        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Could not emit assembly '{compilation.AssemblyName}':{Environment.NewLine}" +
                string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        return new EmittedReferenceResult(
            MetadataReference.CreateFromImage(image.ToArray()),
            result.Diagnostics);
    }

    /// <summary>
    /// Reuses a host run's driver and original input compilation (never its generated trees).
    /// Replacement keys are zero-based input-tree indexes. Unchanged trees retain their identity;
    /// parse-option changes reparse every input so conditional directives are evaluated again.
    /// </summary>
    public static GeneratorRunResult ReRun(
        GeneratorRunResult previousRun,
        IReadOnlyDictionary<int, string>? sourceReplacements = null,
        CSharpParseOptions? parseOptions = null)
    {
        GeneratorDriver driver = previousRun.Driver
            ?? throw new ArgumentException("The previous run must come from GeneratorTestHost.", nameof(previousRun));
        SyntaxTree[] originalTrees = previousRun.InputCompilation.SyntaxTrees.ToArray();
        CSharpParseOptions effectiveParseOptions = parseOptions ?? (CSharpParseOptions)originalTrees[0].Options;
        bool updateParseOptions = !effectiveParseOptions.Equals(originalTrees[0].Options);

        if (sourceReplacements != null && sourceReplacements.Keys.Any(index => index < 0 || index >= originalTrees.Length))
        {
            throw new ArgumentOutOfRangeException(nameof(sourceReplacements), "Replacement indexes must identify an input syntax tree.");
        }

        // Remove the old trees together before changing language version: Roslyn rejects even a
        // transient compilation containing trees from two different language versions.
        CSharpCompilation compilation = updateParseOptions
            ? previousRun.InputCompilation.RemoveAllSyntaxTrees()
            : previousRun.InputCompilation;
        for (int index = 0; index < originalTrees.Length; index++)
        {
            SyntaxTree originalTree = originalTrees[index];
            string? replacement = null;
            bool replaceSource = sourceReplacements != null && sourceReplacements.TryGetValue(index, out replacement);
            if (replaceSource || updateParseOptions)
            {
                SyntaxTree updatedTree = CSharpSyntaxTree.ParseText(
                    replaceSource ? replacement! : originalTree.GetText().ToString(),
                    effectiveParseOptions,
                    originalTree.FilePath);
                compilation = updateParseOptions
                    ? compilation.AddSyntaxTrees(updatedTree)
                    : compilation.ReplaceSyntaxTree(originalTree, updatedTree);
            }
        }

        if (updateParseOptions)
        {
            driver = driver.WithUpdatedParseOptions(effectiveParseOptions);
        }

        return RunInternal(driver, compilation);
    }

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
        GeneratorRunResult referencedRun = Run(
            new GeneratorTestOptions { AssemblyName = "ReferencedAssembly" },
            referencedSource);

        return Run(
            new GeneratorTestOptions { AdditionalReferences = [referencedRun.OutputCompilation.ToMetadataReference()] },
            sourceTexts);
    }

    private static GeneratorRunResult RunInternal(GeneratorDriver driver, CSharpCompilation compilation)
    {
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out ImmutableArray<Diagnostic> outputDiagnostics);
        GeneratorDriverRunResult runResult = driver.GetRunResult();

        Dictionary<string, string> generated = runResult.Results
            .SelectMany(static r => r.GeneratedSources)
            .GroupBy(static g => g.HintName)
            .ToDictionary(static g => g.Key, static g => g.Last().SourceText.ToString());

        return new GeneratorRunResult(
            compilation,
            (CSharpCompilation)outputCompilation,
            runResult,
            outputDiagnostics,
            generated)
        {
            Driver = driver,
        };
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
        GeneratorTestOptions options = new GeneratorTestOptions
        {
            AssemblyName = "GeneratorCachingTestsAssembly",
            TrackIncrementalSteps = true,
        };
        GeneratorRunResult firstRun = Run(options, sourceTexts);
        CSharpCompilation compilation = firstRun.InputCompilation
            .AddSyntaxTrees(CSharpSyntaxTree.ParseText(unrelatedSource, options.ParseOptions));

        return RunInternal(firstRun.Driver!, compilation).DriverRunResult;
    }

    /// <summary>
    /// Compiles the recommended multi-project topology: <paramref name="analyzerHostSource"/> is the
    /// single project that hosts the generator (so it alone gets the post-initialization attribute
    /// types and the generated DAL), and <paramref name="consumerSource"/> is a downstream project
    /// that references the host as METADATA ONLY and does not run the generator itself. Returns both
    /// compilations so a test can assert the consumer binds the host's <em>public</em> attribute
    /// types and generated APIs with no <c>CS0436</c> duplicate-type conflict.
    /// </summary>
    public static (CSharpCompilation AnalyzerHost, CSharpCompilation Consumer) CompileConsumerReferencingGeneratedProject(
        string analyzerHostSource, string consumerSource)
    {
        GeneratorRunResult hostRun = Run(
            new GeneratorTestOptions { AssemblyName = "AnalyzerHostProject" },
            analyzerHostSource);
        CSharpCompilation consumerCompilation = CreateCompilation(
            new GeneratorTestOptions
            {
                AssemblyName = "ConsumerProject",
                AdditionalReferences = [hostRun.OutputCompilation.ToMetadataReference()],
            },
            consumerSource);

        return (hostRun.OutputCompilation, consumerCompilation);
    }

    public static string GetGeneratedSourceByHintSuffix(GeneratorRunResult runResult, string suffix)
    {
        KeyValuePair<string, string> match = runResult.GeneratedSources
            .FirstOrDefault(kvp => kvp.Key.EndsWith(suffix, StringComparison.Ordinal));

        if (string.IsNullOrEmpty(match.Key))
        {
            throw new InvalidOperationException($"Could not find generated source ending with '{suffix}'.");
        }

        return match.Value;
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        string[] tpa = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string)
            ?.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            ?? [];

        return tpa.Select(static p => MetadataReference.CreateFromFile(p));
    }
}
