using System.Data;
using cslightdbgen.sqlitegen.integration.Models;

namespace cslightdbgen.sqlitegen.integration;

public class LightSQLiteGenerator_IntegrationTests
{
    [Fact]
    public void Customer_TableGeneratedApis_Work_EndToEnd()
    {
        using var db = OpenInMemory();

        Customer.DefaultTableName.ShouldBe("customers");
        Customer.CreateTable(db).ShouldBeTrue();

        Customer.LoadMaxKey(db);
        Customer.SelectMaxKey(db).ShouldBeNull();

        var alpha = NewCustomer("Alpha", 30, 10, 90);
        var returnedId = Customer.Insert(db, alpha);
        returnedId.ShouldBe(alpha.CustomerId);
        alpha.CustomerId.ShouldBeGreaterThan(0);

        Customer.Insert(db, new List<Customer>
        {
            NewCustomer("Beta", 28, 10, null),
            NewCustomer("Gamma", 41, 20, 75)
        });

        Customer.Insert(db, (IEnumerable<Customer>)new[]
        {
            NewCustomer("Delta", 26, 30, 88),
            NewCustomer("Epsilon", 34, 20, 92)
        });

        db.Insert(NewCustomer("Zeta", 29, 40, 66));
        db.Insert(new List<Customer> { NewCustomer("Eta", 31, 50, null) });
        db.Insert((IEnumerable<Customer>)new[] { NewCustomer("Theta", 44, 60, 55) });

        var iota = NewCustomer("Iota", 22, 70, 77);
        iota.Insert(db);

        var kappa = NewCustomer("Kappa", 24, 80, 81);
        var lambda = NewCustomer("Lambda", 25, 80, 83);
        new List<Customer> { kappa, lambda }.Insert(db);
        ((IEnumerable<Customer>)new[] { NewCustomer("Mu", 36, 90, 99) }).Insert(db);

        Customer.SelectCount(db).ShouldBeGreaterThan(0);
        Customer.SelectCount(db, Name: "A%", compareStringsWithLike: true).ShouldBe(1);

        var single = Customer.SelectSingle(db, Name: "Alpha");
        single.ShouldNotBeNull();
        single!.Name.ShouldBe("Alpha");

        var list = Customer.SelectList(
            db,
            orderByProperties: new[] { "CustomerId" },
            orderByDirection: "asc",
            SegmentKeyValues: new List<int> { 10, 20, 30 });
        list.ShouldNotBeEmpty();

        var enumerable = Customer.SelectEnumerable(db, resultLimit: 3).ToList();
        enumerable.Count.ShouldBe(3);

        var dict = Customer.SelectDict(db, Age: 20, AgeOperator: ">=");
        dict.ShouldNotBeEmpty();

        db.SelectSingle<Customer>(Name: "Alpha").ShouldNotBeNull();
        db.SelectList<Customer>(resultLimit: 2).Count.ShouldBe(2);
        db.SelectEnumerable<Customer>(resultLimit: 2).Count().ShouldBe(2);
        db.SelectCount<Customer>().ShouldBeGreaterThan(0);

        single.Score = 95;
        Customer.Update(db, single);

        var updateBatch = Customer.SelectList(db, resultLimit: 2);
        updateBatch.ForEach(c => c.Score = 50);
        Customer.Update(db, updateBatch);

        db.Update(single);
        db.Update((IEnumerable<Customer>)updateBatch);
        single.Update(db);
        ((IEnumerable<Customer>)updateBatch).Update(db);

        var deleteOne = Customer.SelectSingle(db, Name: "Mu");
        deleteOne.ShouldNotBeNull();
        Should.Throw<InvalidOperationException>(() => Customer.Delete(db, deleteOne!));

        var deleteMany = Customer.SelectList(db, resultLimit: 2);
        Customer.Delete(db, deleteMany);

        Customer.Delete(db, ScoreIsNull: true);

        var nu = NewCustomer("Nu", 33, 100, 64);
        var xi = NewCustomer("Xi", 35, 110, 73);
        Customer.Insert(db, new List<Customer> { nu, xi });

        Should.Throw<InvalidOperationException>(() => db.Delete(nu));
        db.Delete((IEnumerable<Customer>)new[] { xi });

        var omicron = NewCustomer("Omicron", 37, 120, 68);
        var pi = NewCustomer("Pi", 38, 130, 72);
        Customer.Insert(db, new List<Customer> { omicron, pi });

        Should.Throw<InvalidOperationException>(() => omicron.Delete(db));
        ((IEnumerable<Customer>)new[] { pi }).Delete(db);

        db.Delete(compareStringsWithLike: true, Name: "T%");

        Customer.DropTable(db).ShouldBeTrue();
    }

