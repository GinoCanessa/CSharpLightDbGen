using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CsLightDbGen.SQLiteGenerator;

/// <summary>Semantic configuration is resolved here; only equatable descriptions leave this boundary.</summary>
internal static class GeneratorConfiguration
{
    internal const string MultiHostSymbol = "CSLIGHTDBGEN_MULTI_HOST";

    internal static GenerationParseOptions ResolveParseOptions(ParseOptions options) =>
        new(options.PreprocessorSymbolNames.Contains(MultiHostSymbol, StringComparer.Ordinal));

    internal static JsonOptionsResolution ResolveJsonOptionsBinding(
        INamedTypeSymbol model,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        AttributeData? annotation = model.GetAttributes().FirstOrDefault(static attribute =>
            attribute.AttributeClass?.ToDisplayString() ==
                LightSQLiteGenerator.AttributesNamespace + "." + GeneratorAttributes._ldgSQLiteJsonOptions);
        if (annotation is null)
        {
            return default;
        }

        LocationInfo? location = LocationInfo.From(annotation.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation())
            ?? LocationInfo.From(model);
        JsonOptionsResolution Invalid(string reason) =>
            new(null, new[] { new ConfigurationDiagnostic(reason, location) }.ToEquatableArray());

        INamedTypeSymbol? factoryType = annotation.ConstructorArguments.Length > 0
            ? annotation.ConstructorArguments[0].Value as INamedTypeSymbol
            : null;
        string? factoryMethod = annotation.ConstructorArguments.Length > 1
            ? annotation.ConstructorArguments[1].Value as string
            : null;
        if (factoryType is null || factoryType.TypeKind == TypeKind.Error || !IsClosed(factoryType))
        {
            return Invalid("FactoryType must be an accessible closed named type");
        }

        for (INamedTypeSymbol? container = factoryType; container != null; container = container.ContainingType)
        {
            if (container.IsFileLocal || !compilation.IsSymbolAccessibleWithin(container, compilation.Assembly))
            {
                return Invalid("FactoryType and all containing types must be accessible from a separate generated source file (not file-local)");
            }
        }

        if (factoryMethod is null || factoryMethod.Length == 0)
        {
            return Invalid("FactoryMethod must name a declared ordinary static non-generic zero-parameter method");
        }

        // Parameterized overloads, including optional parameters, are not zero-parameter factories.
        IMethodSymbol[] methods = factoryType.GetMembers(factoryMethod).OfType<IMethodSymbol>()
            .Where(static method => method.Parameters.Length == 0).ToArray();
        if (methods.Length != 1)
        {
            return Invalid($"Factory '{factoryType.ToDisplayString()}.{factoryMethod}' must declare exactly one accessible zero-parameter method with that exact name");
        }

        IMethodSymbol method = methods[0];
        if (method.MethodKind != MethodKind.Ordinary || !method.IsStatic || method.IsGenericMethod ||
            method.ExplicitInterfaceImplementations.Length != 0 ||
            (factoryType.TypeKind == TypeKind.Interface && (method.IsAbstract || method.IsVirtual)) ||
            !compilation.IsSymbolAccessibleWithin(method, compilation.Assembly))
        {
            return Invalid("FactoryMethod must be an accessible ordinary static non-generic zero-parameter method, directly invocable from generated source");
        }

        INamedTypeSymbol? optionsType = compilation.GetTypeByMetadataName("System.Text.Json.JsonSerializerOptions");
        if (optionsType is null || !SymbolEqualityComparer.Default.Equals(method.ReturnType, optionsType))
        {
            return Invalid("FactoryMethod must return this compilation's System.Text.Json.JsonSerializerOptions type");
        }

        string identifier = SyntaxFacts.GetKeywordKind(method.Name) != SyntaxKind.None ||
            SyntaxFacts.GetContextualKeywordKind(method.Name) != SyntaxKind.None ? "@" + method.Name : method.Name;
        string invocation = factoryType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "." + identifier + "()";
        return new JsonOptionsResolution(
            new JsonOptionsBinding(invocation, model.ToDisplayString()), EquatableArray<ConfigurationDiagnostic>.Empty);
    }

