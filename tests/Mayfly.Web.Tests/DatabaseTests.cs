using System.Net;
using System.Text.RegularExpressions;
using Mayfly.Web.Data;
using Mayfly.Web.Files;
using Mayfly.Web.Sharing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Mayfly.Web.Tests;

public class DatabaseTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private const string Password = "Passw0rd!x";

    private static readonly Regex AntiforgeryField = new("name=\"__RequestVerificationToken\" value=\"([^\"]+)\"");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OneTimeLink_OpensForOnlyOneOfManyConcurrentVisitors()
    {
        var (_, ownerId) = await RegisterAsync();
        var pasteId = await SeedPasteAsync(ownerId, "burn after read");
        var token = await SeedShareLinkAsync(pasteId, maxViews: 1);

        var visitors = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            var client = CreateClient();
            return (Client: client, Antiforgery: await GetAntiforgeryTokenAsync(client, $"/s/{token}"));
        }));

        var responses = await Task.WhenAll(visitors.Select(visitor =>
            visitor.Client.PostAsync($"/s/{token}", Form(visitor.Antiforgery, ("_handler", "reveal")), Ct)));

        var winner = Assert.Single(visitors.Zip(responses), pair => pair.Second.StatusCode == HttpStatusCode.Redirect).First.Client;
        Assert.Contains("burn after read", await winner.GetStringAsync($"/s/{token}", Ct));

        var latecomer = await CreateClient().GetAsync($"/s/{token}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, latecomer.StatusCode);

        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        Assert.Equal(1, await db.ShareLinks.Where(link => link.PasteId == pasteId).Select(link => link.ViewCount).SingleAsync(Ct));
    }

    [Fact]
    public async Task PastePage_IsNotFoundForAnotherUser()
    {
        var (owner, ownerId) = await RegisterAsync();
        var (other, _) = await RegisterAsync();
        var pasteId = await SeedPasteAsync(ownerId, "private paste");

        var ownerResponse = await owner.GetAsync($"/pastes/{pasteId}", Ct);
        var otherResponse = await other.GetAsync($"/pastes/{pasteId}", Ct);

        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherResponse.StatusCode);
    }

    [Fact]
    public async Task CreatePaste_WithUndefinedLifetime_ShowsValidationErrorAndSavesNothing()
    {
        var (owner, _) = await RegisterAsync();
        var title = $"invalid-{Guid.NewGuid():N}";
        var antiforgery = await GetAntiforgeryTokenAsync(owner, "/pastes");

        var response = await owner.PostAsync(
            "/pastes",
            Form(antiforgery, ("_handler", "create-paste"), ("Input.Title", title), ("Input.Lifetime", "999")),
            Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("The field Lifetime is invalid.", await response.Content.ReadAsStringAsync(Ct));
        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        Assert.False(await db.Pastes.AnyAsync(paste => paste.Title == title, Ct));
    }

    [Fact]
    public async Task UploadStart_RejectsFileThatWouldExceedStorageQuota()
    {
        var (owner, _) = await RegisterAsync();
        var antiforgery = await GetAntiforgeryTokenAsync(owner, "/files");
        var filesThatFit = FileQuota.MaxBytesPerUser / FileUploadEndpoints.MaxFileSize;

        for (var i = 0; i < filesThatFit; i++)
        {
            var allowed = await StartUploadAsync(owner, antiforgery, FileUploadEndpoints.MaxFileSize);
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        var rejected = await StartUploadAsync(owner, antiforgery, 1);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
    }

    private HttpClient CreateClient() =>
        database.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task<(HttpClient Client, string UserId)> RegisterAsync()
    {
        var client = CreateClient();
        var email = $"user-{Guid.NewGuid():N}@test.local";
        var antiforgery = await GetAntiforgeryTokenAsync(client, "/account/register");

        var response = await client.PostAsync(
            "/account/register",
            Form(antiforgery,
                ("_handler", "register"),
                ("Input.Email", email),
                ("Input.Password", Password),
                ("Input.ConfirmPassword", Password)),
            Ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        var userId = await db.Users.Where(user => user.Email == email).Select(user => user.Id).SingleAsync(Ct);
        return (client, userId);
    }

    private async Task<Guid> SeedPasteAsync(string ownerId, string title)
    {
        var paste = new Paste { OwnerId = ownerId, Title = title, CreatedAt = DateTimeOffset.UtcNow };

        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        db.Pastes.Add(paste);
        await db.SaveChangesAsync(Ct);
        return paste.Id;
    }

    private async Task<string> SeedShareLinkAsync(Guid pasteId, int? maxViews)
    {
        var token = ShareToken.Generate();

        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        db.ShareLinks.Add(new ShareLink
        {
            PasteId = pasteId,
            TokenHash = ShareToken.Hash(token),
            CreatedAt = DateTimeOffset.UtcNow,
            MaxViews = maxViews
        });
        await db.SaveChangesAsync(Ct);
        return token;
    }

    private static Task<HttpResponseMessage> StartUploadAsync(HttpClient client, string antiforgery, long size) =>
        client.PostAsync(
            "/files/uploads",
            Form(antiforgery, ("FileName", $"{Guid.NewGuid():N}.bin"), ("Size", size.ToString()), ("Lifetime", "Forever")),
            Ct);

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var page = await client.GetStringAsync(url, Ct);
        return AntiforgeryField.Match(page).Groups[1].Value;
    }

    private static FormUrlEncodedContent Form(string antiforgery, params (string Name, string Value)[] fields) =>
        new([
            new("__RequestVerificationToken", antiforgery),
            .. fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value))
        ]);
}
