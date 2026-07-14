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
