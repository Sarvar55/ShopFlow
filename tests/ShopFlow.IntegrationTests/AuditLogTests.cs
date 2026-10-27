using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ShopFlow.Application.Auditing;
using ShopFlow.Application.Common;
using ShopFlow.IntegrationTests.Infrastructure;

namespace ShopFlow.IntegrationTests;

public class AuditLogTests(ShopFlowApiFactory factory) : IntegrationTestBase(factory)
{
    [Theory]
    [InlineData("UPDATE audit_logs SET entity_type = 'tampered'")]
    [InlineData("DELETE FROM audit_logs")]
    [InlineData("TRUNCATE audit_logs")]
    public async Task Database_rejects_any_modification_of_audit_rows(string sql)
    {
        await CreateProductAsync(); // guarantees there is at least one audit row

        var act = () => WithDbAsync(db => db.Database.ExecuteSqlRawAsync(sql));

        (await act.Should().ThrowAsync<PostgresException>())
            .Which.MessageText.Should().Contain("append-only");
    }

    [Fact]
    public async Task Application_refuses_to_delete_audit_rows_through_ef()
    {
        await CreateProductAsync();

        var act = () => WithDbAsync(async db =>
        {
            var row = await db.AuditLogs.FirstAsync();
            db.AuditLogs.Remove(row);
            return await db.SaveChangesAsync();
        });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*append-only*");
    }

    [Fact]
    public async Task Password_hash_is_never_written_to_the_audit_log()
    {
        var (_, userId) = await NewCustomerAsync();

        var row = await WithDbAsync(db => db.AuditLogs.SingleAsync(a => a.EntityType == "User" && a.EntityId == userId.ToString()));

        row.Changes.Should().Contain("Email").And.NotContain("PasswordHash");
    }

    [Fact]
    public async Task Admin_can_query_audit_trail_of_an_entity()
    {
        var product = await CreateProductAsync(stock: 1);
        var admin = await AdminAsync();
        await admin.PostAsync($"/api/products/{product.Id}/restock", System.Net.Http.Json.JsonContent.Create(new { quantity = 4 }));

        var response = await admin.GetAsync($"/api/admin/audit-logs?entityType=Product&entityId={product.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResult<AuditLogDto>>(response);
        page.Items.Select(i => i.Action.ToString()).Should().Equal("Update", "Insert"); // newest first
        page.Items[0].Changes.GetProperty("StockQuantity").GetProperty("new").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task Customers_cannot_read_audit_trail()
    {
        var (customer, _) = await NewCustomerAsync();

        (await customer.GetAsync("/api/admin/audit-logs")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
