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
        Customer.Delete(db, deleteOne!);
        Customer.SelectSingle(db, Name: "Mu").ShouldBeNull();

        var deleteMany = Customer.SelectList(db, resultLimit: 2);
        Customer.Delete(db, deleteMany);

        Customer.Delete(db, ScoreIsNull: true);

        var nu = NewCustomer("Nu", 33, 100, 64);
        var xi = NewCustomer("Xi", 35, 110, 73);
        Customer.Insert(db, new List<Customer> { nu, xi });

        db.Delete(nu);
        db.Delete((IEnumerable<Customer>)new[] { xi });

        var omicron = NewCustomer("Omicron", 37, 120, 68);
        var pi = NewCustomer("Pi", 38, 130, 72);
        Customer.Insert(db, new List<Customer> { omicron, pi });

        omicron.Delete(db);
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
        Order.Delete(db, loaded!);
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
    public void EnsureSchema_ReappliesCompositeUniqueConstraint_OnPreExistingTable()
    {
        using var db = OpenInMemory();

        // Simulate a table created by an older generator that lacks the class-level
        // UNIQUE (OrgId, Slug) constraint entirely.
        using (IDbCommand create = db.CreateCommand())
        {
            create.CommandText = """
                CREATE TABLE projects (
                    ProjectId INTEGER PRIMARY KEY,
                    OrgId INTEGER NOT NULL,
                    Slug TEXT NOT NULL,
                    ProjectName TEXT NOT NULL,
                    IsArchived INTEGER NOT NULL
                )
                """;
            create.ExecuteNonQuery();
        }

        using (IDbCommand seed = db.CreateCommand())
        {
            seed.CommandText = "INSERT INTO projects (OrgId, Slug, ProjectName, IsArchived) VALUES (1, 'alpha', 'Alpha', 0)";
            seed.ExecuteNonQuery();
        }

        // Migration must re-assert the composite UNIQUE that CREATE TABLE IF NOT EXISTS cannot
        // apply to a pre-existing table.
        Project.EnsureSchema(db).ShouldBeTrue();

        // A distinct (OrgId, Slug) is still accepted.
        Project.Insert(db, new Project { OrgId = 1, Slug = "beta", ProjectName = "Beta", IsArchived = 0 });

        // The same Slug under a different OrgId is accepted (proves the constraint is composite).
        Project.Insert(db, new Project { OrgId = 2, Slug = "alpha", ProjectName = "Alpha2", IsArchived = 0 });

        // A duplicate (OrgId, Slug) is now rejected — the constraint is enforced post-migration.
        Should.Throw<SqliteException>(() =>
            Project.Insert(db, new Project { OrgId = 1, Slug = "alpha", ProjectName = "Dupe", IsArchived = 0 }));

        // Re-running the migration is idempotent and must not throw.
        Project.EnsureSchema(db).ShouldBeTrue();

        Project.DropTable(db).ShouldBeTrue();
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

    [Fact]
    public void MultiSelect_InAndNotIn_FilterOnNonKeyScalarColumn()
    {
        using var db = OpenInMemory();
        Job.CreateTable(db).ShouldBeTrue();

        Job.Insert(db, new Job { JobName = "a", Status = "queued" });
        Job.Insert(db, new Job { JobName = "b", Status = "running" });
        Job.Insert(db, new Job { JobName = "c", Status = "complete" });
        Job.Insert(db, new Job { JobName = "d", Status = "failed" });
        Job.Insert(db, new Job { JobName = "e", Status = "queued" });

        // IN over a non-key scalar column.
        List<Job> active = Job.SelectList(db, StatusValues: new[] { "queued", "running" });
        active.Select(j => j.JobName).OrderBy(n => n).ToList().ShouldBe(new List<string> { "a", "b", "e" });

        // NOT IN over the same non-key scalar column.
        List<Job> notTerminal = Job.SelectList(db, StatusNotInValues: new[] { "complete", "failed" });
        notTerminal.Select(j => j.JobName).OrderBy(n => n).ToList().ShouldBe(new List<string> { "a", "b", "e" });

        // IN + NOT IN combined on the same column must not collide on bound parameter names.
        List<Job> queuedOnly = Job.SelectList(db, StatusValues: new[] { "queued", "running" }, StatusNotInValues: new[] { "running" });
        queuedOnly.Select(j => j.JobName).OrderBy(n => n).ToList().ShouldBe(new List<string> { "a", "e" });

        // The extension wrapper threads the new NOT IN argument.
        List<Job> viaExtension = db.SelectList<Job>(StatusNotInValues: new[] { "queued" });
        viaExtension.Select(j => j.JobName).OrderBy(n => n).ToList().ShouldBe(new List<string> { "b", "c", "d" });

        Job.DropTable(db).ShouldBeTrue();
    }

    [Fact]
    public void OrderBy_MultiColumn_PerColumnDirection_Works()
    {
        using var db = OpenInMemory();
        Customer.CreateTable(db).ShouldBeTrue();

        // Two customers share Age 30 so the secondary Name sort key is exercised.
        Customer.Insert(db, new List<Customer>
        {
            NewCustomer("Ana", 30, 1, 1),
            NewCustomer("Bob", 30, 1, 1),
            NewCustomer("Cy", 20, 1, 1),
            NewCustomer("Dan", 40, 1, 1)
        });

        // Age DESC, then Name ASC.
        List<Customer> descAsc = Customer.SelectList(
            db,
            orderByProperties: new[] { "Age", "Name" },
            orderByDirections: new[] { "desc", "asc" });
        descAsc.Select(c => c.Name).ToList().ShouldBe(new List<string> { "Dan", "Ana", "Bob", "Cy" });

        // Per-column direction: Age ASC, then Name DESC (proves a single trailing direction is not applied to all columns).
        List<Customer> ascDesc = Customer.SelectList(
            db,
            orderByProperties: new[] { "Age", "Name" },
            orderByDirections: new[] { "asc", "desc" });
        ascDesc.Select(c => c.Name).ToList().ShouldBe(new List<string> { "Cy", "Bob", "Ana", "Dan" });

        // An unknown column in the middle is dropped, but directions stay paired to the surviving columns
        // by their original input index (Name keeps "asc", not the dropped "Bogus" slot's "desc").
        List<Customer> withUnknownMidList = Customer.SelectList(
            db,
            orderByProperties: new[] { "Age", "Bogus", "Name" },
            orderByDirections: new[] { "desc", "desc", "asc" });
        withUnknownMidList.Select(c => c.Name).ToList().ShouldBe(new List<string> { "Dan", "Ana", "Bob", "Cy" });

        // Fewer directions than columns: the missing trailing slots fall back to the scalar orderByDirection.
        List<Customer> shortDirections = Customer.SelectList(
            db,
            orderByProperties: new[] { "Age", "Name" },
            orderByDirection: "desc",
            orderByDirections: new[] { "asc" });
        shortDirections.Select(c => c.Name).ToList().ShouldBe(new List<string> { "Cy", "Bob", "Ana", "Dan" });

        // SelectEnumerable honors per-column directions too.
        List<Customer> viaEnumerable = Customer.SelectEnumerable(
            db,
            orderByProperties: new[] { "Age", "Name" },
            orderByDirections: new[] { "asc", "desc" }).ToList();
        viaEnumerable.Select(c => c.Name).ToList().ShouldBe(new List<string> { "Cy", "Bob", "Ana", "Dan" });

        // The extension wrapper threads the new orderByDirections argument.
        List<Customer> viaExtension = db.SelectList<Customer>(
            orderByProperties: new[] { "Age", "Name" },
            orderByDirections: new[] { "desc", "asc" });
        viaExtension.Select(c => c.Name).ToList().ShouldBe(new List<string> { "Dan", "Ana", "Bob", "Cy" });

        Customer.DropTable(db).ShouldBeTrue();
    }

    [Fact]
    public void PrimaryKey_Long_RoundTrip()
    {
        using var db = OpenInMemory();
        LongKeyEntity.CreateTable(db).ShouldBeTrue();

        LongKeyEntity.SelectMaxKey(db).ShouldBeNull();

        var entity = new LongKeyEntity { LongName = "long-key" };
        long id = LongKeyEntity.Insert(db, entity);

        id.ShouldBeGreaterThan(0L);
        entity.Id.ShouldBe(id);

        var loaded = LongKeyEntity.SelectSingle(db, LongName: "long-key");
        loaded.ShouldNotBeNull();
        loaded!.Id.ShouldBe(id);
        loaded.LongName.ShouldBe("long-key");

        LongKeyEntity.SelectMaxKey(db).ShouldBe(id);
    }

    [Fact]
    public void PrimaryKey_Guid_RoundTrip()
    {
        using var db = OpenInMemory();
        GuidKeyEntity.CreateTable(db).ShouldBeTrue();

        var entity = new GuidKeyEntity { GuidName = "guid-key" };
        Guid id = GuidKeyEntity.Insert(db, entity);

        id.ShouldNotBe(Guid.Empty);
        entity.Id.ShouldBe(id);

        var loaded = GuidKeyEntity.SelectSingle(db, GuidName: "guid-key");
        loaded.ShouldNotBeNull();
        loaded!.Id.ShouldBe(id);
    }

    [Fact]
    public void PrimaryKey_String_RoundTrip()
    {
        using var db = OpenInMemory();
        StringKeyEntity.CreateTable(db).ShouldBeTrue();

        var entity = new StringKeyEntity { Id = "sk-1", StringName = "string-key" };
        string id = StringKeyEntity.Insert(db, entity);

        id.ShouldBe("sk-1");

        var loaded = StringKeyEntity.SelectSingle(db, StringName: "string-key");
        loaded.ShouldNotBeNull();
        loaded!.Id.ShouldBe("sk-1");
    }

    [Fact]
    public void Column_Decimal_RoundTrip()
    {
        using var db = OpenInMemory();
        ScalarSample.CreateTable(db).ShouldBeTrue();

        var sample = new ScalarSample
        {
            Label = "decimal-row",
            Price = 1234.56m,
            OptionalPrice = 78.90m,
            Payload = [],
            Duration = TimeSpan.Zero
        };
        ScalarSample.Insert(db, sample);

        var loaded = ScalarSample.SelectSingle(db, Label: "decimal-row");
        loaded.ShouldNotBeNull();
        loaded!.Price.ShouldBe(1234.56m);
        loaded.OptionalPrice.ShouldBe(78.90m);
    }

    [Fact]
    public void Column_Decimal_NullRoundTrip()
    {
        using var db = OpenInMemory();
        ScalarSample.CreateTable(db).ShouldBeTrue();

        var sample = new ScalarSample
        {
            Label = "null-decimal",
            Price = 0m,
            OptionalPrice = null,
            Payload = [],
            Duration = TimeSpan.Zero
        };
        ScalarSample.Insert(db, sample);

        var loaded = ScalarSample.SelectSingle(db, Label: "null-decimal");
        loaded.ShouldNotBeNull();
        loaded!.OptionalPrice.ShouldBeNull();
    }

    [Fact]
    public void Column_ByteArray_RoundTrip()
    {
        using var db = OpenInMemory();
        ScalarSample.CreateTable(db).ShouldBeTrue();

        byte[] payload = [0x01, 0x02, 0x03, 0xFF, 0x00, 0x7F];
        var sample = new ScalarSample
        {
            Label = "blob-row",
            Price = 0m,
            Payload = payload,
            Duration = TimeSpan.Zero
        };
        ScalarSample.Insert(db, sample);

        var loaded = ScalarSample.SelectSingle(db, Label: "blob-row");
        loaded.ShouldNotBeNull();
        loaded!.Payload.ShouldBe(payload);
    }

    [Fact]
    public void Column_TimeSpan_RoundTrip()
    {
        using var db = OpenInMemory();
        ScalarSample.CreateTable(db).ShouldBeTrue();

        var duration = new TimeSpan(1, 2, 3, 4);
        var sample = new ScalarSample
        {
            Label = "timespan-row",
            Price = 0m,
            Payload = [],
            Duration = duration
        };
        ScalarSample.Insert(db, sample);

        var loaded = ScalarSample.SelectSingle(db, Label: "timespan-row");
        loaded.ShouldNotBeNull();
        loaded!.Duration.ShouldBe(duration);
    }

    [Fact]
    public void Delete_SingleValue_BindsAllPrimaryKeyShapes()
    {
        using var db = OpenInMemory();

        // Identity (int) key: static, connection-extension, and model-extension overloads.
        Customer.CreateTable(db).ShouldBeTrue();
        var cA = NewCustomer("A", 20, 1, 10);
        var cB = NewCustomer("B", 21, 1, 11);
        var cC = NewCustomer("C", 22, 1, 12);
        Customer.Insert(db, new List<Customer> { cA, cB, cC });
        Customer.Delete(db, cA);
        db.Delete(cB);
        cC.Delete(db);
        Customer.SelectCount(db).ShouldBe(0);

        // Natural single string key (caller-supplied).
        StringKeyEntity.CreateTable(db).ShouldBeTrue();
        var sA = new StringKeyEntity { Id = "s-a", StringName = "alpha" };
        var sB = new StringKeyEntity { Id = "s-b", StringName = "bravo" };
        var sC = new StringKeyEntity { Id = "s-c", StringName = "charlie" };
        StringKeyEntity.Insert(db, new List<StringKeyEntity> { sA, sB, sC });
        StringKeyEntity.Delete(db, sA);
        db.Delete(sB);
        sC.Delete(db);
        StringKeyEntity.SelectCount(db).ShouldBe(0);

        // Natural single Guid key (auto-generated on insert).
        GuidKeyEntity.CreateTable(db).ShouldBeTrue();
        var gA = new GuidKeyEntity { GuidName = "alpha" };
        var gB = new GuidKeyEntity { GuidName = "bravo" };
        var gC = new GuidKeyEntity { GuidName = "charlie" };
        GuidKeyEntity.Insert(db, new List<GuidKeyEntity> { gA, gB, gC });
        GuidKeyEntity.Delete(db, gA);
        db.Delete(gB);
        gC.Delete(db);
        GuidKeyEntity.SelectCount(db).ShouldBe(0);

        // Long identity key.
        LongKeyEntity.CreateTable(db).ShouldBeTrue();
        var lA = new LongKeyEntity { LongName = "alpha" };
        var lB = new LongKeyEntity { LongName = "bravo" };
        var lC = new LongKeyEntity { LongName = "charlie" };
        LongKeyEntity.Insert(db, new List<LongKeyEntity> { lA, lB, lC });
        LongKeyEntity.Delete(db, lA);
        db.Delete(lB);
        lC.Delete(db);
        LongKeyEntity.SelectCount(db).ShouldBe(0);

        // Composite key: single-object delete must bind every key column.
        UserWebsite.CreateTable(db).ShouldBeTrue();
        var uA = new UserWebsite { UserId = 1, WebsiteId = 10, Role = "a" };
        var uB = new UserWebsite { UserId = 1, WebsiteId = 20, Role = "b" };
        var uC = new UserWebsite { UserId = 2, WebsiteId = 10, Role = "c" };
        UserWebsite.Insert(db, new List<UserWebsite> { uA, uB, uC });
        UserWebsite.Delete(db, uA);
        db.Delete(uB);
        uC.Delete(db);
        UserWebsite.SelectCount(db).ShouldBe(0);
    }

    [Fact]
    public void Key_AutoIncrementFalse_UsesSuppliedIntegerKey()
    {
        using var db = OpenInMemory();
        SuppliedKeyEntity.CreateTable(db).ShouldBeTrue();

        SuppliedKeyEntity.Insert(db, new SuppliedKeyEntity { Id = 42, SuppliedLabel = "answer" });
        SuppliedKeyEntity.Insert(db, new SuppliedKeyEntity { Id = 7, SuppliedLabel = "lucky" });

        // The caller-supplied integer keys must be preserved verbatim — no counter reassignment.
        var fortyTwo = SuppliedKeyEntity.SelectSingle(db, Id: 42);
        fortyTwo.ShouldNotBeNull();
        fortyTwo!.SuppliedLabel.ShouldBe("answer");

        var seven = SuppliedKeyEntity.SelectSingle(db, Id: 7);
        seven.ShouldNotBeNull();
        seven!.SuppliedLabel.ShouldBe("lucky");

        SuppliedKeyEntity.SelectCount(db).ShouldBe(2);
    }

    [Fact]
    public void ForeignKey_PositionalConstructorArguments_EmitAndEnforceConstraint()
    {
        using var db = OpenInMemory();

        using (IDbCommand pragma = db.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }

        FkParent.CreateTable(db).ShouldBeTrue();
        PosFkChild.CreateTable(db).ShouldBeTrue();

        FkParent parent = new() { Label = "pos-root" };
        FkParent.Insert(db, parent);
        parent.ParentId.ShouldBeGreaterThan(0);

        // A child referencing an existing parent is accepted.
        PosFkChild.Insert(db, new PosFkChild { PosParentRef = parent.ParentId, PosNote = "ok" });
        PosFkChild.SelectCount(db).ShouldBe(1);

        // A child referencing a non-existent parent violates the positional FK constraint.
        Should.Throw<SqliteException>(() =>
            PosFkChild.Insert(db, new PosFkChild { PosParentRef = 999999, PosNote = "orphan" }));

        // The positional onDelete: Cascade argument cascades the delete to children.
        using (IDbCommand delete = db.CreateCommand())
        {
            delete.CommandText = "DELETE FROM fk_parents WHERE ParentId = " + parent.ParentId + ";";
            delete.ExecuteNonQuery();
        }

        PosFkChild.SelectCount(db).ShouldBe(0);

        PosFkChild.DropTable(db).ShouldBeTrue();
        FkParent.DropTable(db).ShouldBeTrue();
    }

    [Fact]
    public void EnsureSchema_RequiredColumnWithoutConstantDefault_FailsOrRemainsReadable()
    {
        using var db = OpenInMemory();

        // Older shape: the required MigRequiredLabel column does not exist yet. On an EMPTY legacy
        // table there are no rows to strand as NULL, so the additive migration proceeds and the
        // migrated table round-trips a fully populated insert.
        using (IDbCommand create = db.CreateCommand())
        {
            create.CommandText = "CREATE TABLE mig_required (MigRequiredId INTEGER PRIMARY KEY)";
            create.ExecuteNonQuery();
        }

        MigRequiredEntity.EnsureSchema(db).ShouldBeTrue();

        MigRequiredEntity entity = new() { MigRequiredLabel = "first" };
        MigRequiredEntity.Insert(db, entity);
        MigRequiredEntity? readback = MigRequiredEntity.SelectSingle(db, MigRequiredId: entity.MigRequiredId);
        readback.ShouldNotBeNull();
        readback!.MigRequiredLabel.ShouldBe("first");

        MigRequiredEntity.DropTable(db).ShouldBeTrue();

        // Older shape again, but POPULATED before the required column is introduced. Adding it
        // nullable would leave the pre-existing row unreadable through the generated reader, so
        // EnsureSchema must fail fast instead of reporting success.
        using (IDbCommand create = db.CreateCommand())
        {
            create.CommandText = "CREATE TABLE mig_required (MigRequiredId INTEGER PRIMARY KEY)";
            create.ExecuteNonQuery();
        }
        using (IDbCommand seed = db.CreateCommand())
        {
            seed.CommandText = "INSERT INTO mig_required (MigRequiredId) VALUES (1)";
            seed.ExecuteNonQuery();
        }

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() => MigRequiredEntity.EnsureSchema(db));
        ex.Message.ShouldContain("MigRequiredLabel");

        // The migration aborted before altering the table — the pre-existing row is intact.
        using (IDbCommand count = db.CreateCommand())
        {
            count.CommandText = "SELECT COUNT(*) FROM mig_required";
            Convert.ToInt64(count.ExecuteScalar()).ShouldBe(1L);
        }

        MigRequiredEntity.DropTable(db).ShouldBeTrue();
    }

    [Fact]
    public void EnsureSchema_RawDefaultColumn_FailsOrPreservesComputedDefault()
    {
        using var db = OpenInMemory();

        // On an EMPTY legacy table the raw-default column is added (SQLite cannot apply the
        // database-computed default via ALTER, but there are no rows to strand), and EnsureSchema
        // reports success.
        using (IDbCommand create = db.CreateCommand())
        {
            create.CommandText = "CREATE TABLE mig_raw (MigRawId INTEGER PRIMARY KEY)";
            create.ExecuteNonQuery();
        }

        MigRawEntity.EnsureSchema(db).ShouldBeTrue();

        bool hasColumn = false;
        using (IDbCommand info = db.CreateCommand())
        {
            info.CommandText = "PRAGMA table_info(mig_raw)";
            using IDataReader r = info.ExecuteReader();
            while (r.Read())
            {
                if (string.Equals(r.GetString(1), "MigRawStamp", StringComparison.OrdinalIgnoreCase))
                {
                    hasColumn = true;
                }
            }
        }
        hasColumn.ShouldBeTrue();

        MigRawEntity.DropTable(db).ShouldBeTrue();

        // On a POPULATED legacy table the raw default cannot backfill the pre-existing row, which
        // would strand it NULL in a non-nullable slot. EnsureSchema must fail fast.
        using (IDbCommand create = db.CreateCommand())
        {
            create.CommandText = "CREATE TABLE mig_raw (MigRawId INTEGER PRIMARY KEY)";
            create.ExecuteNonQuery();
        }
        using (IDbCommand seed = db.CreateCommand())
        {
            seed.CommandText = "INSERT INTO mig_raw (MigRawId) VALUES (1)";
            seed.ExecuteNonQuery();
        }

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() => MigRawEntity.EnsureSchema(db));
        ex.Message.ShouldContain("MigRawStamp");

        using (IDbCommand count = db.CreateCommand())
        {
            count.CommandText = "SELECT COUNT(*) FROM mig_raw";
            Convert.ToInt64(count.ExecuteScalar()).ShouldBe(1L);
        }

        MigRawEntity.DropTable(db).ShouldBeTrue();
    }

    [Fact]
    public void ReservedWordColumn_QuotesAndRoundTrips()
    {
        using var db = OpenInMemory();
        ReservedWordEntity.CreateTable(db).ShouldBeTrue();

        var alpha = new ReservedWordEntity { Group = "alpha", Table = 10 };
        ReservedWordEntity.Insert(db, alpha);
        alpha.Id.ShouldBeGreaterThan(0);

        ReservedWordEntity.Insert(db, new ReservedWordEntity { Group = "beta", Table = 20 });
        ReservedWordEntity.Insert(db, new ReservedWordEntity { Group = "gamma", Table = 30 });

        ReservedWordEntity.SelectCount(db).ShouldBe(3);

        // Equality WHERE on a reserved-word column ("Table" = $Table).
        ReservedWordEntity? single = ReservedWordEntity.SelectSingle(db, Table: 20);
        single.ShouldNotBeNull();
        single!.Group.ShouldBe("beta");

        // IN filter ("Group" IN (...)) and ORDER BY on a reserved-word column ("Table" DESC).
        List<ReservedWordEntity> filtered = ReservedWordEntity.SelectList(
            db,
            orderByProperties: new[] { "Table" },
            orderByDirection: "desc",
            GroupValues: new[] { "alpha", "gamma" });
        filtered.Count.ShouldBe(2);
        filtered[0].Group.ShouldBe("gamma"); // Table = 30 sorts first descending.
        filtered[1].Group.ShouldBe("alpha");

        // Update round-trips (UPDATE ... SET "Group" = ..., "Table" = ... WHERE "Id" = ...).
        single.Table = 99;
        ReservedWordEntity.Update(db, single);
        ReservedWordEntity.SelectSingle(db, Group: "beta")!.Table.ShouldBe(99);

        // Delete round-trips using a reserved-word equality filter.
        ReservedWordEntity.Delete(db, Table: 99);
        ReservedWordEntity.SelectCount(db).ShouldBe(2);

        ReservedWordEntity.DropTable(db).ShouldBeTrue();
    }

    [Fact]
    public void Upsert_UnknownUpdateColumn_FailsPredictably()
    {
        using var db = OpenInMemory();
        Counter.CreateTable(db).ShouldBeTrue();

        Should.Throw<ArgumentException>(() =>
            Counter.Upsert(
                db,
                new Counter { Bucket = "a", Label = "first", Hits = 1 },
                conflictColumns: new[] { "Bucket" },
                updateColumns: new[] { "NotAColumn" }));

        Counter.DropTable(db).ShouldBeTrue();
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
