using System;
using CsLightDbGen.SQLiteGenerator;

namespace cslightdbgen.sqlitegen.integration.Models;

[LdgSQLiteTable("customers")]
public partial class Customer
{
    [LdgSQLiteKey]
    public int CustomerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Age { get; set; }

    public int SegmentKey { get; set; }

    public int? Score { get; set; }
}

[LdgSQLiteTable("orders")]
public partial record class Order
{
    [LdgSQLiteKey]
    public int OrderId { get; set; }

    public int CustomerKey { get; set; }

    public string Description { get; set; } = string.Empty;
}

[LdgSQLiteTable("jobs")]
public partial class Job
{
    [LdgSQLiteKey]
    public int JobId { get; set; }

    public string JobName { get; set; } = string.Empty;

    [LdgSQLiteDefault(0)]
    public int RetryCount { get; set; }

    [LdgSQLiteMultiSelect]
    [LdgSQLiteDefault("queued")]
    public string Status { get; set; } = string.Empty;

    [LdgSQLiteDefault("CURRENT_TIMESTAMP", raw: true)]
    public string CreatedAt { get; set; } = string.Empty;

    [LdgSQLiteDefault(true)]
    public bool IsActive { get; set; }
}

[LdgSQLiteTable("fk_parents")]
public partial class FkParent
{
    [LdgSQLiteKey]
    public int ParentId { get; set; }

    public string Label { get; set; } = string.Empty;
}

[LdgSQLiteTable("fk_children")]
public partial class FkChild
{
    [LdgSQLiteKey]
    public int ChildId { get; set; }

    [LdgSQLiteForeignKey(ReferenceTable = "fk_parents", ReferenceColumn = "ParentId", OnDelete = LdgSQLiteFkAction.Cascade)]
    public int ParentRef { get; set; }

    public string Note { get; set; } = string.Empty;
}

[LdgSQLiteTable("user_websites")]
public partial class UserWebsite
{
    [LdgSQLiteKey]
    public int UserId { get; set; }

    [LdgSQLiteKey]
    public int WebsiteId { get; set; }

    public string Role { get; set; } = string.Empty;
}

[LdgSQLiteTable("projects")]
[LdgSQLiteUnique("OrgId", "Slug")]
[LdgSQLiteIndex("OrgId", "ProjectName", Unique = true, Where = "IsArchived = 0")]
public partial class Project
{
    [LdgSQLiteKey]
    public int ProjectId { get; set; }

    public int OrgId { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public int IsArchived { get; set; }
}

[LdgSQLiteTable("widgets")]
public partial class Widget
{
    [LdgSQLiteKey]
    public int WidgetId { get; set; }

    public string WidgetLabel { get; set; } = string.Empty;

    [LdgSQLiteDefault(0)]
    public int Quantity { get; set; }

    public string? Tag { get; set; }
}

[LdgSQLiteTable("counters")]
[LdgSQLiteUnique("Bucket")]
public partial class Counter
{
    [LdgSQLiteKey]
    public int CounterId { get; set; }

    public string Bucket { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public int Hits { get; set; }
}

[LdgSQLiteFtsTable("article_source")]
public partial class ArticleSearch
{
    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    [LdgSQLiteFtsUnindexed]
    public string? RawHtml { get; set; }
}

[LdgSQLiteFtsTable("article_source", tokenizer: "porter ascii")]
public partial class ArticleSearchPorter
{
    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    [LdgSQLiteFtsUnindexed]
    public string? RawHtml { get; set; }
}

// A2: single non-int integer identity key (long). Verifies the long key counter round-trips.
[LdgSQLiteTable("long_keys")]
public partial class LongKeyEntity
{
    [LdgSQLiteKey]
    public long Id { get; set; }

    public string LongName { get; set; } = string.Empty;
}

// A2: single Guid natural key. Verifies Guid auto-generation on insert and round-trip.
[LdgSQLiteTable("guid_keys")]
public partial class GuidKeyEntity
{
    [LdgSQLiteKey]
    public Guid Id { get; set; }

    public string GuidName { get; set; } = string.Empty;
}

// A2: single string natural key. Verifies caller-supplied string key round-trip.
[LdgSQLiteTable("string_keys")]
public partial class StringKeyEntity
{
    [LdgSQLiteKey]
    public string Id { get; set; } = string.Empty;

    public string StringName { get; set; } = string.Empty;
}

// A2: value-type / binary scalar columns whose read directives were corrected for IDataReader.
[LdgSQLiteTable("scalar_samples")]
public partial class ScalarSample
{
    [LdgSQLiteKey]
    public int Id { get; set; }

    public string Label { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public decimal? OptionalPrice { get; set; }

    public byte[] Payload { get; set; } = [];

    public TimeSpan Duration { get; set; }
}

// A6: integer single key with AutoIncrement disabled. The caller supplies the key; no counter.
[LdgSQLiteTable("supplied_keys")]
public partial class SuppliedKeyEntity
{
    [LdgSQLiteKey(false)]
    public int Id { get; set; }

    public string SuppliedLabel { get; set; } = string.Empty;
}

// A6: foreign key declared with positional constructor arguments (table, column, onDelete).
[LdgSQLiteTable("pos_fk_children")]
public partial class PosFkChild
{
    [LdgSQLiteKey]
    public int PosChildId { get; set; }

    [LdgSQLiteForeignKey("fk_parents", "ParentId", onDelete: LdgSQLiteFkAction.Cascade)]
    public int PosParentRef { get; set; }

    public string PosNote { get; set; } = string.Empty;
}

// A5: required (NOT NULL) column with no constant default. Added nullable by ALTER TABLE, so
// EnsureSchema must fail fast rather than strand pre-existing rows as NULL on a populated table.
[LdgSQLiteTable("mig_required")]
public partial class MigRequiredEntity
{
    [LdgSQLiteKey]
    public int MigRequiredId { get; set; }

    public string MigRequiredLabel { get; set; } = string.Empty;
}

// A5: non-nullable column with a raw (database-computed) default. ALTER TABLE ADD COLUMN cannot
// apply the default, so EnsureSchema must fail fast on a populated table.
[LdgSQLiteTable("mig_raw")]
public partial class MigRawEntity
{
    [LdgSQLiteKey]
    public int MigRawId { get; set; }

    [LdgSQLiteDefault("CURRENT_TIMESTAMP", raw: true)]
    public string MigRawStamp { get; set; } = string.Empty;
}

[LdgSQLiteTable("reserved_words")]
public partial class ReservedWordEntity
{
    [LdgSQLiteKey]
    public int Id { get; set; }

    [LdgSQLiteMultiSelect]
    public string Group { get; set; } = string.Empty;

    public int Table { get; set; }
}

// B1: identity-only model (auto-increment key, no other columns). The default INSERT must use
// "DEFAULT VALUES" so a row can be inserted and the key auto-assigned at runtime.
[LdgSQLiteTable("identity_only")]
public partial class IdentityOnlyEntity
{
    [LdgSQLiteKey]
    public int Id { get; set; }
}
