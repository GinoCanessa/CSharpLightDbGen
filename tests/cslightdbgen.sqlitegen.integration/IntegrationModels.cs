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

[LdgSQLiteFtsTable("article_source")]
public partial class ArticleSearch
{
    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    [LdgSQLiteFtsUnindexed]
    public string? RawHtml { get; set; }
}
