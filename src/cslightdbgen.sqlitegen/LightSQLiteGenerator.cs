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

    private enum LdGenCategory
    {
        Class,
        Record,
    }

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

        IncrementalValuesProvider<ClassDeclarationSyntax> cDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetClassDec(s),
                transform: static (ctx, _) => GetClassTargetForGeneration(ctx));

        IncrementalValueProvider<(Compilation, ImmutableArray<ClassDeclarationSyntax>)> ivpClasses =
                context.CompilationProvider.Combine(cDeclarations.Collect());

        context.RegisterSourceOutput(ivpClasses, (spc, source) => Execute(source.Item1, source.Item2, spc));


        IncrementalValuesProvider<RecordDeclarationSyntax> rDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetRecordDec(s),
                transform: static (ctx, _) => GetRecordTargetForGeneration(ctx));

        IncrementalValueProvider<(Compilation, ImmutableArray<RecordDeclarationSyntax>)> ivpRecords =
                context.CompilationProvider.Combine(rDeclarations.Collect());

        context.RegisterSourceOutput(ivpRecords, (spc, source) => Execute(source.Item1, source.Item2, spc));
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


    /// <summary>
    /// Retrieves the target class declaration syntax for generation.
    /// </summary>
    /// <param name="context">The generator syntax context.</param>
    /// <returns>The class declaration syntax node.</returns>
    public static ClassDeclarationSyntax GetClassTargetForGeneration(GeneratorSyntaxContext context)
    {
        ClassDeclarationSyntax cDeclarationSyntax = (ClassDeclarationSyntax)context.Node;
        return cDeclarationSyntax;
    }

    /// <summary>
    /// Retrieves the target class declaration syntax for generation.
    /// </summary>
    /// <param name="context">The generator syntax context.</param>
    /// <returns>The record declaration syntax node.</returns>
    public static RecordDeclarationSyntax GetRecordTargetForGeneration(GeneratorSyntaxContext context)
    {
        RecordDeclarationSyntax rDeclarationSyntax = (RecordDeclarationSyntax)context.Node;
        return rDeclarationSyntax;
    }

    public void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes, SourceProductionContext context)
    {
        ILookup<string, ClassDeclarationSyntax> classLookup = classes.ToLookup(c => c.Identifier.Text);

        foreach (ClassDeclarationSyntax classSyntax in classes.Where(c => c.AttributeLists.Any(al => al.Attributes.Any(a => a.Name.ToString() == GeneratorAttributes._ldgSQLiteTable))))
        {
            // Converting the class to a semantic model to access much more meaningful data.
            SemanticModel model = compilation.GetSemanticModel(classSyntax.SyntaxTree);

            // Parse to declared symbol, so you can access each part of code separately,
            // such as interfaces, methods, members, contructor parameters etc.
            ISymbol? symbol = model.GetDeclaredSymbol(classSyntax);

            if (symbol == null)
            {
                continue;
            }

            List<MemberDeclarationSyntax> members = [];

            string? btn = ((INamedTypeSymbol)symbol).BaseType?.Name;
            while (btn != null)
            {
                ClassDeclarationSyntax? btcs = classLookup[btn].FirstOrDefault();
                if (btcs == null)
                {
                    break;
                }

                members.AddRange(btcs.Members);
                btn = compilation.GetSemanticModel(btcs.SyntaxTree).GetDeclaredSymbol(btcs) is INamedTypeSymbol ints
                    ? ints.BaseType?.Name
                    : null;
            }

            members.AddRange(classSyntax.Members);

            execute(
                compilation,
                symbol,
                members,
                context,
                LdGenCategory.Class);
        }

        foreach (ClassDeclarationSyntax classSyntax in classes.Where(c => c.AttributeLists.Any(al => al.Attributes.Any(a => a.Name.ToString() == GeneratorAttributes._ldgSQLiteFtsTable))))
        {
            // Converting the class to a semantic model to access much more meaningful data.
            SemanticModel model = compilation.GetSemanticModel(classSyntax.SyntaxTree);

            // Parse to declared symbol, so you can access each part of code separately,
            // such as interfaces, methods, members, contructor parameters etc.
            ISymbol? symbol = model.GetDeclaredSymbol(classSyntax);

            if (symbol == null)
            {
                continue;
            }

            List<MemberDeclarationSyntax> members = [];

            string? btn = ((INamedTypeSymbol)symbol).BaseType?.Name;
            while (btn != null)
            {
                ClassDeclarationSyntax? btcs = classLookup[btn].FirstOrDefault();
                if (btcs == null)
                {
                    break;
                }

                members.AddRange(btcs.Members);
                btn = compilation.GetSemanticModel(btcs.SyntaxTree).GetDeclaredSymbol(btcs) is INamedTypeSymbol ints
                    ? ints.BaseType?.Name
                    : null;
            }

            members.AddRange(classSyntax.Members);

            executeForFts(
                compilation,
                symbol,
                members,
                context,
                LdGenCategory.Class);
        }
    }

    public void Execute(
        Compilation compilation,
        ImmutableArray<RecordDeclarationSyntax> records,
        SourceProductionContext context)
    {
        ILookup<string, RecordDeclarationSyntax> recordLookup = records.ToLookup(c => c.Identifier.Text);

        foreach (RecordDeclarationSyntax recordSyntax in records.Where(c => c.AttributeLists.Any(al => al.Attributes.Any(a => a.Name.ToString() == GeneratorAttributes._ldgSQLiteTable))))
        {
            // Converting the record to a semantic model to access much more meaningful data.
            SemanticModel model = compilation.GetSemanticModel(recordSyntax.SyntaxTree);

            // Parse to declared symbol, so you can access each part of code separately,
            // such as interfaces, methods, members, contructor parameters etc.
            ISymbol? symbol = model.GetDeclaredSymbol(recordSyntax);

            if (symbol == null)
            {
                continue;
            }

            List<MemberDeclarationSyntax> members = [];
            string? btn = ((INamedTypeSymbol)symbol).BaseType?.Name;
            while (btn != null)
            {
                RecordDeclarationSyntax? btcs = recordLookup[btn].FirstOrDefault();
                if (btcs == null)
                {
                    break;
                }

                members.AddRange(btcs.Members);
                btn = compilation.GetSemanticModel(btcs.SyntaxTree).GetDeclaredSymbol(btcs) is INamedTypeSymbol ints
                    ? ints.BaseType?.Name
                    : null;
            }

            members.AddRange(recordSyntax.Members);

            execute(
                compilation,
                symbol,
                members,
                context,
                LdGenCategory.Record);
        }

        foreach (RecordDeclarationSyntax recordSyntax in records.Where(c => c.AttributeLists.Any(al => al.Attributes.Any(a => a.Name.ToString() == GeneratorAttributes._ldgSQLiteFtsTable))))
        {
            // Converting the record to a semantic model to access much more meaningful data.
            SemanticModel model = compilation.GetSemanticModel(recordSyntax.SyntaxTree);

            // Parse to declared symbol, so you can access each part of code separately,
            // such as interfaces, methods, members, contructor parameters etc.
            ISymbol? symbol = model.GetDeclaredSymbol(recordSyntax);

            if (symbol == null)
            {
                continue;
            }

            List<MemberDeclarationSyntax> members = [];
            string? btn = ((INamedTypeSymbol)symbol).BaseType?.Name;
            while (btn != null)
            {
                RecordDeclarationSyntax? btcs = recordLookup[btn].FirstOrDefault();
                if (btcs == null)
                {
                    break;
                }

                members.AddRange(btcs.Members);
                btn = compilation.GetSemanticModel(btcs.SyntaxTree).GetDeclaredSymbol(btcs) is INamedTypeSymbol ints
                    ? ints.BaseType?.Name
                    : null;
            }

            members.AddRange(recordSyntax.Members);

            executeForFts(
                compilation,
                symbol,
                members,
                context,
                LdGenCategory.Record);
        }
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
        string? foreignTable = null,
        string? foreignColumn = null,
        string? foreignModelType = null);

    private void execute(
        Compilation compilation,
        ISymbol symbol,
        List<MemberDeclarationSyntax> members,
        SourceProductionContext context,
        LdGenCategory genCategory)
    {
        string className = symbol.Name;
        string? classNamespace = symbol.ContainingNamespace?.ToDisplayString();
        string? classAssembly = symbol.ContainingAssembly?.Name;

        ILookup<string?, AttributeData> symbolAttributeLookup = symbol.GetAttributes().ToLookup(a => a.AttributeClass?.Name);

        string tableName = symbolAttributeLookup.Contains(GeneratorAttributes._ldgSQLiteTable)
            ? symbolAttributeLookup[GeneratorAttributes._ldgSQLiteTable].First().ConstructorArguments.FirstOrDefault().Value?.ToString() ?? className
            : className;

        int? pkColIndex = null;
        string? pkColName = null;
        string? pkPropType = null;
        bool pkIsIdentity = false;
        bool anyColIsJson = false;

        List<string> createColLines = [];
        List<string> createFKLines = [];
        List<TableColInfoRec> tableColInfo = [];

        foreach (MemberDeclarationSyntax member in members)
        {
            // only process properties
            if (member is not PropertyDeclarationSyntax pds)
            {
                continue;
            }

            SemanticModel pModel = compilation.GetSemanticModel(pds.SyntaxTree);
            //ISymbol? pSymbol = pModel.GetDeclaredSymbol(pds);

            //if (pSymbol == null)
            //{
            //    continue;
            //}

            // check for ignore property
            if (pds.AttributeLists.Any(al => al.Attributes.Any(a => a.Name.ToString() == GeneratorAttributes._ldgSQLiteIgnore)))
            {
                continue;
            }

            Type memberType = pds.GetType();
            string propName = pds.Identifier.ToString();

            TypeInfo propTypeInfo = pModel.GetTypeInfo(pds.Type);
            string propTypeName = pds.Type.ToString();
            INamedTypeSymbol? namedTypeSymbol = propTypeInfo.Type is INamedTypeSymbol ints ? ints : null;

            bool nullable = propTypeName.EndsWith("?") || (namedTypeSymbol?.NullableAnnotation == NullableAnnotation.Annotated);
            if (nullable)
            {
                propTypeName = propTypeName.Substring(0, propTypeName.Length - 1);
            }

            // check for primary key property
            bool isPrimaryKey = pds.AttributeLists.Any(al => al.Attributes.Any(a => a.Name.ToString() == GeneratorAttributes._ldgSQLiteKey));
            if (isPrimaryKey)
            {
                pkColName = propName;
                pkPropType = propTypeName;
                pkIsIdentity = (propTypeName == "int" || propTypeName == "long");
            }

            if (isPrimaryKey)
            {
                if (pkColName == null)
                {
                    throw new Exception("Primary key column name is null");
                }

                if (pkColName != propName)
                {
                    throw new Exception("Primary key column name does not match property name");
                }

                pkColIndex = tableColInfo.Count;
            }

            bool isUnique = pds.AttributeLists.Any(al => al.Attributes.Any(a => a.Name.ToString() == GeneratorAttributes._ldgSQLiteUnique));

            // check for any kind of non-scalar type
            bool memberIsNonScalar = memberType.IsArray || (memberType.IsGenericType && typeof(IEnumerable<object>).IsAssignableFrom(memberType));

            // check for type nullability
            //bool nullable = Nullable.GetUnderlyingType(member.) != null;
            //bool nullable = new NullabilityInfoContext().Create(prop).WriteState is NullabilityState.Nullable;

            string? jsonTypeName = null;
            string? enumTypeName = null;
            bool memberIsEnum = false;

            if ((namedTypeSymbol != null) &&
                (
                    (namedTypeSymbol.TypeKind == TypeKind.Enum) ||
                    ((namedTypeSymbol.TypeArguments.Length != 0) && (namedTypeSymbol.TypeArguments[0].TypeKind == TypeKind.Enum)) ||
                    ((namedTypeSymbol.TypeKind == TypeKind.Struct) && (namedTypeSymbol.TypeArguments.Length != 0) && !namedTypeSymbol.TypeArguments[0].ContainingNamespace.Name.StartsWith("System"))
                ))
            {
                memberIsEnum = true;

                // grab the enum type name
                if (namedTypeSymbol.TypeKind == TypeKind.Enum)
                {
                    if (namedTypeSymbol.ContainingType == null)
                    {
                        enumTypeName = $"{namedTypeSymbol.ContainingNamespace}.{namedTypeSymbol.Name}";
                    }
                    else
                    {
                        enumTypeName = $"{namedTypeSymbol.ContainingType.ContainingNamespace}.{namedTypeSymbol.ContainingType.Name}.{namedTypeSymbol.Name}";
                    }
                }
                else if (namedTypeSymbol.TypeArguments.Length != 0)
                {
                    if (namedTypeSymbol.TypeArguments[0].ContainingType == null)
                    {
                        enumTypeName = $"{namedTypeSymbol.TypeArguments[0].ContainingNamespace}.{namedTypeSymbol.TypeArguments[0].Name}";
                    }
                    else
                    {
                        enumTypeName = $"{namedTypeSymbol.TypeArguments[0].ContainingType.ContainingNamespace}.{namedTypeSymbol.TypeArguments[0].ContainingType.Name}.{namedTypeSymbol.TypeArguments[0].Name}";
                    }
                }
                else
                {
                    enumTypeName = namedTypeSymbol.Name;
                }
            }
            else if (namedTypeSymbol != null)
            {
                if (namedTypeSymbol.TypeArguments.Length != 0)
                {
                    if (namedTypeSymbol.Name.Contains("List") ||
                        namedTypeSymbol.Name.Contains("Enumerable"))
                    {
                        memberIsNonScalar = true;
                        if (namedTypeSymbol.TypeArguments[0].ContainingType == null)
                        {
                            //jsonTypeName = $"System.Collections.Generic.List<{namedTypeSymbol.TypeArguments[0].ContainingNamespace}.{namedTypeSymbol.TypeArguments[0].Name}>";
                            jsonTypeName = $"{namedTypeSymbol.TypeArguments[0].ContainingNamespace}.{namedTypeSymbol.TypeArguments[0].Name}";
                        }
                        else
                        {
                            //jsonTypeName = $"System.Collections.Generic.List<{namedTypeSymbol.TypeArguments[0].ContainingType.ContainingNamespace}.{namedTypeSymbol.TypeArguments[0].ContainingType.Name}.{namedTypeSymbol.TypeArguments[0].Name}>";
                            jsonTypeName = $"{namedTypeSymbol.TypeArguments[0].ContainingType.ContainingNamespace}.{namedTypeSymbol.TypeArguments[0].ContainingType.Name}.{namedTypeSymbol.TypeArguments[0].Name}";
                        }
                    }
                    else
                    {
                        if (namedTypeSymbol.TypeArguments[0].ContainingType == null)
                        {
                            jsonTypeName = $"{namedTypeSymbol.TypeArguments[0].ContainingNamespace}.{namedTypeSymbol.TypeArguments[0].Name}";
                        }
                        else
                        {
                            jsonTypeName = $"{namedTypeSymbol.TypeArguments[0].ContainingType.ContainingNamespace}.{namedTypeSymbol.TypeArguments[0].ContainingType.Name}.{namedTypeSymbol.TypeArguments[0].Name}";
                        }
                    }
                }
                else
                {
                    if (namedTypeSymbol.ContainingType == null)
                    {
                        jsonTypeName = $"{namedTypeSymbol.ContainingNamespace}.{namedTypeSymbol.Name}";
                    }
                    else
                    {
                        jsonTypeName = $"{namedTypeSymbol.ContainingType.ContainingNamespace}.{namedTypeSymbol.ContainingType.Name}.{namedTypeSymbol.Name}";
                    }
                }

                //if (nullable)
                //{
                //    jsonTypeName += "?";
                //}
            }

            bool useJson = !memberIsEnum && !_sqliteTypeMap.ContainsKey(propTypeName);

            // add our column line
            createColLines.Add(
                $"{pds.Identifier.ToString()} {getSqlType(propTypeName, memberIsEnum, useJson, memberIsNonScalar)}" +
                $"{(isPrimaryKey ? getPkDirective(pkPropType) : string.Empty)}" +
                $"{(isUnique ? " UNIQUE" : string.Empty)}" +
                $"{((nullable || isPrimaryKey) ? string.Empty : " NOT NULL")}");

            // check for foreign key property information
            string? foreignTable = null;
            string? foreignColumn = null;
            string? foreignModelType = null;
            foreach (AttributeListSyntax als in pds.AttributeLists)
            {
                foreach (AttributeSyntax a in als.Attributes.Where(a => a.Name.ToString() == GeneratorAttributes._ldgSQLiteForeignKey))
                {
                    foreach (AttributeArgumentSyntax arg in a.ArgumentList?.Arguments ?? [])
                    {
                        if (arg.NameEquals?.Name.ToString() == "ReferenceTable")
                        {
                            foreignTable = arg.Expression.ToString();
                        }
                        else if (arg.NameEquals?.Name.ToString() == "ReferenceColumn")
                        {
                            foreignColumn = arg.Expression.ToString();
                        }
                        else if (arg.NameEquals?.Name.ToString() == "ModelTypeName")
                        {
                            foreignModelType = arg.Expression.ToString();
                        }
                    }
                }
            }

            if ((foreignTable != null) && (foreignColumn != null))
            {
                createFKLines.Add($"FOREIGN KEY ({pds.Identifier}) REFERENCES {foreignTable}({foreignColumn})");
            }

            // create the select retrieval pair
            if (nullable && _sqliteNullableReadDirectives.TryGetValue(propTypeName, out string? readFormat))
            {
                tableColInfo.Add(new (
                    propName,
                    propTypeName,
                    string.Format(readFormat.Remove(0, 6), pds.Identifier.ToString(), "reader", tableColInfo.Count),
                    string.Format(readFormat, pds.Identifier.ToString(), "reader", tableColInfo.Count),
                    isPrimaryKey,
                    isPrimaryKey && (propTypeName == "int" || propTypeName == "long"),
                    nullable,
                    memberIsEnum,
                    useJson,
                    memberIsNonScalar,
                    isUnique,
                    foreignTable,
                    foreignColumn,
                    foreignModelType));
            }
            else if (!nullable && _sqliteReadDirectives.TryGetValue(propTypeName, out readFormat))
            {
                tableColInfo.Add(new (
                    propName,
                    propTypeName,
                    string.Format(readFormat.Remove(0, 6), pds.Identifier.ToString(), "reader", tableColInfo.Count),
                    string.Format(readFormat, pds.Identifier.ToString(), "reader", tableColInfo.Count),
                    isPrimaryKey,
                    isPrimaryKey && (propTypeName == "int" || propTypeName == "long"),
                    nullable,
                    memberIsEnum,
                    useJson,
                    memberIsNonScalar,
                        isUnique,
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
                        ? string.Format(_sqliteNullableReadDirectives["enum"].Remove(0, 6), pds.Identifier.ToString(), "reader", tableColInfo.Count, enumTypeName)
                        : string.Format(_sqliteReadDirectives["enum"].Remove(0, 6), pds.Identifier.ToString(), "reader", tableColInfo.Count, enumTypeName),
                    nullable
                        ? string.Format(_sqliteNullableReadDirectives["enum"], pds.Identifier.ToString(), "reader", tableColInfo.Count, enumTypeName)
                        : string.Format(_sqliteReadDirectives["enum"], pds.Identifier.ToString(), "reader", tableColInfo.Count, enumTypeName),
                    isPrimaryKey,
                    isPrimaryKey && (propTypeName == "int" || propTypeName == "long"),
                    nullable,
                    memberIsEnum,
                    useJson,
                    memberIsNonScalar,
                    isUnique,
                    foreignTable,
                    foreignColumn,
                    foreignModelType));
            }
            else if (memberIsNonScalar)
            {
                anyColIsJson = true;

                tableColInfo.Add(new(
                    pds.Identifier.ToString(),
                    propTypeName,
                    nullable
                        ? string.Format(_sqliteNullableReadDirectives["JSON[]"].Remove(0, 6), pds.Identifier.ToString(), "reader", tableColInfo.Count, jsonTypeName)
                        : string.Format(_sqliteReadDirectives["JSON[]"].Remove(0, 6), pds.Identifier.ToString(), "reader", tableColInfo.Count, jsonTypeName),
                    nullable
                        ? string.Format(_sqliteNullableReadDirectives["JSON[]"], pds.Identifier.ToString(), "reader", tableColInfo.Count, jsonTypeName)
                        : string.Format(_sqliteReadDirectives["JSON[]"], pds.Identifier.ToString(), "reader", tableColInfo.Count, jsonTypeName),
                    isPrimaryKey,
                    isPrimaryKey && (propTypeName == "int" || propTypeName == "long"),
                    nullable,
                    memberIsEnum,
                    useJson,
                    memberIsNonScalar,
                        isUnique,
                        foreignTable,
                        foreignColumn,
                        foreignModelType));
            }
            else
            {
                // tableColInfo.Add((
                //     pds.Identifier.ToString(),
                //     propTypeName,
                //     $"// ERROR: could not determine retrieval directive for type {propName}:{propTypeName}",
                //     $"// ERROR: could not determine retrieval directive for type {propName}:{propTypeName}",
                //     isPrimaryKey,
                //     isPrimaryKey && (propTypeName == "int" || propTypeName == "long"),
                //     nullable,
                //     memberIsEnum
                //     ));

                anyColIsJson = true;

                tableColInfo.Add(new(
                    pds.Identifier.ToString(),
                    propTypeName,
                    nullable
                        ? string.Format(_sqliteNullableReadDirectives["JSON"].Remove(0, 6), pds.Identifier.ToString(), "reader", tableColInfo.Count, jsonTypeName)
                        : string.Format(_sqliteReadDirectives["JSON"].Remove(0, 6), pds.Identifier.ToString(), "reader", tableColInfo.Count, jsonTypeName),
                    nullable
                        ? string.Format(_sqliteNullableReadDirectives["JSON"], pds.Identifier.ToString(), "reader", tableColInfo.Count, jsonTypeName)
                        : string.Format(_sqliteReadDirectives["JSON"], pds.Identifier.ToString(), "reader", tableColInfo.Count, jsonTypeName),
                    isPrimaryKey,
                    isPrimaryKey && (propTypeName == "int" || propTypeName == "long"),
                    nullable,
                    memberIsEnum,
                    useJson,
                    memberIsNonScalar,
                    isUnique,
                    foreignTable,
                    foreignColumn,
                    foreignModelType));
            }
        }

        context.AddSource(
            $"{className}{"SQLite"}.g.cs",
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

                    using System.Collections.Generic;
                    using System.Data;
                    using System.Diagnostics.CodeAnalysis;
                    using System.Text;
                    using System.Text.Json;
                                    
                    namespace {{{classNamespace}}};
                
                    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
                    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
                    public partial {{{decForGenCategory(genCategory)}}} {{{className}}}
                    {
                        public static string DefaultTableName => "{{{tableName}}}";
                        {{{(pkPropType != "long" ? "internal static int _indexValue = 0;" : string.Empty)}}}
                        {{{(pkPropType != "long" ? "public static int GetIndex() => Interlocked.Increment(ref _indexValue);" : string.Empty)}}}
                        {{{(pkPropType == "long" ? "internal static long _indexValue = 0;" : string.Empty)}}}
                        {{{(pkPropType == "long" ? "public static long GetIndex() => Interlocked.Increment(ref _indexValue);" : string.Empty)}}}

                        public static bool CreateTable(IDbConnection dbConnection, string? dbTableName = null)
                        {
                            dbTableName ??= "{{{tableName}}}";

                            IDbCommand command = dbConnection.CreateCommand();
                            command.CommandText = $"""
                                CREATE TABLE IF NOT EXISTS {dbTableName} (
                                    {{{string.Join(_comma_line_4, [.. createColLines, .. createFKLines])}}}
                                )
                                """;

                            command.ExecuteNonQuery();

                            {{{string.Join(_line_2, getIndexLines())}}}
                    
                            LoadMaxKey(dbConnection, dbTableName);

                            return true;
                        }

                        public static bool DropTable(IDbConnection dbConnection, string? dbTableName = null)
                        {
                            dbTableName ??= "{{{tableName}}}";
                    
                            IDbCommand command = dbConnection.CreateCommand();
                            command.CommandText = $"DROP TABLE IF EXISTS {dbTableName}";
                    
                            command.ExecuteNonQuery();
                    
                            return true;
                        }

                        public static void LoadMaxKey(IDbConnection dbConnection, string? dbTableName = null, int defaultValue = 0)
                        {
                            dbTableName ??= "{{{tableName}}}";
                    
                            IDbCommand command = dbConnection.CreateCommand();
                            command.CommandText = $"SELECT MAX({{{(pkColName == null ? "ROWID" : pkColName)}}}) FROM {dbTableName}";

                            try
                            {
                                object? result = command.ExecuteScalar();
                                if (result is {{{(pkColName == null ? "int" : pkPropType)}}} value)
                                {
                                    _indexValue = value;
                                }
                                else if (result is long l)
                                {
                                    _indexValue = {{{((pkColName == null) || (pkPropType == "int") ? "Convert.ToInt32(l)" : "null")}}};
                                }
                            }
                            catch (Exception)
                            {
                                _indexValue = defaultValue;
                            }
                        }

                        public static {{{(pkColName == null ? "int" : pkPropType)}}}? SelectMaxKey(IDbConnection dbConnection, string? dbTableName = null, int defaultValue = 0)
                        {
                            dbTableName ??= "{{{tableName}}}";
                    
                            IDbCommand command = dbConnection.CreateCommand();
                            command.CommandText = $"SELECT MAX({{{(pkColName == null ? "ROWID" : pkColName)}}}) FROM {dbTableName}";

                            object? result = command.ExecuteScalar();
                            if (result is {{{(pkColName == null ? "int" : pkPropType)}}} value)
                            {
                                return value;
                            }
                            else if (result is long l)
                            {
                                return {{{((pkColName == null) || (pkPropType == "int") ? "Convert.ToInt32(l)" : "null")}}};
                            }

                            return null;
                        }
                    
                        public static {{{className}}}? SelectSingle(
                            IDbConnection dbConnection, 
                            string? dbTableName = null, 
                            bool orJoinConditions = false, 
                            bool compareStringsWithLike = false,
                            {{{string.Join(", ", getFnFilterParams(true, true))}}})
                        {
                            dbTableName ??= "{{{tableName}}}";

                            IDbCommand command = dbConnection.CreateCommand();
                            command.CommandText = $"SELECT {{{string.Join(", ", tableColInfo.Select(p => p.name))}}} FROM {dbTableName}";

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
                            {{{string.Join(", ", getFnFilterParams(true, true))}}})
                        {
                            dbTableName ??= "{{{tableName}}}";

                            List<{{{className}}}> results = new();

                            IDbCommand command = dbConnection.CreateCommand();
                            command.CommandText = $"SELECT {{{string.Join(", ", tableColInfo.Select(p => p.name))}}} FROM {dbTableName}";
                    
                            string joiner = orJoinConditions ? " OR " : " AND ";
                            string stringComparator = compareStringsWithLike ? " LIKE " : " = ";
                            bool addedCondition = false;
                            {{{(anyColIsJson ? "string? dbJson;" : string.Empty)}}}
                                        
                            {{{string.Join(_line_2, getConditionLines(true, true, true, true))}}}

                            if ((orderByProperties != null) && (orderByProperties.Length > 0))
                            {
                                command.CommandText += $" ORDER BY {string.Join(", ", orderByProperties)}";
                                if (orderByDirection?.StartsWith("d", StringComparison.OrdinalIgnoreCase) == true)
                                {
                                    command.CommandText += $" DESC";
                                }
                                else
                                {
                                    command.CommandText += $" ASC";
                                }
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
                            {{{string.Join(", ", getFnFilterParams(true, true))}}})
                        {
                            dbTableName ??= "{{{tableName}}}";

                            IDbCommand command = dbConnection.CreateCommand();
                            command.CommandText = $"SELECT {{{string.Join(", ", tableColInfo.Select(p => p.name))}}} FROM {dbTableName}";

                            string joiner = orJoinConditions ? " OR " : " AND ";
                            string stringComparator = compareStringsWithLike ? " LIKE " : " = ";
                            bool addedCondition = false;
                            {{{(anyColIsJson ? "string? dbJson;" : string.Empty)}}}

                            {{{string.Join(_line_2, getConditionLines(true, true, true, true))}}}

                            if ((orderByProperties != null) && (orderByProperties.Length > 0))
                            {
                                command.CommandText += $" ORDER BY {string.Join(", ", orderByProperties)}";
                                if (orderByDirection?.StartsWith("d", StringComparison.OrdinalIgnoreCase) == true)
                                {
                                    command.CommandText += $" DESC";
                                }
                                else
                                {
                                    command.CommandText += $" ASC";
                                }
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

                        public static Dictionary<{{{(pkColName == null ? "int" : pkPropType)}}}, {{{className}}}> SelectDict(
                            IDbConnection dbConnection, 
                            string? dbTableName = null, 
                            bool orJoinConditions = false,
                            bool compareStringsWithLike = false,
                            {{{string.Join(", ", getFnFilterParams(true, true))}}})
                        {
                            dbTableName ??= "{{{tableName}}}";
                    
                            Dictionary<{{{(pkColName == null ? "int" : pkPropType)}}}, {{{className}}}> results = new();
                    
                            IDbCommand command = dbConnection.CreateCommand();
                            command.CommandText = $"SELECT {{{string.Join(", ", tableColInfo.Select(p => p.name))}}} FROM {dbTableName}";
                    
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

                        public static int SelectCount(
                            IDbConnection dbConnection,
                            string? dbTableName = null, 
                            bool orJoinConditions = false,
                            bool compareStringsWithLike = false,
                            {{{string.Join(", ", getFnFilterParams(true, true))}}})
                        {
                            dbTableName ??= "{{{tableName}}}";
                    
                            IDbCommand command = dbConnection.CreateCommand();
                            command.CommandText = $"SELECT COUNT({{{(pkColName == null ? "*" : pkColName)}}}) FROM {dbTableName}";
                    
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
                                return {{{((pkColName == null) || (pkPropType == "int") ? "Convert.ToInt32(l)" : "null")}}};
                            }

                            return -1;
                        }

                        public static {{{(pkColName == null ? "void" : pkPropType)}}} Insert(
                            IDbConnection dbConnection,
                            {{{className}}} value,
                            string? dbTableName = null,
                            bool ignoreDuplicates = false,
                            bool insertPrimaryKey = false)
                        {
                            dbTableName ??= "{{{tableName}}}";
                            {{{getNonIdentityPkInit(pkColName, pkPropType)}}}
                            {{{(anyColIsJson ? "string? dbJson;" : string.Empty)}}}
                            string insertLiteral = ignoreDuplicates ? "INSERT OR IGNORE" : "INSERT";

                            if (insertPrimaryKey)
                            {
                                using (IDbTransaction transaction = dbConnection.BeginTransaction())
                                {
                                    IDbCommand command = dbConnection.CreateCommand();
                                    command.CommandText = $"""
                                        {insertLiteral} INTO {dbTableName} (
                                            {{{string.Join(_comma_line_6, tableColInfo.Select(p => p.name))}}}
                                        ) VALUES (
                                            {{{string.Join(_comma_line_6, tableColInfo.Select(p => "$" + p.name))}}}
                                        );
                                        """;
                    
                                    {{{string.Join(_line_4, getInsertCommandParamLines(true, null, pkPropType, includeIdentity: true, ignoreDupeProperty: "ignoreDuplicates"))}}}
                    
                                    transaction.Commit();
                                }
                            }
                            else
                            {
                                using (IDbTransaction transaction = dbConnection.BeginTransaction())
                                {
                                    IDbCommand command = dbConnection.CreateCommand();
                                    command.CommandText = $"""
                                        {insertLiteral} INTO {dbTableName} (
                                            {{{string.Join(_comma_line_6, tableColInfo.Where(p => p.isIdentity == false).Select(p => p.name))}}}
                                        ) VALUES (
                                            {{{string.Join(_comma_line_6, tableColInfo.Where(p => p.isIdentity == false).Select(p => "$" + p.name))}}}
                                        ) {{{(pkIsIdentity ? " RETURNING " + pkColName : string.Empty)}}};
                                        """;
                    
                                    {{{string.Join(_line_4, getInsertCommandParamLines(true, pkIsIdentity ? pkColName : null, pkPropType, ignoreDupeProperty: "ignoreDuplicates"))}}}
                    
                                    transaction.Commit();
                                }
                            }

                            {{{(pkColName == null ? "return" : $"return value.{pkColName}")}}};
                        }

                        public static void Insert(
                            IDbConnection dbConnection,
                            List<{{{className}}}> values,
                            string? dbTableName = null,
                            bool ignoreDuplicates = false,
                            bool insertPrimaryKey = false)
                        {
                            dbTableName ??= "{{{tableName}}}";
                            {{{(anyColIsJson ? "string? dbJson;" : string.Empty)}}}
                            string insertLiteral = ignoreDuplicates ? "INSERT OR IGNORE" : "INSERT";

                            if (insertPrimaryKey)
                            {
                                using (IDbTransaction transaction = dbConnection.BeginTransaction())
                                {
                                    IDbCommand command = dbConnection.CreateCommand();
                                    command.Transaction = transaction;
                                    command.CommandText = $"""
                                        {insertLiteral} INTO {dbTableName} (
                                            {{{string.Join(_comma_line_6, tableColInfo.Select(p => p.name))}}}
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
                    
                                    transaction.Commit();
                                }
                            }
                            else
                            {
                                using (IDbTransaction transaction = dbConnection.BeginTransaction())
                                {
                                    IDbCommand command = dbConnection.CreateCommand();
                                    command.Transaction = transaction;
                                    command.CommandText = $"""
                                        {insertLiteral} INTO {dbTableName} (
                                            {{{string.Join(_comma_line_6, tableColInfo.Where(p => p.isIdentity == false).Select(p => p.name))}}}
                                        ) VALUES (
                                            {{{string.Join(_comma_line_6, tableColInfo.Where(p => p.isIdentity == false).Select(p => "$" + p.name))}}}
                                        ) {{{(pkIsIdentity ? " RETURNING " + pkColName : string.Empty)}}};
                                        """;

                                    {{{string.Join(_line_4, getInsertCommandParamLines(false, pkIsIdentity ? pkColName : null, pkPropType, createParameters: true, ignoreDupeProperty: "ignoreDuplicates"))}}}

                                    command.Prepare();

                                    foreach ({{{className}}} value in values)
                                    {
                                        {{{getNonIdentityPkInit(pkColName, pkPropType)}}}
                                        {{{string.Join(_line_5, getInsertCommandParamLines(false, pkIsIdentity ? pkColName : null, pkPropType, instantiateParameters: true, executeCommand: true, ignoreDupeProperty: "ignoreDuplicates"))}}}
                                    }
                    
                                    transaction.Commit();
                                }
                            }
                        }

                        public static void Insert(
                            IDbConnection dbConnection,
                            IEnumerable<{{{className}}}> values,
                            string? dbTableName = null,
                            bool ignoreDuplicates = false,
                            bool insertPrimaryKey = false)
                        {
                            dbTableName ??= "{{{tableName}}}";
                            {{{(anyColIsJson ? "string? dbJson;" : string.Empty)}}}
                            string insertLiteral = ignoreDuplicates ? "INSERT OR IGNORE" : "INSERT";
                    
                            if (insertPrimaryKey)
                            {
                                using (IDbTransaction transaction = dbConnection.BeginTransaction())
                                {
                                    IDbCommand command = dbConnection.CreateCommand();
                                    command.Transaction = transaction;
                                    command.CommandText = $"""
                                        {insertLiteral} INTO {dbTableName} (
                                            {{{string.Join(_comma_line_6, tableColInfo.Select(p => p.name))}}}
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
                    
                                    transaction.Commit();
                                }
                            }
                            else
                            {
                                using (IDbTransaction transaction = dbConnection.BeginTransaction())
                                {
                                    IDbCommand command = dbConnection.CreateCommand();
                                    command.Transaction = transaction;
                                    command.CommandText = $"""
                                        {insertLiteral} INTO {dbTableName} (
                                            {{{string.Join(_comma_line_6, tableColInfo.Where(p => p.isIdentity == false).Select(p => p.name))}}}
                                        ) VALUES (
                                            {{{string.Join(_comma_line_6, tableColInfo.Where(p => p.isIdentity == false).Select(p => "$" + p.name))}}}
                                        ) {{{(pkIsIdentity ? " RETURNING " + pkColName : string.Empty)}}};
                                        """;
                    
                                    {{{string.Join(_line_4, getInsertCommandParamLines(false, pkIsIdentity ? pkColName : null, pkPropType, createParameters: true, ignoreDupeProperty: "ignoreDuplicates"))}}}
                    
                                    command.Prepare();
                    
                                    foreach ({{{className}}} value in values)
                                    {
                                        {{{getNonIdentityPkInit(pkColName, pkPropType)}}}
                                        {{{string.Join(_line_5, getInsertCommandParamLines(false, pkIsIdentity ? pkColName : null, pkPropType, instantiateParameters: true, executeCommand: true, setIdentity: false, ignoreDupeProperty: "ignoreDuplicates"))}}}
                                    }
                    
                                    transaction.Commit();
                                }
                            }
                        }

                        public static {{{className}}} Update(IDbConnection dbConnection, {{{className}}} value, string? dbTableName = null)
                        {
                            dbTableName ??= "{{{tableName}}}";
                            {{{(anyColIsJson ? "string? dbJson;" : string.Empty)}}}
                                        
                            using (IDbTransaction transaction = dbConnection.BeginTransaction())
                            {
                                IDbCommand command = dbConnection.CreateCommand();
                                command.CommandText = $"""
                                    UPDATE {dbTableName} SET
                                        {{{string.Join(_comma_line_5, tableColInfo.Where(p => p.isPrimaryKey == false).Select(p => p.name + " = $" + p.name))}}}
                                    WHERE
                                        {{{pkColName}}} = ${{{pkColName}}}
                                    """;
                    
                                {{{string.Join(_line_3, getInsertCommandParamLines(true, pkIsIdentity ? pkColName : null, pkPropType, includeIdentity: true, isInsert: false))}}}
                    
                                transaction.Commit();
                            }
                    
                            return value;
                        }

                        public static void Update(IDbConnection dbConnection, IEnumerable<{{{className}}}> values, string? dbTableName = null)
                        {
                            dbTableName ??= "{{{tableName}}}";
                            {{{(anyColIsJson ? "string? dbJson;" : string.Empty)}}}
                                                
                            using (IDbTransaction transaction = dbConnection.BeginTransaction())
                            {
                                IDbCommand command = dbConnection.CreateCommand();
                                command.CommandText = $"""
                                    UPDATE {dbTableName} SET
                                        {{{string.Join(_comma_line_5, tableColInfo.Where(p => p.isPrimaryKey == false).Select(p => p.name + " = $" + p.name))}}}
                                    WHERE
                                        {{{pkColName}}} = ${{{pkColName}}}
                                    """;
                    
                                {{{string.Join(
                                _line_3,
                                getInsertCommandParamLines(
                                    false,
                                    pkIsIdentity ? pkColName : null,
                                    pkPropType,
                                    createParameters: true,
                                    includeIdentity: true,
                                    isInsert: false))}}}
                    
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
                                        isInsert: false))}}}
                                }
                    
                                transaction.Commit();
                            }
                        }

                        public static void Delete(IDbConnection dbConnection, {{{className}}} value, string? dbTableName = null)
                        {
                            dbTableName ??= "{{{tableName}}}";
                                        
                            using (IDbTransaction transaction = dbConnection.BeginTransaction())
                            {
                                IDbCommand command = dbConnection.CreateCommand();
                                command.CommandText = $"DELETE FROM {dbTableName} WHERE {{{pkColName}}} = ${{{pkColName}}}";
                    
                                {{{string.Join(
                                _line_3,
                                getInsertCommandParamLines(
                                    true,
                                    pkIsIdentity ? pkColName : null,
                                    pkPropType,
                                    executeCommand: true,
                                    includeIdentity: true,
                                    identityOnly: true,
                                    setIdentity: false))}}}
                    
                                transaction.Commit();
                            }
                        }
                    
                        public static void Delete(IDbConnection dbConnection, IEnumerable<{{{className}}}> values, string? dbTableName = null)
                        {
                            dbTableName ??= "{{{tableName}}}";
                                                
                            using (IDbTransaction transaction = dbConnection.BeginTransaction())
                            {
                                IDbCommand command = dbConnection.CreateCommand();
                                command.CommandText = $"DELETE FROM {dbTableName} WHERE {{{pkColName}}} = ${{{pkColName}}}";
                                        
                                {{{string.Join(
                                _line_3,
                                getInsertCommandParamLines(
                                    false,
                                    pkIsIdentity ? pkColName : null,
                                    pkPropType,
                                    createParameters: true,
                                    includeIdentity: true,
                                    identityOnly: true,
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
                                        includeIdentity: true,
                                        identityOnly: true))}}}
                                }
                    
                                transaction.Commit();
                            }
                        }

                        public static void Delete(
                            IDbConnection dbConnection,
                            string? dbTableName = null,
                            bool orJoinConditions = false,
                            bool compareStringsWithLike = false,
                            {{{string.Join(", ", getFnFilterParams(true, true))}}})
                        {
                            dbTableName ??= "{{{tableName}}}";
                            string joiner = orJoinConditions ? " OR " : " AND ";
                            string stringComparator = compareStringsWithLike ? " LIKE " : " = ";
                            {{{(anyColIsJson ? "string? dbJson;" : string.Empty)}}}
                                        
                            using (IDbTransaction transaction = dbConnection.BeginTransaction())
                            {
                                IDbCommand command = dbConnection.CreateCommand();
                                command.CommandText = $"DELETE FROM {dbTableName}";
                                        
                                bool addedCondition = false;
                    
                                {{{string.Join(_line_2, getConditionLines(true, true, true, true))}}}

                                command.ExecuteNonQuery();
                                transaction.Commit();
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

                        private static readonly JsonSerializerOptions _options = new()
                        {
                            WriteIndented = false,
                        };

                        private static System.Text.RegularExpressions.Regex _htmlStripRegex = new("<.*?>", System.Text.RegularExpressions.RegexOptions.Compiled);

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

                        private static bool TrySerializeForDb<T>(List<T>? instances, [NotNullWhen(true)] out string? json) where T : class
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

                        private static List<T> ParseArrayFromDb<T>(string json) where T : class
                        {
                            if (string.IsNullOrWhiteSpace(json))
                            {
                                return new List<T>();
                            }

                            return JsonSerializer.Deserialize<List<T>>(json, _options) ?? [];
                        }

                        private static string StripHtml(string? input)
                        {
                            if (string.IsNullOrWhiteSpace(input))
                            {
                                return string.Empty;
                            }

                            return _htmlStripRegex.Replace(input, string.Empty).Trim();
                        }
                    }

                    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
                    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
                    public static class {{{className}}}Extensions
                    {
                        public static {{{className}}}? SelectSingle<T>(
                            this IDbConnection dbCon, 
                            string? dbTableName = null,
                            bool orJoinConditions = false,
                            bool compareStringsWithLike = false,
                            {{{string.Join(", ", getFnFilterParams(true, true))}}})
                            where T : {{{className}}}
                        {
                            return {{{className}}}.SelectSingle(dbCon, dbTableName, orJoinConditions, compareStringsWithLike, {{{string.Join(", ", getFnFilterArgs(true, true))}}});
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
                            {{{string.Join(", ", getFnFilterParams(true, true))}}})
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
                                {{{string.Join(", ", getFnFilterArgs(true, true))}}});
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
                            {{{string.Join(", ", getFnFilterParams(true, true))}}})
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
                                {{{string.Join(", ", getFnFilterArgs(true, true))}}});
                        }

                        public static int SelectCount<T>(
                            this IDbConnection dbCon,
                            string? dbTableName = null,
                            bool orJoinConditions = false,
                            bool compareStringsWithLike = false,
                            {{{string.Join(", ", getFnFilterParams(true, true))}}})
                            where T : {{{className}}}
                        {
                            return {{{className}}}.SelectCount(dbCon, dbTableName, orJoinConditions, compareStringsWithLike, {{{string.Join(", ", getFnFilterArgs(true, true))}}});
                        }
                                                            
                        public static void Insert(this IDbConnection dbCon, {{{className}}} value, string? dbTableName = null, bool ignoreDuplicates = false, bool insertPrimaryKey = false)
                        {
                            {{{className}}}.Insert(dbCon, value, dbTableName, ignoreDuplicates, insertPrimaryKey);
                        }

                        public static void Insert(this IDbConnection dbCon, List<{{{className}}}> values, string? dbTableName = null, bool ignoreDuplicates = false, bool insertPrimaryKey = false)
                        {
                            {{{className}}}.Insert(dbCon, values, dbTableName, ignoreDuplicates, insertPrimaryKey);
                        }

                        public static void Insert(this IDbConnection dbCon, IEnumerable<{{{className}}}> values, string? dbTableName = null, bool ignoreDuplicates = false, bool insertPrimaryKey = false)
                        {
                            {{{className}}}.Insert(dbCon, values, dbTableName, ignoreDuplicates, insertPrimaryKey);
                        }

                        public static void Update(this IDbConnection dbCon, {{{className}}} value, string? dbTableName = null)
                        {
                            {{{className}}}.Update(dbCon, value, dbTableName);
                        }

                        public static void Update(this IDbConnection dbCon, IEnumerable<{{{className}}}> values, string? dbTableName = null)
                        {
                            {{{className}}}.Update(dbCon, values, dbTableName);
                        }

                        public static void Delete(this IDbConnection dbCon, {{{className}}} value, string? dbTableName = null)
                        {
                            {{{className}}}.Delete(dbCon, value, dbTableName);
                        }
                    
                        public static void Delete(this IDbConnection dbCon, IEnumerable<{{{className}}}> values, string? dbTableName = null)
                        {
                            {{{className}}}.Delete(dbCon, values, dbTableName);
                        }

                        public static void Delete(
                            this IDbConnection dbCon,
                            string? dbTableName = null,
                            bool orJoinConditions = false,
                            bool compareStringsWithLike = false,
                            {{{string.Join(", ", getFnFilterParams(true, true))}}})
                        {
                            {{{className}}}.Delete(dbCon, dbTableName, orJoinConditions, compareStringsWithLike, {{{string.Join(", ", getFnFilterArgs(true, true))}}});
                        }

                        public static void Insert(this {{{className}}} value, IDbConnection dbCon, string? dbTableName = null, bool ignoreDuplicates = false, bool insertPrimaryKey = false)
                        {
                            {{{className}}}.Insert(dbCon, value, dbTableName, ignoreDuplicates, insertPrimaryKey);
                        }

                        public static void Insert(this List<{{{className}}}> values, IDbConnection dbCon, string? dbTableName = null, bool ignoreDuplicates = false, bool insertPrimaryKey = false)
                        {
                            {{{className}}}.Insert(dbCon, values, dbTableName, ignoreDuplicates, insertPrimaryKey);
                        }

                        public static void Insert(this IEnumerable<{{{className}}}> values, IDbConnection dbCon, string? dbTableName = null, bool ignoreDuplicates = false, bool insertPrimaryKey = false)
                        {
                            {{{className}}}.Insert(dbCon, values, dbTableName, ignoreDuplicates, insertPrimaryKey);
                        }
                    
                        public static void Update(this {{{className}}} value, IDbConnection dbCon, string? dbTableName = null)
                        {
                            {{{className}}}.Update(dbCon, value, dbTableName);
                        }
                    
                        public static void Update(this IEnumerable<{{{className}}}> values, IDbConnection dbCon, string? dbTableName = null)
                        {
                            {{{className}}}.Update(dbCon, values, dbTableName);
                        }

                        public static void Delete(this {{{className}}} value, IDbConnection dbCon, string? dbTableName = null)
                        {
                            {{{className}}}.Delete(dbCon, value, dbTableName);
                        }
                    
                        public static void Delete(this IEnumerable<{{{className}}}> values, IDbConnection dbCon, string? dbTableName = null)
                        {
                            {{{className}}}.Delete(dbCon, values, dbTableName);
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

        string getPkDirective(string? pkTypeName) => pkTypeName switch
        {
            // note that in SQLite, an INTEGER PRIMARY KEY uses the internal ROWID automatically, so no IDENTITY declaration is necessary
            "int" => " UNIQUE PRIMARY KEY NOT NULL",            // " IDENTITY(1,1) PRIMARY KEY",
            "long" => " UNIQUE PRIMARY KEY NOT NULL",           // " IDENTITY(1,1) PRIMARY KEY",
            _ => " UNIQUE PRIMARY KEY NOT NULL",
        };

        string getNonIdentityPkInit(string? pkColName, string? pkTypeName) => pkTypeName switch
        {
            "guid" => $"value.{pkColName} = Guid.NewGuid();",
            _ => string.Empty,
        };

        IEnumerable<string> getIndexLines()
        {
            if (!symbolAttributeLookup.Contains(GeneratorAttributes._ldgSQLiteIndex))
            {
                yield break;
            }

            // generate any indexes
            foreach (AttributeData ad in symbolAttributeLookup[GeneratorAttributes._ldgSQLiteIndex])
            {
                string[] columns = ad.ConstructorArguments
                    .FirstOrDefault()
                    .Values
                    .Select(tc => tc.Value?.ToString() ?? string.Empty)
                    .Where(v => !string.IsNullOrEmpty(v)).
                    ToArray();

                if (columns.Length == 0)
                {
                    continue;
                }

                string indexName = $"IDX_{{dbTableName}}_{(string.Join("_", columns))}";

                yield return "command = dbConnection.CreateCommand();";
                yield return "command.CommandText = $\"\"\"";
                yield return $"    CREATE INDEX IF NOT EXISTS \"{indexName}\" ON \"{{dbTableName}}\" (";
                yield return $"        {string.Join(", ", columns.Select(v => $"\"{v}\""))}";
                yield return "    )";
                yield return "    \"\"\";";
                yield return "command.ExecuteNonQuery();";
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

                if (allowKeysIn && (!rec.isArray) && rec.name.EndsWith("Key"))
                {
                    yield return $"List<{rec.propType}>? {rec.name}Values = null";
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

                if (allowKeysIn && (!rec.isArray) && rec.name.EndsWith("Key"))
                {
                    yield return $"{rec.name}Values";
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
                    yield return $$$"""    command.CommandText += (addedCondition ? $" {joiner} " : " WHERE ") + $"{{{rec.name}}} {{{conditionComparator}}} ${{{rec.name}}}";""";
                }
                else
                {
                    yield return $$$"""    command.CommandText += (addedCondition ? " AND " : " WHERE ") + $"{{{rec.name}}} {{{conditionComparator}}} ${{{rec.name}}}";""";
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
                        yield return $$$"""    command.CommandText += (addedCondition ? $" {joiner} " : " WHERE ") + "{{{rec.name}}} IS NULL";""";
                    }
                    else
                    {
                        yield return $$$"""    command.CommandText += (addedCondition ? " AND " : " WHERE ") + "{{{rec.name}}} IS NULL";""";
                    }

                    yield return "    addedCondition = true;";
                    yield return "}";
                    yield return $"else if ({rec.name}IsNull == false)";
                    yield return "{";

                    if (allowsOrJoining)
                    {
                        yield return $$$"""    command.CommandText += (addedCondition ? $" {joiner} " : " WHERE ") + "{{{rec.name}}} IS NOT NULL";""";
                    }
                    else
                    {
                        yield return $$$"""    command.CommandText += (addedCondition ? " AND " : " WHERE ") + "{{{rec.name}}} IS NOT NULL";""";
                    }

                    yield return "    addedCondition = true;";
                    yield return "}";

                    yield return string.Empty;
                }

                if (allowKeysIn && (!rec.isArray) && rec.name.EndsWith("Key"))
                {
                    yield return $"if (({rec.name}Values is not null) && ({rec.name}Values.Count != 0))";
                    yield return "{";

                    if (allowsOrJoining)
                    {
                        yield return $$$"""    command.CommandText += (addedCondition ? $" {joiner} " : " WHERE ") + $"{{{rec.name}}} IN ";""";
                    }
                    else
                    {
                        yield return $$$"""    command.CommandText += (addedCondition ? " AND " : " WHERE ") + $"{{{rec.name}}} IN ";""";
                    }

                    yield return "    addedCondition = true;";
                    yield return "    List<string> vParamNames = new();";
                    yield return string.Empty;

                    yield return $"    for (int vIndex = 0; vIndex < {rec.name}Values.Count; vIndex++)";
                    yield return "    {";
                    yield return $$$"""        string vParamName = "{{{rec.name}}}Param" + vIndex.ToString();""";
                    yield return "        vParamNames.Add(\"@\" + vParamName);";
                    yield return string.Empty;
                    yield return "        IDbDataParameter vp = command.CreateParameter();";
                    yield return "        vp.ParameterName = vParamName;";
                    yield return $"        vp.Value = {rec.name}Values[vIndex];";
                    yield return "        command.Parameters.Add(vp);";
                    yield return "    }";
                    yield return string.Empty;

                    yield return "    command.CommandText += \"(\" + string.Join(',', vParamNames) + \")\";";
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
            bool isInsert = true,
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
                // do not insert identity key values
                if (rec.isIdentity && !includeIdentity)
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
                    if (ignoreDupeProperty == null)
                    {
                        yield return "int rowsAffected = command.ExecuteNonQuery();";
                        yield return "if (rowsAffected == 0) throw new Exception(\"Command failed!\");";
                    }
                    else
                    {
                        yield return "int rowsAffected = command.ExecuteNonQuery();";
                        yield return $"if (!{ignoreDupeProperty} && (rowsAffected == 0)) throw new Exception(\"Command failed!\");";
                    }
                }
                else
                {
                    if (ignoreDupeProperty == null)
                    {
                        yield return "object? commandResult = command.ExecuteScalar();";
                        yield return "if (commandResult == null) throw new Exception(\"Command failed!\");";

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
                        yield return $"if (!{ignoreDupeProperty} && (commandResult == null)) throw new Exception(\"Command failed!\");";

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

    private void executeForFts(
        Compilation compilation,
        ISymbol symbol,
        List<MemberDeclarationSyntax> members,
        SourceProductionContext context,
        LdGenCategory genCategory)
    {
        string className = symbol.Name;
        string? classNamespace = symbol.ContainingNamespace?.ToDisplayString();
        string? classAssembly = symbol.ContainingAssembly?.Name;

        ILookup<string?, AttributeData> symbolAttributeLookup = symbol.GetAttributes().ToLookup(a => a.AttributeClass?.Name);

        List<TypedConstant> ftsTableArgs = symbolAttributeLookup.Contains(GeneratorAttributes._ldgSQLiteFtsTable)
            ? symbolAttributeLookup[GeneratorAttributes._ldgSQLiteFtsTable].First().ConstructorArguments.ToList()
            : [];

        string sourceTableName = ftsTableArgs.Count > 0
            ? ftsTableArgs[0].Value?.ToString() ?? className
            : className;

        string tableName = ftsTableArgs.Count > 1
            ? ftsTableArgs[1].Value?.ToString() ?? (className + "_fts")
            : (className + "_fts");

        List<string> createColLines = [];
        List<string> createForeignKeyLines = [];
        List<TableColInfoRec> tableColInfo = [];

        foreach (MemberDeclarationSyntax member in members)
        {
            // only process properties
            if (member is not PropertyDeclarationSyntax pds)
            {
                continue;
            }

            SemanticModel pModel = compilation.GetSemanticModel(pds.SyntaxTree);

            // check for ignore property
            if (pds.AttributeLists.Any(al => al.Attributes.Any(a => a.Name.ToString() == GeneratorAttributes._ldgSQLiteIgnore)))
            {
                continue;
            }

            Type memberType = pds.GetType();
            string propName = pds.Identifier.ToString();

            TypeInfo propTypeInfo = pModel.GetTypeInfo(pds.Type);
            string propTypeName = pds.Type.ToString();
            INamedTypeSymbol? namedTypeSymbol = propTypeInfo.Type is INamedTypeSymbol ints ? ints : null;

            bool nullable = propTypeName.EndsWith("?") || (namedTypeSymbol?.NullableAnnotation == NullableAnnotation.Annotated);
            if (nullable)
            {
                propTypeName = propTypeName.Substring(0, propTypeName.Length - 1);
            }

            bool isUnique = pds.AttributeLists.Any(al => al.Attributes.Any(a => a.Name.ToString() == GeneratorAttributes._ldgSQLiteUnique));
            bool isUnindexed = pds.AttributeLists.Any(al => al.Attributes.Any(a => a.Name.ToString() == GeneratorAttributes._ldgSQLiteFtsUnindexed));

            // check for any kind of non-scalar type
            bool memberIsNonScalar = memberType.IsArray || (memberType.IsGenericType && typeof(IEnumerable<object>).IsAssignableFrom(memberType));

            string? jsonTypeName = null;
            string? enumTypeName = null;
            bool memberIsEnum = false;

            if ((namedTypeSymbol != null) &&
                (
                    (namedTypeSymbol.TypeKind == TypeKind.Enum) ||
                    ((namedTypeSymbol.TypeArguments.Length != 0) && (namedTypeSymbol.TypeArguments[0].TypeKind == TypeKind.Enum)) ||
                    ((namedTypeSymbol.TypeKind == TypeKind.Struct) && (namedTypeSymbol.TypeArguments.Length != 0) && !namedTypeSymbol.TypeArguments[0].ContainingNamespace.Name.StartsWith("System"))
                ))
            {
                memberIsEnum = true;

                // grab the enum type name
                if (namedTypeSymbol.TypeKind == TypeKind.Enum)
                {
                    if (namedTypeSymbol.ContainingType == null)
                    {
                        enumTypeName = $"{namedTypeSymbol.ContainingNamespace}.{namedTypeSymbol.Name}";
                    }
                    else
                    {
                        enumTypeName = $"{namedTypeSymbol.ContainingType.ContainingNamespace}.{namedTypeSymbol.ContainingType.Name}.{namedTypeSymbol.Name}";
                    }
                }
                else if (namedTypeSymbol.TypeArguments.Length != 0)
                {
                    if (namedTypeSymbol.TypeArguments[0].ContainingType == null)
                    {
                        enumTypeName = $"{namedTypeSymbol.TypeArguments[0].ContainingNamespace}.{namedTypeSymbol.TypeArguments[0].Name}";
                    }
                    else
                    {
                        enumTypeName = $"{namedTypeSymbol.TypeArguments[0].ContainingType.ContainingNamespace}.{namedTypeSymbol.TypeArguments[0].ContainingType.Name}.{namedTypeSymbol.TypeArguments[0].Name}";
                    }
                }
                else
                {
                    enumTypeName = namedTypeSymbol.Name;
                }
            }
            else if (namedTypeSymbol != null)
            {
                if (namedTypeSymbol.TypeArguments.Length != 0)
                {
                    if (namedTypeSymbol.Name.Contains("List") ||
                        namedTypeSymbol.Name.Contains("Enumerable"))
                    {
                        memberIsNonScalar = true;
                        if (namedTypeSymbol.TypeArguments[0].ContainingType == null)
                        {
                            jsonTypeName = $"{namedTypeSymbol.TypeArguments[0].ContainingNamespace}.{namedTypeSymbol.TypeArguments[0].Name}";
                        }
                        else
                        {
                            jsonTypeName = $"{namedTypeSymbol.TypeArguments[0].ContainingType.ContainingNamespace}.{namedTypeSymbol.TypeArguments[0].ContainingType.Name}.{namedTypeSymbol.TypeArguments[0].Name}";
                        }
                    }
                    else
                    {
                        if (namedTypeSymbol.TypeArguments[0].ContainingType == null)
                        {
                            jsonTypeName = $"{namedTypeSymbol.TypeArguments[0].ContainingNamespace}.{namedTypeSymbol.TypeArguments[0].Name}";
                        }
                        else
                        {
                            jsonTypeName = $"{namedTypeSymbol.TypeArguments[0].ContainingType.ContainingNamespace}.{namedTypeSymbol.TypeArguments[0].ContainingType.Name}.{namedTypeSymbol.TypeArguments[0].Name}";
                        }
                    }
                }
                else
                {
                    if (namedTypeSymbol.ContainingType == null)
                    {
                        jsonTypeName = $"{namedTypeSymbol.ContainingNamespace}.{namedTypeSymbol.Name}";
                    }
                    else
                    {
                        jsonTypeName = $"{namedTypeSymbol.ContainingType.ContainingNamespace}.{namedTypeSymbol.ContainingType.Name}.{namedTypeSymbol.Name}";
                    }
                }
            }

            bool useJson = !memberIsEnum && !_sqliteTypeMap.ContainsKey(propTypeName);

            // add our column line
            if (isUnindexed)
            {
                createColLines.Add(pds.Identifier.ToString() + " UNINDEXED");
            }
            else
            {
                createColLines.Add(pds.Identifier.ToString());
            }

            // create the select retrieval pair
            if (nullable && _sqliteNullableReadDirectives.TryGetValue(propTypeName, out string? readFormat))
            {
                tableColInfo.Add(new(
                    propName,
                    propTypeName,
                    string.Format(readFormat.Remove(0, 6), pds.Identifier.ToString(), "reader", tableColInfo.Count),
                    string.Format(readFormat, pds.Identifier.ToString(), "reader", tableColInfo.Count),
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
                    string.Format(readFormat.Remove(0, 6), pds.Identifier.ToString(), "reader", tableColInfo.Count),
                    string.Format(readFormat, pds.Identifier.ToString(), "reader", tableColInfo.Count),
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
                        ? string.Format(_sqliteNullableReadDirectives["enum"].Remove(0, 6), pds.Identifier.ToString(), "reader", tableColInfo.Count, enumTypeName)
                        : string.Format(_sqliteReadDirectives["enum"].Remove(0, 6), pds.Identifier.ToString(), "reader", tableColInfo.Count, enumTypeName),
                    nullable
                        ? string.Format(_sqliteNullableReadDirectives["enum"], pds.Identifier.ToString(), "reader", tableColInfo.Count, enumTypeName)
                        : string.Format(_sqliteReadDirectives["enum"], pds.Identifier.ToString(), "reader", tableColInfo.Count, enumTypeName),
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
                tableColInfo.Add(new(
                    pds.Identifier.ToString(),
                    propTypeName,
                    nullable
                        ? string.Format(_sqliteNullableReadDirectives["JSON[]"].Remove(0, 6), pds.Identifier.ToString(), "reader", tableColInfo.Count, jsonTypeName)
                        : string.Format(_sqliteReadDirectives["JSON[]"].Remove(0, 6), pds.Identifier.ToString(), "reader", tableColInfo.Count, jsonTypeName),
                    nullable
                        ? string.Format(_sqliteNullableReadDirectives["JSON[]"], pds.Identifier.ToString(), "reader", tableColInfo.Count, jsonTypeName)
                        : string.Format(_sqliteReadDirectives["JSON[]"], pds.Identifier.ToString(), "reader", tableColInfo.Count, jsonTypeName),
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
                tableColInfo.Add(new(
                    pds.Identifier.ToString(),
                    propTypeName,
                    nullable
                        ? string.Format(_sqliteNullableReadDirectives["JSON"].Remove(0, 6), pds.Identifier.ToString(), "reader", tableColInfo.Count, jsonTypeName)
                        : string.Format(_sqliteReadDirectives["JSON"].Remove(0, 6), pds.Identifier.ToString(), "reader", tableColInfo.Count, jsonTypeName),
                    nullable
                        ? string.Format(_sqliteNullableReadDirectives["JSON"], pds.Identifier.ToString(), "reader", tableColInfo.Count, jsonTypeName)
                        : string.Format(_sqliteReadDirectives["JSON"], pds.Identifier.ToString(), "reader", tableColInfo.Count, jsonTypeName),
                    false,
                    false,
                    nullable,
                    memberIsEnum,
                    useJson,
                    memberIsNonScalar,
                    isUnique));
            }
        }

        context.AddSource(
            $"{className}SQLite.g.cs",
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

                    using System.Collections.Generic;
                    using System.Data;
                    using System.Text;
                    using System.Text.Json;
                                    
                    namespace {{{classNamespace}}};
                
                    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
                    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
                    public partial {{{decForGenCategory(genCategory)}}} {{{className}}}
                    {
                        public static string DefaultTableName => "{{{tableName}}}";
                        public static string SourceTableName => "{{{sourceTableName}}}";
                    
                        public static bool CreateTable(IDbConnection dbConnection, string? dbTableName = null)
                        {
                            dbTableName ??= "{{{tableName}}}";

                            IDbCommand command = dbConnection.CreateCommand();
                            command.CommandText = $"""
                                CREATE VIRTUAL TABLE IF NOT EXISTS {dbTableName} using fts5 (
                                    {{{string.Join(_comma_line_4, createColLines)}}}
                                )
                                """;

                            command.ExecuteNonQuery();
                    
                            return true;
                        }

                        public static bool DropTable(IDbConnection dbConnection, string? dbTableName = null)
                        {
                            dbTableName ??= "{{{tableName}}}";
                    
                            IDbCommand command = dbConnection.CreateCommand();
                            command.CommandText = $"DROP TABLE IF EXISTS {dbTableName}";
                    
                            command.ExecuteNonQuery();
                    
                            return true;
                        }

                        public static int Populate(
                            IDbConnection dbConnection,
                            string? dbTableName = null,
                            string? sourceTableName = null,
                            bool sanitizeText = false)
                        {
                            dbTableName ??= "{{{tableName}}}";
                            sourceTableName ??= "{{{sourceTableName}}}";
                            int rowsAffected = 0;

                            if (sanitizeText == false)
                            {
                                IDbCommand popCommand = dbConnection.CreateCommand();
                                popCommand.CommandText = $"""
                                    INSERT INTO {dbTableName} ({{{string.Join(", ", tableColInfo.Select(c => c.name))}}})
                                    SELECT {{{string.Join(", ", tableColInfo.Select(c => c.name))}}} FROM {sourceTableName}
                                    """;
                                rowsAffected = popCommand.ExecuteNonQuery();

                                return rowsAffected;
                            }

                            IDbCommand readCommand = dbConnection.CreateCommand();
                            readCommand.CommandText = $"""
                                SELECT {{{string.Join(", ", tableColInfo.Select(c => c.name))}}} FROM {sourceTableName}
                                """;

                            using (IDataReader reader = readCommand.ExecuteReader())
                            using (IDbTransaction transaction = dbConnection.BeginTransaction())
                            {
                                IDbCommand command = dbConnection.CreateCommand();
                                command.CommandText = $"""
                                    INSERT INTO {dbTableName} ({{{string.Join(", ", tableColInfo.Select(c => c.name))}}})
                                    VALUES ({{{string.Join(", ", tableColInfo.Select(c => $"${c.name}"))}}})
                                    """;

                                {{{string.Join(_line_3, getPopulateCleanLines(addValue: false))}}}
                                        
                                command.Prepare();
                                
                                while (reader.Read())
                                {
                                    {{{string.Join(_line_4, getPopulateCleanLines(addParam: false))}}}

                                    int ra = command.ExecuteNonQuery();
                                    if (ra == 0) throw new Exception("Failed to insert cleaned FTS value!");
                                    rowsAffected += ra;
                                }

                                transaction.Commit();
                            }

                            return rowsAffected;
                        }

                        public static List<{{{className}}}> Select(
                            IDbConnection dbConnection,
                            List<string> matchTerms,
                            string? dbTableName = null,
                            string[]? orderByProperties = null,
                            string? orderByDirection = null)
                        {
                            dbTableName ??= "{{{tableName}}}";

                            List<{{{className}}}> results = new();

                            IDbCommand command = dbConnection.CreateCommand();
                            command.CommandText = $"SELECT {{{string.Join(", ", tableColInfo.Select(p => p.name))}}} FROM {dbTableName}";
                    
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
                    
                            if ((orderByProperties != null) && (orderByProperties.Length > 0))
                            {
                                command.CommandText += $" ORDER BY {string.Join(", ", orderByProperties)}";
                                if (orderByDirection?.StartsWith("d", StringComparison.OrdinalIgnoreCase) == true)
                                {
                                    command.CommandText += $" DESC";
                                }
                                else
                                {
                                    command.CommandText += $" ASC";
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

                        public static int SelectCount(
                            IDbConnection dbConnection, 
                            List<string> matchTerms,
                            string? dbTableName = null)
                        {
                            dbTableName ??= "{{{tableName}}}";
                    
                            IDbCommand command = dbConnection.CreateCommand();
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

                        private static readonly System.Text.RegularExpressions.Regex _htmlStripRegex = new("<.*?>", System.Text.RegularExpressions.RegexOptions.Compiled);

                        private static string StripHtml(string? input)
                        {
                            if (string.IsNullOrWhiteSpace(input))
                            {
                                return string.Empty;
                            }

                            return _htmlStripRegex.Replace(input, string.Empty).Trim();
                        }
                    }

                    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
                    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
                    public static class {{{className}}}Extensions
                    {
                        public static List<{{{className}}}> Select<T>(this IDbConnection dbCon, List<string> matchTerms, string? dbTableName = null, string[]? orderByProperties = null, string? orderByDirection = null)
                            where T : {{{className}}}
                        {
                            return {{{className}}}.Select(dbCon, matchTerms, dbTableName, orderByProperties, orderByDirection);
                        }

                        public static int SelectCount<T>(this IDbConnection dbCon, List<string> matchTerms, string? dbTableName = null)
                            where T : {{{className}}}
                        {
                            return {{{className}}}.SelectCount(dbCon, matchTerms, dbTableName);
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

    private static Dictionary<string, string> _sqliteReadDirectives = new()
    {
        { "bool", "{0} = {1}.GetBoolean({2})" },
        { "byte", "{0} = {1}.GetByte({2})" },
        { "byte[]", "{0} = {1}.GetBytes({2})" },
        { "char", "{0} = {1}.GetChar({2})" },
        { "char[]", "{0} = {1}.GetChars({2})" },
        { "DateTime", "{0} = {1}.GetDateTime({2})" },
        { "DateTimeOffset", "{0} = new DateTimeOffset({1}.GetDateTime({2}))" },
        { "Decimal", "{0} = {1}.GetDecimal({2})" },
        { "double", "{0} = {1}.GetDouble({2})" },
        { "enum", "{0} = Enum.Parse<{3}>({1}.GetString({2}))" },
        { "float", "{0} = {1}.GetFloat({2})" },
        { "Guid", "{0} = {1}.GetGuid({2})" },
        { "short", "{0} = {1}.GetInt16({2})" },
        { "int", "{0} = {1}.GetInt32({2})" },
        { "long", "{0} = {1}.GetInt64({2})" },
        { "sbyte", "(sbyte){0} = {1}.GetByte({2})" },
        { "string", "{0} = {1}.GetString({2})" },
        { "TimeSpan", "{0} = {1}.GetTimeSpan({2})" },
        { "ushort", "(ushort){0} = {1}.GetInt16({2})" },
        { "uint", "(uint){0} = {1}.GetInt32({2})" },
        { "ulong", "(ulong){0} = {1}.GetInt64({2})" },
        { "Uri", "{0} = new Uri({1}.GetString({2}))" },
        { "JSON", "{0} = ParseFromDb<{3}>({1}.GetString({2})) ?? new {3}()" },
        //{ "JSON", "{0} = ParseFromDb({1}.GetString({2}), typeof({3})) ?? new {3}()" },
        { "JSON[]", "{0} = ParseArrayFromDb<{3}>({1}.GetString({2})) ?? new List<{3}>()" },
        //{ "JSON[]", "{0} = ParseArrayFromDb({1}.GetString({2}), typeof({3})) ?? new List<{3}>()" },
    };

    private static Dictionary<string, string> _sqliteNullableReadDirectives = new()
    {
        { "bool", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetBoolean({2})" },
        { "byte", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetByte({2})" },
        { "byte[]", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetBytes({2})" },
        { "char", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetChar({2})" },
        { "char[]", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetChars({2})" },
        { "DateTime", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetDateTime({2})" },
        { "DateTimeOffset", "{0} = {1}.IsDBNull({2}) ? null : new DateTimeOffset({1}.GetDateTime({2}))" },
        { "Decimal", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetDecimal({2})" },
        { "double", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetDouble({2})" },
        { "enum", "{0} = {1}.IsDBNull({2}) ? null : Enum.Parse<{3}>({1}.GetString({2}))" },
        { "float", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetFloat({2})" },
        { "Guid", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetGuid({2})" },
        { "short", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetInt16({2})" },
        { "int", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetInt32({2})" },
        { "long", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetInt64({2})" },
        { "sbyte", "(sbyte){0} = {1}.IsDBNull({2}) ? null : {1}.GetByte({2})" },
        { "string", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetString({2})" },
        { "TimeSpan", "{0} = {1}.IsDBNull({2}) ? null : {1}.GetTimeSpan({2})" },
        { "ushort", "(ushort){0} = {1}.IsDBNull({2}) ? null : {1}.GetInt16({2})" },
        { "uint", "(uint){0} = {1}.IsDBNull({2}) ? null : {1}.GetInt32({2})" },
        { "ulong", "(ulong){0} = {1}.IsDBNull({2}) ? null : {1}.GetInt64({2})" },
        { "Uri", "{0} = {1}.IsDBNull({2}) ? null : new Uri({1}.GetString({2}))" },
        { "JSON", "{0} = {1}.IsDBNull({2}) ? null : ParseFromDb<{3}>({1}.GetString({2}))" },
        //{ "JSON", "{0} = {1}.IsDBNull({2}) ? null : ParseFromDb({1}.GetString({2}), typeof({3}))" },
        { "JSON[]", "{0} = {1}.IsDBNull({2}) ? null : ParseArrayFromDb<{3}>({1}.GetString({2}))" },
        //{ "JSON[]", "{0} = {1}.IsDBNull({2}) ? null : ParseArrayFromDb({1}.GetString({2}), typeof({3}))" },
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
        { "Decimal", "TEXT" },
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
