using cslightdbgen.sqlitegen.tests.TestFixtures;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using Shouldly;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

namespace cslightdbgen.sqlitegen.tests;

/// <summary>
/// Guards the "generated code disposes deterministically" invariant that AGENTS.md declares.
/// A leaked <c>IDbCommand</c> was reported by a downstream consumer, fixed in 35c3406, and shipped
/// in 2026.716.1308 with no test behind it — reverting that commit broke nothing. These tests make
/// a reintroduced bare <c>CreateCommand()</c> or <c>ExecuteReader()</c> fail the suite instead.
///
/// The assertion is over the parsed syntax tree rather than the raw emitted text because the
/// invariant IS the emit shape: a <c>using</c> in the emitted source is what makes the C# compiler
/// guarantee disposal. Parsing is also immune to whitespace churn and can name the offending
/// generated method.
/// </summary>
public class LightSQLiteGenerator_DisposalTests
{
    private sealed record DisposalSite(string HintName, string Method, string Api, bool DisposedByUsing);

    /// <summary>
    /// Minimum site count per (fixture, generated file, generated method, API). Every floor is the
    /// count observed from a real generator run, not a guess: a count that later drops means an emit
    /// path stopped being reached, which would silently shrink the guard.
    /// </summary>
    private static readonly (string Fixture, string HintSuffix, string Method, string Api, int MinSites)[] CoverageFloor =
    [
        // CREATE TABLE + getIndexLines
        ("BasicTableFixture", "BasicEntity.Table.g.cs", "CreateTable", "CreateCommand", 2),
        // CREATE TABLE + PRAGMA table_info probe + getEnsureAddColumnLines (incl. the
        // migration-blocked probes) + getIndexLines
        ("BasicTableFixture", "BasicEntity.Table.g.cs", "EnsureSchema", "CreateCommand", 16),
        // the PRAGMA table_info reader
        ("BasicTableFixture", "BasicEntity.Table.g.cs", "EnsureSchema", "ExecuteReader", 1),
        ("BasicTableFixture", "BasicEntity.Table.g.cs", "LoadMaxKey", "CreateCommand", 1),
        ("BasicTableFixture", "BasicEntity.Table.g.cs", "SelectMaxKey", "CreateCommand", 1),
        // the lazy SelectEnumerable iterator reader
        ("BasicTableFixture", "BasicEntity.Table.g.cs", "SelectEnumerable", "ExecuteReader", 1),
        // additionally getEnsureUniqueConstraintIndexLines
        ("UniqueIndexFixture", "PackageRecord.Table.g.cs", "EnsureSchema", "CreateCommand", 14),
        // FTS popCommand + readCommand, and the readCommand reader
        ("FtsFixture", "FtsEntity.Fts.g.cs", "Populate", "CreateCommand", 3),
        ("FtsFixture", "FtsEntity.Fts.g.cs", "Populate", "ExecuteReader", 1),
    ];

    public static IEnumerable<object[]> DisposalFixtureNames()
    {
        return
        [
            [nameof(FixtureSources.BasicTableFixture)],
            [nameof(FixtureSources.UniqueIndexFixture)],
            [nameof(FixtureSources.CompositePrimaryKeyFixture)],
            [nameof(FixtureSources.KeylessFixture)],
            [nameof(FixtureSources.FtsFixture)],
        ];
    }

    [Theory]
    [MemberData(nameof(DisposalFixtureNames))]
    public void EveryGeneratedDisposable_IsCreatedInsideUsing(string fixtureName)
    {
        GeneratorRunResult run = GeneratorTestHost.Run(ResolveFixture(fixtureName));

        // Assert this FIRST: a model the generator skipped on a CSLDG### diagnostic would leave the
        // fixture silently under-covered and the leak assertion vacuously green.
        run.Errors.ShouldBeEmpty();

        IReadOnlyList<DisposalSite> sites = DisposalSites(run);

        List<DisposalSite> commandSites = sites.Where(static s => s.Api == "CreateCommand").ToList();
        List<DisposalSite> readerSites = sites.Where(static s => s.Api == "ExecuteReader").ToList();

        commandSites.ShouldNotBeEmpty($"Fixture '{fixtureName}' emitted no CreateCommand() site; the guard would pass vacuously.");
        readerSites.ShouldNotBeEmpty($"Fixture '{fixtureName}' emitted no ExecuteReader() site; the guard would pass vacuously.");

        List<DisposalSite> leaked = sites.Where(static s => !s.DisposedByUsing).ToList();

        leaked.ShouldBeEmpty(
            $"Fixture '{fixtureName}' emitted {leaked.Count} disposable(s) not bound by a using declaration " +
            $"or a block-form using:\n" +
            string.Join("\n", leaked.Select(static s => $"  {s.HintName}.{s.Method} ({s.Api})")));
    }

