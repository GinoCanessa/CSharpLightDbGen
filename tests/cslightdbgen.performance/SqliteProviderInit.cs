using System.Runtime.CompilerServices;

namespace cslightdbgen.performance;

internal static class SqliteProviderInit
{
    [ModuleInitializer]
    internal static void Init()
        => SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
}