    [Fact]
    public void RecordModel_GeneratedApis_Work_WithRuntimeSqlite()
    {
        using var db = OpenInMemory();

        Order.CreateTable(db).ShouldBeTrue();

        var first = new Order { CustomerKey = 101, Description = "starter" };
        var key = Order.Insert(db, first);
        key.ShouldBe(first.OrderId);

        var loaded = Order.SelectSingle(db, OrderId: first.OrderId);
        loaded.ShouldNotBeNull();

        loaded!.Description = "updated";
        Order.Update(db, loaded);

        Order.SelectCount(db, Description: "updated").ShouldBe(1);
        Should.Throw<InvalidOperationException>(() => Order.Delete(db, loaded));

        Order.Delete(db, Description: "updated");
        Order.SelectCount(db).ShouldBe(0);

        Order.DropTable(db).ShouldBeTrue();
    }

    [Fact]
    public void FtsModel_GeneratedApis_Work_WithPopulateAndSearch()
    {
        using var db = OpenInMemory();

        CreateArticleSource(db);

        ArticleSearch.CreateTable(db).ShouldBeTrue();
        ArticleSearch.Populate(db).ShouldBe(3);

        var exactMatches = ArticleSearch.Select(db, new List<string> { "alpha" });
        exactMatches.ShouldNotBeEmpty();
        ArticleSearch.SelectCount(db, new List<string> { "alpha" }).ShouldBeGreaterThan(0);

        db.Select<ArticleSearch>(new List<string> { "beta" }).ShouldNotBeEmpty();
        db.SelectCount<ArticleSearch>(new List<string> { "beta" }).ShouldBeGreaterThan(0);

        ArticleSearch.CreateTable(db, "article_search_clean").ShouldBeTrue();
        ArticleSearch.Populate(
            db,
            dbTableName: "article_search_clean",
            sourceTableName: "article_source",
            sanitizeText: true).ShouldBe(3);

        ArticleSearch.Select(
            db,
            new List<string> { "alpha" },
            dbTableName: "article_search_clean").ShouldNotBeEmpty();

        ArticleSearch.DropTable(db).ShouldBeTrue();
        ArticleSearch.DropTable(db, "article_search_clean").ShouldBeTrue();
    }

    private static SqliteConnection OpenInMemory()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static Customer NewCustomer(string name, int age, int segmentKey, int? score)
        => new()
        {
            Name = name,
            Age = age,
            SegmentKey = segmentKey,
            Score = score
        };

    private static void CreateArticleSource(IDbConnection db)
    {
        using var create = db.CreateCommand();
        create.CommandText = """
            CREATE TABLE article_source (
                Title TEXT NOT NULL,
                Body TEXT NOT NULL,
                RawHtml TEXT NULL
            );
            """;
        create.ExecuteNonQuery();

        using var insert = db.CreateCommand();
        insert.CommandText = """
            INSERT INTO article_source (Title, Body, RawHtml)
            VALUES
                ('alpha entry', 'first alpha content', '<p>alpha <b>HTML</b></p>'),
                ('beta entry', 'second beta content', '<div>beta HTML fragment</div>'),
                ('gamma entry', 'third gamma content', '<span>gamma markup</span>');
            """;
        insert.ExecuteNonQuery();
    }
}
