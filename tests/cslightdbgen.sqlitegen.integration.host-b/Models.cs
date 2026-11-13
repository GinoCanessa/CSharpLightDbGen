using CsLightDbGen.Integration.HostA.Models;
using CsLightDbGen.SQLiteGenerator;

[assembly: LdgSQLiteRuntimeNamespace("CsLightDbGen.Integration.HostB.Runtime")]

namespace CsLightDbGen.Integration.HostB.Models;

[LdgSQLiteTable("multi_host_b")]
public partial class HostBItem : HostAItemBase
{
    [LdgSQLiteKeyNoUnique]
    public string LocalKey { get; set; } = string.Empty;

    [LdgSQLiteForeignKey("multi_host_a", "ItemKey", onDelete: LdgSQLiteFkAction.Cascade)]
    public int HostAItemKey { get; set; }

    public string Label { get; set; } = string.Empty;
}

[LdgSQLiteFtsTable("multi_host_a", "multi_host_a_search")]
public partial class HostASearch
{
    [LdgSQLiteFtsUnindexed]
    public int ItemKey { get; set; }

    public string Label { get; set; } = string.Empty;
}
