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
        Customer.SQLiteColumnNames.ShouldContain("CustomerId");
        Customer.SQLiteColumnNames.ShouldContain("Name");

        var filteredOrderedList = Customer.SelectList(
            db,
            orderByProperties: new[] { "MissingColumn", "Name" },
            orderByDirection: "desc");
        filteredOrderedList.Select(c => c.Name).ToList()
            .ShouldBe(filteredOrderedList.Select(c => c.Name).OrderByDescending(name => name).ToList());

        Customer.SelectList(db, orderByProperties: new[] { "MissingColumn" }).ShouldNotBeEmpty();

        var enumerable = Customer.SelectEnumerable(db, resultLimit: 3).ToList();
        enumerable.Count.ShouldBe(3);

        var filteredEnumerable = Customer.SelectEnumerable(
            db,
            orderByProperties: new[] { "MissingColumn", "CustomerId" },
            orderByDirection: "desc",
            resultLimit: 3).ToList();
        filteredEnumerable.Select(c => c.CustomerId).ToList()
            .ShouldBe(filteredEnumerable.Select(c => c.CustomerId).OrderByDescending(id => id).ToList());

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
        ArticleSearch.SQLiteColumnNames.ShouldContain("Title");
        ArticleSearch.SQLiteColumnNames.ShouldContain("RawHtml");

        db.Select<ArticleSearch>(new List<string> { "beta" }).ShouldNotBeEmpty();
        db.SelectCount<ArticleSearch>(new List<string> { "beta" }).ShouldBeGreaterThan(0);

        var filteredFtsResults = ArticleSearch.Select(
            db,
            new List<string> { "entry" },
            orderByProperties: new[] { "MissingColumn", "Title" },
            orderByDirection: "desc");
        filteredFtsResults.Select(a => a.Title).ToList()
            .ShouldBe(filteredFtsResults.Select(a => a.Title).OrderByDescending(title => title).ToList());

        ArticleSearch.Select(
            db,
            new List<string> { "entry" },
            orderByProperties: new[] { "MissingColumn" }).ShouldNotBeEmpty();

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

    [Fact]
    public void FtsModel_WithTokenizer_CreatesAndSearches()
    {
        using var db = OpenInMemory();

        CreateArticleSource(db);

        ArticleSearchPorter.CreateTable(db).ShouldBeTrue();
        ArticleSearchPorter.Populate(db).ShouldBe(3);

        ArticleSearchPorter.Select(db, new List<string> { "alpha" }).ShouldNotBeEmpty();
        ArticleSearchPorter.SelectCount(db, new List<string> { "alpha" }).ShouldBeGreaterThan(0);

        // Porter stemming: "entries" should match "entry"
        ArticleSearchPorter.Select(db, new List<string> { "entries" }).ShouldNotBeEmpty();

        ArticleSearchPorter.DropTable(db).ShouldBeTrue();
    }

    [Fact]
    public void Transaction_Composition_RollbackDiscards_CommitPersists()
    {
        using var db = OpenInMemory();
        Customer.CreateTable(db).ShouldBeTrue();

        // Rollback path: a write enrolled in the caller's transaction is discarded,
        // yet a read enrolled in the same transaction observes it (read-your-writes).
        using (IDbTransaction rollbackTxn = db.BeginTransaction())
        {
            Customer discarded = NewCustomer("Discarded", 40, 1, 10);
            Customer.Insert(db, discarded, transaction: rollbackTxn);
            discarded.CustomerId.ShouldBeGreaterThan(0);

            Customer.SelectCount(db, transaction: rollbackTxn).ShouldBe(1);

            // A read that omits the transaction still auto-enrolls in the connection's
            // ambient transaction, so it also observes the pending write.
            Customer.SelectCount(db).ShouldBe(1);

            rollbackTxn.Rollback();
        }

        Customer.SelectCount(db).ShouldBe(0);

        // Commit path: an enrolled Insert and Update commit atomically together.
        using (IDbTransaction commitTxn = db.BeginTransaction())
        {
            Customer kept = NewCustomer("Kept", 41, 2, 20);
            Customer.Insert(db, kept, transaction: commitTxn);

            kept.Score = 200;
            Customer.Update(db, kept, transaction: commitTxn);

            commitTxn.Commit();
        }

        Customer.SelectCount(db).ShouldBe(1);
        Customer? keptRow = Customer.SelectSingle(db, Name: "Kept");
        keptRow.ShouldNotBeNull();
        keptRow!.Score.ShouldBe(200);

        // No-transaction path: each generated write opens and commits its own transaction.
        Customer.Insert(db, NewCustomer("Auto", 42, 3, 30));
        Customer.SelectCount(db).ShouldBe(2);

        Customer.DropTable(db).ShouldBeTrue();
    }

    [Fact]
    public void ColumnDefaults_ApplyWhenColumnsOmitted()
    {
        using var db = OpenInMemory();
        Job.CreateTable(db).ShouldBeTrue();

        // Insert a row that omits every defaulted column; SQLite must fill them
        // from the DDL DEFAULT clauses emitted by the generator.
        using (IDbCommand insert = db.CreateCommand())
        {
            insert.CommandText = "INSERT INTO jobs (JobName) VALUES ('build');";
            insert.ExecuteNonQuery();
        }

        Job? row = Job.SelectSingle(db, JobName: "build");
        row.ShouldNotBeNull();
        row!.RetryCount.ShouldBe(0);
        row.Status.ShouldBe("queued");
        row.IsActive.ShouldBeTrue();
        row.CreatedAt.ShouldNotBeNullOrWhiteSpace();

        // An explicit value still overrides the default.
        using (IDbCommand insertExplicit = db.CreateCommand())
        {
            insertExplicit.CommandText = "INSERT INTO jobs (JobName, Status, RetryCount) VALUES ('deploy', 'running', 3);";
            insertExplicit.ExecuteNonQuery();
        }

        Job? explicitRow = Job.SelectSingle(db, JobName: "deploy");
        explicitRow.ShouldNotBeNull();
        explicitRow!.Status.ShouldBe("running");
        explicitRow.RetryCount.ShouldBe(3);

        Job.DropTable(db).ShouldBeTrue();
    }

    [Fact]
    public void ForeignKey_OnDeleteCascade_RemovesChildRows()
    {
        using var db = OpenInMemory();

        using (IDbCommand pragma = db.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }

        FkParent.CreateTable(db).ShouldBeTrue();
        FkChild.CreateTable(db).ShouldBeTrue();

        FkParent parent = new() { Label = "root" };
        FkParent.Insert(db, parent);
        parent.ParentId.ShouldBeGreaterThan(0);

        FkChild.Insert(db, new FkChild { ParentRef = parent.ParentId, Note = "leaf-a" });
        FkChild.Insert(db, new FkChild { ParentRef = parent.ParentId, Note = "leaf-b" });
        FkChild.SelectCount(db).ShouldBe(2);

        // Deleting the parent must cascade to the children (ON DELETE CASCADE),
        // which only fires when PRAGMA foreign_keys is enabled on the connection.
        using (IDbCommand delete = db.CreateCommand())
        {
            delete.CommandText = "DELETE FROM fk_parents WHERE ParentId = " + parent.ParentId + ";";
            delete.ExecuteNonQuery();
        }

        FkChild.SelectCount(db).ShouldBe(0);

        FkChild.DropTable(db).ShouldBeTrue();
        FkParent.DropTable(db).ShouldBeTrue();
    }

    [Fact]
    public void CompositeKey_InsertUpdateDelete_RoundTripsAndEnforcesUniqueness()
    {
        using var db = OpenInMemory();

        UserWebsite.DefaultTableName.ShouldBe("user_websites");
        UserWebsite.CreateTable(db).ShouldBeTrue();

        // Composite key columns carry no identity, so both must be supplied explicitly.
        UserWebsite first = new() { UserId = 1, WebsiteId = 100, Role = "owner" };
        UserWebsite.Insert(db, first);

        db.Insert(new UserWebsite { UserId = 1, WebsiteId = 200, Role = "editor" });
        db.Insert(new UserWebsite { UserId = 2, WebsiteId = 100, Role = "viewer" });

        UserWebsite.SelectCount(db).ShouldBe(3);

        // Round-trip: the key columns persisted their supplied values (not NULL/0).
        UserWebsite? loaded = UserWebsite.SelectSingle(db, UserId: 1, WebsiteId: 100);
        loaded.ShouldNotBeNull();
        loaded!.UserId.ShouldBe(1);
        loaded.WebsiteId.ShouldBe(100);
        loaded.Role.ShouldBe("owner");

        // The full composite key is unique; re-inserting it violates the PRIMARY KEY constraint.
        Should.Throw<SqliteException>(() => UserWebsite.Insert(db, new UserWebsite { UserId = 1, WebsiteId = 100, Role = "dup" }));

        // Sharing only one key column is allowed (proves a composite, not single, key).
        UserWebsite.SelectCount(db, UserId: 1).ShouldBe(2);
        UserWebsite.SelectCount(db, WebsiteId: 100).ShouldBe(2);

        // Update matches on the full key and rewrites the non-key column.
        loaded.Role = "admin";
        UserWebsite.Update(db, loaded);
        UserWebsite.SelectSingle(db, UserId: 1, WebsiteId: 100)!.Role.ShouldBe("admin");

        // Delete-by-key uses the batch overload (binds every key column).
        UserWebsite.Delete(db, new List<UserWebsite> { new() { UserId = 1, WebsiteId = 200 } });
        UserWebsite.SelectCount(db).ShouldBe(2);
        UserWebsite.SelectSingle(db, UserId: 1, WebsiteId: 200).ShouldBeNull();

        UserWebsite.DropTable(db).ShouldBeTrue();
    }

    [Fact]
    public void UniqueConstraintAndPartialIndex_EnforceExpectedUniqueness()
    {
        using var db = OpenInMemory();
        Project.CreateTable(db).ShouldBeTrue();

        // Composite UNIQUE (OrgId, Slug): the same (OrgId, Slug) pair is rejected.
        Project.Insert(db, new Project { OrgId = 1, Slug = "alpha", ProjectName = "Alpha", IsArchived = 0 });
        Should.Throw<SqliteException>(() =>
            Project.Insert(db, new Project { OrgId = 1, Slug = "alpha", ProjectName = "Different", IsArchived = 1 }));

        // A different slug within the same org is allowed.
        Project.Insert(db, new Project { OrgId = 1, Slug = "beta", ProjectName = "Beta", IsArchived = 0 });

        // Partial UNIQUE INDEX (OrgId, ProjectName) WHERE IsArchived = 0:
        // two active rows sharing (OrgId, ProjectName) collide.
        Project.Insert(db, new Project { OrgId = 2, Slug = "s1", ProjectName = "Shared", IsArchived = 0 });
        Should.Throw<SqliteException>(() =>
            Project.Insert(db, new Project { OrgId = 2, Slug = "s2", ProjectName = "Shared", IsArchived = 0 }));

        // Archived rows fall outside the predicate, so duplicates are permitted there.
        Project.Insert(db, new Project { OrgId = 2, Slug = "s3", ProjectName = "Shared", IsArchived = 1 });
        Project.Insert(db, new Project { OrgId = 2, Slug = "s4", ProjectName = "Shared", IsArchived = 1 });

        Project.SelectCount(db).ShouldBe(5);

        Project.DropTable(db).ShouldBeTrue();
    }

    [Fact]
    public void EnsureSchema_AddsMissingColumnsAndIndexes_Idempotently()
    {
        using var db = OpenInMemory();

        // Simulate an older database shape: only the key and one column exist.
        using (IDbCommand create = db.CreateCommand())
        {
            create.CommandText = "CREATE TABLE widgets (WidgetId INTEGER PRIMARY KEY, WidgetLabel TEXT NOT NULL)";
            create.ExecuteNonQuery();
        }

        using (IDbCommand seed = db.CreateCommand())
        {
            seed.CommandText = "INSERT INTO widgets (WidgetLabel) VALUES ('legacy')";
            seed.ExecuteNonQuery();
        }

        // Additive migration brings the existing table up to the current model.
        Widget.EnsureSchema(db).ShouldBeTrue();

        // The pre-existing row is backfilled with the constant default (Quantity => 0);
        // the newly added nullable column stays null.
        Widget? legacy = Widget.SelectSingle(db, WidgetLabel: "legacy");
        legacy.ShouldNotBeNull();
        legacy!.Quantity.ShouldBe(0);
        legacy.Tag.ShouldBeNull();

        // The migrated table now accepts a fully populated insert.
        Widget fresh = new() { WidgetLabel = "fresh", Quantity = 7, Tag = "t" };
        Widget.Insert(db, fresh);
        fresh.WidgetId.ShouldBeGreaterThan(0);

        Widget? readback = Widget.SelectSingle(db, WidgetLabel: "fresh");
        readback.ShouldNotBeNull();
        readback!.Quantity.ShouldBe(7);
        readback.Tag.ShouldBe("t");

        // Re-running EnsureSchema is a no-op and must not throw.
        Widget.EnsureSchema(db).ShouldBeTrue();
        Widget.SelectCount(db).ShouldBe(2);

        // The extension wrapper resolves through its generic constraint.
        db.EnsureSchema<Widget>().ShouldBeTrue();

        Widget.DropTable(db).ShouldBeTrue();
    }

    [Fact]
    public void Upsert_InsertsUpdatesRespectsDoNothingAndIncrements()
    {
        using var db = OpenInMemory();
        Counter.CreateTable(db).ShouldBeTrue();

        string[] conflict = new[] { "Bucket" };

        // Fresh conflict key inserts a new row.
        Counter.Upsert(db, new Counter { Bucket = "a", Label = "first", Hits = 1 }, conflictColumns: conflict);
        Counter? a1 = Counter.SelectSingle(db, Bucket: "a");
        a1.ShouldNotBeNull();
        a1!.Label.ShouldBe("first");
        a1.Hits.ShouldBe(1);
        Counter.SelectCount(db).ShouldBe(1);

        // Existing key with default update columns overwrites every non-conflict column.
        Counter.Upsert(db, new Counter { Bucket = "a", Label = "second", Hits = 9 }, conflictColumns: conflict);
        Counter? a2 = Counter.SelectSingle(db, Bucket: "a");
        a2!.Label.ShouldBe("second");
        a2.Hits.ShouldBe(9);
        Counter.SelectCount(db).ShouldBe(1);

        // updateColumns: [] performs DO NOTHING, leaving the stored row untouched.
        Counter.Upsert(db, new Counter { Bucket = "a", Label = "ignored", Hits = 100 }, conflictColumns: conflict, updateColumns: Array.Empty<string>());
        Counter? a3 = Counter.SelectSingle(db, Bucket: "a");
        a3!.Label.ShouldBe("second");
        a3.Hits.ShouldBe(9);

        // incrementColumns accumulate (Hits = Hits + excluded.Hits) while other columns overwrite.
        Counter.Upsert(db, new Counter { Bucket = "a", Label = "third", Hits = 5 }, conflictColumns: conflict, updateColumns: new[] { "Label", "Hits" }, incrementColumns: new[] { "Hits" });
        Counter? a4 = Counter.SelectSingle(db, Bucket: "a");
        a4!.Label.ShouldBe("third");
        a4.Hits.ShouldBe(14);

        // A different conflict key inserts rather than updates.
        Counter.Upsert(db, new Counter { Bucket = "b", Label = "other", Hits = 3 }, conflictColumns: conflict);
        Counter.SelectCount(db).ShouldBe(2);

        // The extension wrapper resolves by the value's type.
        db.Upsert(new Counter { Bucket = "b", Label = "ext", Hits = 1 }, conflictColumns: conflict);
        Counter? b = Counter.SelectSingle(db, Bucket: "b");
        b!.Label.ShouldBe("ext");
        b.Hits.ShouldBe(1);

        Counter.DropTable(db).ShouldBeTrue();
    }

    [Fact]
    public void InsertReturning_And_UpdateReturning_HydrateModel_AndBulkInsertSurfacesKeys()
    {
        using var db = OpenInMemory();
        Job.CreateTable(db).ShouldBeTrue();

        // InsertReturning surfaces the generated identity key and the server-computed
        // CURRENT_TIMESTAMP default in a single round-trip, hydrating the passed instance.
        Job inserted = new() { JobName = "hydrate-me", Status = "running", RetryCount = 2 };
        Job returned = Job.InsertReturning(db, inserted);

        ReferenceEquals(returned, inserted).ShouldBeTrue();
        inserted.JobId.ShouldBeGreaterThan(0);
        inserted.CreatedAt.ShouldNotBeNullOrWhiteSpace();
        inserted.Status.ShouldBe("running");
        inserted.RetryCount.ShouldBe(2);

        // The row actually persisted with the database-computed timestamp.
        Job? persisted = Job.SelectSingle(db, JobId: inserted.JobId);
        persisted.ShouldNotBeNull();
        persisted!.CreatedAt.ShouldBe(inserted.CreatedAt);

        // UpdateReturning reflects the post-update state (RETURNING *).
        inserted.Status = "done";
        inserted.RetryCount = 5;
        Job updated = Job.UpdateReturning(db, inserted);
        updated.Status.ShouldBe("done");
        updated.RetryCount.ShouldBe(5);

        Job? persistedAfter = Job.SelectSingle(db, JobId: inserted.JobId);
        persistedAfter!.Status.ShouldBe("done");
        persistedAfter.RetryCount.ShouldBe(5);

        Job.DropTable(db).ShouldBeTrue();

        // Bulk key surfacing: both the List<T> (regression) and IEnumerable<T> insert
        // overloads populate each element's generated identity key.
        Customer.CreateTable(db).ShouldBeTrue();

        List<Customer> listRows = new()
        {
            NewCustomer("List-A", 20, 1, null),
            NewCustomer("List-B", 21, 1, null),
        };
        Customer.Insert(db, listRows);
        listRows[0].CustomerId.ShouldBeGreaterThan(0);
        listRows[1].CustomerId.ShouldBeGreaterThan(0);
        listRows[0].CustomerId.ShouldNotBe(listRows[1].CustomerId);

        Customer[] enumRows =
        [
            NewCustomer("Enum-A", 22, 2, null),
            NewCustomer("Enum-B", 23, 2, null),
            NewCustomer("Enum-C", 24, 2, null),
        ];
        Customer.Insert(db, (IEnumerable<Customer>)enumRows);
        enumRows[0].CustomerId.ShouldBeGreaterThan(0);
        enumRows[1].CustomerId.ShouldBeGreaterThan(0);
        enumRows[2].CustomerId.ShouldBeGreaterThan(0);
        enumRows[0].CustomerId.ShouldNotBe(enumRows[1].CustomerId);
        enumRows[1].CustomerId.ShouldNotBe(enumRows[2].CustomerId);

        Customer.DropTable(db).ShouldBeTrue();
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
