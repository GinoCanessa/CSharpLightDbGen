namespace CsLightDbGen.SQLiteGenerator;

/// <summary>Owns the shared table/FTS wrappers; SQL binding and read shapes stay at their call sites.</summary>
internal static class JsonPersistenceEmitter
{
    internal static string Emit(JsonOptionsBinding? binding)
    {
        if (binding is not JsonOptionsBinding configured)
        {
            return DefaultMembers;
        }

        string message = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(
            $"JSON options factory '{configured.FactoryInvocation}' for model '{configured.ModelName}' returned null.", true);
        string storage = $$"""
            private static readonly global::System.Lazy<JsonSerializerOptions> _jsonOptions = new(
                () =>
                {
                    JsonSerializerOptions? returnedOptions = {{configured.FactoryInvocation}};
                    if (returnedOptions == null)
                    {
                        throw new global::System.InvalidOperationException({{message}});
                    }

                    return new JsonSerializerOptions(returnedOptions);
                },
                global::System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

            """;
        return storage + DefaultMembers.Substring(DefaultMembers.IndexOf("private static bool", System.StringComparison.Ordinal))
            .Replace(", _options)", ", _jsonOptions.Value)");
    }

    private const string DefaultMembers = """
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
}
