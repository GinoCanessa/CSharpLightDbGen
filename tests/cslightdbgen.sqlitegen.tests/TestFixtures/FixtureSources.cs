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

    public const string ForeignKeyActionsFixture = """
using CsLightDbGen.SQLiteGenerator;

namespace CsLightDbGen.SQLiteGenerator;

[LdgSQLiteTable("fk_children")]
public partial class FkChild
{
    [LdgSQLiteKey]
    public int Id { get; set; }

    [LdgSQLiteForeignKey(ReferenceTable = "fk_parents", ReferenceColumn = "Id", OnDelete = LdgSQLiteFkAction.Cascade, OnUpdate = LdgSQLiteFkAction.SetNull)]
    public int ParentId { get; set; }

    [LdgSQLiteForeignKey(ReferenceTable = "fk_owners", ReferenceColumn = "Id")]
    public int OwnerId { get; set; }
}
""";

    public const string CompositeForeignKeyFixture = """
using CsLightDbGen.SQLiteGenerator;

namespace CsLightDbGen.SQLiteGenerator;

[LdgSQLiteTable("manifest_entries")]
[LdgSQLiteForeignKeyComposite(new string[] { "TaskId", "ManifestGeneration" }, "task_manifests", new string[] { "TaskId", "Generation" }, onDelete: LdgSQLiteFkAction.Cascade)]
public partial class ManifestEntry
{
    [LdgSQLiteKey]
    public int Id { get; set; }

    public int TaskId { get; set; }

    public int ManifestGeneration { get; set; }
}
""";

        public const string ForeignKeyConstantResolutionFixture = """
    using CsLightDbGen.SQLiteGenerator;

    namespace CsLightDbGen.SQLiteGenerator;

    public class Users { }

    [LdgSQLiteTable("fk_resolve")]
    public partial class FkResolve
    {
        private const string AccountsTable = "accounts";

        [LdgSQLiteKey]
        public int Id { get; set; }

        [LdgSQLiteForeignKey(nameof(Users), "Id")]
        public int UserId { get; set; }

        [LdgSQLiteForeignKey(AccountsTable, "Id")]
        public int AccountId { get; set; }

        [LdgSQLiteForeignKey(@"Orders", "Id")]
        public int OrderId { get; set; }
    }
    """;

        public const string CompositePrimaryKeyFixture = """
using CsLightDbGen.SQLiteGenerator;

namespace CsLightDbGen.SQLiteGenerator;

[LdgSQLiteTable("user_websites")]
public partial class UserWebsite
{
    [LdgSQLiteKey]
    public int UserId { get; set; }

    [LdgSQLiteKey]
    public int WebsiteId { get; set; }

    public string Role { get; set; } = string.Empty;
}
""";

    public const string UniqueIndexFixture = """
using CsLightDbGen.SQLiteGenerator;

namespace CsLightDbGen.SQLiteGenerator;

[LdgSQLiteTable("packages")]
[LdgSQLiteUnique("Provider", "Subject")]
[LdgSQLiteIndex("NpmId", "Version", Unique = true, Where = "Status NOT IN ('complete','failed')")]
public partial class PackageRecord
{
    [LdgSQLiteKey]
    public int Id { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string NpmId { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
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

[LdgSQLiteTable("source_table")]
public partial class SourceTable
{
    [LdgSQLiteKey]
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? RawHtml { get; set; }
}

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

[LdgSQLiteTable("source_table")]
public partial class SourceTable
{
    [LdgSQLiteKey]
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? RawHtml { get; set; }
}

[LdgSQLiteFtsTable("source_table", tokenizer: "porter ascii")]
public partial class FtsTokenizerEntity
{
    public string Title { get; set; } = string.Empty;

    [LdgSQLiteFtsUnindexed]
    public string? RawHtml { get; set; }
}
""";

    // A2: single non-int integer identity key (long). Exercises the long key counter
    // (_indexValue/GetIndex/LoadMaxKey/SelectMaxKey typed as long), which previously emitted
    // `_indexValue = null` (CS0037) in LoadMaxKey.
    public const string LongKeyFixture = """
using CsLightDbGen.SQLiteGenerator;

namespace CsLightDbGen.SQLiteGenerator;

[LdgSQLiteTable("long_key_table")]
public partial class LongKeyEntity
{
    [LdgSQLiteKey]
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
""";

    // A2: single Guid natural key. The counter machinery must be omitted (a Guid cannot back an
    // int counter) and Guid auto-generation must fire via getNonIdentityPkInit.
    public const string GuidKeyFixture = """
using System;
using CsLightDbGen.SQLiteGenerator;

namespace CsLightDbGen.SQLiteGenerator;

[LdgSQLiteTable("guid_key_table")]
public partial class GuidKeyEntity
{
    [LdgSQLiteKey]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
""";

    // A2: single string natural key. The counter machinery must be omitted; the caller supplies
    // the key value.
    public const string StringKeyFixture = """
using CsLightDbGen.SQLiteGenerator;

namespace CsLightDbGen.SQLiteGenerator;

[LdgSQLiteTable("string_key_table")]
public partial class StringKeyEntity
{
    [LdgSQLiteKey]
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
""";

    // A2: every supported non-nullable scalar column type. Exercises the corrected read directives
    // (byte[]/char[]/TimeSpan/decimal and the sbyte/ushort/uint/ulong RHS casts).
    public const string ScalarTypesFixture = """
using System;
using CsLightDbGen.SQLiteGenerator;

namespace CsLightDbGen.SQLiteGenerator;

[LdgSQLiteTable("scalar_types")]
public partial class ScalarTypesEntity
{
    [LdgSQLiteKey]
    public int Id { get; set; }

    public bool Flag { get; set; }
    public byte Byte { get; set; }
    public sbyte SByte { get; set; }
    public short Short { get; set; }
    public ushort UShort { get; set; }
    public uint UInt { get; set; }
    public long Long { get; set; }
    public ulong ULong { get; set; }
    public float Float { get; set; }
    public double Double { get; set; }
    public decimal Decimal { get; set; }
    public char Char { get; set; }
    public char[] Chars { get; set; } = [];
    public byte[] Bytes { get; set; } = [];
    public string Text { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public DateTimeOffset TimestampOffset { get; set; }
    public TimeSpan Duration { get; set; }
    public Guid Identifier { get; set; }
    public Uri Link { get; set; } = new Uri("https://example.com");
}
""";

    // A2: every supported nullable scalar column type. Exercises the corrected nullable read
    // directives.
    public const string NullableScalarTypesFixture = """
using System;
using CsLightDbGen.SQLiteGenerator;

namespace CsLightDbGen.SQLiteGenerator;

[LdgSQLiteTable("nullable_scalar_types")]
public partial class NullableScalarTypesEntity
{
    [LdgSQLiteKey]
    public int Id { get; set; }

    public bool? Flag { get; set; }
    public byte? Byte { get; set; }
    public sbyte? SByte { get; set; }
    public short? Short { get; set; }
    public ushort? UShort { get; set; }
    public uint? UInt { get; set; }
    public long? Long { get; set; }
    public ulong? ULong { get; set; }
    public float? Float { get; set; }
    public double? Double { get; set; }
    public decimal? Decimal { get; set; }
    public char? Char { get; set; }
    public char[]? Chars { get; set; }
    public byte[]? Bytes { get; set; }
    public string? Text { get; set; }
    public DateTime? Timestamp { get; set; }
    public DateTimeOffset? TimestampOffset { get; set; }
    public TimeSpan? Duration { get; set; }
    public Guid? Identifier { get; set; }
    public Uri? Link { get; set; }
}
""";

    public const string ReservedWordFixture = """
using CsLightDbGen.SQLiteGenerator;

namespace CsLightDbGen.SQLiteGenerator;

[LdgSQLiteTable("reserved_words")]
public partial class ReservedWordEntity
{
    [LdgSQLiteKey]
    public int Id { get; set; }

    [LdgSQLiteMultiSelect]
    public string Group { get; set; } = string.Empty;

    public int Table { get; set; }
}
""";

    // L4: reserved-word TABLE name. "Order" is a SQL keyword; the compile-time default table name
    // must be quoted at every bare-identifier SQL site so the emitted DDL/DML is valid.
    public const string ReservedWordTableFixture = """
using CsLightDbGen.SQLiteGenerator;

namespace CsLightDbGen.SQLiteGenerator;

[LdgSQLiteTable("Order")]
public partial class ReservedTableEntity
{
    [LdgSQLiteKey]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
""";

    // B1: identity-only model (single auto-increment key, no other columns). The default INSERT has
    // no columns to supply, so it must emit "DEFAULT VALUES" rather than an invalid "() VALUES ()".
    public const string IdentityOnlyFixture = """
using CsLightDbGen.SQLiteGenerator;

namespace CsLightDbGen.SQLiteGenerator;

[LdgSQLiteTable("identity_only")]
public partial class IdentityOnlyEntity
{
    [LdgSQLiteKey]
    public int Id { get; set; }
}
""";

    // B1: keyless model (no primary key). By-key Update/Delete have no row identity, so their WHERE
    // predicate must be a valid no-op ("1 = 0") rather than the invalid " = $".
    public const string KeylessFixture = """
using CsLightDbGen.SQLiteGenerator;

namespace CsLightDbGen.SQLiteGenerator;

[LdgSQLiteTable("keyless_table")]
public partial class KeylessEntity
{
    public string Name { get; set; } = string.Empty;

    public int Value { get; set; }
}
""";

    // B1: primary-key-only model (a single natural key, no data columns). UPDATE has no assignable
    // columns, so the SET clause must fall back to a harmless self-assignment instead of being empty.
    public const string PrimaryKeyOnlyFixture = """
using CsLightDbGen.SQLiteGenerator;

namespace CsLightDbGen.SQLiteGenerator;

[LdgSQLiteTable("primary_key_only")]
public partial class PrimaryKeyOnlyEntity
{
    [LdgSQLiteKey]
    public string Code { get; set; } = string.Empty;
}
""";
}
