using Mayfly.Core.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.Azurite;
using Testcontainers.MsSql;

namespace Mayfly.Web.Tests;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly MsSqlContainer sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-CU9-ubuntu-24.04").Build();

    private readonly AzuriteContainer azurite = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.37.0")
        .WithCommand("--skipApiVersionCheck")
        .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = default!;

    public IDbContextFactory<MayflyDbContext> DbFactory => Factory.Services.GetRequiredService<IDbContextFactory<MayflyDbContext>>();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(sql.StartAsync(), azurite.StartAsync());

        var connectionString = new SqlConnectionStringBuilder(sql.GetConnectionString())
        {
            InitialCatalog = "mayfly_tests"
        }.ConnectionString;

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder
                .UseSetting("ConnectionStrings:Mayfly", connectionString)
                .UseSetting("Storage:ConnectionString", azurite.GetConnectionString()));

        await using var db = await DbFactory.CreateDbContextAsync();
        await db.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await sql.DisposeAsync();
        await azurite.DisposeAsync();
    }
}
