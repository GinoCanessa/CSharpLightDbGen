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

    public const string DefaultsFixture = """
using CsLightDbGen.SQLiteGenerator;

namespace CsLightDbGen.SQLiteGenerator;

[LdgSQLiteTable("defaults_table")]
public partial class DefaultsEntity
{
    [LdgSQLiteKey]
    public int Id { get; set; }

    [LdgSQLiteDefault(0)]
    public int RetryCount { get; set; }

    [LdgSQLiteDefault("queued")]
    public string Status { get; set; } = string.Empty;

    [LdgSQLiteDefault("CURRENT_TIMESTAMP", raw: true)]
    public string CreatedAt { get; set; } = string.Empty;

    [LdgSQLiteDefault(true)]
    public bool IsActive { get; set; }

    [LdgSQLiteDefault("O'Brien")]
    public string EscapedName { get; set; } = string.Empty;

    public string NoDefault { get; set; } = string.Empty;
}
""";

    public const string MultiSelectFixture = """
using System.Collections.Generic;
using CsLightDbGen.SQLiteGenerator;

namespace CsLightDbGen.SQLiteGenerator;

[LdgSQLiteTable("MultiSelectTargets")]
public partial class MultiSelectTarget
{
    [LdgSQLiteKey]
    public int Id { get; set; }

    [LdgSQLiteMultiSelect]
    public string Slug { get; set; } = string.Empty;

    public int? ParentKey { get; set; }

    [LdgSQLiteMultiSelect]
    public List<string> Tags { get; set; } = new();
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

    public const string FtsTokenizerFixture = """
using CsLightDbGen.SQLiteGenerator;

namespace CsLightDbGen.SQLiteGenerator;

[LdgSQLiteFtsTable("source_table", tokenizer: "porter ascii")]
public partial class FtsTokenizerEntity
{
    public string Title { get; set; } = string.Empty;

    [LdgSQLiteFtsUnindexed]
    public string? RawHtml { get; set; }
}
""";
}
