namespace CsLightDbGen.SQLiteGenerator;

internal static class RuntimeHelpersEmitter
{
    internal static string Emit(string runtimeNamespace) => $$$"""
        #nullable enable
        namespace {{{runtimeNamespace}}}
        {
            /// <summary>
            /// Thrown by generated keyed by-key mutations (Update/Delete) when a command that was
            /// expected to affect at least one row affected none (for example, a stale or
            /// already-deleted key). Carries the originating model name and SQL text. Keyless
            /// models never throw this because their by-key predicate matches nothing.
            /// </summary>
            public sealed class {{{GeneratorAttributes._ldgCommandFailedException}}} : System.Exception
            {
                public {{{GeneratorAttributes._ldgCommandFailedException}}}(string modelName, string sql)
                    : base($"The command for model '{modelName}' affected no rows (expected at least one). SQL: {sql}")
                {
                    ModelName = modelName;
                    Sql = sql;
                }

                public string ModelName { get; }

                public string Sql { get; }
            }
        }
        #nullable restore
        """;
}
