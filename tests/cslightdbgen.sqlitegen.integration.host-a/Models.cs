using CsLightDbGen.SQLiteGenerator;

[assembly: LdgSQLiteRuntimeNamespace("CsLightDbGen.Integration.HostA.Runtime")]

namespace CsLightDbGen.Integration.HostA.Models;

[LdgSQLiteBaseClass]
public abstract class HostAItemBase
{
    [LdgSQLiteKey(autoIncrement: false)]
    public int ItemKey { get; set; }
}

[LdgSQLiteTable("multi_host_a")]
public partial class HostAItem : HostAItemBase
{
    public string Label { get; set; } = string.Empty;
}
