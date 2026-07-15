using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace CsLightDbGen.SQLiteGenerator;

[Generator]
public sealed class LightSQLiteGenerator : IIncrementalGenerator
{
    //private const string _joiner_0 = ",\r\n";
    //private const string _joiner_1 = ",\r\n    ";
    //private const string _joiner_2 = ",\r\n        ";
    private const string _line_2 = "\r\n        ";
    private const string _line_3 = "\r\n            ";
    private const string _line_4 = "\r\n                ";
    private const string _line_5 = "\r\n                    ";
    private const string _comma_line_2 = ",\r\n        ";
    private const string _comma_line_4 = ",\r\n                ";
    private const string _comma_line_5 = ",\r\n                    ";
    private const string _comma_line_6 = ",\r\n                        ";

    /// <summary>Namespace the generator emits its attribute types into (see <see cref="GeneratorAttributes.LdgAttributes"/>).</summary>
    internal const string AttributesNamespace = "CsLightDbGen.SQLiteGenerator";

    /// <summary>Tracking name for the regular-table equatable model step (asserted by caching tests).</summary>
    public const string TableModelTrackingName = "LdgTableModel";

    /// <summary>Tracking name for the FTS equatable model step (asserted by caching tests).</summary>
    public const string FtsModelTrackingName = "LdgFtsModel";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
#if DO_NOT_ATTACH_DEBUGGER
        if (!System.Diagnostics.Debugger.IsAttached)
        {
            System.Diagnostics.Debugger.Launch();
        }
#endif

