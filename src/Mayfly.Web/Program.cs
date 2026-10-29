using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Storage.Blobs;
using Mayfly.Core.Data;
using Mayfly.Core.Files;
using Mayfly.Web.Cleanup;
using Mayfly.Web.Components;
using Mayfly.Web.Files;
using Mayfly.Web.Sharing;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Resources;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();

builder.Services.AddDbContextFactory<MayflyDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Mayfly"),
        sql => sql.EnableRetryOnFailure().MigrationsAssembly(typeof(Program).Assembly)));

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("mayfly");

var keyVaultKeyUri = builder.Configuration["DataProtection:KeyVaultKeyUri"];
if (keyVaultKeyUri is not null)
{
    dataProtection
        .PersistKeysToDbContext<MayflyDbContext>()
        .ProtectKeysWithAzureKeyVault(new Uri(keyVaultKeyUri), AppIdentityCredential());
}

var storageConnectionString = builder.Configuration["Storage:ConnectionString"];
builder.Services.AddSingleton(_ => storageConnectionString is not null
    ? new BlobServiceClient(storageConnectionString)
    : new BlobServiceClient(
        new Uri(builder.Configuration["Storage:BlobEndpoint"]
            ?? throw new InvalidOperationException("Storage:BlobEndpoint or Storage:ConnectionString is required.")),
        AppIdentityCredential()));
builder.Services.AddSingleton<FileStorage>();
builder.Services.AddHostedService<ExpiredContentCleanupService>();
if (storageConnectionString is not null)
{
    builder.Services.AddHostedService<LocalStorageSetup>();
}

builder.Services.AddSingleton<ShareSession>();
builder.Services.AddSingleton<ShareTokenProtector>();
builder.Services.AddShareLinkRateLimit();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options => options.LoginPath = "/account/login");
builder.Services.AddAuthorization();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<MayflyDbContext>()
    .AddSignInManager();

if (builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] is not null)
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService("mayfly-web"))
        .UseAzureMonitor(options => options.Credential = AppIdentityCredential());
    builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(options =>
        options.Filter = context => !context.Request.Path.StartsWithSegments("/healthz"));
}

builder.Services.AddHealthChecks()
    .AddDbContextCheck<MayflyDbContext>(tags: ["ready"]);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseRateLimiter();
app.UseAntiforgery();

var appVersion = builder.Configuration["APP_VERSION"];
Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    if (appVersion is not null)
    {
        context.Response.Headers["X-App-Version"] = appVersion;
    }

    context.Response.ContentType = "text/plain";
    return context.Response.WriteAsync(report.Status.ToString());
}

app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthResponse
});
app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponse
});

app.MapPost("/account/logout", async (
    HttpContext context,
    IAntiforgery antiforgery,
    SignInManager<ApplicationUser> signInManager) =>
{
    if (!await antiforgery.IsRequestValidAsync(context))
    {
        return Results.BadRequest();
    }

    await signInManager.SignOutAsync();
    return Results.LocalRedirect("/");
});

app.MapGuestEntryEndpoint();
app.MapSharedFileDownloadEndpoint();
app.MapFileUploadEndpoints();
app.MapFileDownloadEndpoint();

app.MapStaticAssets();
app.MapRazorComponents<App>();

app.Run();

ManagedIdentityCredential AppIdentityCredential() =>
    new(ManagedIdentityId.FromUserAssignedClientId(builder.Configuration["AZURE_CLIENT_ID"]
        ?? throw new InvalidOperationException("AZURE_CLIENT_ID is required to use the app managed identity.")));
