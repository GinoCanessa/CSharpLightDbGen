using cslightdbgen.sqlitegen.tests.TestFixtures;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using Shouldly;

namespace cslightdbgen.sqlitegen.tests;

public class LightSQLiteGenerator_TransactionTests
{
    private static string GetBasicEntitySource()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.BasicTableFixture);
        return GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "BasicEntity.Table.g.cs");
    }

    [Theory]
    [InlineData("public static int Insert(")]
    [InlineData("public static BasicEntity Update(")]
    [InlineData("public static void Delete(")]
    public void WriteMethods_AcceptOptionalTransaction(string signaturePrefix)
    {
        string source = GetBasicEntitySource();

        int idx = source.IndexOf(signaturePrefix, StringComparison.Ordinal);
        idx.ShouldBeGreaterThan(-1);
        int bodyIdx = source.IndexOf('{', idx);
        string signature = source.Substring(idx, bodyIdx - idx);

        signature.ShouldContain("IDbTransaction? transaction = null");
    }

    [Theory]
    [InlineData("public static List<BasicEntity> SelectList(")]
    [InlineData("public static IEnumerable<BasicEntity> SelectEnumerable(")]
    [InlineData("public static BasicEntity? SelectSingle(")]
    [InlineData("public static int SelectCount(")]
    public void ReadMethods_AcceptOptionalTransaction(string signaturePrefix)
    {
        string source = GetBasicEntitySource();

        int idx = source.IndexOf(signaturePrefix, StringComparison.Ordinal);
        idx.ShouldBeGreaterThan(-1);
        int bodyIdx = source.IndexOf('{', idx);
        string signature = source.Substring(idx, bodyIdx - idx);

        signature.ShouldContain("IDbTransaction? transaction = null");
    }

    [Fact]
    public void WriteBodies_UseOwnOrEnrolledTransaction()
    {
        string source = GetBasicEntitySource();

        source.ShouldContain("bool _ownTxn = transaction is null;");
        source.ShouldContain("IDbTransaction _txn = transaction ?? dbConnection.BeginTransaction();");
        source.ShouldContain("if (_ownTxn) _txn.Commit();");
        source.ShouldContain("if (_ownTxn) _txn.Dispose();");
    }

    [Fact]
    public void EveryWriteCommand_AssignsTransaction()
    {
        string source = GetBasicEntitySource();

        // Every generated write body must enroll its command in the (own or supplied) transaction.
        // The number of "command.Transaction = _txn;" assignments must match the number of
        // own-vs-enrolled preambles so no write path silently omits enrollment.
        int preambles = CountOccurrences(source, "bool _ownTxn = transaction is null;");
        int enrollments = CountOccurrences(source, "command.Transaction = _txn;");

        preambles.ShouldBeGreaterThan(0);
        enrollments.ShouldBe(preambles);
    }

    [Fact]
    public void ReadBodies_EnrollSuppliedTransaction()
    {
        string source = GetBasicEntitySource();

        // Reads only enroll a caller-supplied transaction; when null they leave the
        // command's auto-assigned (ambient) transaction untouched.
        source.ShouldContain("if (transaction != null) command.Transaction = transaction;");
    }

    [Fact]
    public void Extensions_ForwardTransaction()
    {
        string source = GetBasicEntitySource();

        // Connection-first and value/collection-first wrappers both forward the transaction.
        source.ShouldContain("this IDbConnection dbCon, BasicEntity value, string? dbTableName = null, bool ignoreDuplicates = false, bool insertPrimaryKey = false, IDbTransaction? transaction = null");
        source.ShouldContain("this BasicEntity value, IDbConnection dbCon, string? dbTableName = null, bool ignoreDuplicates = false, bool insertPrimaryKey = false, IDbTransaction? transaction = null");
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) != -1)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}