        // create a generated file with our attributes so the target project can use them
        context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
            "LdgSQLiteGeneratorAttributes.g.cs",
            SourceText.From(GeneratorAttributes.LdgAttributes, Encoding.UTF8)));

        // Regular [LdgSQLiteTable] models. ForAttributeWithMetadataName finds targets cheaply and,
        // paired with a fully value-equatable TableModel, lets the pipeline cache each model
        // independently: editing one model (or unrelated code) no longer re-runs generation for
        // every model in the compilation.
        IncrementalValuesProvider<TableModel> tableModels = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                $"{AttributesNamespace}.{GeneratorAttributes._ldgSQLiteTable}",
                predicate: static (node, _) => IsSyntaxTargetClassDec(node) || IsSyntaxTargetRecordDec(node),
                transform: static (ctx, ct) => TransformTable(ctx, ct))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!.Value)
            .WithTrackingName(TableModelTrackingName);

        context.RegisterSourceOutput(tableModels, static (spc, model) => emit(model, spc));

        // FTS5 [LdgSQLiteFtsTable] models follow the same equatable pipeline shape.
        IncrementalValuesProvider<FtsModel> ftsModels = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                $"{AttributesNamespace}.{GeneratorAttributes._ldgSQLiteFtsTable}",
                predicate: static (node, _) => IsSyntaxTargetClassDec(node) || IsSyntaxTargetRecordDec(node),
                transform: static (ctx, ct) => TransformFts(ctx, ct))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!.Value)
            .WithTrackingName(FtsModelTrackingName);

        // Table names an FTS source may resolve against: every compile-time [LdgSQLiteTable] name in
        // THIS compilation (including dynamic-named tables' base names) unioned with every generated
        // table declared in a referenced assembly. Kept value-equatable (sorted, deduped) so that an
        // unrelated edit re-firing CompilationProvider does not invalidate cached FTS emission.
        IncrementalValueProvider<EquatableArray<string>> localTableNames = tableModels
            .Select(static (m, _) => m.TableName)
            .Collect()
            .Select(static (names, _) => SortDistinct(names));

        IncrementalValueProvider<EquatableArray<string>> referencedTableNames = context.CompilationProvider
            .Select(static (compilation, ct) => CollectReferencedTableNames(compilation, ct));

        IncrementalValueProvider<EquatableArray<string>> knownTableNames = localTableNames
            .Combine(referencedTableNames)
            .Select(static (pair, _) => MergeTableNames(pair.Left, pair.Right));

        // An FTS model whose source table matches no known [LdgSQLiteTable] (here or in a referenced
        // assembly) reports CSLDG006 and is skipped; membership is case-insensitive because SQLite
        // identifiers are.
        context.RegisterSourceOutput(
            ftsModels.Combine(knownTableNames),
            static (spc, pair) => EmitFtsResolved(pair.Left, pair.Right, spc));
    }

    /// <summary>
    /// Emits an FTS model only when its source table resolves to a known <c>[LdgSQLiteTable]</c>
    /// table (in this compilation or a referenced assembly); otherwise reports CSLDG006 and skips it.
    /// </summary>
    private static void EmitFtsResolved(FtsModel model, EquatableArray<string> knownTableNames, SourceProductionContext context)
    {
        ImmutableHashSet<string> known = ImmutableHashSet.CreateRange(StringComparer.OrdinalIgnoreCase, knownTableNames);

        if (!known.Contains(model.SourceTableName))
        {
            GeneratorDiagnostics.Report(
                context,
                GeneratorDiagnostics.FtsSourceTableUnresolved,
                model.Location?.ToLocation(),
                model.ClassName,
                model.SourceTableName);
            return;
        }

        emitFts(model, context);
    }

    /// <summary>Sorts and de-duplicates (ordinal) a collected set of table names into an equatable array.</summary>
    private static EquatableArray<string> SortDistinct(ImmutableArray<string> names)
    {
        SortedSet<string> distinct = new(StringComparer.Ordinal);
        foreach (string name in names)
        {
            if (!string.IsNullOrEmpty(name))
            {
                distinct.Add(name);
            }
        }

        return distinct.ToEquatableArray();
    }

    /// <summary>
    /// Unions local and referenced-assembly table names case-insensitively (SQLite identifiers are
    /// case-insensitive) into a sorted, de-duplicated equatable array.
    /// </summary>
    private static EquatableArray<string> MergeTableNames(EquatableArray<string> local, EquatableArray<string> referenced)
    {
        SortedSet<string> union = new(StringComparer.OrdinalIgnoreCase);
        foreach (string name in local)
        {
            union.Add(name);
        }

        foreach (string name in referenced)
        {
            union.Add(name);
        }

        return union.ToEquatableArray();
    }

    /// <summary>
    /// Collects the table names of every <c>[LdgSQLiteTable]</c>-annotated type declared in a
    /// referenced assembly. An assembly that never ran the generator cannot declare the attribute
    /// type, so it is pruned before the namespace walk (N2). The table name is constructor argument 0,
    /// falling back to the type name — mirroring <see cref="TransformTable"/>.
    /// </summary>
    private static EquatableArray<string> CollectReferencedTableNames(Compilation compilation, CancellationToken cancellationToken)
    {
        SortedSet<string> names = new(StringComparer.Ordinal);

        foreach (IAssemblySymbol assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!assembly.TypeNames.Contains(GeneratorAttributes._ldgSQLiteTable))
            {
                continue;
            }

            foreach (INamedTypeSymbol type in EnumerateNamedTypes(assembly.GlobalNamespace, cancellationToken))
            {
                foreach (AttributeData attribute in type.GetAttributes())
                {
                    if (attribute.AttributeClass?.Name != GeneratorAttributes._ldgSQLiteTable)
                    {
                        continue;
                    }

                    string? ctorName = attribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    names.Add(string.IsNullOrEmpty(ctorName) ? type.Name : ctorName!);
                    break;
                }
            }
        }

        return names.ToEquatableArray();
    }

    /// <summary>Depth-first enumeration of all named types (including nested) under a namespace.</summary>
    private static IEnumerable<INamedTypeSymbol> EnumerateNamedTypes(INamespaceSymbol root, CancellationToken cancellationToken)
    {
        Stack<INamespaceOrTypeSymbol> pending = new();
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            INamespaceOrTypeSymbol current = pending.Pop();
            foreach (ISymbol member in current.GetMembers())
            {
                if (member is INamespaceSymbol childNamespace)
                {
                    pending.Push(childNamespace);
                }
                else if (member is INamedTypeSymbol type)
                {
                    yield return type;
                    pending.Push(type);
                }
            }
        }
    }


    /// <summary>
    /// Determines if the given <see cref="SyntaxNode"/> is a target for generation.
    /// </summary>
    /// <param name="syntaxNode">The syntax node to evaluate.</param>
    /// <returns><c>true</c> if the syntax node is a class declaration with specific attributes; otherwise, <c>false</c>.</returns>
    public static bool IsSyntaxTargetClassDec(SyntaxNode syntaxNode)
    {
        return
            (syntaxNode is ClassDeclarationSyntax cDeclarationSyntax) &&
            (cDeclarationSyntax.AttributeLists.Count > 0) &&
            cDeclarationSyntax.AttributeLists.Any(al => al.Attributes.Any(a => GeneratorAttributes._ldClassAttributes.Contains(a.Name.ToString())));
    }


    /// <summary>
    /// Determines if the given <see cref="SyntaxNode"/> is a target for generation.
    /// </summary>
    /// <param name="syntaxNode">The syntax node to evaluate.</param>
    /// <returns><c>true</c> if the syntax node is a class declaration with specific attributes; otherwise, <c>false</c>.</returns>
    public static bool IsSyntaxTargetRecordDec(SyntaxNode syntaxNode)
    {
        return
            (syntaxNode is RecordDeclarationSyntax rDeclarationSyntax) &&
            (rDeclarationSyntax.AttributeLists.Count > 0) &&
            rDeclarationSyntax.AttributeLists.Any(al => al.Attributes.Any(a => GeneratorAttributes._ldClassAttributes.Contains(a.Name.ToString())));
    }


    private record struct TableColInfoRec(
        string name,
        string propType,
        string shortRead,
        string readerDirective,
        bool isPrimaryKey,
        bool isIdentity,
        bool isNullable,
        bool isEnum,
        bool useJson,
        bool isArray,
        bool isUnique,
        bool isMultiSelect = false,
        string? foreignTable = null,
        string? foreignColumn = null,
        string? foreignModelType = null);

    private record struct ColumnClassification(
        bool IsEnum,
        string? EnumTypeName,
        bool IsNonScalar,
        string? JsonTypeName,
        bool IsArray);

    /// <summary>
    /// Collects the properties that become table columns: the properties of any
    /// <c>[LdgSQLiteBaseClass]</c>-annotated base classes (nearest base first), followed by the
    /// type's own properties. <see cref="INamedTypeSymbol.GetMembers"/> aggregates every partial
    /// declaration and also resolves bases declared in referenced assemblies (which have no syntax).
    /// </summary>
    private static List<IPropertySymbol> CollectColumnProperties(INamedTypeSymbol type)
    {
        List<IPropertySymbol> members = [];

        INamedTypeSymbol? baseType = type.BaseType;
        while ((baseType != null) &&
               baseType.GetAttributes().Any(a => a.AttributeClass?.Name == GeneratorAttributes._ldgSQLiteBaseClass))
        {
            members.AddRange(baseType.GetMembers().OfType<IPropertySymbol>().Where(IsColumnCandidate));
            baseType = baseType.BaseType;
        }

        members.AddRange(type.GetMembers().OfType<IPropertySymbol>().Where(IsColumnCandidate));
        return members;
    }

    /// <summary>
    /// Determines whether a property should be materialized as a table column. Excludes
    /// compiler-synthesized members (e.g. a record's <c>EqualityContract</c>) and indexers.
    /// </summary>
    private static bool IsColumnCandidate(IPropertySymbol property) =>
        !property.IsImplicitlyDeclared && !property.IsIndexer;

    /// <summary>
    /// Returns whether <paramref name="symbol"/> carries the generator attribute whose simple class
    /// name is <paramref name="attributeName"/>.
    /// </summary>
    private static bool HasLdgAttribute(ISymbol symbol, string attributeName) =>
        symbol.GetAttributes().Any(a => a.AttributeClass?.Name == attributeName);

    /// <summary>
    /// Recovers the declaring <see cref="PropertyDeclarationSyntax"/> for a property, or
    /// <see langword="null"/> when the property has no syntax (e.g. it is declared in a referenced
    /// assembly).
    /// </summary>
    private static PropertyDeclarationSyntax? TryGetPropertySyntax(IPropertySymbol property)
    {
        foreach (SyntaxReference reference in property.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is PropertyDeclarationSyntax pds)
            {
                return pds;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves the source-level type name for a property. Uses the declaration syntax when
    /// available (preserving the exact spelling, e.g. <c>int?</c>) and otherwise falls back to a
    /// minimally-qualified symbol display.
    /// </summary>
    private static string PropertyTypeName(IPropertySymbol property, PropertyDeclarationSyntax? syntax) =>
        syntax != null
            ? syntax.Type.ToString()
            : property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

    /// <summary>
    /// Renders the "<c>namespace.Name</c>" (or "<c>namespace.Outer.Name</c>" for a nested type) form
    /// used when emitting enum and JSON element type names.
    /// </summary>
    private static string QualifiedTypeName(ITypeSymbol type) =>
        type.ContainingType == null
            ? $"{type.ContainingNamespace}.{type.Name}"
            : $"{type.ContainingType.ContainingNamespace}.{type.ContainingType.Name}.{type.Name}";

    /// <summary>
    /// Classifies a property's type into the buckets the column emitter switches on: enum, non-scalar
    /// collection (arrays, <c>List&lt;T&gt;</c>, <c>IEnumerable&lt;T&gt;</c>), or single reference
    /// object. Arrays are detected via <see cref="IArrayTypeSymbol"/> and routed to the JSON-array bucket.
    /// </summary>
    private static ColumnClassification ClassifyColumn(ITypeSymbol? typeSymbol)
    {
        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            return new ColumnClassification(false, null, true, QualifiedTypeName(arrayType.ElementType), true);
        }

        INamedTypeSymbol? namedTypeSymbol = typeSymbol as INamedTypeSymbol;

        bool isEnum = false;
        string? enumTypeName = null;
        bool isNonScalar = false;
        string? jsonTypeName = null;

        // Only a bare enum (MyEnum) or a nullable enum (MyEnum?, i.e. Nullable<Enum>) is an enum
        // scalar. A generic whose first type-arg merely happens to be an enum (List<MyEnum>,
        // IEnumerable<MyEnum>) is NOT a scalar and must route to the JSON collection path; and an
        // arbitrary non-System generic struct (ImmutableArray<T>, value tuples) is neither — it
        // falls through to the JSON/CSLDG001 path rather than being misclassified as an enum.
        bool isNullableEnum =
            (namedTypeSymbol != null) &&
            (namedTypeSymbol.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T) &&
            (namedTypeSymbol.TypeArguments.Length != 0) &&
            (namedTypeSymbol.TypeArguments[0].TypeKind == TypeKind.Enum);

        if ((namedTypeSymbol != null) &&
            ((namedTypeSymbol.TypeKind == TypeKind.Enum) || isNullableEnum))
        {
            isEnum = true;

            enumTypeName = (namedTypeSymbol.TypeKind == TypeKind.Enum)
                ? QualifiedTypeName(namedTypeSymbol)
                : QualifiedTypeName(namedTypeSymbol.TypeArguments[0]);
        }
        else if (namedTypeSymbol != null)
        {
            if (namedTypeSymbol.TypeArguments.Length != 0)
            {
                if (namedTypeSymbol.Name.Contains("List") || namedTypeSymbol.Name.Contains("Enumerable"))
                {
                    isNonScalar = true;
                }

                jsonTypeName = QualifiedTypeName(namedTypeSymbol.TypeArguments[0]);
            }
            else
            {
                jsonTypeName = QualifiedTypeName(namedTypeSymbol);
            }
        }

        return new ColumnClassification(isEnum, enumTypeName, isNonScalar, jsonTypeName, false);
    }

    /// <summary>
    /// Pipeline transform for a regular <c>[LdgSQLiteTable]</c> target: projects the target symbol
    /// and its column properties into a fully value-equatable <see cref="TableModel"/>. Keeping the
    /// <see cref="Compilation"/> and every <see cref="ISymbol"/> out of the returned value lets the
    /// incremental generator cache an unchanged model and skip re-emitting its source.
    /// </summary>
    private static TableModel? TransformTable(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        LdGenCategory genCategory = context.TargetNode is RecordDeclarationSyntax ? LdGenCategory.Record : LdGenCategory.Class;

        string className = typeSymbol.Name;
        string? classNamespace = typeSymbol.ContainingNamespace?.ToDisplayString();

        ILookup<string?, AttributeData> symbolAttributeLookup = typeSymbol.GetAttributes().ToLookup(a => a.AttributeClass?.Name);

        string tableName = symbolAttributeLookup.Contains(GeneratorAttributes._ldgSQLiteTable)
            ? symbolAttributeLookup[GeneratorAttributes._ldgSQLiteTable].First().ConstructorArguments.FirstOrDefault().Value?.ToString() ?? className
            : className;

        List<IPropertySymbol> members = CollectColumnProperties(typeSymbol);

        List<ColumnInput> columns = [];
        foreach (IPropertySymbol propSymbol in members)
        {
            if (HasLdgAttribute(propSymbol, GeneratorAttributes._ldgSQLiteIgnore))
            {
                continue;
            }

            columns.Add(BuildColumnInput(propSymbol));
        }

        // class-level composite foreign keys, pre-rendered to DDL fragments
        List<string> compositeFkLines = [];
        if (symbolAttributeLookup.Contains(GeneratorAttributes._ldgSQLiteForeignKeyComposite))
        {
            foreach (AttributeData ad in symbolAttributeLookup[GeneratorAttributes._ldgSQLiteForeignKeyComposite])
            {
                ImmutableArray<TypedConstant> ctorArgs = ad.ConstructorArguments;
                if (ctorArgs.Length < 3)
                {
                    continue;
                }

                string[] fkColumns = ctorArgs[0].Values
                    .Select(tc => tc.Value?.ToString() ?? string.Empty)
                    .Where(v => !string.IsNullOrEmpty(v))
                    .ToArray();
                string fkRefTable = ctorArgs[1].Value?.ToString() ?? string.Empty;
                string[] fkRefColumns = ctorArgs[2].Values
                    .Select(tc => tc.Value?.ToString() ?? string.Empty)
                    .Where(v => !string.IsNullOrEmpty(v))
                    .ToArray();

                if ((fkColumns.Length == 0) || string.IsNullOrEmpty(fkRefTable) || (fkRefColumns.Length == 0))
                {
                    continue;
                }

                string compositeActions = string.Empty;
                if (ctorArgs.Length > 3)
                {
                    string onDelete = fkActionFromValue(ctorArgs[3].Value);
                    if (onDelete != "NO ACTION")
                    {
                        compositeActions += $" ON DELETE {onDelete}";
                    }
                }

                if (ctorArgs.Length > 4)
                {
                    string onUpdate = fkActionFromValue(ctorArgs[4].Value);
                    if (onUpdate != "NO ACTION")
                    {
                        compositeActions += $" ON UPDATE {onUpdate}";
                    }
                }

                compositeFkLines.Add($"FOREIGN KEY ({string.Join(", ", fkColumns.Select(quoteIdent))}) REFERENCES {quoteIdent(fkRefTable)} ({string.Join(", ", fkRefColumns.Select(quoteIdent))}){compositeActions}");
            }
        }

        // class-level [LdgSQLiteUnique(cols...)] column sets (used both as a table constraint and to
        // re-assert the composite UNIQUE as an index during additive migration)
        List<EquatableColumnSet> classUniqueColumnSets = [];
        if (symbolAttributeLookup.Contains(GeneratorAttributes._ldgSQLiteUnique))
        {
            foreach (AttributeData ad in symbolAttributeLookup[GeneratorAttributes._ldgSQLiteUnique])
            {
                string[] uniqueColumns = ad.ConstructorArguments
                    .FirstOrDefault()
                    .Values
                    .Select(tc => tc.Value?.ToString() ?? string.Empty)
                    .Where(v => !string.IsNullOrEmpty(v))
                    .ToArray();

                if (uniqueColumns.Length == 0)
                {
                    continue;
                }

                classUniqueColumnSets.Add(new EquatableColumnSet(uniqueColumns.ToEquatableArray()));
            }
        }

        // class-level [LdgSQLiteIndex] definitions (rendered to CREATE INDEX statements at emit time)
        List<IndexInfo> indexes = [];
        if (symbolAttributeLookup.Contains(GeneratorAttributes._ldgSQLiteIndex))
        {
            foreach (AttributeData ad in symbolAttributeLookup[GeneratorAttributes._ldgSQLiteIndex])
            {
                string[] indexColumns = ad.ConstructorArguments
                    .FirstOrDefault()
                    .Values
                    .Select(tc => tc.Value?.ToString() ?? string.Empty)
                    .Where(v => !string.IsNullOrEmpty(v))
                    .ToArray();

                if (indexColumns.Length == 0)
                {
                    continue;
                }

                bool unique = false;
                string? whereClause = null;
                foreach (KeyValuePair<string, TypedConstant> na in ad.NamedArguments)
                {
                    if (na.Key == "Unique")
                    {
                        unique = na.Value.Value is bool b && b;
                    }
                    else if (na.Key == "Where")
                    {
                        whereClause = na.Value.Value?.ToString();
                    }
                }

                indexes.Add(new IndexInfo(indexColumns.ToEquatableArray(), unique, whereClause));
            }
        }

        return new TableModel(
            className,
            classNamespace,
            tableName,
            genCategory,
            LocationInfo.From(typeSymbol),
            columns.ToEquatableArray(),
            compositeFkLines.ToEquatableArray(),
            classUniqueColumnSets.ToEquatableArray(),
            indexes.ToEquatableArray());
    }

    /// <summary>
    /// Pipeline transform for an FTS5 <c>[LdgSQLiteFtsTable]</c> target: the FTS counterpart of
    /// <see cref="TransformTable"/>, producing a reduced value-equatable <see cref="FtsModel"/>
    /// (no keys, foreign keys, indexes, or column defaults).
    /// </summary>
    private static FtsModel? TransformFts(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        LdGenCategory genCategory = context.TargetNode is RecordDeclarationSyntax ? LdGenCategory.Record : LdGenCategory.Class;

        string className = typeSymbol.Name;
        string? classNamespace = typeSymbol.ContainingNamespace?.ToDisplayString();

        ILookup<string?, AttributeData> symbolAttributeLookup = typeSymbol.GetAttributes().ToLookup(a => a.AttributeClass?.Name);

        // A type tagged as BOTH a table and an FTS source is emitted table-only: the table pipeline
        // owns it, and emitting an FTS partial as well would redeclare the same members on the same
        // partial type (uncompilable). This restores the pre-refactor "table wins" precedence that
        // the shared `seenTargets` dedup provided before the table and FTS pipelines became
        // independent `ForAttributeWithMetadataName` providers.
        if (symbolAttributeLookup.Contains(GeneratorAttributes._ldgSQLiteTable))
        {
            return null;
        }

        List<TypedConstant> ftsTableArgs = symbolAttributeLookup.Contains(GeneratorAttributes._ldgSQLiteFtsTable)
            ? symbolAttributeLookup[GeneratorAttributes._ldgSQLiteFtsTable].First().ConstructorArguments.ToList()
            : [];

        string sourceTableName = ftsTableArgs.Count > 0
            ? ftsTableArgs[0].Value?.ToString() ?? className
            : className;

        string tableName = ftsTableArgs.Count > 1
            ? ftsTableArgs[1].Value?.ToString() ?? (className + "_fts")
            : (className + "_fts");

        string? tokenizer = ftsTableArgs.Count > 2
            ? ftsTableArgs[2].Value?.ToString()
            : null;

        List<IPropertySymbol> members = CollectColumnProperties(typeSymbol);

        List<ColumnInput> columns = [];
        foreach (IPropertySymbol propSymbol in members)
        {
            if (HasLdgAttribute(propSymbol, GeneratorAttributes._ldgSQLiteIgnore))
            {
                continue;
            }

            columns.Add(BuildColumnInput(propSymbol));
        }

        return new FtsModel(
            className,
            classNamespace,
            tableName,
            sourceTableName,
            tokenizer,
            genCategory,
            LocationInfo.From(typeSymbol),
            columns.ToEquatableArray());
    }

    /// <summary>
    /// Extracts every value the emitter needs from a single column property (and its declaration
    /// syntax) into a value-equatable <see cref="ColumnInput"/>. This is where the symbol/syntax
    /// reads that used to live inline in the per-column emit loop now happen, once, in the pipeline
    /// transform.
    /// </summary>
    private static ColumnInput BuildColumnInput(IPropertySymbol propSymbol)
    {
        PropertyDeclarationSyntax? pds = TryGetPropertySyntax(propSymbol);
        string propName = propSymbol.Name;

        string propTypeName = PropertyTypeName(propSymbol, pds);
        INamedTypeSymbol? namedTypeSymbol = propSymbol.Type as INamedTypeSymbol;

        bool nullable = propTypeName.EndsWith("?") || (namedTypeSymbol?.NullableAnnotation == NullableAnnotation.Annotated);
        if (nullable)
        {
            propTypeName = propTypeName.Substring(0, propTypeName.Length - 1);
        }

        bool isPrimaryKey = HasLdgAttribute(propSymbol, GeneratorAttributes._ldgSQLiteKey);

        // Decode [LdgSQLiteKey(autoIncrement)] — constructor arg 0 or the named AutoIncrement
        // property (default true). AttributeData yields the effective value even for
        // referenced-assembly symbols; the syntax pass records whether AutoIncrement was written
        // explicitly, which is needed to diagnose (but not over-report) composite + AutoIncrement.
        bool keyAutoIncrement = true;
        bool keyAutoIncrementExplicit = false;
        if (isPrimaryKey)
        {
            AttributeData? keyAttrData = propSymbol.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == GeneratorAttributes._ldgSQLiteKey);
            if (keyAttrData != null)
            {
                if ((keyAttrData.ConstructorArguments.Length > 0) && (keyAttrData.ConstructorArguments[0].Value is bool ctorAutoInc))
                {
                    keyAutoIncrement = ctorAutoInc;
                }

                foreach (KeyValuePair<string, TypedConstant> namedArg in keyAttrData.NamedArguments)
                {
                    if ((namedArg.Key == "AutoIncrement") && (namedArg.Value.Value is bool namedAutoInc))
                    {
                        keyAutoIncrement = namedAutoInc;
                    }
                }
            }

            foreach (AttributeListSyntax keyAls in pds?.AttributeLists ?? default)
            {
                foreach (AttributeSyntax keyAttr in keyAls.Attributes.Where(a => a.Name.ToString() == GeneratorAttributes._ldgSQLiteKey))
                {
                    if ((keyAttr.ArgumentList?.Arguments.Count ?? 0) > 0)
                    {
                        keyAutoIncrementExplicit = true;
                    }
                }
            }
        }

        bool isUnique = HasLdgAttribute(propSymbol, GeneratorAttributes._ldgSQLiteUnique);
        bool isUnindexed = HasLdgAttribute(propSymbol, GeneratorAttributes._ldgSQLiteFtsUnindexed);
        bool hasMultiSelectAttr = HasLdgAttribute(propSymbol, GeneratorAttributes._ldgSQLiteMultiSelect);

        ColumnClassification classification = ClassifyColumn(propSymbol.Type);

        // check for a column default (literal, boolean, numeric, or raw SQL expression)
        string defaultClause = string.Empty;
        bool defaultIsRaw = false;
        foreach (AttributeListSyntax defAls in pds?.AttributeLists ?? default)
        {
            foreach (AttributeSyntax defAttr in defAls.Attributes.Where(a => a.Name.ToString() == GeneratorAttributes._ldgSQLiteDefault))
            {
                ExpressionSyntax? defaultValueExpr = null;
                bool defaultRaw = false;
                int positional = 0;

                foreach (AttributeArgumentSyntax arg in defAttr.ArgumentList?.Arguments ?? [])
                {
                    string? argName = arg.NameColon?.Name.ToString() ?? arg.NameEquals?.Name.ToString();

                    if ((argName == "raw") || (argName == "Raw"))
                    {
                        defaultRaw = arg.Expression.ToString() == "true";
                    }
                    else if ((argName == "value") || (argName == "Value"))
                    {
                        defaultValueExpr = arg.Expression;
                    }
                    else if (argName == null)
                    {
                        if (positional == 0)
                        {
                            defaultValueExpr = arg.Expression;
                        }
                        else if (positional == 1)
                        {
                            defaultRaw = arg.Expression.ToString() == "true";
                        }

                        positional++;
                    }
                }

                defaultClause = formatDefault(defaultValueExpr, defaultRaw);
                defaultIsRaw = defaultRaw;
            }
        }

        // Foreign-key metadata is read from the property's resolved attribute data (constant
        // VALUES), not attribute syntax text: a reference table/column given as nameof(...), a
        // const string, or a verbatim @"..." string must resolve to the same identifier the
        // composite-FK path produces, not to its source spelling.
        string? foreignTable = null;
        string? foreignColumn = null;
        string? foreignModelType = null;
        string fkActions = string.Empty;
        foreach (AttributeData fkAttr in propSymbol.GetAttributes()
            .Where(a => a.AttributeClass?.Name == GeneratorAttributes._ldgSQLiteForeignKey))
        {
            string? fkOnDelete = null;
            string? fkOnUpdate = null;

            // Positional constructor arguments, in LdgSQLiteForeignKey ctor order:
            // (referenceTable, referenceColumn, modelTypeName, onDelete, onUpdate).
            ImmutableArray<TypedConstant> ctorArgs = fkAttr.ConstructorArguments;
            if ((ctorArgs.Length > 0) && (ctorArgs[0].Value is string ctorTable))
            {
                foreignTable = ctorTable;
            }
            if ((ctorArgs.Length > 1) && (ctorArgs[1].Value is string ctorColumn))
            {
                foreignColumn = ctorColumn;
            }
            if ((ctorArgs.Length > 2) && (ctorArgs[2].Value is string ctorModel))
            {
                foreignModelType = ctorModel;
            }
            if (ctorArgs.Length > 3)
            {
                fkOnDelete = fkActionFromValue(ctorArgs[3].Value);
            }
            if (ctorArgs.Length > 4)
            {
                fkOnUpdate = fkActionFromValue(ctorArgs[4].Value);
            }

            // Named-property initializers (ReferenceTable = ..., OnDelete = ..., etc.).
            foreach (KeyValuePair<string, TypedConstant> named in fkAttr.NamedArguments)
            {
                switch (named.Key)
                {
                    case "ReferenceTable":
                        foreignTable = named.Value.Value as string ?? foreignTable;
                        break;
                    case "ReferenceColumn":
                        foreignColumn = named.Value.Value as string ?? foreignColumn;
                        break;
                    case "ModelTypeName":
                        foreignModelType = named.Value.Value as string ?? foreignModelType;
                        break;
                    case "OnDelete":
                        fkOnDelete = fkActionFromValue(named.Value.Value);
                        break;
                    case "OnUpdate":
                        fkOnUpdate = fkActionFromValue(named.Value.Value);
                        break;
                }
            }

            if ((fkOnDelete != null) && (fkOnDelete != "NO ACTION"))
            {
                fkActions += $" ON DELETE {fkOnDelete}";
            }

            if ((fkOnUpdate != null) && (fkOnUpdate != "NO ACTION"))
            {
                fkActions += $" ON UPDATE {fkOnUpdate}";
            }
        }

        return new ColumnInput(
            propName,
            propTypeName,
            nullable,
            isPrimaryKey,
            keyAutoIncrement,
            keyAutoIncrementExplicit,
            isUnique,
            isUnindexed,
            hasMultiSelectAttr,
            propSymbol.Type.IsValueType,
            classification.IsEnum,
            classification.EnumTypeName,
            classification.IsNonScalar,
            classification.IsArray,
            classification.JsonTypeName,
            defaultClause,
            defaultIsRaw,
            foreignTable,
            foreignColumn,
            foreignModelType,
            fkActions,
            LocationInfo.From(propSymbol));
    }

    private static void emit(TableModel model, SourceProductionContext context)
    {
        string className = model.ClassName;
        string? classNamespace = model.ClassNamespace;
        LdGenCategory genCategory = model.GenCategory;
        string tableName = model.TableName;

        int? pkColIndex = null;
        string? pkColName = null;
        string? pkPropType = null;
        bool pkIsIdentity = false;
        bool anyColIsJson = false;

        // pre-pass: identify composite primary key columns. Composite-ness must be known
        // before the per-property column loop builds createColLines / TableColInfoRec.isIdentity,
        // so it cannot be decided retroactively in a single forward pass.
        List<(string name, string propType)> pkCols = [];
        foreach (ColumnInput preCol in model.Columns)
        {
            if (preCol.IsKey)
            {
                pkCols.Add((preCol.Name, preCol.TypeName));
            }
        }

        bool compositePk = pkCols.Count > 1;

        List<string> createColLines = [];
        List<string> createFKLines = [];
        List<(string name, string addColumnDdl, string? migrationBlockReason)> alterAddColumns = [];
        HashSet<string> rawDefaultColumnNames = new(System.StringComparer.Ordinal);
        List<TableColInfoRec> tableColInfo = [];

        foreach (ColumnInput col in model.Columns)
        {
            string propName = col.Name;
            string propTypeName = col.TypeName;
            bool nullable = col.Nullable;
            bool isPrimaryKey = col.IsKey;
            bool keyAutoIncrement = col.KeyAutoIncrement;
            bool keyAutoIncrementExplicit = col.KeyAutoIncrementExplicit;

            if (isPrimaryKey)
            {
                pkColName = propName;
                pkPropType = propTypeName;
                pkIsIdentity = !compositePk && (propTypeName == "int" || propTypeName == "long") && keyAutoIncrement;

                // SQLite AUTOINCREMENT applies only to a single INTEGER primary key. Diagnose an
                // explicit AutoIncrement request on a composite key; a bare [LdgSQLiteKey] on a
                // composite member is the normal, supported spelling and must not be flagged.
                if (compositePk && keyAutoIncrement && keyAutoIncrementExplicit)
                {
                    GeneratorDiagnostics.Report(
                        context,
                        GeneratorDiagnostics.CompositeKeyAutoIncrementConflict,
                        col.Location?.ToLocation(),
                        className,
                        propName);
                }
            }

            if (isPrimaryKey)
            {
                pkColIndex = tableColInfo.Count;
            }

            // a column is an auto-increment identity only when it is the sole primary key
            // (composite keys are inserted explicitly and never auto-assigned) and AutoIncrement
            // has not been disabled via [LdgSQLiteKey(false)].
            bool colIsIdentity = isPrimaryKey && !compositePk && (propTypeName == "int" || propTypeName == "long") && keyAutoIncrement;

            bool isUnique = col.IsUnique;

            bool hasMultiSelectAttr = col.HasMultiSelectAttr;

            bool memberIsEnum = col.IsEnum;
            string? enumTypeName = col.EnumTypeName;
            bool memberIsNonScalar = col.IsNonScalar;
            string? jsonTypeName = col.JsonTypeName;

            bool useJson = !memberIsEnum && !_sqliteTypeMap.ContainsKey(propTypeName);

            // A value type with no scalar mapping cannot be persisted: the JSON helpers are
            // reference-type-only (where T : class), so routing it there would emit code that does
            // not compile. Report CSLDG001 and skip the column instead of emitting broken output.
            if (useJson && col.IsValueType)
            {
                GeneratorDiagnostics.Report(context, GeneratorDiagnostics.UnmappedValueTypeColumn, col.Location?.ToLocation(), propName, className, propTypeName);
                continue;
            }

            // isMultiSelect enables the "{Name}Values" IEnumerable<T> IN-clause parameter on
            // filter/delete methods. Triggers: explicit [LdgSQLiteMultiSelect] attribute, a
            // primary key ([LdgSQLiteKey]), or the legacy name-ends-with-"Key" heuristic.
            // Non-scalar / JSON / array columns are excluded.
            bool isMultiSelect =
                !memberIsNonScalar &&
                (hasMultiSelectAttr || isPrimaryKey || propName.EndsWith("Key"));

            // check for a column default (literal, boolean, numeric, or raw SQL expression)
            string defaultClause = col.DefaultClause;
            bool defaultIsRaw = col.DefaultIsRaw;

            // Columns whose default is a raw SQL expression (e.g. CURRENT_TIMESTAMP) are computed
            // by the database. InsertReturning omits them from the INSERT so the engine fills them,
            // then hydrates the computed value back via RETURNING.
            if (defaultIsRaw && (defaultClause.Length > 0))
            {
                rawDefaultColumnNames.Add(propName);
            }

            // add our column line
            createColLines.Add(
                $"{quoteIdent(propName)} {getSqlType(propTypeName, memberIsEnum, useJson, memberIsNonScalar)}" +
                $"{((isPrimaryKey && !compositePk) ? " UNIQUE PRIMARY KEY NOT NULL" : string.Empty)}" +
                $"{(isUnique ? " UNIQUE" : string.Empty)}" +
                $"{((nullable || (isPrimaryKey && !compositePk)) ? string.Empty : " NOT NULL")}" +
                $"{defaultClause}");

            // Additive-migration (EnsureSchema) column fragment. SQLite's ALTER TABLE ADD COLUMN
            // forbids PRIMARY KEY / UNIQUE constraints and only permits constant defaults, and a
            // NOT NULL added column must carry a constant default. Primary-key columns are never
            // added this way (they exist with the original table); for other columns we emit type +
            // constant default, keeping NOT NULL only when a constant default backs it.
            if (!isPrimaryKey)
            {
                bool hasConstDefault = (defaultClause.Length > 0) && !defaultIsRaw;
                string addColDefault = hasConstDefault ? defaultClause : string.Empty;
                string addColNotNull = (!nullable && hasConstDefault) ? " NOT NULL" : string.Empty;

                // A NOT NULL column with no constant default (added as nullable) and a raw-default
                // column (added without its database-computed default) both leave pre-existing rows
                // NULL in a slot the generated readers hydrate as non-nullable. Record why such a
                // column cannot be additively migrated; EnsureSchema fails fast at runtime rather
                // than reporting success with a schema its own readers cannot hydrate.
                string? migrationBlockReason = null;
                if (!nullable && !hasConstDefault)
                {
                    migrationBlockReason = (defaultIsRaw && (defaultClause.Length > 0))
                        ? "it carries a database-computed (raw) default that ALTER TABLE ADD COLUMN cannot apply, which would leave existing rows NULL in a non-nullable column"
                        : "it is required (NOT NULL) but has no constant default to backfill existing rows";
                }

                alterAddColumns.Add((
                    propName,
                    $"{quoteIdent(propName)} {getSqlType(propTypeName, memberIsEnum, useJson, memberIsNonScalar)}{addColDefault}{addColNotNull}",
                    migrationBlockReason));
            }

            // check for foreign key property information
            string? foreignTable = col.ForeignTable;
            string? foreignColumn = col.ForeignColumn;
            string? foreignModelType = col.ForeignModelType;
            string fkActions = col.FkActions;

            if ((foreignTable != null) && (foreignColumn != null))
            {
                createFKLines.Add($"FOREIGN KEY ({quoteIdent(propName)}) REFERENCES {quoteIdent(foreignTable)}({quoteIdent(foreignColumn)}){fkActions}");
            }
            else if ((foreignTable != null) || (foreignColumn != null))
            {
                // An incomplete foreign key (only one of table / column supplied) would otherwise be
                // silently dropped; surface it so the missing constraint is not a surprise.
                GeneratorDiagnostics.Report(
                    context,
                    GeneratorDiagnostics.UnsupportedKeyOrForeignKeyCombination,
                    col.Location?.ToLocation(),
                    propName,
                    className,
                    "a foreign key must specify both a reference table and a reference column");
            }

            // create the select retrieval pair
            if (nullable && _sqliteNullableReadDirectives.TryGetValue(propTypeName, out string? readFormat))
            {
                tableColInfo.Add(new (
                    propName,
                    propTypeName,
                    string.Format(readFormat.Remove(0, 6), propName, "reader", tableColInfo.Count),
                    string.Format(readFormat, propName, "reader", tableColInfo.Count),
                    isPrimaryKey,
                    colIsIdentity,
                    nullable,
                    memberIsEnum,
                    useJson,
                    memberIsNonScalar,
                    isUnique,
                    isMultiSelect,
                    foreignTable,
                    foreignColumn,
                    foreignModelType));
            }
            else if (!nullable && _sqliteReadDirectives.TryGetValue(propTypeName, out readFormat))
            {
                tableColInfo.Add(new (
                    propName,
                    propTypeName,
                    string.Format(readFormat.Remove(0, 6), propName, "reader", tableColInfo.Count),
                    string.Format(readFormat, propName, "reader", tableColInfo.Count),
                    isPrimaryKey,
                    colIsIdentity,
                    nullable,
                    memberIsEnum,
                    useJson,
                    memberIsNonScalar,
                        isUnique,
                        isMultiSelect,
                        foreignTable,
                        foreignColumn,
                        foreignModelType));
            }
            else if (memberIsEnum)
            {
                //// build the reader directive for the enum type
                //string ef = $"Enum.TryParse(reader.GetString({tableColInfo.Count}), out {propName});";

                tableColInfo.Add(new (
                    propName,
                    propTypeName,
                    nullable
                        ? string.Format(_sqliteNullableReadDirectives["enum"].Remove(0, 6), propName, "reader", tableColInfo.Count, enumTypeName)
                        : string.Format(_sqliteReadDirectives["enum"].Remove(0, 6), propName, "reader", tableColInfo.Count, enumTypeName),
                    nullable
                        ? string.Format(_sqliteNullableReadDirectives["enum"], propName, "reader", tableColInfo.Count, enumTypeName)
                        : string.Format(_sqliteReadDirectives["enum"], propName, "reader", tableColInfo.Count, enumTypeName),
                    isPrimaryKey,
                    colIsIdentity,
                    nullable,
                    memberIsEnum,
                    useJson,
                    memberIsNonScalar,
                    isUnique,
                    isMultiSelect,
                    foreignTable,
                    foreignColumn,
                    foreignModelType));
            }
            else if (memberIsNonScalar)
            {
                anyColIsJson = true;

                // A real T[] array column reads back as List<T>.ToArray(); List<T>/IEnumerable<T>
                // stay as List<T>. Both share the JSON[] serialization; only the read differs.
                string jsonArrKey = col.IsArray ? "JSON[]array" : "JSON[]";

                tableColInfo.Add(new(
                    propName,
                    propTypeName,
                    nullable
                        ? string.Format(_sqliteNullableReadDirectives[jsonArrKey].Remove(0, 6), propName, "reader", tableColInfo.Count, jsonTypeName)
                        : string.Format(_sqliteReadDirectives[jsonArrKey].Remove(0, 6), propName, "reader", tableColInfo.Count, jsonTypeName),
                    nullable
                        ? string.Format(_sqliteNullableReadDirectives[jsonArrKey], propName, "reader", tableColInfo.Count, jsonTypeName)
                        : string.Format(_sqliteReadDirectives[jsonArrKey], propName, "reader", tableColInfo.Count, jsonTypeName),
                    isPrimaryKey,
                    colIsIdentity,
                    nullable,
                    memberIsEnum,
                    useJson,
                    memberIsNonScalar,
                        isUnique,
                        isMultiSelect,
                        foreignTable,
                        foreignColumn,
                        foreignModelType));
            }
            else
            {
                // tableColInfo.Add((
                //     propName,
                //     propTypeName,
                //     $"// ERROR: could not determine retrieval directive for type {propName}:{propTypeName}",
                //     $"// ERROR: could not determine retrieval directive for type {propName}:{propTypeName}",
                //     isPrimaryKey,
                //     colIsIdentity,
                //     nullable,
                //     memberIsEnum
                //     ));

                anyColIsJson = true;

                tableColInfo.Add(new(
                    propName,
                    propTypeName,
                    nullable
                        ? string.Format(_sqliteNullableReadDirectives["JSON"].Remove(0, 6), propName, "reader", tableColInfo.Count, jsonTypeName)
                        : string.Format(_sqliteReadDirectives["JSON"].Remove(0, 6), propName, "reader", tableColInfo.Count, jsonTypeName),
                    nullable
                        ? string.Format(_sqliteNullableReadDirectives["JSON"], propName, "reader", tableColInfo.Count, jsonTypeName)
                        : string.Format(_sqliteReadDirectives["JSON"], propName, "reader", tableColInfo.Count, jsonTypeName),
                    isPrimaryKey,
                    colIsIdentity,
                    nullable,
                    memberIsEnum,
                    useJson,
                    memberIsNonScalar,
                    isUnique,
                    isMultiSelect,
                    foreignTable,
                    foreignColumn,
                    foreignModelType));
            }
        }

        // class-level composite foreign keys (pre-rendered to DDL fragments in the transform)
        createFKLines.AddRange(model.CompositeFkLines);

        // WHERE predicate used by the by-key Update/Delete overloads. Composite keys AND-join
        // every primary-key column; a single key uses "col = $col". A keyless model has no row
        // identity, so its by-key Update/Delete match nothing ("1 = 0") instead of emitting the
        // invalid " = $" predicate.
        string pkWhereClause = compositePk
            ? string.Join(" AND ", pkCols.Select(c => $"{quoteIdent(c.name)} = ${c.name}"))
            : (pkColName == null
                ? "1 = 0"
                : $"{quoteIdent(pkColName)} = ${pkColName}");

        // A keyless model (no primary key, single or composite) has no row identity: its by-key
        // Update/Delete predicate is "1 = 0", so it can never affect rows and never throws. Such
        // models omit the throwOnZeroRowsAffected opt-out entirely (the knob would be a permanent
        // no-op). Keyed models expose it (default true) so a stale/already-gone key throws
        // LdgCommandFailedException unless the caller opts out per call.
        bool isKeyless = (pkColName == null) && !compositePk;
        string throwOnZeroParam = isKeyless ? string.Empty : ", bool throwOnZeroRowsAffected = true";

        // UPDATE ... SET assignments cover every non-primary-key column. A primary-key-only model
        // has none, so fall back to a harmless self-assignment of a key column to keep valid SQL
        // (an empty SET clause is a syntax error).
        string updateSetClause = tableColInfo.Any(p => !p.isPrimaryKey)
            ? string.Join(_comma_line_5, tableColInfo.Where(p => p.isPrimaryKey == false).Select(p => quoteIdent(p.name) + " = $" + p.name))
            : (tableColInfo.Count > 0
                ? quoteIdent(tableColInfo[0].name) + " = " + quoteIdent(tableColInfo[0].name)
                : "1 = 1");

        // Table constraint lines: composite keys are declared as a trailing PRIMARY KEY (...) constraint
        // rather than an inline column directive.
        List<string> createTableLines = [.. createColLines, .. createFKLines];
        if (compositePk)
        {
            createTableLines.Add($"PRIMARY KEY ({string.Join(", ", pkCols.Select(c => quoteIdent(c.name)))})");
        }

        // class-level [LdgSQLiteUnique(cols...)] declares a multi-column UNIQUE table constraint.
        foreach (EquatableColumnSet uniqueSet in model.ClassUniqueColumnSets)
        {
            createTableLines.Add($"UNIQUE ({string.Join(", ", uniqueSet.Columns.Select(quoteIdent))})");
        }

        // Upsert conflict-target defaulting. Composite and natural (non-identity) primary keys
        // default the ON CONFLICT target to their key columns. An identity primary key never
        // collides on insert (auto-rowid), and a keyless model has nothing to conflict on, so
        // those models must supply conflictColumns (typically a UNIQUE/natural key) explicitly.
        string upsertNonIdentityColsQuoted = string.Join(", ", tableColInfo.Where(p => !p.isIdentity).Select(p => $"\"{p.name}\""));
        bool upsertHasDefaultConflict = compositePk || ((pkColName != null) && !pkIsIdentity);
        string upsertDefaultConflictAssign = upsertHasDefaultConflict
            ? (compositePk
                ? $"conflictColumns = new string[] {{ {string.Join(", ", pkCols.Select(c => $"\"{c.name}\""))} }};"
                : $"conflictColumns = new string[] {{ \"{pkColName}\" }};")
            : "throw new System.ArgumentException(\"Upsert requires explicit conflictColumns for a model without a natural (non-identity) primary key.\", nameof(conflictColumns));";

        // SelectDict keys a dictionary by the single primary key, which has no meaning for a
        // composite key, so the method is omitted entirely for composite-key tables.
        string selectDictMethod = compositePk ? string.Empty : $$$""""
                        public static Dictionary<{{{((pkColName == null) || compositePk ? "int" : pkPropType)}}}, {{{className}}}> SelectDict(
                            IDbConnection dbConnection, 
                            string? dbTableName = null, 
                            bool orJoinConditions = false,
                            bool compareStringsWithLike = false,
                            {{{string.Join(", ", getFnFilterParams(true, true))}}}, IDbTransaction? transaction = null)
                        {
                            dbTableName ??= "{{{tableName}}}";
                    
                            Dictionary<{{{((pkColName == null) || compositePk ? "int" : pkPropType)}}}, {{{className}}}> results = new();
                    
                            using IDbCommand command = dbConnection.CreateCommand();
                            if (transaction != null) command.Transaction = transaction;
                            command.CommandText = $"SELECT {{{string.Join(", ", tableColInfo.Select(p => quoteIdentLit(p.name)))}}} FROM {dbTableName}";
                    
                            string joiner = orJoinConditions ? " OR " : " AND ";
                            string stringComparator = compareStringsWithLike ? " LIKE " : " = ";
                            bool addedCondition = false;
                            {{{(anyColIsJson ? "string? dbJson;" : string.Empty)}}}
                                        
                            {{{string.Join(_line_2, getConditionLines(true, true, true, true))}}}

                            {{{(pkColName == null ? "int rowId = 0;" : string.Empty)}}}
                            using (IDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    results.Add({{{(pkColName == null ? "rowId++" : tableColInfo[(int)pkColIndex!].shortRead)}}}, new()
                                    {
                                        {{{string.Join(_comma_line_5, tableColInfo.Select(p => p.readerDirective))}}}
                                    });
                                }
                            }
                            return results;
                        }
                        """";

        string hintName = string.IsNullOrEmpty(classNamespace)
            ? $"{className}.Table.g.cs"
            : $"{classNamespace}.{className}.Table.g.cs";

        context.AddSource(
            hintName,
            SourceText.From($$$""""
                    //------------------------------------------------------------------------------
                    // <auto-generated>
                    //     This code was generated by a tool.
                    //
                    //     Changes to this file may cause incorrect behavior and will be lost if
                    //     the code is regenerated.
                    // </auto-generated>
                    //------------------------------------------------------------------------------

                    #nullable enable

                    using System;
                    using System.Collections.Generic;
                    using System.Data;
                    using System.Diagnostics.CodeAnalysis;
                    using System.Text;
                    using System.Text.Json;
                    using System.Threading;
                                    
                    namespace {{{classNamespace}}};
                
                    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
                    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
                     public partial {{{decForGenCategory(genCategory)}}} {{{className}}}
                     {
                         public static string DefaultTableName => "{{{tableName}}}";
                         private static readonly HashSet<string> _sqliteColumnNames = new HashSet<string>(global::System.StringComparer.OrdinalIgnoreCase)
                         {
                             {{{string.Join(_comma_line_5, tableColInfo.Select(c => $"\"{c.name}\""))}}}
                         };

                         public static IReadOnlyCollection<string> SQLiteColumnNames { get; } = _sqliteColumnNames;

                         private static string quoteRuntimeIdent(string ident) => "\"" + ident.Replace("\"", "\"\"") + "\"";
                         {{{getKeyIndexMembers()}}}

                         {{{emitResolveOrderByPropertiesMember()}}}
 
                         public static bool CreateTable(IDbConnection dbConnection, string? dbTableName = null)
                         {
                             dbTableName ??= "{{{tableName}}}";

                            using (IDbCommand command = dbConnection.CreateCommand())
                            {
                            command.CommandText = $"""
                                CREATE TABLE IF NOT EXISTS {dbTableName} (
                                    {{{string.Join(_comma_line_4, createTableLines)}}}
                                )
                                """;

                            command.ExecuteNonQuery();
                            }

                            {{{string.Join(_line_2, getIndexLines())}}}
                    
                            {{{(pkIsIdentity ? "LoadMaxKey(dbConnection, dbTableName);" : string.Empty)}}}

                            return true;
                        }

                        public static bool DropTable(IDbConnection dbConnection, string? dbTableName = null)
                        {
                            dbTableName ??= "{{{tableName}}}";
                    
                            using IDbCommand command = dbConnection.CreateCommand();
                            command.CommandText = $"DROP TABLE IF EXISTS {dbTableName}";
                    
                            command.ExecuteNonQuery();
                    
                            return true;
                        }

                        /// <summary>
                        /// Additively brings an existing table up to the current model: creates the table
                        /// if absent, adds any missing columns via ALTER TABLE ADD COLUMN, and creates any
                        /// missing indexes. It never drops or retypes columns and never backfills data, so
                        /// it is safe to run on every startup. Columns added to a pre-existing table cannot
                        /// carry PRIMARY KEY / UNIQUE constraints or non-constant defaults; a NOT NULL model
                        /// column without a constant default is added as nullable (SQLite forbids adding a
                        /// NOT NULL column with no default to a populated table).
                        /// </summary>
                        public static bool EnsureSchema(IDbConnection dbConnection, string? dbTableName = null, IDbTransaction? transaction = null)
                        {
                            dbTableName ??= "{{{tableName}}}";

                            using (IDbCommand command = dbConnection.CreateCommand())
                            {
                            if (transaction != null) command.Transaction = transaction;
                            command.CommandText = $"""
                                CREATE TABLE IF NOT EXISTS {dbTableName} (
                                    {{{string.Join(_comma_line_4, createTableLines)}}}
                                )
                                """;
                            command.ExecuteNonQuery();
                            }

                            HashSet<string> existingColumns = new(global::System.StringComparer.OrdinalIgnoreCase);
                            using (IDbCommand command = dbConnection.CreateCommand())
                            {
                            if (transaction != null) command.Transaction = transaction;
                            command.CommandText = $"PRAGMA table_info({dbTableName})";
                            using (IDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    existingColumns.Add(reader.GetString(1));
                                }
                            }
                            }

                            {{{string.Join(_line_2, getEnsureAddColumnLines())}}}

                            {{{string.Join(_line_2, getIndexLines())}}}

                            {{{string.Join(_line_2, getEnsureUniqueConstraintIndexLines())}}}

                            {{{(pkIsIdentity ? "LoadMaxKey(dbConnection, dbTableName);" : string.Empty)}}}

                            return true;
                        }
                    
                        public static {{{className}}}? SelectSingle(
                            IDbConnection dbConnection, 
                            string? dbTableName = null, 
                            bool orJoinConditions = false, 
                            bool compareStringsWithLike = false,
                            {{{string.Join(", ", getFnFilterParams(true, true))}}}, IDbTransaction? transaction = null)
                        {
                            dbTableName ??= "{{{tableName}}}";

                            using IDbCommand command = dbConnection.CreateCommand();
                            if (transaction != null) command.Transaction = transaction;
                            command.CommandText = $"SELECT {{{string.Join(", ", tableColInfo.Select(p => quoteIdentLit(p.name)))}}} FROM {dbTableName}";

                            string joiner = orJoinConditions ? " OR " : " AND ";
                            string stringComparator = compareStringsWithLike ? " LIKE " : " = ";
                            bool addedCondition = false;
                            {{{(anyColIsJson ? "string? dbJson;" : string.Empty)}}}
                    
                            {{{string.Join(_line_2, getConditionLines(true, true, true, true))}}}

                            using (IDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    return new()
                                    {
                                        {{{string.Join(_comma_line_5, tableColInfo.Select(p => p.readerDirective))}}}
                                    };
                                }
                            }
                            return null;
                        }

                        public static List<{{{className}}}> SelectList(
                            IDbConnection dbConnection, 
                            string? dbTableName = null, 
                            string[]? orderByProperties = null, 
                            string? orderByDirection = null, 
                            bool orJoinConditions = false, 
                            bool compareStringsWithLike = false,
                            int? resultLimit = null,
                            int? resultOffset = null,
                            {{{string.Join(", ", getFnFilterParams(true, true))}}}, IDbTransaction? transaction = null, string[]? orderByDirections = null)
                        {
                            dbTableName ??= "{{{tableName}}}";

                            List<{{{className}}}> results = new();

                            using IDbCommand command = dbConnection.CreateCommand();
                            if (transaction != null) command.Transaction = transaction;
                            command.CommandText = $"SELECT {{{string.Join(", ", tableColInfo.Select(p => quoteIdentLit(p.name)))}}} FROM {dbTableName}";
                    
                             string joiner = orJoinConditions ? " OR " : " AND ";
                             string stringComparator = compareStringsWithLike ? " LIKE " : " = ";
                             bool addedCondition = false;
                             string[]? resolvedOrderByProperties = ResolveOrderByProperties(orderByProperties, orderByDirections, orderByDirection);
                             {{{(anyColIsJson ? "string? dbJson;" : string.Empty)}}}
                                         
                             {{{string.Join(_line_2, getConditionLines(true, true, true, true))}}}

                             if ((resolvedOrderByProperties != null) && (resolvedOrderByProperties.Length > 0))
                             {
                                 command.CommandText += $" ORDER BY {string.Join(", ", resolvedOrderByProperties)}";
                            }
                                
                            if (resultLimit.HasValue && (resultLimit.Value > 0))
                            {
                                command.CommandText += $" LIMIT {resultLimit.Value}";
                                if (resultOffset.HasValue && (resultOffset.Value > 0))
                                {
                                    command.CommandText += $" OFFSET {resultOffset.Value}";
                                }
                            }
                                
                            using (IDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    results.Add(new()
                                    {
                                        {{{string.Join(_comma_line_5, tableColInfo.Select(p => p.readerDirective))}}}
                                    });
                                }
                            }
                            return results;
                        }

                        public static IEnumerable<{{{className}}}> SelectEnumerable(
                            IDbConnection dbConnection,
                            string? dbTableName = null,
                            string[]? orderByProperties = null,
                            string? orderByDirection = null,
                            bool orJoinConditions = false,
                            bool compareStringsWithLike = false,
                            int? resultLimit = null,
                            int? resultOffset = null,
                            {{{string.Join(", ", getFnFilterParams(true, true))}}}, IDbTransaction? transaction = null, string[]? orderByDirections = null)
                        {
                            dbTableName ??= "{{{tableName}}}";

                            using IDbCommand command = dbConnection.CreateCommand();
                            if (transaction != null) command.Transaction = transaction;
                            command.CommandText = $"SELECT {{{string.Join(", ", tableColInfo.Select(p => quoteIdentLit(p.name)))}}} FROM {dbTableName}";

                             string joiner = orJoinConditions ? " OR " : " AND ";
                             string stringComparator = compareStringsWithLike ? " LIKE " : " = ";
                             bool addedCondition = false;
                             string[]? resolvedOrderByProperties = ResolveOrderByProperties(orderByProperties, orderByDirections, orderByDirection);
                             {{{(anyColIsJson ? "string? dbJson;" : string.Empty)}}}
 
                             {{{string.Join(_line_2, getConditionLines(true, true, true, true))}}}
 
                             if ((resolvedOrderByProperties != null) && (resolvedOrderByProperties.Length > 0))
                             {
                                 command.CommandText += $" ORDER BY {string.Join(", ", resolvedOrderByProperties)}";
                            }

                            if (resultLimit.HasValue && (resultLimit.Value > 0))
                            {
                                command.CommandText += $" LIMIT {resultLimit.Value}";
                                if (resultOffset.HasValue && (resultOffset.Value > 0))
                                {
                                    command.CommandText += $" OFFSET {resultOffset.Value}";
                                }
                            }

                            using (IDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    yield return new()
                                    {
                                        {{{string.Join(_comma_line_5, tableColInfo.Select(p => p.readerDirective))}}}
                                    };
                                }
                            }

                            yield break;
                        }

                        {{{selectDictMethod}}}

                        public static int SelectCount(
                            IDbConnection dbConnection,
                            string? dbTableName = null, 
                            bool orJoinConditions = false,
                            bool compareStringsWithLike = false,
                            {{{string.Join(", ", getFnFilterParams(true, true))}}}, IDbTransaction? transaction = null)
                        {
                            dbTableName ??= "{{{tableName}}}";
                    
                            using IDbCommand command = dbConnection.CreateCommand();
                            if (transaction != null) command.Transaction = transaction;
                            command.CommandText = $"SELECT COUNT({{{((pkColName == null) || compositePk ? "*" : quoteIdentLit(pkColName))}}}) FROM {dbTableName}";
                    
                            string joiner = orJoinConditions ? " OR " : " AND ";
                            string stringComparator = compareStringsWithLike ? " LIKE " : " = ";
                            bool addedCondition = false;
                            {{{(anyColIsJson ? "string? dbJson;" : string.Empty)}}}
                                        
                            {{{string.Join(_line_2, getConditionLines(true, true, true, true))}}}

                            object? result = command.ExecuteScalar();
                            if (result is int value)
                            {
                                return value;
                            }
                            else if (result is long l)
                            {
                                return Convert.ToInt32(l);
                            }

                            return -1;
                        }

                        public static {{{((pkColName == null) || compositePk ? "void" : pkPropType)}}} Insert(
                            IDbConnection dbConnection,
                            {{{className}}} value,
                            string? dbTableName = null,
                            bool ignoreDuplicates = false,
                            bool insertPrimaryKey = false, IDbTransaction? transaction = null)
                        {
                            dbTableName ??= "{{{tableName}}}";
                            {{{getNonIdentityPkInit(pkColName, pkPropType)}}}
                            {{{(anyColIsJson ? "string? dbJson;" : string.Empty)}}}
                            string insertLiteral = ignoreDuplicates ? "INSERT OR IGNORE" : "INSERT";

                            if (insertPrimaryKey)
                            {
                                bool _ownTxn = transaction is null;
                                IDbTransaction _txn = transaction ?? dbConnection.BeginTransaction();
                                try
                                {
                                    using IDbCommand command = dbConnection.CreateCommand();
                                    command.Transaction = _txn;
                                    command.CommandText = $"""
                                        {insertLiteral} INTO {dbTableName} (
                                            {{{string.Join(_comma_line_6, tableColInfo.Select(p => quoteIdent(p.name)))}}}
                                        ) VALUES (
                                            {{{string.Join(_comma_line_6, tableColInfo.Select(p => "$" + p.name))}}}
                                        );
                                        """;
                    
                                    {{{string.Join(_line_4, getInsertCommandParamLines(true, null, pkPropType, includeIdentity: true, ignoreDupeProperty: "ignoreDuplicates"))}}}
                    
                                    if (_ownTxn) _txn.Commit();
                                    }
                                    finally
                                    {
                                        if (_ownTxn) _txn.Dispose();
                                }
                            }
                            else
                            {
                                bool _ownTxn = transaction is null;
                                IDbTransaction _txn = transaction ?? dbConnection.BeginTransaction();
                                try
                                {
                                    using IDbCommand command = dbConnection.CreateCommand();
                                    command.Transaction = _txn;
                                    command.CommandText = $"""
                                        {insertLiteral} INTO {dbTableName} {{{buildInsertColumnsAndValues(tableColInfo.Where(p => p.isIdentity == false))}}} {{{(pkIsIdentity ? " RETURNING " + quoteIdent(pkColName!) : string.Empty)}}};
                                        """;
                    
                                    {{{string.Join(_line_4, getInsertCommandParamLines(true, pkIsIdentity ? pkColName : null, pkPropType, ignoreDupeProperty: "ignoreDuplicates"))}}}
                    
                                    if (_ownTxn) _txn.Commit();
                                    }
                                    finally
                                    {
                                        if (_ownTxn) _txn.Dispose();
                                }
                            }

                            {{{((pkColName == null) || compositePk ? "return" : $"return value.{pkColName}")}}};
                        }

                        public static {{{className}}}? InsertReturning(
                            IDbConnection dbConnection,
                            {{{className}}} value,
                            string? dbTableName = null,
                            bool ignoreDuplicates = false,
                            bool insertPrimaryKey = false, IDbTransaction? transaction = null)
                        {
                            dbTableName ??= "{{{tableName}}}";
                            {{{getNonIdentityPkInit(pkColName, pkPropType)}}}
                            {{{(anyColIsJson ? "string? dbJson;" : string.Empty)}}}
                            string insertLiteral = ignoreDuplicates ? "INSERT OR IGNORE" : "INSERT";

                            bool _ownTxn = transaction is null;
                            IDbTransaction _txn = transaction ?? dbConnection.BeginTransaction();
                            try
                            {
                                using IDbCommand command = dbConnection.CreateCommand();
                                command.Transaction = _txn;
                                if (insertPrimaryKey)
                                {
                                    command.CommandText = $"""
                                        {insertLiteral} INTO {dbTableName} (
                                            {{{string.Join(_comma_line_6, tableColInfo.Where(p => !rawDefaultColumnNames.Contains(p.name)).Select(p => quoteIdent(p.name)))}}}
                                        ) VALUES (
                                            {{{string.Join(_comma_line_6, tableColInfo.Where(p => !rawDefaultColumnNames.Contains(p.name)).Select(p => "$" + p.name))}}}
                                        ) RETURNING {{{string.Join(", ", tableColInfo.Select(p => quoteIdent(p.name)))}}};
                                        """;

                                    {{{string.Join(_line_5, getInsertCommandParamLines(true, null, pkPropType, createParameters: true, instantiateParameters: true, includeIdentity: true, skipRawDefaults: true))}}}
                                }
                                else
                                {
                                    command.CommandText = $"""
                                        {insertLiteral} INTO {dbTableName} {{{buildInsertColumnsAndValues(tableColInfo.Where(p => (p.isIdentity == false) && !rawDefaultColumnNames.Contains(p.name)))}}} RETURNING {{{string.Join(", ", tableColInfo.Select(p => quoteIdent(p.name)))}}};
                                        """;

                                    {{{string.Join(_line_5, getInsertCommandParamLines(true, null, pkPropType, createParameters: true, instantiateParameters: true, skipRawDefaults: true))}}}
                                }

                                {{{className}}}? _returned = null;
                                using (IDataReader reader = command.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        {{{string.Join(_line_5, tableColInfo.Select(p => "value." + p.readerDirective + ";"))}}}
                                        _returned = value;
                                    }
                                }

                                if (_ownTxn) _txn.Commit();

                                return _returned;
                            }
                            finally
                            {
                                if (_ownTxn) _txn.Dispose();
                            }
                        }

                        public static void Insert(
                            IDbConnection dbConnection,
                            List<{{{className}}}> values,
                            string? dbTableName = null,
                            bool ignoreDuplicates = false,
                            bool insertPrimaryKey = false, IDbTransaction? transaction = null)
                        {
                            dbTableName ??= "{{{tableName}}}";
                            {{{(anyColIsJson ? "string? dbJson;" : string.Empty)}}}
                            string insertLiteral = ignoreDuplicates ? "INSERT OR IGNORE" : "INSERT";

                            if (insertPrimaryKey)
                            {
                                bool _ownTxn = transaction is null;
                                IDbTransaction _txn = transaction ?? dbConnection.BeginTransaction();
                                try
                                {
                                    using IDbCommand command = dbConnection.CreateCommand();
                                    command.Transaction = _txn;
                                    command.CommandText = $"""
                                        {insertLiteral} INTO {dbTableName} (
                                            {{{string.Join(_comma_line_6, tableColInfo.Select(p => quoteIdent(p.name)))}}}
                                        ) VALUES (
                                            {{{string.Join(_comma_line_6, tableColInfo.Select(p => "$" + p.name))}}}
                                        );
                                        """;
                    
                                    {{{string.Join(_line_4, getInsertCommandParamLines(false, null, pkPropType, createParameters: true, includeIdentity: true, ignoreDupeProperty: "ignoreDuplicates"))}}}
                    
                                    command.Prepare();
                    
                                    foreach ({{{className}}} value in values)
                                    {
                                        {{{string.Join(_line_5, getInsertCommandParamLines(false, null, pkPropType, instantiateParameters: true, executeCommand: true, includeIdentity: true, ignoreDupeProperty: "ignoreDuplicates"))}}}
                                    }
                    
                                    if (_ownTxn) _txn.Commit();
                                    }
                                    finally
                                    {
                                        if (_ownTxn) _txn.Dispose();
                                }
                            }
                            else
                            {
                                bool _ownTxn = transaction is null;
                                IDbTransaction _txn = transaction ?? dbConnection.BeginTransaction();
                                try
                                {
                                    using IDbCommand command = dbConnection.CreateCommand();
                                    command.Transaction = _txn;
                                    command.CommandText = $"""
                                        {insertLiteral} INTO {dbTableName} {{{buildInsertColumnsAndValues(tableColInfo.Where(p => p.isIdentity == false))}}} {{{(pkIsIdentity ? " RETURNING " + quoteIdent(pkColName!) : string.Empty)}}};
                                        """;

                                    {{{string.Join(_line_4, getInsertCommandParamLines(false, pkIsIdentity ? pkColName : null, pkPropType, createParameters: true, ignoreDupeProperty: "ignoreDuplicates"))}}}

                                    command.Prepare();

                                    foreach ({{{className}}} value in values)
                                    {
                                        {{{getNonIdentityPkInit(pkColName, pkPropType)}}}
                                        {{{string.Join(_line_5, getInsertCommandParamLines(false, pkIsIdentity ? pkColName : null, pkPropType, instantiateParameters: true, executeCommand: true, ignoreDupeProperty: "ignoreDuplicates"))}}}
                                    }
                    
                                    if (_ownTxn) _txn.Commit();
                                    }
                                    finally
                                    {
                                        if (_ownTxn) _txn.Dispose();
                                }
                            }
                        }

                        public static void Insert(
                            IDbConnection dbConnection,
                            IEnumerable<{{{className}}}> values,
                            string? dbTableName = null,
                            bool ignoreDuplicates = false,
                            bool insertPrimaryKey = false, IDbTransaction? transaction = null)
                        {
                            dbTableName ??= "{{{tableName}}}";
                            {{{(anyColIsJson ? "string? dbJson;" : string.Empty)}}}
                            string insertLiteral = ignoreDuplicates ? "INSERT OR IGNORE" : "INSERT";
                    
                            if (insertPrimaryKey)
                            {
                                bool _ownTxn = transaction is null;
                                IDbTransaction _txn = transaction ?? dbConnection.BeginTransaction();
                                try
                                {
                                    using IDbCommand command = dbConnection.CreateCommand();
                                    command.Transaction = _txn;
                                    command.CommandText = $"""
                                        {insertLiteral} INTO {dbTableName} (
                                            {{{string.Join(_comma_line_6, tableColInfo.Select(p => quoteIdent(p.name)))}}}
                                        ) VALUES (
                                            {{{string.Join(_comma_line_6, tableColInfo.Select(p => "$" + p.name))}}}
                                        );
                                        """;
                    
                                    {{{string.Join(_line_4, getInsertCommandParamLines(false, null, pkPropType, createParameters: true, includeIdentity: true, ignoreDupeProperty: "ignoreDuplicates"))}}}
                    
                                    command.Prepare();
                    
                                    foreach ({{{className}}} value in values)
                                    {
                                        {{{string.Join(_line_5, getInsertCommandParamLines(false, null, pkPropType, instantiateParameters: true, executeCommand: true, includeIdentity: true, setIdentity: false, ignoreDupeProperty: "ignoreDuplicates"))}}}
                                    }
                    
                                    if (_ownTxn) _txn.Commit();
                                    }
                                    finally
                                    {
                                        if (_ownTxn) _txn.Dispose();
                                }
                            }
                            else
                            {
                                bool _ownTxn = transaction is null;
                                IDbTransaction _txn = transaction ?? dbConnection.BeginTransaction();
                                try
                                {
                                    using IDbCommand command = dbConnection.CreateCommand();
                                    command.Transaction = _txn;
                                    command.CommandText = $"""
                                        {insertLiteral} INTO {dbTableName} {{{buildInsertColumnsAndValues(tableColInfo.Where(p => p.isIdentity == false))}}} {{{(pkIsIdentity ? " RETURNING " + quoteIdent(pkColName!) : string.Empty)}}};
                                        """;
                    
                                    {{{string.Join(_line_4, getInsertCommandParamLines(false, pkIsIdentity ? pkColName : null, pkPropType, createParameters: true, ignoreDupeProperty: "ignoreDuplicates"))}}}
                    
                                    command.Prepare();
                    
                                    foreach ({{{className}}} value in values)
                                    {
                                        {{{getNonIdentityPkInit(pkColName, pkPropType)}}}
                                        {{{string.Join(_line_5, getInsertCommandParamLines(false, pkIsIdentity ? pkColName : null, pkPropType, instantiateParameters: true, executeCommand: true, ignoreDupeProperty: "ignoreDuplicates"))}}}
                                    }
                    
                                    if (_ownTxn) _txn.Commit();
                                    }
                                    finally
                                    {
                                        if (_ownTxn) _txn.Dispose();
                                }
                            }
                        }

                        public static void Upsert(
                            IDbConnection dbConnection,
                            {{{className}}} value,
                            string[]? conflictColumns = null,
                            string[]? updateColumns = null,
                            string[]? incrementColumns = null,
                            string? dbTableName = null,
                            IDbTransaction? transaction = null)
                        {
                            dbTableName ??= "{{{tableName}}}";
                            {{{(anyColIsJson ? "string? dbJson;" : string.Empty)}}}

                            if ((conflictColumns == null) || (conflictColumns.Length == 0))
                            {
                                {{{upsertDefaultConflictAssign}}}
                            }

                            if (updateColumns == null)
                            {
                                HashSet<string> _conflictSet = new(conflictColumns, System.StringComparer.OrdinalIgnoreCase);
                                List<string> _autoUpdate = new();
                                foreach (string _col in new string[] { {{{upsertNonIdentityColsQuoted}}} })
                                {
                                    if (!_conflictSet.Contains(_col)) _autoUpdate.Add(_col);
                                }
                                updateColumns = _autoUpdate.ToArray();
                            }

                            foreach (string _validateCol in conflictColumns)
                            {
                                if (!_sqliteColumnNames.Contains(_validateCol))
                                {
                                    throw new System.ArgumentException($"Upsert conflict column '{_validateCol}' is not a known column of '{dbTableName}'.", nameof(conflictColumns));
                                }
                            }
                            if (updateColumns != null)
                            {
                                foreach (string _validateCol in updateColumns)
                                {
                                    if (!_sqliteColumnNames.Contains(_validateCol))
                                    {
                                        throw new System.ArgumentException($"Upsert update column '{_validateCol}' is not a known column of '{dbTableName}'.", nameof(updateColumns));
                                    }
                                }
                            }
                            if (incrementColumns != null)
                            {
                                foreach (string _validateCol in incrementColumns)
                                {
                                    if (!_sqliteColumnNames.Contains(_validateCol))
                                    {
                                        throw new System.ArgumentException($"Upsert increment column '{_validateCol}' is not a known column of '{dbTableName}'.", nameof(incrementColumns));
                                    }
                                }
                            }

                            List<string> _conflictQuoted = new(conflictColumns.Length);
                            foreach (string _cc in conflictColumns) _conflictQuoted.Add(quoteRuntimeIdent(_cc));
                            string _conflictTarget = string.Join(", ", _conflictQuoted);
                            string _onConflict;
                            if (updateColumns.Length == 0)
                            {
                                _onConflict = $"ON CONFLICT({_conflictTarget}) DO NOTHING";
                            }
                            else
                            {
                                HashSet<string> _incrementSet = (incrementColumns == null) ? new() : new(incrementColumns, System.StringComparer.OrdinalIgnoreCase);
                                List<string> _setClauses = new();
                                foreach (string _col in updateColumns)
                                {
                                    _setClauses.Add(_incrementSet.Contains(_col) ? $"{quoteRuntimeIdent(_col)} = {quoteRuntimeIdent(_col)} + excluded.{quoteRuntimeIdent(_col)}" : $"{quoteRuntimeIdent(_col)} = excluded.{quoteRuntimeIdent(_col)}");
                                }
                                _onConflict = $"ON CONFLICT({_conflictTarget}) DO UPDATE SET {string.Join(", ", _setClauses)}";
                            }

                            bool _ownTxn = transaction is null;
                            IDbTransaction _txn = transaction ?? dbConnection.BeginTransaction();
                            try
                            {
                                using IDbCommand command = dbConnection.CreateCommand();
                                command.Transaction = _txn;
                                command.CommandText = $"""
                                    INSERT INTO {dbTableName} {{{buildInsertColumnsAndValues(tableColInfo.Where(p => !p.isIdentity))}}}
                                    {_onConflict};
                                    """;

                                {{{string.Join(_line_4, getInsertCommandParamLines(true, null, pkPropType, createParameters: true, instantiateParameters: true, executeCommand: false))}}}

                                command.ExecuteNonQuery();

                                if (_ownTxn) _txn.Commit();
                            }
                            finally
                            {
                                if (_ownTxn) _txn.Dispose();
                            }
                        }

                        public static {{{className}}} Update(IDbConnection dbConnection, {{{className}}} value, string? dbTableName = null, IDbTransaction? transaction = null{{{throwOnZeroParam}}})
                        {
                            dbTableName ??= "{{{tableName}}}";
                            {{{(anyColIsJson ? "string? dbJson;" : string.Empty)}}}
                                        
                            bool _ownTxn = transaction is null;
                            IDbTransaction _txn = transaction ?? dbConnection.BeginTransaction();
                            try
                            {
                                using IDbCommand command = dbConnection.CreateCommand();
                                command.Transaction = _txn;
                                command.CommandText = $"""
                                    UPDATE {dbTableName} SET
                                        {{{updateSetClause}}}
                                    WHERE
                                        {{{pkWhereClause}}}
                                    """;
                    
                                {{{string.Join(_line_3, getInsertCommandParamLines(true, pkIsIdentity ? pkColName : null, pkPropType, includeIdentity: true, isInsert: false, byKeyMutation: true))}}}
                    
                                if (_ownTxn) _txn.Commit();
                                }
                                finally
                                {
                                    if (_ownTxn) _txn.Dispose();
                            }
                    
                            return value;
                        }

                        public static {{{className}}}? UpdateReturning(IDbConnection dbConnection, {{{className}}} value, string? dbTableName = null, IDbTransaction? transaction = null)
                        {
                            dbTableName ??= "{{{tableName}}}";
                            {{{(anyColIsJson ? "string? dbJson;" : string.Empty)}}}

                            bool _ownTxn = transaction is null;
                            IDbTransaction _txn = transaction ?? dbConnection.BeginTransaction();
                            try
                            {
                                using IDbCommand command = dbConnection.CreateCommand();
                                command.Transaction = _txn;
                                command.CommandText = $"""
                                    UPDATE {dbTableName} SET
                                        {{{updateSetClause}}}
                                    WHERE
                                        {{{pkWhereClause}}}
                                    RETURNING {{{string.Join(", ", tableColInfo.Select(p => quoteIdent(p.name)))}}}
                                    """;

                                {{{string.Join(_line_4, getInsertCommandParamLines(true, null, pkPropType, createParameters: true, instantiateParameters: true, includeIdentity: true))}}}

                                {{{className}}}? _returned = null;
                                using (IDataReader reader = command.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        {{{string.Join(_line_5, tableColInfo.Select(p => "value." + p.readerDirective + ";"))}}}
                                        _returned = value;
                                    }
                                }

                                if (_ownTxn) _txn.Commit();

                                return _returned;
                            }
                            finally
                            {
                                if (_ownTxn) _txn.Dispose();
                            }
                        }

                        public static void Update(IDbConnection dbConnection, IEnumerable<{{{className}}}> values, string? dbTableName = null, IDbTransaction? transaction = null{{{throwOnZeroParam}}})
                        {
                            dbTableName ??= "{{{tableName}}}";
                            {{{(anyColIsJson ? "string? dbJson;" : string.Empty)}}}
                                                
                            bool _ownTxn = transaction is null;
                            IDbTransaction _txn = transaction ?? dbConnection.BeginTransaction();
                            try
                            {
                                using IDbCommand command = dbConnection.CreateCommand();
                                command.Transaction = _txn;
                                command.CommandText = $"""
                                    UPDATE {dbTableName} SET
                                        {{{updateSetClause}}}
                                    WHERE
                                        {{{pkWhereClause}}}
                                    """;
                    
                                {{{string.Join(
                                _line_3,
                                getInsertCommandParamLines(
                                    false,
                                    pkIsIdentity ? pkColName : null,
                                    pkPropType,
                                    createParameters: true,
                                    includeIdentity: true,
                                    isInsert: false,
                                    byKeyMutation: true))}}}
                    
                                foreach ({{{className}}} value in values)
                                {
                                    {{{string.Join(
                                    _line_4,
                                    getInsertCommandParamLines(
                                        false,
                                        pkIsIdentity ? pkColName : null,
                                        pkPropType,
                                        instantiateParameters: true,
                                        executeCommand: true,
                                        setIdentity: false,
                                        includeIdentity: true,
                                        isInsert: false,
                                        byKeyMutation: true))}}}
                                }
                    
                                if (_ownTxn) _txn.Commit();
                                }
                                finally
                                {
                                    if (_ownTxn) _txn.Dispose();
                            }
                        }

                        public static void Delete(IDbConnection dbConnection, {{{className}}} value, string? dbTableName = null, IDbTransaction? transaction = null{{{throwOnZeroParam}}})
                        {
                            dbTableName ??= "{{{tableName}}}";
                                        
                            bool _ownTxn = transaction is null;
                            IDbTransaction _txn = transaction ?? dbConnection.BeginTransaction();
                            try
                            {
                                using IDbCommand command = dbConnection.CreateCommand();
                                command.Transaction = _txn;
                                command.CommandText = $"""DELETE FROM {dbTableName} WHERE {{{pkWhereClause}}}""";
                    
                                {{{string.Join(
                                _line_3,
                                getInsertCommandParamLines(
                                    true,
                                    pkIsIdentity ? pkColName : null,
                                    pkPropType,
                                    createParameters: true,
                                    instantiateParameters: true,
                                    executeCommand: true,
                                    primaryKeyOnly: true,
                                    setIdentity: false,
                                    byKeyMutation: true))}}}
                    
                                if (_ownTxn) _txn.Commit();
                                }
                                finally
                                {
                                    if (_ownTxn) _txn.Dispose();
                            }
                        }
                    
                        public static void Delete(IDbConnection dbConnection, IEnumerable<{{{className}}}> values, string? dbTableName = null, IDbTransaction? transaction = null{{{throwOnZeroParam}}})
                        {
                            dbTableName ??= "{{{tableName}}}";
                                                
                            bool _ownTxn = transaction is null;
                            IDbTransaction _txn = transaction ?? dbConnection.BeginTransaction();
                            try
                            {
                                using IDbCommand command = dbConnection.CreateCommand();
                                command.Transaction = _txn;
                                command.CommandText = $"""DELETE FROM {dbTableName} WHERE {{{pkWhereClause}}}""";
                                        
                                {{{string.Join(
                                _line_3,
                                getInsertCommandParamLines(
                                    false,
                                    pkIsIdentity ? pkColName : null,
                                    pkPropType,
                                    createParameters: true,
                                    primaryKeyOnly: true,
                                    setIdentity: false))}}}
                    
                                foreach ({{{className}}} value in values)
                                {
                                    {{{string.Join(
                                    _line_4,
                                    getInsertCommandParamLines(
                                        false,
                                        pkIsIdentity ? pkColName : null,
                                        pkPropType,
                                        instantiateParameters: true,
                                        executeCommand: true,
                                        setIdentity: false,
                                        primaryKeyOnly: true,
                                        byKeyMutation: true))}}}
                                }
                    
                                if (_ownTxn) _txn.Commit();
                                }
                                finally
                                {
                                    if (_ownTxn) _txn.Dispose();
                            }
                        }

                        public static void Delete(
                            IDbConnection dbConnection,
                            string? dbTableName = null,
                            bool orJoinConditions = false,
                            bool compareStringsWithLike = false,
                            {{{string.Join(", ", getFnFilterParams(true, true))}}}, IDbTransaction? transaction = null)
                        {
                            dbTableName ??= "{{{tableName}}}";
                            string joiner = orJoinConditions ? " OR " : " AND ";
                            string stringComparator = compareStringsWithLike ? " LIKE " : " = ";
                            {{{(anyColIsJson ? "string? dbJson;" : string.Empty)}}}
                                        
                            bool _ownTxn = transaction is null;
                            IDbTransaction _txn = transaction ?? dbConnection.BeginTransaction();
                            try
                            {
                                using IDbCommand command = dbConnection.CreateCommand();
                                command.Transaction = _txn;
                                command.CommandText = $"DELETE FROM {dbTableName}";
                                        
                                bool addedCondition = false;
                    
                                {{{string.Join(_line_2, getConditionLines(true, true, true, true))}}}

                                command.ExecuteNonQuery();
                                if (_ownTxn) _txn.Commit();
                                }
                                finally
                                {
                                    if (_ownTxn) _txn.Dispose();
                            }
                        }


                        
                        private static string getNumericOperator(string requested) => requested switch
                        {
                            "Equal" => "=",
                            "Equals" => "=",
                            "=" => "=",
                            "DoesNotEqual" => "!=",
                            "NotEquals" => "!=",
                            "!=" => "!=",
                            "GreaterThan" => ">",
                            ">" => ">",
                            "GreaterThanOrEqual" => ">=",
                            "GreaterThanOrEquals" => ">=",
                            ">=" => ">=",
                            "LessThan" => "<",
                            "<" => "<",
                            "LessThanOrEqual" => "<=",
                            "LessThanOrEquals" => "<=",
                            "<=" => "<=",
                            _ => "=",
                        };

                        {{{(anyColIsJson ? emitJsonHelperMembers() : string.Empty)}}}
                    }

                    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
                    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
                    public static class {{{className}}}Extensions
                    {
                        public static bool EnsureSchema<T>(this IDbConnection dbCon, string? dbTableName = null, IDbTransaction? transaction = null)
                            where T : {{{className}}}
                        {
                            return {{{className}}}.EnsureSchema(dbCon, dbTableName, transaction);
                        }

                        public static {{{className}}}? SelectSingle<T>(
                            this IDbConnection dbCon, 
                            string? dbTableName = null,
                            bool orJoinConditions = false,
                            bool compareStringsWithLike = false,
                            {{{string.Join(", ", getFnFilterParams(true, true))}}}, IDbTransaction? transaction = null)
                            where T : {{{className}}}
                        {
                            return {{{className}}}.SelectSingle(dbCon, dbTableName, orJoinConditions, compareStringsWithLike, {{{string.Join(", ", getFnFilterArgs(true, true))}}}, transaction);
                        }

                        public static List<{{{className}}}> SelectList<T>(
                            this IDbConnection dbCon,
                            string? dbTableName = null,
                            string[]? orderByProperties = null,
                            string? orderByDirection = null,
                            bool orJoinConditions = false,
                            bool compareStringsWithLike = false,
                            int? resultLimit = null,
                            int? resultOffset = null,
                            {{{string.Join(", ", getFnFilterParams(true, true))}}}, IDbTransaction? transaction = null, string[]? orderByDirections = null)
                            where T : {{{className}}}
                        {
                            return {{{className}}}.SelectList(
                                dbCon,
                                dbTableName,
                                orderByProperties,
                                orderByDirection,
                                orJoinConditions,
                                compareStringsWithLike,
                                resultLimit,
                                resultOffset,
                                {{{string.Join(", ", getFnFilterArgs(true, true))}}}, transaction, orderByDirections);
                        }

                        public static IEnumerable<{{{className}}}> SelectEnumerable<T>(
                            this IDbConnection dbCon,
                            string? dbTableName = null,
                            string[]? orderByProperties = null,
                            string? orderByDirection = null,
                            bool orJoinConditions = false,
                            bool compareStringsWithLike = false,
                            int? resultLimit = null,
                            int? resultOffset = null,
                            {{{string.Join(", ", getFnFilterParams(true, true))}}}, IDbTransaction? transaction = null, string[]? orderByDirections = null)
                            where T : {{{className}}}
                        {
                            return {{{className}}}.SelectEnumerable(
                                dbCon,
                                dbTableName,
                                orderByProperties,
                                orderByDirection,
                                orJoinConditions,
                                compareStringsWithLike,
                                resultLimit,
                                resultOffset,
                                {{{string.Join(", ", getFnFilterArgs(true, true))}}}, transaction, orderByDirections);
                        }

                        public static int SelectCount<T>(
                            this IDbConnection dbCon,
                            string? dbTableName = null,
                            bool orJoinConditions = false,
                            bool compareStringsWithLike = false,
                            {{{string.Join(", ", getFnFilterParams(true, true))}}}, IDbTransaction? transaction = null)
                            where T : {{{className}}}
                        {
                            return {{{className}}}.SelectCount(dbCon, dbTableName, orJoinConditions, compareStringsWithLike, {{{string.Join(", ", getFnFilterArgs(true, true))}}}, transaction);
                        }
                                                            
                        public static void Insert(this IDbConnection dbCon, {{{className}}} value, string? dbTableName = null, bool ignoreDuplicates = false, bool insertPrimaryKey = false, IDbTransaction? transaction = null)
                        {
                            {{{className}}}.Insert(dbCon, value, dbTableName, ignoreDuplicates, insertPrimaryKey, transaction);
                        }

                        public static void Insert(this IDbConnection dbCon, List<{{{className}}}> values, string? dbTableName = null, bool ignoreDuplicates = false, bool insertPrimaryKey = false, IDbTransaction? transaction = null)
                        {
                            {{{className}}}.Insert(dbCon, values, dbTableName, ignoreDuplicates, insertPrimaryKey, transaction);
                        }

                        public static void Insert(this IDbConnection dbCon, IEnumerable<{{{className}}}> values, string? dbTableName = null, bool ignoreDuplicates = false, bool insertPrimaryKey = false, IDbTransaction? transaction = null)
                        {
                            {{{className}}}.Insert(dbCon, values, dbTableName, ignoreDuplicates, insertPrimaryKey, transaction);
                        }

                        public static void Upsert(this IDbConnection dbCon, {{{className}}} value, string[]? conflictColumns = null, string[]? updateColumns = null, string[]? incrementColumns = null, string? dbTableName = null, IDbTransaction? transaction = null)
                        {
                            {{{className}}}.Upsert(dbCon, value, conflictColumns, updateColumns, incrementColumns, dbTableName, transaction);
                        }

                        public static {{{className}}}? InsertReturning(this IDbConnection dbCon, {{{className}}} value, string? dbTableName = null, bool ignoreDuplicates = false, bool insertPrimaryKey = false, IDbTransaction? transaction = null)
                        {
                            return {{{className}}}.InsertReturning(dbCon, value, dbTableName, ignoreDuplicates, insertPrimaryKey, transaction);
                        }

                        public static {{{className}}}? UpdateReturning(this IDbConnection dbCon, {{{className}}} value, string? dbTableName = null, IDbTransaction? transaction = null)
                        {
                            return {{{className}}}.UpdateReturning(dbCon, value, dbTableName, transaction);
                        }

                        public static void Update(this IDbConnection dbCon, {{{className}}} value, string? dbTableName = null, IDbTransaction? transaction = null)
                        {
                            {{{className}}}.Update(dbCon, value, dbTableName, transaction);
                        }

                        public static void Update(this IDbConnection dbCon, IEnumerable<{{{className}}}> values, string? dbTableName = null, IDbTransaction? transaction = null)
                        {
                            {{{className}}}.Update(dbCon, values, dbTableName, transaction);
                        }

                        public static void Delete(this IDbConnection dbCon, {{{className}}} value, string? dbTableName = null, IDbTransaction? transaction = null)
                        {
                            {{{className}}}.Delete(dbCon, value, dbTableName, transaction);
                        }
                    
                        public static void Delete(this IDbConnection dbCon, IEnumerable<{{{className}}}> values, string? dbTableName = null, IDbTransaction? transaction = null)
                        {
                            {{{className}}}.Delete(dbCon, values, dbTableName, transaction);
                        }

                        public static void Delete(
                            this IDbConnection dbCon,
                            string? dbTableName = null,
                            bool orJoinConditions = false,
                            bool compareStringsWithLike = false,
                            {{{string.Join(", ", getFnFilterParams(true, true))}}}, IDbTransaction? transaction = null)
                        {
                            {{{className}}}.Delete(dbCon, dbTableName, orJoinConditions, compareStringsWithLike, {{{string.Join(", ", getFnFilterArgs(true, true))}}}, transaction);
                        }

                        public static void Insert(this {{{className}}} value, IDbConnection dbCon, string? dbTableName = null, bool ignoreDuplicates = false, bool insertPrimaryKey = false, IDbTransaction? transaction = null)
                        {
                            {{{className}}}.Insert(dbCon, value, dbTableName, ignoreDuplicates, insertPrimaryKey, transaction);
                        }

                        public static void Insert(this List<{{{className}}}> values, IDbConnection dbCon, string? dbTableName = null, bool ignoreDuplicates = false, bool insertPrimaryKey = false, IDbTransaction? transaction = null)
                        {
                            {{{className}}}.Insert(dbCon, values, dbTableName, ignoreDuplicates, insertPrimaryKey, transaction);
                        }

                        public static void Insert(this IEnumerable<{{{className}}}> values, IDbConnection dbCon, string? dbTableName = null, bool ignoreDuplicates = false, bool insertPrimaryKey = false, IDbTransaction? transaction = null)
                        {
                            {{{className}}}.Insert(dbCon, values, dbTableName, ignoreDuplicates, insertPrimaryKey, transaction);
                        }

                        public static void Upsert(this {{{className}}} value, IDbConnection dbCon, string[]? conflictColumns = null, string[]? updateColumns = null, string[]? incrementColumns = null, string? dbTableName = null, IDbTransaction? transaction = null)
                        {
                            {{{className}}}.Upsert(dbCon, value, conflictColumns, updateColumns, incrementColumns, dbTableName, transaction);
                        }

                        public static {{{className}}}? InsertReturning(this {{{className}}} value, IDbConnection dbCon, string? dbTableName = null, bool ignoreDuplicates = false, bool insertPrimaryKey = false, IDbTransaction? transaction = null)
                        {
                            return {{{className}}}.InsertReturning(dbCon, value, dbTableName, ignoreDuplicates, insertPrimaryKey, transaction);
                        }

                        public static {{{className}}}? UpdateReturning(this {{{className}}} value, IDbConnection dbCon, string? dbTableName = null, IDbTransaction? transaction = null)
                        {
                            return {{{className}}}.UpdateReturning(dbCon, value, dbTableName, transaction);
                        }
                    
                        public static void Update(this {{{className}}} value, IDbConnection dbCon, string? dbTableName = null, IDbTransaction? transaction = null)
                        {
                            {{{className}}}.Update(dbCon, value, dbTableName, transaction);
                        }
                    
                        public static void Update(this IEnumerable<{{{className}}}> values, IDbConnection dbCon, string? dbTableName = null, IDbTransaction? transaction = null)
                        {
                            {{{className}}}.Update(dbCon, values, dbTableName, transaction);
                        }

                        public static void Delete(this {{{className}}} value, IDbConnection dbCon, string? dbTableName = null, IDbTransaction? transaction = null)
                        {
                            {{{className}}}.Delete(dbCon, value, dbTableName, transaction);
                        }
                    
                        public static void Delete(this IEnumerable<{{{className}}}> values, IDbConnection dbCon, string? dbTableName = null, IDbTransaction? transaction = null)
                        {
                            {{{className}}}.Delete(dbCon, values, dbTableName, transaction);
                        }
                    }

                    #nullable restore
                    """"
            , Encoding.UTF8)
        );

        return;

        string decForGenCategory(LdGenCategory genCategory) => genCategory switch
        {
            LdGenCategory.Class => "class",
            LdGenCategory.Record => "record class",
            _ => "class",
        };

        string getNonIdentityPkInit(string? pkColName, string? pkTypeName) =>
            pkTypeName?.Equals("Guid", StringComparison.OrdinalIgnoreCase) == true
                ? $"value.{pkColName} = Guid.NewGuid();"
                : string.Empty;

        // Emits the auto-increment key counter (_indexValue/GetIndex) plus LoadMaxKey/SelectMaxKey,
        // but only for a single integer identity key. Natural keys (Guid/string), composite keys,
        // and keyless models are inserted explicitly and never auto-assigned, so the counter
        // machinery is omitted entirely (avoids CS0037/CS0029 on non-integer key types).
        string getKeyIndexMembers()
        {
            if (!pkIsIdentity)
            {
                return string.Empty;
            }

            string counterType = pkPropType!;
            string maxCol = pkColName!;

            // SQLite returns Int64 for INTEGER columns, so an int counter must also narrow the
            // long result; a long counter takes the value directly and needs no extra branch.
            string loadElse = counterType == "int"
                ? "else if (result is long l) { _indexValue = Convert.ToInt32(l); }"
                : string.Empty;
            string selectElse = counterType == "int"
                ? "else if (result is long l) { return Convert.ToInt32(l); }"
                : string.Empty;

            return $$"""
            internal static {{counterType}} _indexValue = 0;
            public static {{counterType}} GetIndex() => Interlocked.Increment(ref _indexValue);

            public static void LoadMaxKey(IDbConnection dbConnection, string? dbTableName = null, {{counterType}} defaultValue = 0)
            {
                dbTableName ??= "{{tableName}}";

                using IDbCommand command = dbConnection.CreateCommand();
                command.CommandText = $"SELECT MAX({{quoteIdentLit(maxCol)}}) FROM {dbTableName}";

                object? result = command.ExecuteScalar();
                if (result is {{counterType}} value)
                {
                    _indexValue = value;
                }
                {{loadElse}}
                else
                {
                    // MAX(...) over an empty table is NULL, so ExecuteScalar returns DBNull and the
                    // counter resets to its default. Provider/SQL/connection errors are intentionally
                    // NOT caught here: a genuine failure must propagate so CreateTable/EnsureSchema
                    // cannot report success while the counter is silently wrong.
                    _indexValue = defaultValue;
                }
            }

            public static {{counterType}}? SelectMaxKey(IDbConnection dbConnection, string? dbTableName = null, {{counterType}} defaultValue = 0)
            {
                dbTableName ??= "{{tableName}}";

                using IDbCommand command = dbConnection.CreateCommand();
                command.CommandText = $"SELECT MAX({{quoteIdentLit(maxCol)}}) FROM {dbTableName}";

                object? result = command.ExecuteScalar();
                if (result is {{counterType}} value)
                {
                    return value;
                }
                {{selectElse}}

                return null;
            }
            """;
        }

        IEnumerable<string> getIndexLines()
        {
            // generate any indexes
            foreach (IndexInfo idx in model.Indexes)
            {
                string[] columns = [.. idx.Columns];

                if (columns.Length == 0)
                {
                    continue;
                }

                bool unique = idx.Unique;
                string? whereClause = idx.Where;

                bool hasWhere = !string.IsNullOrWhiteSpace(whereClause);

                // index names must stay distinct per (columns, uniqueness, predicate) so a plain
                // index and a partial/unique index on the same columns do not collide.
                string indexName = $"IDX_{{dbTableName}}_{(string.Join("_", columns))}";
                if (unique)
                {
                    indexName += "_U";
                }
                if (hasWhere)
                {
                    indexName += "_" + partialIndexSuffix(whereClause!);
                }

                string indexKind = unique ? "UNIQUE INDEX" : "INDEX";

                yield return "using (IDbCommand command = dbConnection.CreateCommand())";
                yield return "{";
                yield return "command.CommandText = $\"\"\"";
                yield return $"    CREATE {indexKind} IF NOT EXISTS \"{indexName}\" ON \"{{dbTableName}}\" (";
                yield return $"        {string.Join(", ", columns.Select(v => $"\"{v}\""))}";
                yield return hasWhere ? $"    ) WHERE {whereClause}" : "    )";
                yield return "    \"\"\";";
                yield return "command.ExecuteNonQuery();";
                yield return "}";
                yield return string.Empty;
            }
        }

        // FNV-1a 32-bit hash; deterministic across builds so partial-index names are stable.
        static string partialIndexSuffix(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in value)
                {
                    hash ^= c;
                    hash *= 16777619;
                }

                return hash.ToString("x8");
            }
        }

        // Class-level [LdgSQLiteUnique(cols...)] is emitted by CreateTable as a table-level UNIQUE
        // constraint, but CREATE TABLE IF NOT EXISTS is a no-op on a pre-existing table, so migrating
        // an older table via EnsureSchema would otherwise silently leave the composite UNIQUE
        // unenforced. Re-assert each composite UNIQUE as an equivalent CREATE UNIQUE INDEX IF NOT
        // EXISTS. Emitted only inside EnsureSchema (which owns the transaction); CreateTable already
        // carries the table constraint, so a table it creates fresh needs no extra index.
        IEnumerable<string> getEnsureUniqueConstraintIndexLines()
        {
            foreach (EquatableColumnSet uniqueSet in model.ClassUniqueColumnSets)
            {
                string[] columns = [.. uniqueSet.Columns];

                if (columns.Length == 0)
                {
                    continue;
                }

                string indexName = $"UQ_{{dbTableName}}_{string.Join("_", columns)}";

                yield return "using (IDbCommand command = dbConnection.CreateCommand())";
                yield return "{";
                yield return "if (transaction != null) command.Transaction = transaction;";
                yield return "command.CommandText = $\"\"\"";
                yield return $"    CREATE UNIQUE INDEX IF NOT EXISTS \"{indexName}\" ON \"{{dbTableName}}\" (";
                yield return $"        {string.Join(", ", columns.Select(v => $"\"{v}\""))}";
                yield return "    )";
                yield return "    \"\"\";";
                yield return "command.ExecuteNonQuery();";
                yield return "}";
                yield return string.Empty;
            }
        }

        // Additive-migration ALTER TABLE ADD COLUMN blocks for EnsureSchema. Each block adds a
        // column only when PRAGMA table_info reports it missing, so re-running EnsureSchema is
        // idempotent.
        IEnumerable<string> getEnsureAddColumnLines()
        {
            foreach ((string name, string addColumnDdl, string? migrationBlockReason) in alterAddColumns)
            {
                yield return $"if (!existingColumns.Contains(\"{name}\"))";
                yield return "{";
                if (migrationBlockReason != null)
                {
                    // Adding this column to an EMPTY table is harmless (no rows to strand as NULL);
                    // adding it to a POPULATED table would leave a schema the generated readers
                    // cannot hydrate, so fail fast with a column-specific migration error instead of
                    // returning success.
                    yield return "    using (IDbCommand command = dbConnection.CreateCommand())";
                    yield return "    {";
                    yield return "    if (transaction != null) command.Transaction = transaction;";
                    yield return "    command.CommandText = $\"SELECT EXISTS(SELECT 1 FROM {dbTableName})\";";
                    yield return "    if (global::System.Convert.ToInt64(command.ExecuteScalar()) != 0L)";
                    yield return "    {";
                    yield return $"        throw new global::System.InvalidOperationException($\"EnsureSchema cannot add column '{name}' to populated table '{{dbTableName}}' because {migrationBlockReason}. Migrate this table manually (for example, recreate it and copy the rows) before calling EnsureSchema.\");";
                    yield return "    }";
                    yield return "    }";
                }
                yield return "    using (IDbCommand command = dbConnection.CreateCommand())";
                yield return "    {";
                yield return "    if (transaction != null) command.Transaction = transaction;";
                yield return "    command.CommandText = $\"\"\"";
                yield return $"        ALTER TABLE {{dbTableName}} ADD COLUMN {addColumnDdl}";
                yield return "        \"\"\";";
                yield return "    command.ExecuteNonQuery();";
                yield return "    }";
                yield return "}";
                yield return string.Empty;
            }
        }

        IEnumerable<string> getFnFilterParams(bool includeNullFilter, bool allowKeysIn = false)
        {
            foreach (TableColInfoRec rec in tableColInfo)
            {
                yield return $"{rec.propType}? {rec.name} = null";

                switch (rec.propType)
                {
                    case "int":
                    case "long":
                    case "decimal":
                    case "double":
                        yield return $"string {rec.name}Operator = \"=\"";
                        break;
                }
                
                if (rec.isNullable)
                {
                    yield return $"bool? {rec.name}IsNull = null";
                }

                if (allowKeysIn && rec.isMultiSelect)
                {
                    yield return $"IEnumerable<{rec.propType}>? {rec.name}Values = null";
                    yield return $"IEnumerable<{rec.propType}>? {rec.name}NotInValues = null";
                }
            }
        }

        IEnumerable<string> getFnFilterArgs(bool includeNullFilter, bool allowKeysIn = false)
        {
            foreach (TableColInfoRec rec in tableColInfo)
            {
                yield return rec.name;

                switch (rec.propType)
                {
                    case "int":
                    case "long":
                    case "decimal":
                    case "double":
                        yield return $"{rec.name}Operator";
                        break;
                }

                if (rec.isNullable)
                {
                    yield return $"{rec.name}IsNull";
                }

                if (allowKeysIn && rec.isMultiSelect)
                {
                    yield return $"{rec.name}Values";
                    yield return $"{rec.name}NotInValues";
                }
            }
        }

        IEnumerable<string> getConditionLines(bool includeNullFilter, bool allowsOrJoining, bool allowsStringLike = false, bool allowKeysIn = false)
        {
            foreach (TableColInfoRec rec in tableColInfo)
            {
                yield return $"if ({rec.name} != null)";
                yield return "{";

                string conditionComparator = "=";
                switch (rec.propType)
                {
                    case "int":
                    case "long":
                    case "decimal":
                    case "double":
                        conditionComparator = $$$"""{getNumericOperator({{{rec.name}}}Operator)}""";
                        break;
                    case "string":
                        conditionComparator = allowsStringLike ? "{stringComparator}" : "=";
                        break;
                }
                
                if (allowsOrJoining)
                {
                    yield return $$$"""    command.CommandText += (addedCondition ? $" {joiner} " : " WHERE ") + $"{{{quoteIdentLit(rec.name)}}} {{{conditionComparator}}} ${{{rec.name}}}";""";
                }
                else
                {
                    yield return $$$"""    command.CommandText += (addedCondition ? " AND " : " WHERE ") + $"{{{quoteIdentLit(rec.name)}}} {{{conditionComparator}}} ${{{rec.name}}}";""";
                }
                
                yield return "    addedCondition = true;";
                yield return string.Empty;
                yield return $"    IDbDataParameter {rec.name}Param = command.CreateParameter();";
                yield return $"    {rec.name}Param.ParameterName = \"${rec.name}\";";

                if (rec.isEnum)
                {
                    yield return $"    {rec.name}Param.Value = {rec.name}.ToString();";
                }
                else if (rec.useJson)
                {
                    if (rec.isNullable)
                    {
                        yield return $"    {rec.name}Param.Value = TrySerializeForDb({rec.name}, out dbJson) ? dbJson : DBNull.Value;";
                    }
                    else
                    {
                        yield return $"    {rec.name}Param.Value = TrySerializeForDb({rec.name}, out dbJson) ? dbJson : string.Empty;";
                    }
                }
                else
                {
                    yield return $"    {rec.name}Param.Value = {rec.name};";
                }

                yield return $"    command.Parameters.Add({rec.name}Param);";
                yield return "}";
                yield return string.Empty;


                if (includeNullFilter && rec.isNullable)
                {
                    yield return $"if ({rec.name}IsNull == true)";
                    yield return "{";

                    if (allowsOrJoining)
                    {
                        yield return $$$"""    command.CommandText += (addedCondition ? $" {joiner} " : " WHERE ") + "{{{quoteIdentLit(rec.name)}}} IS NULL";""";
                    }
                    else
                    {
                        yield return $$$"""    command.CommandText += (addedCondition ? " AND " : " WHERE ") + "{{{quoteIdentLit(rec.name)}}} IS NULL";""";
                    }

                    yield return "    addedCondition = true;";
                    yield return "}";
                    yield return $"else if ({rec.name}IsNull == false)";
                    yield return "{";

                    if (allowsOrJoining)
                    {
                        yield return $$$"""    command.CommandText += (addedCondition ? $" {joiner} " : " WHERE ") + "{{{quoteIdentLit(rec.name)}}} IS NOT NULL";""";
                    }
                    else
                    {
                        yield return $$$"""    command.CommandText += (addedCondition ? " AND " : " WHERE ") + "{{{quoteIdentLit(rec.name)}}} IS NOT NULL";""";
                    }

                    yield return "    addedCondition = true;";
                    yield return "}";

                    yield return string.Empty;
                }

                if (allowKeysIn && rec.isMultiSelect)
                {
                    yield return $"if ({rec.name}Values is not null)";
                    yield return "{";
                    yield return $"    List<{rec.propType}> {rec.name}ValuesList = {rec.name}Values as List<{rec.propType}> ?? new List<{rec.propType}>({rec.name}Values);";
                    yield return $"    if ({rec.name}ValuesList.Count != 0)";
                    yield return "    {";

                    if (allowsOrJoining)
                    {
                        yield return $$$"""        command.CommandText += (addedCondition ? $" {joiner} " : " WHERE ") + $"{{{quoteIdentLit(rec.name)}}} IN ";""";
                    }
                    else
                    {
                        yield return $$$"""        command.CommandText += (addedCondition ? " AND " : " WHERE ") + $"{{{quoteIdentLit(rec.name)}}} IN ";""";
                    }

                    yield return "        addedCondition = true;";
                    yield return "        List<string> vParamNames = new();";
                    yield return string.Empty;

                    yield return $"        for (int vIndex = 0; vIndex < {rec.name}ValuesList.Count; vIndex++)";
                    yield return "        {";
                    yield return $$$"""            string vParamName = "{{{rec.name}}}Param" + vIndex.ToString();""";
                    yield return "            vParamNames.Add(\"$\" + vParamName);";
                    yield return string.Empty;
                    yield return "            IDbDataParameter vp = command.CreateParameter();";
                    yield return "            vp.ParameterName = \"$\" + vParamName;";
                    yield return $"            vp.Value = {rec.name}ValuesList[vIndex];";
                    yield return "            command.Parameters.Add(vp);";
                    yield return "        }";
                    yield return string.Empty;

                    yield return "        command.CommandText += \"(\" + string.Join(',', vParamNames) + \")\";";
                    yield return "    }";
                    yield return "}";

                    yield return string.Empty;

                    yield return $"if ({rec.name}NotInValues is not null)";
                    yield return "{";
                    yield return $"    List<{rec.propType}> {rec.name}NotInValuesList = {rec.name}NotInValues as List<{rec.propType}> ?? new List<{rec.propType}>({rec.name}NotInValues);";
                    yield return $"    if ({rec.name}NotInValuesList.Count != 0)";
                    yield return "    {";

                    if (allowsOrJoining)
                    {
                        yield return $$$"""        command.CommandText += (addedCondition ? $" {joiner} " : " WHERE ") + $"{{{quoteIdentLit(rec.name)}}} NOT IN ";""";
                    }
                    else
                    {
                        yield return $$$"""        command.CommandText += (addedCondition ? " AND " : " WHERE ") + $"{{{quoteIdentLit(rec.name)}}} NOT IN ";""";
                    }

                    yield return "        addedCondition = true;";
                    yield return "        List<string> vParamNames = new();";
                    yield return string.Empty;

                    yield return $"        for (int vIndex = 0; vIndex < {rec.name}NotInValuesList.Count; vIndex++)";
                    yield return "        {";
                    yield return $$$"""            string vParamName = "{{{rec.name}}}NotInParam" + vIndex.ToString();""";
                    yield return "            vParamNames.Add(\"$\" + vParamName);";
                    yield return string.Empty;
                    yield return "            IDbDataParameter vp = command.CreateParameter();";
                    yield return "            vp.ParameterName = \"$\" + vParamName;";
                    yield return $"            vp.Value = {rec.name}NotInValuesList[vIndex];";
                    yield return "            command.Parameters.Add(vp);";
                    yield return "        }";
                    yield return string.Empty;

                    yield return "        command.CommandText += \"(\" + string.Join(',', vParamNames) + \")\";";
                    yield return "    }";
                    yield return "}";
                }
            }
        }

        IEnumerable<string> getInsertCommandParamLines(
            bool singleValue,
            string? identityColName,
            string? identityColType,
            bool? createParameters = null,
            bool? instantiateParameters = null,
            bool? executeCommand = null,
            bool setIdentity = true,
            bool includeIdentity = false,
            bool identityOnly = false,
            bool primaryKeyOnly = false,
            bool skipRawDefaults = false,
            bool isInsert = true,
            bool byKeyMutation = false,
            string? ignoreDupeProperty = null)
        {
            // if no specific type is specified, default to true
            if ((createParameters == null) && (instantiateParameters == null) && (executeCommand == null))
            {
                createParameters = true;
                instantiateParameters = true;
                executeCommand = true;
            }

            foreach (TableColInfoRec rec in tableColInfo)
            {
                // InsertReturning lets the database compute raw/expression defaults, so their
                // columns are neither listed nor parameterized.
                if (skipRawDefaults && rawDefaultColumnNames.Contains(rec.name))
                {
                    continue;
                }

                // composite-key operations bind only the primary-key columns
                if (primaryKeyOnly)
                {
                    if (!rec.isPrimaryKey)
                    {
                        continue;
                    }
                }
                // do not insert identity key values
                else if (rec.isIdentity && !includeIdentity)
                {
                    continue;
                }
                else if (identityOnly && !rec.isIdentity)
                {
                    continue;
                }

                if (createParameters == true)
                {
                    yield return $"IDbDataParameter {rec.name}Param = command.CreateParameter();";

                    yield return $"{rec.name}Param.ParameterName = \"${rec.name}\";";
                    yield return $"command.Parameters.Add({rec.name}Param);";
                }
                
                if (instantiateParameters == true)
                {
                    if (rec.isNullable == true)
                    {
                        if (rec.isEnum)
                        {
                            yield return $"{rec.name}Param.Value = (value.{rec.name} == null) ? DBNull.Value : value.{rec.name}.ToString();";
                        }
                        else if (rec.useJson)
                        {
                            yield return $"{rec.name}Param.Value = TrySerializeForDb(value.{rec.name}, out dbJson) ? dbJson : DBNull.Value;";
                        }
                        else
                        {
                            yield return $"{rec.name}Param.Value = (value.{rec.name} == null) ? DBNull.Value : value.{rec.name};";
                        }
                    }
                    else
                    {
                        if (rec.isEnum)
                        {
                            yield return $"{rec.name}Param.Value = value.{rec.name}.ToString();";
                        }
                        else if (rec.useJson)
                        {
                            yield return $"{rec.name}Param.Value = TrySerializeForDb(value.{rec.name}, out dbJson) ? dbJson : string.Empty;";
                        }
                        else
                        {
                            yield return $"{rec.name}Param.Value = value.{rec.name};";
                        }
                    }
                }

                if (createParameters == true)
                {
                    // add an empty line between parameters
                    yield return string.Empty;
                }
            }

            if (executeCommand == true)
            {
                if ((identityColName == null) || (!setIdentity) || (!isInsert))
                {
                    if (byKeyMutation)
                    {
                        // By-key Update/Delete. A keyless model's predicate is "1 = 0" (never matches),
                        // so zero rows is expected and must not throw. A keyed model throws the typed
                        // exception on a stale/already-gone key unless the caller opts out per call.
                        if (isKeyless)
                        {
                            yield return "command.ExecuteNonQuery();";
                        }
                        else
                        {
                            yield return "int rowsAffected = command.ExecuteNonQuery();";
                            yield return $"if ((rowsAffected == 0) && throwOnZeroRowsAffected) throw new global::CsLightDbGen.SQLiteGenerator.{GeneratorAttributes._ldgCommandFailedException}(\"{className}\", command.CommandText);";
                        }
                    }
                    else if (ignoreDupeProperty == null)
                    {
                        yield return "int rowsAffected = command.ExecuteNonQuery();";
                        yield return $"if (rowsAffected == 0) throw new global::CsLightDbGen.SQLiteGenerator.{GeneratorAttributes._ldgCommandFailedException}(\"{className}\", command.CommandText);";
                    }
                    else
                    {
                        yield return "int rowsAffected = command.ExecuteNonQuery();";
                        yield return $"if (!{ignoreDupeProperty} && (rowsAffected == 0)) throw new global::CsLightDbGen.SQLiteGenerator.{GeneratorAttributes._ldgCommandFailedException}(\"{className}\", command.CommandText);";
                    }
                }
                else
                {
                    if (ignoreDupeProperty == null)
                    {
                        yield return "object? commandResult = command.ExecuteScalar();";
                        yield return $"if (commandResult == null) throw new global::CsLightDbGen.SQLiteGenerator.{GeneratorAttributes._ldgCommandFailedException}(\"{className}\", command.CommandText);";

                        switch (identityColType)
                        {
                            case "int":
                                yield return $"value.{identityColName} = Convert.ToInt32(commandResult);";
                                break;
                            case "long":
                                yield return $"value.{identityColName} = Convert.ToInt64(commandResult);";
                                break;
                            default:
                                yield return $"value.{identityColName} = ({identityColType})commandResult;";
                                break;
                        }
                    }
                    else
                    {
                        yield return "object? commandResult = command.ExecuteScalar();";
                        yield return $"if (!{ignoreDupeProperty} && (commandResult == null)) throw new global::CsLightDbGen.SQLiteGenerator.{GeneratorAttributes._ldgCommandFailedException}(\"{className}\", command.CommandText);";

                        switch (identityColType)
                        {
                            case "int":
                                yield return $"if (commandResult != null) value.{identityColName} = Convert.ToInt32(commandResult);";
                                break;
                            case "long":
                                yield return $"if (commandResult != null) value.{identityColName} = Convert.ToInt64(commandResult);";
                                break;
                            default:
                                yield return $"if (commandResult != null) value.{identityColName} = ({identityColType})commandResult;";
                                break;
                        }
                    }
                }
            }
        }
    }

    private static void emitFts(FtsModel model, SourceProductionContext context)
    {
        string className = model.ClassName;
        string? classNamespace = model.ClassNamespace;
        LdGenCategory genCategory = model.GenCategory;
        string sourceTableName = model.SourceTableName;
        string tableName = model.TableName;
        string? tokenizer = model.Tokenizer;

        List<string> createColLines = [];
        List<string> createForeignKeyLines = [];
        List<TableColInfoRec> tableColInfo = [];
        bool anyColIsJson = false;

        foreach (ColumnInput col in model.Columns)
        {
            string propName = col.Name;
            string propTypeName = col.TypeName;
            bool nullable = col.Nullable;

            bool isUnique = col.IsUnique;
            bool isUnindexed = col.IsUnindexed;

            bool memberIsEnum = col.IsEnum;
            string? enumTypeName = col.EnumTypeName;
            bool memberIsNonScalar = col.IsNonScalar;
            string? jsonTypeName = col.JsonTypeName;

            bool useJson = !memberIsEnum && !_sqliteTypeMap.ContainsKey(propTypeName);

            // A value type with no scalar mapping cannot be persisted (JSON helpers are
            // reference-type-only). Report CSLDG001 and skip the column.
            if (useJson && col.IsValueType)
            {
                GeneratorDiagnostics.Report(context, GeneratorDiagnostics.UnmappedValueTypeColumn, col.Location?.ToLocation(), propName, className, propTypeName);
                continue;
            }

            // add our column line
            if (isUnindexed)
            {
                createColLines.Add(quoteIdent(propName) + " UNINDEXED");
            }
            else
            {
                createColLines.Add(quoteIdent(propName));
            }

            // create the select retrieval pair
            if (nullable && _sqliteNullableReadDirectives.TryGetValue(propTypeName, out string? readFormat))
            {
                tableColInfo.Add(new(
                    propName,
                    propTypeName,
                    string.Format(readFormat.Remove(0, 6), propName, "reader", tableColInfo.Count),
                    string.Format(readFormat, propName, "reader", tableColInfo.Count),
                    false,
                    false,
                    nullable,
                    memberIsEnum,
                    useJson,
                    memberIsNonScalar,
                    isUnique));
            }
            else if (!nullable && _sqliteReadDirectives.TryGetValue(propTypeName, out readFormat))
            {
                tableColInfo.Add(new(
                    propName,
                    propTypeName,
                    string.Format(readFormat.Remove(0, 6), propName, "reader", tableColInfo.Count),
                    string.Format(readFormat, propName, "reader", tableColInfo.Count),
                    false,
                    false,
                    nullable,
                    memberIsEnum,
                    useJson,
                    memberIsNonScalar,
                    isUnique));
            }
            else if (memberIsEnum)
            {
                //// build the reader directive for the enum type
                //string ef = $"Enum.TryParse(reader.GetString({tableColInfo.Count}), out {propName});";

                tableColInfo.Add(new(
                    propName,
                    propTypeName,
                    nullable
                        ? string.Format(_sqliteNullableReadDirectives["enum"].Remove(0, 6), propName, "reader", tableColInfo.Count, enumTypeName)
                        : string.Format(_sqliteReadDirectives["enum"].Remove(0, 6), propName, "reader", tableColInfo.Count, enumTypeName),
                    nullable
                        ? string.Format(_sqliteNullableReadDirectives["enum"], propName, "reader", tableColInfo.Count, enumTypeName)
                        : string.Format(_sqliteReadDirectives["enum"], propName, "reader", tableColInfo.Count, enumTypeName),
                    false,
                    false,
                    nullable,
                    memberIsEnum,
                    useJson,
                    memberIsNonScalar,
                    isUnique));
            }
            else if (memberIsNonScalar)
            {
                anyColIsJson = true;

                string jsonArrKey = col.IsArray ? "JSON[]array" : "JSON[]";

                tableColInfo.Add(new(
                    propName,
                    propTypeName,
                    nullable
                        ? string.Format(_sqliteNullableReadDirectives[jsonArrKey].Remove(0, 6), propName, "reader", tableColInfo.Count, jsonTypeName)
                        : string.Format(_sqliteReadDirectives[jsonArrKey].Remove(0, 6), propName, "reader", tableColInfo.Count, jsonTypeName),
                    nullable
                        ? string.Format(_sqliteNullableReadDirectives[jsonArrKey], propName, "reader", tableColInfo.Count, jsonTypeName)
                        : string.Format(_sqliteReadDirectives[jsonArrKey], propName, "reader", tableColInfo.Count, jsonTypeName),
                    false,
                    false,
                    nullable,
                    memberIsEnum,
                    useJson,
                    memberIsNonScalar,
                    isUnique));
            }
            else
            {
                anyColIsJson = true;

                tableColInfo.Add(new(
                    propName,
                    propTypeName,
                    nullable
                        ? string.Format(_sqliteNullableReadDirectives["JSON"].Remove(0, 6), propName, "reader", tableColInfo.Count, jsonTypeName)
                        : string.Format(_sqliteReadDirectives["JSON"].Remove(0, 6), propName, "reader", tableColInfo.Count, jsonTypeName),
                    nullable
                        ? string.Format(_sqliteNullableReadDirectives["JSON"], propName, "reader", tableColInfo.Count, jsonTypeName)
                        : string.Format(_sqliteReadDirectives["JSON"], propName, "reader", tableColInfo.Count, jsonTypeName),
                    false,
                    false,
                    nullable,
                    memberIsEnum,
                    useJson,
                    memberIsNonScalar,
                    isUnique));
            }
        }

        string hintName = string.IsNullOrEmpty(classNamespace)
            ? $"{className}.Fts.g.cs"
            : $"{classNamespace}.{className}.Fts.g.cs";

        context.AddSource(
            hintName,
            SourceText.From($$$""""
                    //------------------------------------------------------------------------------
                    // <auto-generated>
                    //     This code was generated by a tool.
                    //
                    //     Changes to this file may cause incorrect behavior and will be lost if
                    //     the code is regenerated.
                    // </auto-generated>
                    //------------------------------------------------------------------------------

                    #nullable enable

                    using System;
                    using System.Collections.Generic;
                    using System.Data;
                    using System.Diagnostics.CodeAnalysis;
                    using System.Text;
                    using System.Text.Json;
                    using System.Threading;
                                    
                    namespace {{{classNamespace}}};
                
                    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
                    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
                     public partial {{{decForGenCategory(genCategory)}}} {{{className}}}
                     {
                         public static string DefaultTableName => "{{{tableName}}}";
                         public static string SourceTableName => "{{{sourceTableName}}}";
                         private static readonly HashSet<string> _sqliteColumnNames = new HashSet<string>(global::System.StringComparer.OrdinalIgnoreCase)
                         {
                             {{{string.Join(_comma_line_5, tableColInfo.Select(c => $"\"{c.name}\""))}}}
                         };

                         public static IReadOnlyCollection<string> SQLiteColumnNames { get; } = _sqliteColumnNames;

                         private static string quoteRuntimeIdent(string ident) => "\"" + ident.Replace("\"", "\"\"") + "\"";

                         {{{emitResolveOrderByPropertiesMember()}}}
                     
                         public static bool CreateTable(IDbConnection dbConnection, string? dbTableName = null)
                         {
                             dbTableName ??= "{{{tableName}}}";

                            using IDbCommand command = dbConnection.CreateCommand();
                            command.CommandText = $"""
                                CREATE VIRTUAL TABLE IF NOT EXISTS {dbTableName} using fts5 (
                                    {{{string.Join(_comma_line_4, createColLines)}}}{{{(tokenizer != null ? $",\n                tokenize='{tokenizer}'" : "")}}}
                                )
                                """;

                            command.ExecuteNonQuery();
                    
                            return true;
                        }

                        public static bool DropTable(IDbConnection dbConnection, string? dbTableName = null)
                        {
                            dbTableName ??= "{{{tableName}}}";
                    
                            using IDbCommand command = dbConnection.CreateCommand();
                            command.CommandText = $"DROP TABLE IF EXISTS {dbTableName}";
                    
                            command.ExecuteNonQuery();
                    
                            return true;
                        }

                        public static int Populate(
                            IDbConnection dbConnection,
                            string? dbTableName = null,
                            string? sourceTableName = null,
                            bool sanitizeText = false,
                            IDbTransaction? transaction = null)
                        {
                            dbTableName ??= "{{{tableName}}}";
                            sourceTableName ??= "{{{sourceTableName}}}";
                            int rowsAffected = 0;

                            if (sanitizeText == false)
                            {
                                using IDbCommand popCommand = dbConnection.CreateCommand();
                                if (transaction != null) popCommand.Transaction = transaction;
                                popCommand.CommandText = $"""
                                    INSERT INTO {dbTableName} ({{{string.Join(", ", tableColInfo.Select(c => quoteIdent(c.name)))}}})
                                    SELECT {{{string.Join(", ", tableColInfo.Select(c => quoteIdent(c.name)))}}} FROM {sourceTableName}
                                    """;
                                rowsAffected = popCommand.ExecuteNonQuery();

                                return rowsAffected;
                            }

                            bool _ownTxn = transaction is null;
                            IDbTransaction _txn = transaction ?? dbConnection.BeginTransaction();
                            try
                            {
                                using IDbCommand readCommand = dbConnection.CreateCommand();
                                readCommand.Transaction = _txn;
                                readCommand.CommandText = $"""
                                    SELECT {{{string.Join(", ", tableColInfo.Select(c => quoteIdent(c.name)))}}} FROM {sourceTableName}
                                    """;

                                using (IDataReader reader = readCommand.ExecuteReader())
                                {
                                using IDbCommand command = dbConnection.CreateCommand();
                                command.Transaction = _txn;
                                command.CommandText = $"""
                                    INSERT INTO {dbTableName} ({{{string.Join(", ", tableColInfo.Select(c => quoteIdent(c.name)))}}})
                                    VALUES ({{{string.Join(", ", tableColInfo.Select(c => $"${c.name}"))}}})
                                    """;

                                {{{string.Join(_line_3, getPopulateCleanLines(addValue: false))}}}
                                        
                                command.Prepare();
                                
                                while (reader.Read())
                                {
                                    {{{string.Join(_line_4, getPopulateCleanLines(addParam: false))}}}

                                    int ra = command.ExecuteNonQuery();
                                    if (ra == 0) throw new global::CsLightDbGen.SQLiteGenerator.{{{GeneratorAttributes._ldgCommandFailedException}}}("{{{className}}}", command.CommandText);
                                    rowsAffected += ra;
                                }

                                }
                                if (_ownTxn) _txn.Commit();
                            }
                            finally
                            {
                                if (_ownTxn) _txn.Dispose();
                            }

                            return rowsAffected;
                        }

                        public static List<{{{className}}}> Select(
                            IDbConnection dbConnection,
                            List<string> matchTerms,
                            string? dbTableName = null,
                            string[]? orderByProperties = null,
                            string? orderByDirection = null, IDbTransaction? transaction = null, string[]? orderByDirections = null)
                        {
                             dbTableName ??= "{{{tableName}}}";
 
                             List<{{{className}}}> results = new();
                             string[]? resolvedOrderByProperties = ResolveOrderByProperties(orderByProperties, orderByDirections, orderByDirection);
 
                             using IDbCommand command = dbConnection.CreateCommand();
                             if (transaction != null) command.Transaction = transaction;
                             command.CommandText = $"SELECT {{{string.Join(", ", tableColInfo.Select(p => quoteIdentLit(p.name)))}}} FROM {dbTableName}";
                    
                            bool addedCondition = false;
                            int index = 0;
                            foreach (string mt in matchTerms)
                            {
                                if (!string.IsNullOrWhiteSpace(mt))
                                {
                                    command.CommandText += (addedCondition ? " AND " : " WHERE ") + $" {dbTableName} MATCH $matchTerm{index}";
                                    addedCondition = true;
                                    IDbDataParameter matchParam = command.CreateParameter();
                                    matchParam.ParameterName = $"$matchTerm{index}";
                                    matchParam.Value = mt;
                                    command.Parameters.Add(matchParam);
                                    index++;
                                }
                            }
                    
                             if ((resolvedOrderByProperties != null) && (resolvedOrderByProperties.Length > 0))
                             {
                                 command.CommandText += $" ORDER BY {string.Join(", ", resolvedOrderByProperties)}";
                            }
                    
                            using (IDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    results.Add(new()
                                    {
                                        {{{string.Join(_comma_line_5, tableColInfo.Select(p => p.readerDirective))}}}
                                    });
                                }
                            }

                            return results;
                        }

                        public static int SelectCount(
                            IDbConnection dbConnection, 
                            List<string> matchTerms,
                            string? dbTableName = null,
                            IDbTransaction? transaction = null)
                        {
                            dbTableName ??= "{{{tableName}}}";
                    
                            using IDbCommand command = dbConnection.CreateCommand();
                            if (transaction != null) command.Transaction = transaction;
                            command.CommandText = $"SELECT COUNT(*) FROM {dbTableName}";
                    
                            bool addedCondition = false;
                            int index = 0;
                            foreach (string mt in matchTerms)
                            {
                                if (!string.IsNullOrWhiteSpace(mt))
                                {
                                    command.CommandText += (addedCondition ? " AND " : " WHERE ") + $" {dbTableName} MATCH $matchTerm{index}";
                                    addedCondition = true;
                                    IDbDataParameter matchParam = command.CreateParameter();
                                    matchParam.ParameterName = $"$matchTerm{index}";
                                    matchParam.Value = mt;
                                    command.Parameters.Add(matchParam);
                                    index++;
                                }
                            }
                    
                            object? result = command.ExecuteScalar();
                            if (result is int value)
                            {
                                return value;
                            }
                            else if (result is long l)
                            {
                                return Convert.ToInt32(l);
                            }

                            return -1;
                        }

                        {{{emitHtmlStripRegexField()}}}

                        {{{emitStripHtmlMethod()}}}

                        {{{(anyColIsJson ? emitJsonHelperMembers() : string.Empty)}}}
                    }

                    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
                    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
                    public static class {{{className}}}Extensions
                    {
                        public static List<{{{className}}}> Select<T>(this IDbConnection dbCon, List<string> matchTerms, string? dbTableName = null, string[]? orderByProperties = null, string? orderByDirection = null, IDbTransaction? transaction = null, string[]? orderByDirections = null)
                            where T : {{{className}}}
                        {
                            return {{{className}}}.Select(dbCon, matchTerms, dbTableName, orderByProperties, orderByDirection, transaction, orderByDirections);
                        }

                        public static int SelectCount<T>(this IDbConnection dbCon, List<string> matchTerms, string? dbTableName = null, IDbTransaction? transaction = null)
                            where T : {{{className}}}
                        {
                            return {{{className}}}.SelectCount(dbCon, matchTerms, dbTableName, transaction);
                        }
                    }

                    #nullable restore
                    """"
            , Encoding.UTF8)
        );

        return;

        string decForGenCategory(LdGenCategory genCategory) => genCategory switch
        {
            LdGenCategory.Class => "class",
            LdGenCategory.Record => "record class",
            _ => "class",
        };

        IEnumerable<string> getPopulateCleanLines(bool addParam = true, bool addValue = true)
        {
            foreach ((TableColInfoRec rec, int index) in tableColInfo.Select((p, i) => (p, i)))
            {
                if (addParam)
                {
                    yield return $"IDbDataParameter {rec.name}Param = command.CreateParameter();";
                    yield return $"{rec.name}Param.ParameterName = \"${rec.name}\";";
                }

                int assignmentIndex = rec.readerDirective.IndexOf('=');
                string assignment = (assignmentIndex == -1)
                    ? $"{rec.name}Param.Value = {rec.readerDirective}"
                    : $"{rec.name}Param.Value {rec.readerDirective.Substring(assignmentIndex)}";

                // check for strings we want to sanitize
                if (rec.propType.StartsWith("string", StringComparison.OrdinalIgnoreCase))
                {
                    assignment = (assignmentIndex == -1)
                        ? $"{rec.name}Param.Value = StripHtml({rec.readerDirective})"
                        : $"{rec.name}Param.Value = StripHtml({rec.readerDirective.Substring(assignmentIndex + 2)})";
                }

                if (addValue)
                {
                    // if it's nullable, we need to check for null first
                    if (rec.isNullable)
                    {
                        yield return $"if (reader.IsDBNull({index}))";
                        yield return "{";
                        yield return $"    {rec.name}Param.Value = DBNull.Value;";
                        yield return "}";
                        yield return "else";
                        yield return "{";
                        yield return $"    {assignment};";
                        yield return "}";
                    }
                    else
                    {
                        yield return $"{assignment};";
                    }
                }

                if (addParam)
                {
                    yield return $"command.Parameters.Add({rec.name}Param);";
                    yield return string.Empty;
                }
            }
        }
    }

    /// <summary>
    /// Resolves an <c>LdgSQLiteFkAction</c> referential action from a boxed enum value
    /// (as delivered by <c>AttributeData.ConstructorArguments</c>, where enum args arrive as ints).
    /// </summary>
    private static string fkActionFromValue(object? value)
    {
        int ordinal = value is int i ? i : 0;
        return ordinal switch
        {
            1 => "RESTRICT",
            2 => "SET NULL",
            3 => "SET DEFAULT",
            4 => "CASCADE",
            _ => "NO ACTION",
        };
    }

    /// <summary>
    /// Renders a SQLite column DEFAULT clause from an <c>[LdgSQLiteDefault]</c> attribute argument.
    /// </summary>
    /// <remarks>
    /// Reads directly from syntax to avoid boxing/enum-conversion hazards. String literals are
    /// SQL single-quoted (embedded quotes doubled) unless <paramref name="raw"/> is set, in which
    /// case the unquoted text is emitted verbatim (e.g. <c>CURRENT_TIMESTAMP</c>). Booleans map to
    /// <c>1</c>/<c>0</c>; numeric and other literals emit their source text. A null/absent value
    /// yields no clause.
    /// </remarks>
    private static string formatDefault(ExpressionSyntax? expr, bool raw)
    {
        if (expr is null)
        {
            return string.Empty;
        }

        if (expr is LiteralExpressionSyntax lit)
        {
            object? val = lit.Token.Value;

            if (val is null)
            {
                return string.Empty;
            }

            if (val is string s)
            {
                return raw ? $" DEFAULT {s}" : $" DEFAULT '{s.Replace("'", "''")}'";
            }

            if (val is bool b)
            {
                return b ? " DEFAULT 1" : " DEFAULT 0";
            }

            return $" DEFAULT {lit.Token.Text}";
        }

        return $" DEFAULT {expr.ToString()}";
    }

    /// <summary>
    /// Gets the SQL type for a given type.
    /// </summary>
    /// <remarks>
    /// Explicitly fetch the 'enum' type for anything that is an enum so we don't have to worry about indexing *all* the various enum types
    /// </remarks>
    private static string getSqlType(string type, bool isEnum = false, bool useJson = false, bool isArray = false)
    {
        if (isEnum)
        {
            return _sqliteTypeMap["enum"];
        }

        if (useJson)
        {
            return isArray
                ? _sqliteTypeMap["JSON[]"]
                : _sqliteTypeMap["JSON"];
        }

        return _sqliteTypeMap.TryGetValue(type, out string? name) ? name : "TEXT";
    }

    // Double-quotes a SQL identifier (column name) so reserved words (e.g. Order, Group) and other
    // otherwise-unparseable identifiers are emitted safely. Table names are trusted identifiers and
    // are not routed through this helper (see docs/api-contracts.md).
    // Builds the "(col, ...) VALUES ($col, ...)" fragment of an INSERT, or "DEFAULT VALUES" when
    // there are no columns to insert (e.g. an identity-only model). SQLite rejects an empty
    // "() VALUES ()" list, so DEFAULT VALUES is the valid spelling for inserting an all-defaults row.
    private static string buildInsertColumnsAndValues(IEnumerable<TableColInfoRec> columns)
    {
        List<TableColInfoRec> cols = columns.ToList();
        if (cols.Count == 0)
        {
            return "DEFAULT VALUES";
        }

        return "(" + string.Join(", ", cols.Select(c => quoteIdent(c.name)))
            + ") VALUES (" + string.Join(", ", cols.Select(c => "$" + c.name)) + ")";
    }

    private static string quoteIdent(string ident) => "\"" + ident + "\"";

    // Column-identifier quoting for emission INTO a normal (non-raw) C# interpolated
    // string literal ($"..."). The surrounding literal is delimited by ", so the SQL
    // double-quotes must be C#-escaped (\") or they terminate the emitted string.
    // Use quoteIdent (bare ") only when the emission target is a raw string literal ($"""...""").
    private static string quoteIdentLit(string ident) => "\\\"" + ident + "\\\"";

    // === E2: single-source emitted members shared by the table (`emit`) and FTS (`emitFts`) paths ===
    // Each returns C# member text emitted VERBATIM into BOTH generated partials via a {{{ ... }}}
    // interpolation. Keeping one source here removes the mirror-bug risk of hand-maintaining
    // byte-identical copies in the two code paths. These members carry no per-model values, so the
    // text is a plain (non-interpolated) raw string.
    private static string emitResolveOrderByPropertiesMember() => """
        private static string[]? ResolveOrderByProperties(string[]? orderByProperties, string[]? orderByDirections, string? orderByDirection)
        {
            if ((orderByProperties == null) || (orderByProperties.Length == 0))
            {
                return null;
            }

            bool defaultDescending = orderByDirection?.StartsWith("d", StringComparison.OrdinalIgnoreCase) == true;

            List<string> resolvedOrderByProperties = new(orderByProperties.Length);
            for (int orderByIndex = 0; orderByIndex < orderByProperties.Length; orderByIndex++)
            {
                string orderByProperty = orderByProperties[orderByIndex];
                if (string.IsNullOrWhiteSpace(orderByProperty) || !_sqliteColumnNames.Contains(orderByProperty))
                {
                    continue;
                }

                bool descending = defaultDescending;
                if ((orderByDirections != null) && (orderByIndex < orderByDirections.Length) && !string.IsNullOrWhiteSpace(orderByDirections[orderByIndex]))
                {
                    descending = orderByDirections[orderByIndex].StartsWith("d", StringComparison.OrdinalIgnoreCase);
                }

                resolvedOrderByProperties.Add(quoteRuntimeIdent(orderByProperty) + (descending ? " DESC" : " ASC"));
            }

            return resolvedOrderByProperties.Count > 0
                ? resolvedOrderByProperties.ToArray()
                : null;
        }
        """;

    // Unified to `readonly` (the FTS path already emitted it readonly; the table path did not). A
    // compiled Regex is never reassigned, so this is behavior-preserving and pre-empts F2's readonly
    // request.
    private static string emitHtmlStripRegexField() => """
        private static readonly System.Text.RegularExpressions.Regex _htmlStripRegex = new("<.*?>", System.Text.RegularExpressions.RegexOptions.Compiled);
        """;

    private static string emitStripHtmlMethod() => """
        private static string StripHtml(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            return _htmlStripRegex.Replace(input, string.Empty).Trim();
        }
        """;

    // JSON (de)serialization helpers, emitted only for models that actually carry a JSON column
    // (anyColIsJson). The per-column serialize/parse call sites are gated on the same condition, so a
    // model with no JSON column never references these and they are omitted from its output.
    private static string emitJsonHelperMembers() => """
        private static readonly JsonSerializerOptions _options = new()
        {
            WriteIndented = false,
        };

        private static bool TrySerializeForDb<T>(T? instance, [NotNullWhen(true)]out string? json) where T : class
        {
            if (instance == null)
            {
                json = null;
                return false;
            }

            json = JsonSerializer.Serialize(instance, _options);
            return true;
        }

        private static bool TrySerializeForDb<T>(List<T>? instances, [NotNullWhen(true)] out string? json)
        {
            if ((instances == null) || (instances.Count == 0))
            {
                json = null;
                return false;
            }

            json = JsonSerializer.Serialize(instances, _options);
            return true;
        }

        private static T? ParseFromDb<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<T>(json, _options);
        }

        private static List<T> ParseArrayFromDb<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            return JsonSerializer.Deserialize<List<T>>(json, _options) ?? [];
        }
        """;

    private static Dictionary<string, string> _sqliteReadDirectives = new()
    {
        { "bool", "{0} = {1}.GetBoolean({2})" },
        { "byte", "{0} = {1}.GetByte({2})" },
        { "byte[]", "{0} = (byte[]){1}.GetValue({2})" },
        { "char", "{0} = {1}.GetChar({2})" },
        { "char[]", "{0} = {1}.GetString({2}).ToCharArray()" },
        { "DateTime", "{0} = {1}.GetDateTime({2})" },
        { "DateTimeOffset", "{0} = global::System.DateTimeOffset.Parse({1}.GetString({2}), global::System.Globalization.CultureInfo.InvariantCulture)" },
        { "decimal", "{0} = {1}.GetDecimal({2})" },
        { "double", "{0} = {1}.GetDouble({2})" },
        { "enum", "{0} = Enum.Parse<{3}>({1}.GetString({2}))" },
        { "float", "{0} = {1}.GetFloat({2})" },
        { "Guid", "{0} = {1}.GetGuid({2})" },
        { "short", "{0} = {1}.GetInt16({2})" },
        { "int", "{0} = {1}.GetInt32({2})" },
        { "long", "{0} = {1}.GetInt64({2})" },
        { "sbyte", "{0} = (sbyte){1}.GetByte({2})" },
        { "string", "{0} = {1}.GetString({2})" },
        { "TimeSpan", "{0} = TimeSpan.Parse({1}.GetString({2}))" },
        { "ushort", "{0} = (ushort){1}.GetInt16({2})" },
        { "uint", "{0} = (uint){1}.GetInt32({2})" },
        { "ulong", "{0} = (ulong){1}.GetInt64({2})" },
        { "Uri", "{0} = new Uri({1}.GetString({2}))" },
        { "JSON", "{0} = ParseFromDb<{3}>({1}.GetString({2})) ?? new {3}()" },
        { "JSON[]", "{0} = ParseArrayFromDb<{3}>({1}.GetString({2})) ?? new List<{3}>()" },
        { "JSON[]array", "{0} = ParseArrayFromDb<{3}>({1}.GetString({2})).ToArray()" },
    };

    private static Dictionary<string, string> _sqliteNullableReadDirectives = new()
    {
        { "bool", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetBoolean({2})" },
        { "byte", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetByte({2})" },
        { "byte[]", "{0} = {1}.IsDBNull({2}) ? null : (byte[]){1}.GetValue({2})" },
        { "char", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetChar({2})" },
        { "char[]", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetString({2}).ToCharArray()" },
        { "DateTime", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetDateTime({2})" },
        { "DateTimeOffset", "{0} = {1}.IsDBNull({2}) ? null : global::System.DateTimeOffset.Parse({1}.GetString({2}), global::System.Globalization.CultureInfo.InvariantCulture)" },
        { "decimal", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetDecimal({2})" },
        { "double", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetDouble({2})" },
        { "enum", "{0} = {1}.IsDBNull({2}) ? null : Enum.Parse<{3}>({1}.GetString({2}))" },
        { "float", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetFloat({2})" },
        { "Guid", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetGuid({2})" },
        { "short", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetInt16({2})" },
        { "int", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetInt32({2})" },
        { "long", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetInt64({2})" },
        { "sbyte", "{0} = {1}.IsDBNull({2}) ? null : (sbyte){1}.GetByte({2})" },
        { "string", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetString({2})" },
        { "TimeSpan", "{0} = {1}.IsDBNull({2}) ? null : TimeSpan.Parse({1}.GetString({2}))" },
        { "ushort", "{0} = {1}.IsDBNull({2}) ? null : (ushort){1}.GetInt16({2})" },
        { "uint", "{0} = {1}.IsDBNull({2}) ? null : (uint){1}.GetInt32({2})" },
        { "ulong", "{0} = {1}.IsDBNull({2}) ? null : (ulong){1}.GetInt64({2})" },
        { "Uri", "{0} = {1}.IsDBNull({2}) ? null : new Uri({1}.GetString({2}))" },
        { "JSON", "{0} = {1}.IsDBNull({2}) ? null : ParseFromDb<{3}>({1}.GetString({2}))" },
        { "JSON[]", "{0} = {1}.IsDBNull({2}) ? null : ParseArrayFromDb<{3}>({1}.GetString({2}))" },
        { "JSON[]array", "{0} = {1}.IsDBNull({2}) ? null : ParseArrayFromDb<{3}>({1}.GetString({2})).ToArray()" },
    };

    // Mapping pulled from https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/types
    private static Dictionary<string, string> _sqliteTypeMap = new()
    {
        { "bool", "INTEGER" },
        { "byte", "INTEGER" },
        { "byte[]", "BLOB" },
        { "char", "TEXT" },
        { "char[]", "TEXT" },
        { "DateTime", "TEXT" },
        { "DateTimeOffset", "TEXT" },
        { "decimal", "TEXT" },
        { "double", "REAL" },
        { "enum", "TEXT" },
        { "float", "REAL" },
        { "Guid", "TEXT" },
        { "short", "INTEGER" },
        { "int", "INTEGER" },
        { "long", "INTEGER" },
        { "sbyte", "INTEGER" },
        { "string", "TEXT" },
        { "TimeSpan", "TEXT" },
        { "ushort", "INTEGER" },
        { "uint", "INTEGER" },
        { "ulong", "INTEGER" },
        { "Uri", "TEXT" },
        { "JSON", "TEXT" },
        { "JSON[]", "TEXT" },
    };

}
