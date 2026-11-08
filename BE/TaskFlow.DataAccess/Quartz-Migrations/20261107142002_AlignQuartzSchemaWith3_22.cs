using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.DataAccess.QuartzMigrations
{
    // ROADMAP R11 / f05-overdue.md Technical contract T6: QuartzContext has no
    // mapped model, so this is hand-written SQL, matching
    // 20240828071644_InitDatabase.cs's style. The five columns below are the
    // only difference between the 3.13-era schema this scaffold shipped with
    // and the installed Quartz.NET 3.22.7 package, confirmed by diffing the
    // committed InitDatabase script against the official
    // database/tables/tables_mysql_innodb.sql at tag v3.22.0
    // (quartznet/quartznet, 2026-11-07). Additive: both tables are empty
    // (nothing has scheduled successfully against the current schema), and
    // MySQL 8.4 adds nullable columns and one NOT NULL DEFAULT column
    // instantly on an empty table.
    /// <inheritdoc />
    public partial class AlignQuartzSchemaWith3_22 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                                 ALTER TABLE QRTZ_TRIGGERS
                                     ADD COLUMN MISFIRE_ORIG_FIRE_TIME BIGINT NULL,
                                     ADD COLUMN EXECUTION_GROUP VARCHAR(200) NULL,
                                     ADD COLUMN PREFERRED_NODE VARCHAR(200) NULL,
                                     ADD COLUMN PREFERRED_NODE_AUTO BOOLEAN NOT NULL DEFAULT FALSE;

                                 ALTER TABLE QRTZ_FIRED_TRIGGERS
                                     ADD COLUMN EXECUTION_GROUP VARCHAR(200) NULL;
                                 """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                                 ALTER TABLE QRTZ_FIRED_TRIGGERS
                                     DROP COLUMN EXECUTION_GROUP;

                                 ALTER TABLE QRTZ_TRIGGERS
                                     DROP COLUMN MISFIRE_ORIG_FIRE_TIME,
                                     DROP COLUMN EXECUTION_GROUP,
                                     DROP COLUMN PREFERRED_NODE,
                                     DROP COLUMN PREFERRED_NODE_AUTO;
                                 """);
        }
    }
}
