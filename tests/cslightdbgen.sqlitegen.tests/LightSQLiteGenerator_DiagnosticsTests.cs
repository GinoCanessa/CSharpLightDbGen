using System.Reflection;
using CsLightDbGen.SQLiteGenerator;
using Microsoft.CodeAnalysis;
using Shouldly;

namespace cslightdbgen.sqlitegen.tests;

public class LightSQLiteGenerator_DiagnosticsTests
{
    [Fact]
    public void Diagnostics_AllDeclared_AreReachable()
    {
        // Guards the M5a invariant: no declared-but-unreachable diagnostics. Every remaining
        // descriptor has a real generation-time trigger, each covered by an emitting test:
        //   CSLDG001 (UnmappedValueTypeColumn)              -> LightSQLiteGenerator_EnumTests / _GenerationTests
        //   CSLDG003 (CompositeKeyAutoIncrementConflict)    -> LightSQLiteGenerator_CompositeKeyTests
        //   CSLDG005 (UnsupportedKeyOrForeignKeyCombination)-> incomplete-FK path
        //   CSLDG006 (FtsSourceTableUnresolved)             -> Fts_UnresolvedSource_ReportsCSLDG006
        // Re-introducing an orphan descriptor (as CSLDG002/CSLDG004 once were) fails this test.
        Type diagnosticsType = typeof(LightSQLiteGenerator).Assembly
            .GetType("CsLightDbGen.SQLiteGenerator.GeneratorDiagnostics")!;

        string[] declaredIds = diagnosticsType
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static f => f.FieldType == typeof(DiagnosticDescriptor))
            .Select(static f => ((DiagnosticDescriptor)f.GetValue(null)!).Id)
            .Distinct()
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();

        declaredIds.ShouldBe(["CSLDG001", "CSLDG003", "CSLDG005", "CSLDG006"]);
    }
}
