using System;
using System.Collections.Generic;
using System.Text;

namespace CsLightDbGen.SQLiteGenerator;

public class GeneratorAttributes
{
    internal const string _ldgSQLiteBaseClass = "LdgSQLiteBaseClass";
    internal const string _ldgSQLiteTable = "LdgSQLiteTable";
    internal const string _ldgSQLiteIndex = "LdgSQLiteIndex";
    internal const string _ldgSQLiteKey = "LdgSQLiteKey";
    internal const string _ldgSQLiteKeyNoUnique = "LdgSQLiteKeyNoUnique";
    internal const string _ldgSQLiteForeignKey = "LdgSQLiteForeignKey";
    internal const string _ldgSQLiteForeignKeyComposite = "LdgSQLiteForeignKeyComposite";
    internal const string _ldgSQLiteFkAction = "LdgSQLiteFkAction";
    internal const string _ldgSQLiteIgnore = "LdgSQLiteIgnore";
    internal const string _ldgSQLiteUnique = "LdgSQLiteUnique";
    internal const string _ldgSQLiteMultiSelect = "LdgSQLiteMultiSelect";
    internal const string _ldgSQLiteDefault = "LdgSQLiteDefault";

    internal const string _ldgSQLiteFtsTable = "LdgSQLiteFtsTable";
    internal const string _ldgSQLiteFtsUnindexed = "LdgSQLiteFtsUnindexed";
    internal const string _ldgSQLiteRuntimeNamespace = "LdgSQLiteRuntimeNamespace";
    internal const string _ldgSQLiteJsonOptions = "LdgSQLiteJsonOptions";

    internal const string _ldgCommandFailedException = "LdgCommandFailedException";

    internal static HashSet<string> _ldAttributes = [
        _ldgSQLiteBaseClass,
        _ldgSQLiteTable,
        _ldgSQLiteIndex,
        _ldgSQLiteKey,
        _ldgSQLiteKeyNoUnique,
        _ldgSQLiteForeignKey,
        _ldgSQLiteForeignKeyComposite,
        _ldgSQLiteIgnore,
        _ldgSQLiteUnique,
        _ldgSQLiteMultiSelect,
        _ldgSQLiteDefault,
        _ldgSQLiteFtsTable,
        _ldgSQLiteFtsUnindexed,
        _ldgSQLiteJsonOptions,
        ];

    internal static HashSet<string> _ldClassAttributes = [
        _ldgSQLiteBaseClass,
        _ldgSQLiteTable,
        _ldgSQLiteFtsTable,
        ];