    private static bool IsClosed(ITypeSymbol type)
    {
        if (type is ITypeParameterSymbol || type.TypeKind == TypeKind.Error)
        {
            return false;
        }
        if (type is IArrayTypeSymbol array)
        {
            return IsClosed(array.ElementType);
        }
        if (type is INamedTypeSymbol named)
        {
            return !named.IsUnboundGenericType &&
                (named.ContainingType is null || IsClosed(named.ContainingType)) &&
                named.TypeArguments.All(IsClosed);
        }
        return true;
    }

    internal static GenerationEnvironment ResolveEnvironment(
        Compilation compilation,
        GenerationParseOptions options,
        CancellationToken cancellationToken)
    {
        AttributeData? annotation = compilation.Assembly.GetAttributes().FirstOrDefault(static attribute =>
            attribute.AttributeClass?.ToDisplayString() ==
                LightSQLiteGenerator.AttributesNamespace + "." + GeneratorAttributes._ldgSQLiteRuntimeNamespace);
        LocationInfo? location = LocationInfo.From(annotation?.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation());
        List<ConfigurationDiagnostic> diagnostics = [];
        string runtimeNamespace = LightSQLiteGenerator.AttributesNamespace;

        if (!options.MultiHost)
        {
            if (annotation != null)
            {
                diagnostics.Add(new ConfigurationDiagnostic(
                    $"Define the project-wide {MultiHostSymbol} symbol to use LdgSQLiteRuntimeNamespace, or remove the annotation",
                    location));
            }

            // Default hosts retain their historical public helper and duplicate-type warning policy.
            return new GenerationEnvironment(new RuntimeHelperNames(runtimeNamespace), false, diagnostics.ToEquatableArray());
        }

        string? configuredNamespace = annotation?.ConstructorArguments.FirstOrDefault().Value as string;
        if (annotation is null)
        {
            diagnostics.Add(new ConfigurationDiagnostic(
                $"Add [assembly: {LightSQLiteGenerator.AttributesNamespace}.{GeneratorAttributes._ldgSQLiteRuntimeNamespace}(\"Your.Host.Runtime\")] when {MultiHostSymbol} is defined",
                null));
        }
        else if (configuredNamespace is null || !IsOrdinaryNamespace(configuredNamespace))
        {
            diagnostics.Add(new ConfigurationDiagnostic(
                "Choose a nonempty dot-separated namespace of ordinary C# identifiers; keywords, escaped identifiers and source fragments are not allowed",
                location));
        }
        else if (configuredNamespace == LightSQLiteGenerator.AttributesNamespace)
        {
            diagnostics.Add(new ConfigurationDiagnostic(
                $"Choose a host-specific runtime namespace other than '{LightSQLiteGenerator.AttributesNamespace}'",
                location));
        }
        else
        {
            runtimeNamespace = configuredNamespace;
            string metadataName = runtimeNamespace + "." + GeneratorAttributes._ldgCommandFailedException;
            if (compilation.Assembly.GetTypeByMetadataName(metadataName) != null)
            {
                diagnostics.Add(new ConfigurationDiagnostic(
                    $"Runtime helper '{metadataName}' already exists in this assembly; choose a different runtime namespace",
                    location));
            }

            // Compilation.GetTypeByMetadataName returns null on ambiguity. Inspect each image
            // independently so two colliding referenced hosts cannot masquerade as absence.
            foreach (IAssemblySymbol reference in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reference.GetTypeByMetadataName(metadataName) != null)
                {
                    diagnostics.Add(new ConfigurationDiagnostic(
                        $"Runtime helper '{metadataName}' already exists in referenced assembly '{reference.Identity.Name}'; choose a different runtime namespace",
                        location));
                }
            }
        }

        return new GenerationEnvironment(new RuntimeHelperNames(runtimeNamespace), true, diagnostics.ToEquatableArray());
    }

    private static bool IsOrdinaryNamespace(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        foreach (string segment in value.Split('.'))
        {
            if (segment.Length == 0 || segment[0] == '@' || segment.IndexOf('\\') >= 0 ||
                !SyntaxFacts.IsValidIdentifier(segment) ||
                SyntaxFacts.GetKeywordKind(segment) != SyntaxKind.None)
            {
                return false;
            }
        }

        return true;
    }
}
