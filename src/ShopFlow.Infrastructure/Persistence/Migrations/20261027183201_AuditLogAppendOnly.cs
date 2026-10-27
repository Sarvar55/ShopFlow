using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopFlow.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Makes audit_logs append-only at the database level. Even if application code (or someone with
    /// a SQL console and the app's credentials) tries to UPDATE, DELETE or TRUNCATE audit rows,
    /// PostgreSQL rejects the statement and the surrounding transaction fails.
    /// </summary>
    public partial class AuditLogAppendOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION audit_logs_prevent_mutation() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'audit_logs is append-only: % is not allowed', TG_OP
                        USING ERRCODE = 'insufficient_privilege';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_audit_logs_no_update_delete
                    BEFORE UPDATE OR DELETE ON audit_logs
                    FOR EACH ROW EXECUTE FUNCTION audit_logs_prevent_mutation();

                CREATE TRIGGER trg_audit_logs_no_truncate
                    BEFORE TRUNCATE ON audit_logs
                    FOR EACH STATEMENT EXECUTE FUNCTION audit_logs_prevent_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_audit_logs_no_truncate ON audit_logs;
                DROP TRIGGER IF EXISTS trg_audit_logs_no_update_delete ON audit_logs;
                DROP FUNCTION IF EXISTS audit_logs_prevent_mutation();
                """);
        }
    }
}