    internal const string LdgAttributes = $$$"""
        #nullable enable
        namespace CsLightDbGen.SQLiteGenerator
        {
            [System.AttributeUsage(System.AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
        #if CSLIGHTDBGEN_MULTI_HOST
            internal class {{{_ldgSQLiteRuntimeNamespace}}} : System.Attribute
        #else
            public class {{{_ldgSQLiteRuntimeNamespace}}} : System.Attribute
        #endif
            {
                public {{{_ldgSQLiteRuntimeNamespace}}}(string value)
                {
                    Value = value;
                }

                public string Value { get; }
            }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
        #if CSLIGHTDBGEN_MULTI_HOST
            internal class {{{_ldgSQLiteJsonOptions}}} : System.Attribute
        #else
            public class {{{_ldgSQLiteJsonOptions}}} : System.Attribute
        #endif
            {
                public {{{_ldgSQLiteJsonOptions}}}(System.Type factoryType, string factoryMethod)
                {
                    FactoryType = factoryType;
                    FactoryMethod = factoryMethod;
                }

                public System.Type FactoryType { get; }
                public string FactoryMethod { get; }
            }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
        #if CSLIGHTDBGEN_MULTI_HOST
            internal class {{{_ldgSQLiteBaseClass}}} : System.Attribute
        #else
            public class {{{_ldgSQLiteBaseClass}}} : System.Attribute
        #endif
            {
                public {{{_ldgSQLiteBaseClass}}}()
                {
                }
            }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
        #if CSLIGHTDBGEN_MULTI_HOST
            internal class {{{_ldgSQLiteTable}}} : System.Attribute
        #else
            public class {{{_ldgSQLiteTable}}} : System.Attribute
        #endif
            {
                public string? TableName { get; set; }
                public bool DynamicTableNames { get; set; }

                public {{{_ldgSQLiteTable}}}(string? tableName = null, bool dynamicTableNames = false)
                {
                    TableName = tableName;
                    DynamicTableNames = dynamicTableNames;
                }
            }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
        #if CSLIGHTDBGEN_MULTI_HOST
            internal class {{{_ldgSQLiteIndex}}} : System.Attribute
        #else
            public class {{{_ldgSQLiteIndex}}} : System.Attribute
        #endif
            {
                public string[] Columns { get; set; }

                public bool Unique { get; set; }

                public string? Where { get; set; }
        
                public {{{_ldgSQLiteIndex }}}(params string[] columns)
                {
                    Columns = columns;
                }
            }
        
            [System.AttributeUsage(System.AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
        #if CSLIGHTDBGEN_MULTI_HOST
            internal class {{{_ldgSQLiteKey}}} : System.Attribute
        #else
            public class {{{_ldgSQLiteKey}}} : System.Attribute
        #endif
            {
                public bool AutoIncrement { get; set; }
                public {{{_ldgSQLiteKey}}}(bool autoIncrement = true)
                {
                    AutoIncrement = autoIncrement;
                }
            }

            [System.AttributeUsage(System.AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
        #if CSLIGHTDBGEN_MULTI_HOST
            internal class {{{_ldgSQLiteKeyNoUnique}}} : System.Attribute
        #else
            public class {{{_ldgSQLiteKeyNoUnique}}} : System.Attribute
        #endif
            {
                public bool AutoIncrement { get; set; }
                public {{{_ldgSQLiteKeyNoUnique}}}(bool autoIncrement = true)
                {
                    AutoIncrement = autoIncrement;
                }
            }

            [System.AttributeUsage(System.AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
        #if CSLIGHTDBGEN_MULTI_HOST
            internal class {{{_ldgSQLiteForeignKey}}} : System.Attribute
        #else
            public class {{{_ldgSQLiteForeignKey}}} : System.Attribute
        #endif
            {
                public string? ReferenceTable { get; set; }
                public string? ReferenceColumn { get; set; }
                public string? ModelTypeName { get; set; }
                public {{{_ldgSQLiteFkAction}}} OnDelete { get; set; }
                public {{{_ldgSQLiteFkAction}}} OnUpdate { get; set; }
                public {{{_ldgSQLiteForeignKey}}}(string? referenceTable = null, string? referenceColumn = null, string? modelTypeName = null, {{{_ldgSQLiteFkAction}}} onDelete = {{{_ldgSQLiteFkAction}}}.NoAction, {{{_ldgSQLiteFkAction}}} onUpdate = {{{_ldgSQLiteFkAction}}}.NoAction)
                {
                    ReferenceTable = referenceTable;
                    ReferenceColumn = referenceColumn;
                    ModelTypeName = modelTypeName;
                    OnDelete = onDelete;
                    OnUpdate = onUpdate;
                }
            }

        #if CSLIGHTDBGEN_MULTI_HOST
            internal enum {{{_ldgSQLiteFkAction}}}
        #else
            public enum {{{_ldgSQLiteFkAction}}}
        #endif
            {
                NoAction = 0,
                Restrict = 1,
                SetNull = 2,
                SetDefault = 3,
                Cascade = 4,
            }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
        #if CSLIGHTDBGEN_MULTI_HOST
            internal class {{{_ldgSQLiteForeignKeyComposite}}} : System.Attribute
        #else
            public class {{{_ldgSQLiteForeignKeyComposite}}} : System.Attribute
        #endif
            {
                public string[] Columns { get; set; }
                public string ReferenceTable { get; set; }
                public string[] ReferenceColumns { get; set; }
                public {{{_ldgSQLiteFkAction}}} OnDelete { get; set; }
                public {{{_ldgSQLiteFkAction}}} OnUpdate { get; set; }
                public {{{_ldgSQLiteForeignKeyComposite}}}(string[] columns, string referenceTable, string[] referenceColumns, {{{_ldgSQLiteFkAction}}} onDelete = {{{_ldgSQLiteFkAction}}}.NoAction, {{{_ldgSQLiteFkAction}}} onUpdate = {{{_ldgSQLiteFkAction}}}.NoAction)
                {
                    Columns = columns;
                    ReferenceTable = referenceTable;
                    ReferenceColumns = referenceColumns;
                    OnDelete = onDelete;
                    OnUpdate = onUpdate;
                }
            }

            [System.AttributeUsage(System.AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
        #if CSLIGHTDBGEN_MULTI_HOST
            internal class {{{_ldgSQLiteIgnore}}} : System.Attribute
        #else
            public class {{{_ldgSQLiteIgnore}}} : System.Attribute
        #endif
            {
                public {{{_ldgSQLiteIgnore}}}()
                {
                }
            }

            [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
        #if CSLIGHTDBGEN_MULTI_HOST
            internal class {{{_ldgSQLiteUnique}}} : System.Attribute
        #else
            public class {{{_ldgSQLiteUnique}}} : System.Attribute
        #endif
            {
                public string[] Columns { get; set; }

                public {{{_ldgSQLiteUnique}}}(params string[] columns)
                {
                    Columns = columns;
                }
            }

            [System.AttributeUsage(System.AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
        #if CSLIGHTDBGEN_MULTI_HOST
            internal class {{{_ldgSQLiteMultiSelect}}} : System.Attribute
        #else
            public class {{{_ldgSQLiteMultiSelect}}} : System.Attribute
        #endif
            {
                public {{{_ldgSQLiteMultiSelect}}}()
                {
                }
            }

            [System.AttributeUsage(System.AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
        #if CSLIGHTDBGEN_MULTI_HOST
            internal class {{{_ldgSQLiteDefault}}} : System.Attribute
        #else
            public class {{{_ldgSQLiteDefault}}} : System.Attribute
        #endif
            {
                public object? Value { get; set; }
                public bool Raw { get; set; }

                public {{{_ldgSQLiteDefault}}}(object? value = null, bool raw = false)
                {
                    Value = value;
                    Raw = raw;
                }
            }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
        #if CSLIGHTDBGEN_MULTI_HOST
            internal class {{{_ldgSQLiteFtsTable}}} : System.Attribute
        #else
            public class {{{_ldgSQLiteFtsTable}}} : System.Attribute
        #endif
            {
                public string? TableName { get; set; }
                public string? SourceTableName { get; set; }
                public string? Tokenizer { get; set; }
        
                public {{{_ldgSQLiteFtsTable}}}(string sourceTable, string? tableName = null, string? tokenizer = null)
                {
                    SourceTableName = sourceTable;
                    TableName = tableName == null ? (sourceTable + "_fts") : tableName;
                    Tokenizer = tokenizer;
                }
            }
        
            [System.AttributeUsage(System.AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
        #if CSLIGHTDBGEN_MULTI_HOST
            internal class {{{_ldgSQLiteFtsUnindexed}}} : System.Attribute
        #else
            public class {{{_ldgSQLiteFtsUnindexed}}} : System.Attribute
        #endif
            {
                public {{{_ldgSQLiteFtsUnindexed}}}()
                {
                }
            }

        }
        #nullable restore
        """;
}