    [Fact]
    public void EmitPathCoverage_IsPinned()
    {
        Dictionary<string, IReadOnlyList<DisposalSite>> byFixture = [];

        foreach (object[] row in DisposalFixtureNames())
        {
            string fixtureName = (string)row[0];
            GeneratorRunResult run = GeneratorTestHost.Run(ResolveFixture(fixtureName));
            run.Errors.ShouldBeEmpty();
            byFixture[fixtureName] = DisposalSites(run);
        }

        List<string> shortfalls = [];

        foreach ((string fixture, string hintSuffix, string method, string api, int minSites) in CoverageFloor)
        {
            byFixture.ShouldContainKey(fixture);

            int observed = byFixture[fixture].Count(s =>
                s.HintName.EndsWith(hintSuffix, StringComparison.Ordinal) &&
                s.Method == method &&
                s.Api == api);

            if (observed < minSites)
            {
                shortfalls.Add($"  {fixture} → {hintSuffix}.{method} ({api}): expected >= {minSites}, observed {observed}");
            }
        }

        shortfalls.ShouldBeEmpty(
            "An emit path stopped being reached, so the disposal guard now covers less than it did:\n" +
            string.Join("\n", shortfalls) + "\n\nObserved inventory:\n" + Inventory(byFixture));
    }

    private static string ResolveFixture(string fixtureName)
    {
        FieldInfo? field = typeof(FixtureSources).GetField(fixtureName, BindingFlags.Public | BindingFlags.Static);
        field.ShouldNotBeNull();

        return (string)field!.GetRawConstantValue()!;
    }

    /// <summary>
    /// Parses every emitted source and returns one record per <c>CreateCommand()</c> and
    /// <c>ExecuteReader()</c> invocation, noting whether a <c>using</c> owns it.
    /// </summary>
    private static IReadOnlyList<DisposalSite> DisposalSites(GeneratorRunResult run)
    {
        List<DisposalSite> sites = [];

        foreach (KeyValuePair<string, string> generated in run.GeneratedSources)
        {
            CompilationUnitSyntax root = CSharpSyntaxTree.ParseText(generated.Value).GetCompilationUnitRoot();

            foreach (InvocationExpressionSyntax invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                {
                    continue;
                }

                string api = memberAccess.Name.Identifier.ValueText;
                if (api != "CreateCommand" && api != "ExecuteReader")
                {
                    continue;
                }

                string method = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>()?.Identifier.ValueText ?? "<none>";

                sites.Add(new DisposalSite(generated.Key, method, api, IsDisposedByUsing(invocation)));
            }
        }

        return sites;
    }

    /// <summary>
    /// True when the invocation is the initializer of a using declaration or of a block-form using.
    /// The declaration span check is load-bearing: it stops an unrelated enclosing
    /// <c>using (IDataReader reader = ...)</c> from laundering a bare command created inside its
    /// body, which is exactly how the FTS Populate read path nests.
    /// </summary>
    private static bool IsDisposedByUsing(InvocationExpressionSyntax invocation)
    {
        LocalDeclarationStatementSyntax? localDeclaration = invocation.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>();
        if (localDeclaration is not null &&
            localDeclaration.Declaration.Span.Contains(invocation.Span) &&
            !localDeclaration.UsingKeyword.Equals(default))
        {
            return true;
        }

        UsingStatementSyntax? usingStatement = invocation.FirstAncestorOrSelf<UsingStatementSyntax>();
        if (usingStatement?.Declaration is not null &&
            usingStatement.Declaration.Span.Contains(invocation.Span))
        {
            return true;
        }

        return false;
    }

    private static string Inventory(Dictionary<string, IReadOnlyList<DisposalSite>> byFixture)
    {
        return string.Join("\n", byFixture
            .SelectMany(static kvp => kvp.Value.Select(s => (Fixture: kvp.Key, Site: s)))
            .GroupBy(static e => (e.Fixture, e.Site.HintName, e.Site.Method, e.Site.Api))
            .OrderBy(static g => g.Key.Fixture, StringComparer.Ordinal)
            .ThenBy(static g => g.Key.HintName, StringComparer.Ordinal)
            .ThenBy(static g => g.Key.Method, StringComparer.Ordinal)
            .ThenBy(static g => g.Key.Api, StringComparer.Ordinal)
            .Select(static g => $"  {g.Key.Fixture} → {g.Key.HintName}.{g.Key.Method} ({g.Key.Api}): {g.Count()}"));
    }
}
