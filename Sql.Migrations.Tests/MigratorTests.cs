﻿using Dapper;
using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Sql.Migrations.Tests
{
    public class MigratorTests
    {
        [Fact]
        public void String_with_no_GO_is_only_one_script()
        {
            var test = @"CREATE TABLE [dbo].[Migrations](
        [Id] [uniqueidentifier] NOT NULL,
        [Filename] [nvarchar](255) NOT NULL,
        [AppliedOn] [datetimeoffset](7) NOT NULL,
 CONSTRAINT [PK_Migrations] PRIMARY KEY CLUSTERED
(
        [Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]";

            Assert.Single(Migrator.SplitIntoStatements(test));
        }

        [Fact]
        public void String_with_two_GOs_should_result_in_two_Statements()
        {
            var twoGos = @"/****** CREATE TABLES HERE ******/

/*

CREATE TABLE ....

Sripts can be generated with SQL Server Management Studio,
or with a Visual Studio database project

*/

CREATE TABLE [dbo].[Foo](
        [Id] [uniqueidentifier] NOT NULL,

PRIMARY KEY CLUSTERED
(
        [Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[Bar] CHECK CONSTRAINT [FooBar]
GO";

            // TODO: Discuss: should we remove empty lines?
            Assert.Collection(Migrator.SplitIntoStatements(twoGos),
                x => Assert.StartsWith("/****", x),
                x => Assert.StartsWith(@"


ALTER", x)
                );
        }

        [Fact]
        public void On_empty_db_can_execute_no_Scripts()
        {
            TestSuite.WithCleanDbConnection((c) =>
            {
                Migrator.ExecuteMigrations(c, Enumerable.Empty<MigrationScript>());
            });
        }

        [Fact]
        public void On_empty_db_can_create_a_Migrations_table()
        {
            TestSuite.WithCleanDbConnection((c) =>
            {
                using (var tx = c.BeginTransaction())
                {
                    Migrator.CreateMigrationsTable(c, tx);
                    tx.Commit();
                }

                Assert.NotEmpty(c.Query<string>("SELECT Filename FROM [dbo].[Migrations]"));
            });
        }

        [Fact]
        public void Can_execute_one_create_Table_Script_in_empty_Db()
        {
            TestSuite.WithCleanDbConnection((c) =>
            {
                var migration = new MigrationScript
                {
                    Name = "create bla table",
                    Script = "CREATE TABLE [dbo].[bla]([Id] [uniqueidentifier] NOT NULL)"
                };
                Migrator.ExecuteMigrations(c, new[] { migration }, true);

                Assert.Empty(c.Query<object>("SELECT * FROM [dbo].[bla]"));
            });
        }

        [Fact]
        public void Does_not_execute_the_same_script_twice()
        {
            TestSuite.WithCleanDbConnection((c) =>
            {
                var migration = new MigrationScript
                {
                    Name = "create bla table",
                    Script = "CREATE TABLE [dbo].[bla]([Id] [uniqueidentifier] NOT NULL)"
                };
                Migrator.ExecuteMigrations(c, new[] { migration }, true);

                Migrator.ExecuteMigrations(c, new[] { migration }, true);

                // Migrations table + bla
                Assert.Equal(2,c.Query<string>("SELECT Filename FROM [dbo].[Migrations]").ToList().Count);
            });
        }
    }
}
