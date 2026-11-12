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
