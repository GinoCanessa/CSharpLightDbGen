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
    internal const string _ldgSQLiteForeignKey = "LdgSQLiteForeignKey";
    internal const string _ldgSQLiteIgnore = "LdgSQLiteIgnore";
    internal const string _ldgSQLiteUnique = "LdgSQLiteUnique";

    internal const string _ldgSQLiteFtsTable = "LdgSQLiteFtsTable";
    internal const string _ldgSQLiteFtsUnindexed = "LdgSQLiteFtsUnindexed";

    internal static HashSet<string> _ldAttributes = [
        _ldgSQLiteBaseClass,
        _ldgSQLiteTable,
        _ldgSQLiteIndex,
        _ldgSQLiteKey,
        _ldgSQLiteForeignKey,
        _ldgSQLiteIgnore,
        _ldgSQLiteUnique,
        _ldgSQLiteFtsTable,
        _ldgSQLiteFtsUnindexed,
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
            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
            public class {{{_ldgSQLiteBaseClass}}} : System.Attribute
            {
                public {{{_ldgSQLiteBaseClass}}}()
                {
                }
            }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
            public class {{{_ldgSQLiteTable}}} : System.Attribute
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
            public class {{{_ldgSQLiteIndex}}} : System.Attribute
            {
                public string[] Columns { get; set; }
        
                public {{{_ldgSQLiteIndex }}}(params string[] columns)
                {
                    Columns = columns;
                }
            }
        
            [System.AttributeUsage(System.AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
            public class {{{_ldgSQLiteKey}}} : System.Attribute
            {
                public bool AutoIncrement { get; set; }
                public {{{_ldgSQLiteKey}}}(bool autoIncrement = true)
                {
                    AutoIncrement = autoIncrement;
                }
            }

            [System.AttributeUsage(System.AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
            public class {{{_ldgSQLiteForeignKey}}} : System.Attribute
            {
                public string? ReferenceTable { get; set; }
                public string? ReferenceColumn { get; set; }
                public string? ModelTypeName { get; set; }
                public {{{_ldgSQLiteForeignKey}}}(string? referenceTable = null, string? referenceColumn = null, string? modelTypeName = null)
                {
                    ReferenceTable = referenceTable;
                    ReferenceColumn = referenceColumn;
                    ModelTypeName = modelTypeName;
                }
            }

            [System.AttributeUsage(System.AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
            public class {{{_ldgSQLiteIgnore}}} : System.Attribute
            {
                public {{{_ldgSQLiteIgnore}}}()
                {
                }
            }

            [System.AttributeUsage(System.AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
            public class {{{_ldgSQLiteUnique}}} : System.Attribute
            {
                public {{{_ldgSQLiteUnique}}}()
                {
                }
            }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
            public class {{{_ldgSQLiteFtsTable}}} : System.Attribute
            {
                public string? TableName { get; set; }
                public string? SourceTableName { get; set; }
        
                public {{{_ldgSQLiteFtsTable}}}(string sourceTable, string? tableName = null)
                {
                    SourceTableName = sourceTable;
                    TableName = tableName == null ? (sourceTable + "_fts") : tableName;
                }
            }
        
            [System.AttributeUsage(System.AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
            public class {{{_ldgSQLiteFtsUnindexed}}} : System.Attribute
            {
                public {{{_ldgSQLiteFtsUnindexed}}}()
                {
                }
            }
        }
        #nullable restore
        """;
}
