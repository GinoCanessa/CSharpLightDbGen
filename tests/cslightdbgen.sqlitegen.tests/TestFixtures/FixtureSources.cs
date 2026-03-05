namespace cslightdbgen.sqlitegen.tests.TestFixtures;

internal static class FixtureSources
{
    public const string BasicTableFixture = """
using System.Collections.Generic;
using CsLightDbGen.SQLiteGenerator;

namespace CsLightDbGen.SQLiteGenerator;

[LdgSQLiteTable("basic_table")]
[LdgSQLiteIndex("Name", "ParentKey")]
public partial class BasicEntity
{
    [LdgSQLiteKey]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int ParentKey { get; set; }

    [LdgSQLiteForeignKey(ReferenceTable = "Parent", ReferenceColumn = "Id")]
    public int ParentForeignKey { get; set; }

    [LdgSQLiteUnique]
    public string UniqueCode { get; set; } = string.Empty;

    public int? OptionalScore { get; set; }

    [LdgSQLiteIgnore]
    public string? IgnoredNote { get; set; }

    public CustomMeta Metadata { get; set; } = new();

    public List<MetaTag> Tags { get; set; } = new();
}

public sealed class CustomMeta
{
    public string? Value { get; set; }
}

public sealed class MetaTag
{
    public string Label { get; set; } = string.Empty;
}
""";

    public const string TableWithIndexFixture = """
using CsLightDbGen.SQLiteGenerator;

namespace CsLightDbGen.SQLiteGenerator;

[LdgSQLiteTable]
[LdgSQLiteIndex("ColA", "ColB")]
public partial class IndexedEntity
{
    [LdgSQLiteKey]
    public int Id { get; set; }

    public string ColA { get; set; } = string.Empty;

    public string ColB { get; set; } = string.Empty;
}
""";

    public const string TableWithJsonFixture = """
using System.Collections.Generic;
using CsLightDbGen.SQLiteGenerator;

namespace CsLightDbGen.SQLiteGenerator;

[LdgSQLiteTable]
public partial class JsonEntity
{
    [LdgSQLiteKey]
    public int Id { get; set; }

    public JsonPayload Payload { get; set; } = new();

    public List<JsonTag> PayloadTags { get; set; } = new();
}

public sealed class JsonPayload
{
    public string? Name { get; set; }
}

public sealed class JsonTag
{
    public string Tag { get; set; } = string.Empty;
}
""";

    public const string RecordTableFixture = """
using CsLightDbGen.SQLiteGenerator;

namespace CsLightDbGen.SQLiteGenerator;

[LdgSQLiteTable]
public partial record RecordEntity
{
    [LdgSQLiteKey]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
""";

    public const string InheritanceFixture = """
using CsLightDbGen.SQLiteGenerator;

namespace CsLightDbGen.SQLiteGenerator;

[LdgSQLiteBaseClass]
public partial class BaseEntity
{
    public string BaseName { get; set; } = string.Empty;
}

[LdgSQLiteTable]
public partial class DerivedEntity : BaseEntity
{
    [LdgSQLiteKey]
    public int Id { get; set; }

    public string DerivedName { get; set; } = string.Empty;
}
""";

    public const string FtsFixture = """
using CsLightDbGen.SQLiteGenerator;

namespace CsLightDbGen.SQLiteGenerator;

[LdgSQLiteFtsTable("source_table")]
public partial class FtsEntity
{
    public string Title { get; set; } = string.Empty;

    [LdgSQLiteFtsUnindexed]
    public string? RawHtml { get; set; }
}
""";
}
