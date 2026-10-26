using Azure.Identity;
using Azure.Storage.Blobs;
using Mayfly.Core.Data;
using Mayfly.Core.Files;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

builder.Services.AddDbContextFactory<MayflyDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Mayfly"),
        sql => sql.EnableRetryOnFailure()));

var storageConnectionString = builder.Configuration["Storage:ConnectionString"];
builder.Services.AddSingleton(_ => storageConnectionString is not null
    ? new BlobServiceClient(storageConnectionString)
    : new BlobServiceClient(
        new Uri(builder.Configuration["Storage:BlobEndpoint"]
            ?? throw new InvalidOperationException("Storage:BlobEndpoint or Storage:ConnectionString is required.")),
        new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(builder.Configuration["AZURE_CLIENT_ID"]
            ?? throw new InvalidOperationException("AZURE_CLIENT_ID is required to use the functions managed identity.")))));
builder.Services.AddSingleton<FileStorage>();

builder.Build().Run();
