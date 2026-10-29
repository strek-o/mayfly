using System.Net;
using System.Text.RegularExpressions;
using Azure.Storage.Blobs;
using Mayfly.Core.Cleanup;
using Mayfly.Core.Data;
using Mayfly.Core.Files;
using Mayfly.Web.Files;
using Mayfly.Web.Sharing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
        var antiforgery = await GetAntiforgeryTokenAsync(owner, "/desktop");
        var filesThatFit = FileQuota.MaxBytesPerUser / FileUploadEndpoints.MaxFileSize;

        for (var i = 0; i < filesThatFit; i++)
        {
            var allowed = await StartUploadAsync(owner, antiforgery, FileUploadEndpoints.MaxFileSize);
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        var rejected = await StartUploadAsync(owner, antiforgery, 1);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
    }

    [Fact]
    public async Task DeletePaste_RemovesPasteWithThreadsAndShareLinks()
    {
        var (owner, ownerId) = await RegisterAsync();
        var pasteId = await SeedPasteAsync(ownerId, "to delete");
        var threadId = await SeedThreadAsync(pasteId);
        await SeedShareLinkAsync(pasteId, maxViews: null);
        await SeedShareLinkAsync(pasteId, maxViews: null, threadId);

        var response = await owner.PostAsync(
            $"/pastes/{pasteId}",
            Form(await GetAntiforgeryTokenAsync(owner, $"/pastes/{pasteId}"), ("_handler", "delete-paste")),
            Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        Assert.False(await db.Pastes.AnyAsync(paste => paste.Id == pasteId, Ct));
        Assert.False(await db.Threads.AnyAsync(thread => thread.Id == threadId, Ct));
        Assert.False(await db.ShareLinks.AnyAsync(link => link.PasteId == pasteId, Ct));
    }

    [Fact]
    public async Task DeletePaste_ByAnotherUser_KeepsPaste()
    {
        var (_, ownerId) = await RegisterAsync();
        var (other, _) = await RegisterAsync();
        var pasteId = await SeedPasteAsync(ownerId, "not yours");

        var response = await other.PostAsync(
            $"/pastes/{pasteId}",
            Form(await GetAntiforgeryTokenAsync(other, "/pastes"), ("_handler", "delete-paste")),
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        Assert.True(await db.Pastes.AnyAsync(paste => paste.Id == pasteId, Ct));
    }

    [Fact]
    public async Task Desktop_ListsOnlyOwnFilesWithTimeLeft()
    {
        var (owner, ownerId) = await RegisterAsync();
        var (_, otherId) = await RegisterAsync();
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(12);
        await SeedFileAsync(ownerId, now, uploaded: true, expiresAt, "mine.txt");
        await SeedFileAsync(otherId, now, uploaded: true, fileName: "theirs.txt");

        var page = await owner.GetStringAsync("/desktop", Ct);

        Assert.Contains("mine.txt", page);
        Assert.Contains("12 min", page);
        Assert.Contains($"data-expires=\"{expiresAt.UtcDateTime:yyyy-MM-dd'T'HH:mm:ss'Z'}\"", page);
        Assert.Contains("no view limit", page);
        Assert.DoesNotContain("theirs.txt", page);
    }

    [Fact]
    public async Task CreateFolder_KeepsItsFilesOffTheDesktop()
    {
        var (owner, ownerId) = await RegisterAsync();

        var created = await owner.PostAsync(
            "/desktop",
            Form(await GetAntiforgeryTokenAsync(owner, "/desktop"),
                ("_handler", "create-folder"),
                ("FolderInput.Name", "  Screenshots  ")),
            Ct);

        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        Guid folderId;
        await using (var db = await database.DbFactory.CreateDbContextAsync(Ct))
        {
            folderId = await db.Folders.Where(f => f.OwnerId == ownerId && f.Name == "Screenshots").Select(f => f.Id).SingleAsync(Ct);
        }

        var now = DateTimeOffset.UtcNow;
        await SeedFileAsync(ownerId, now, uploaded: true, fileName: "inside.png", folderId: folderId);
        await SeedFileAsync(ownerId, now, uploaded: true, fileName: "outside.txt");

        var desktop = await owner.GetStringAsync("/desktop", Ct);
        Assert.Contains("Screenshots", desktop);
        Assert.Contains("1 file", desktop);
        Assert.Contains("outside.txt", desktop);
        Assert.DoesNotContain("inside.png", desktop);

        var inFolder = await owner.GetStringAsync($"/desktop/folders/{folderId}", Ct);
        Assert.Contains("inside.png", inFolder);
        Assert.DoesNotContain("outside.txt", inFolder);
    }

    [Fact]
    public async Task FolderView_ForAnotherUsersFolder_ReturnsNotFound()
    {
        var (_, ownerId) = await RegisterAsync();
        var (other, _) = await RegisterAsync();
        var folderId = await SeedFolderAsync(ownerId);

        var response = await other.GetAsync($"/desktop/folders/{folderId}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UploadStart_AcceptsOnlyOwnFolder()
    {
        var (owner, ownerId) = await RegisterAsync();
        var (_, otherId) = await RegisterAsync();
        var ownFolder = await SeedFolderAsync(ownerId);
        var foreignFolder = await SeedFolderAsync(otherId);
        var antiforgery = await GetAntiforgeryTokenAsync(owner, "/desktop");

        var accepted = await owner.PostAsync(
            "/files/uploads",
            Form(antiforgery, ("FileName", "own.txt"), ("Size", "5"), ("Lifetime", "Forever"), ("FolderId", ownFolder.ToString())),
            Ct);
        var rejected = await owner.PostAsync(
            "/files/uploads",
            Form(antiforgery, ("FileName", "foreign.txt"), ("Size", "5"), ("Lifetime", "Forever"), ("FolderId", foreignFolder.ToString())),
            Ct);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        Assert.Equal(ownFolder, await db.Files.Where(f => f.OwnerId == ownerId && f.FileName == "own.txt").Select(f => f.FolderId).SingleAsync(Ct));
        Assert.False(await db.Files.AnyAsync(f => f.FileName == "foreign.txt", Ct));
    }

    [Fact]
    public async Task MoveFile_IntoOwnFolderAndBackToTheDesktop()
    {
        var (owner, ownerId) = await RegisterAsync();
        var folderId = await SeedFolderAsync(ownerId);
        var (file, _) = await SeedFileAsync(ownerId, DateTimeOffset.UtcNow, uploaded: true);
        var antiforgery = await GetAntiforgeryTokenAsync(owner, "/desktop");

        var intoFolder = await owner.PostAsync($"/files/{file.Id}/move", Form(antiforgery, ("FolderId", folderId.ToString())), Ct);
        Assert.Equal(HttpStatusCode.NoContent, intoFolder.StatusCode);
        Assert.Equal(folderId, await GetFolderIdAsync(file.Id));

        var backToDesktop = await owner.PostAsync($"/files/{file.Id}/move", Form(antiforgery), Ct);
        Assert.Equal(HttpStatusCode.NoContent, backToDesktop.StatusCode);
        Assert.Null(await GetFolderIdAsync(file.Id));
    }

    [Fact]
    public async Task MoveFile_RejectsAnotherUsersFolderOrFile()
    {
        var (owner, ownerId) = await RegisterAsync();
        var (_, otherId) = await RegisterAsync();
        var foreignFolder = await SeedFolderAsync(otherId);
        var (ownFile, _) = await SeedFileAsync(ownerId, DateTimeOffset.UtcNow, uploaded: true);
        var (foreignFile, _) = await SeedFileAsync(otherId, DateTimeOffset.UtcNow, uploaded: true);
        var ownFolder = await SeedFolderAsync(ownerId);
        var antiforgery = await GetAntiforgeryTokenAsync(owner, "/desktop");

        var toForeignFolder = await owner.PostAsync($"/files/{ownFile.Id}/move", Form(antiforgery, ("FolderId", foreignFolder.ToString())), Ct);
        var foreignFileMoved = await owner.PostAsync($"/files/{foreignFile.Id}/move", Form(antiforgery, ("FolderId", ownFolder.ToString())), Ct);
        var withoutAntiforgery = await owner.PostAsync(
            $"/files/{ownFile.Id}/move",
            new FormUrlEncodedContent([new("FolderId", ownFolder.ToString())]),
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, toForeignFolder.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignFileMoved.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, withoutAntiforgery.StatusCode);
        Assert.Null(await GetFolderIdAsync(ownFile.Id));
        Assert.Null(await GetFolderIdAsync(foreignFile.Id));
    }

    [Fact]
    public async Task FileWindow_ForAnotherUsersFile_ReturnsNotFound()
    {
        var (_, ownerId) = await RegisterAsync();
        var (other, _) = await RegisterAsync();
        var (file, _) = await SeedFileAsync(ownerId, DateTimeOffset.UtcNow, uploaded: true);

        var response = await other.GetAsync($"/desktop/files/{file.Id}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ShareLinksPanel_CreatesAndRevokesFileLink()
    {
        var (owner, ownerId) = await RegisterAsync();
        var (file, _) = await SeedFileAsync(ownerId, DateTimeOffset.UtcNow, uploaded: true);
        var url = $"/desktop/files/{file.Id}";

        var created = await owner.PostAsync(
            url,
            Form(await GetAntiforgeryTokenAsync(owner, url),
                ("_handler", "create-share-link"),
                ("LinkInput.Lifetime", "OneDay"),
                ("LinkInput.MaxViews", "3")),
            Ct);

        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        Guid linkId;
        await using (var db = await database.DbFactory.CreateDbContextAsync(Ct))
        {
            linkId = await db.ShareLinks.Where(link => link.FileId == file.Id && link.MaxViews == 3).Select(link => link.Id).SingleAsync(Ct);
        }
        Assert.Contains("3 of 3 views left", await owner.GetStringAsync(url, Ct));

        var revoked = await owner.PostAsync(
            url,
            Form(await GetAntiforgeryTokenAsync(owner, url),
                ("_handler", "revoke-share-link"),
                ("RevokeId", linkId.ToString())),
            Ct);

        Assert.Equal(HttpStatusCode.Redirect, revoked.StatusCode);
        var page = await owner.GetStringAsync(url, Ct);
        Assert.DoesNotContain("3 of 3 views left", page);
        Assert.Contains("Past links (1)", page);
        Assert.Contains("Revoked</span>", page);
    }

    [Fact]
    public async Task DeleteFile_RemovesBlobShareLinksAndFile()
    {
        var (owner, ownerId) = await RegisterAsync();
        var (file, blob) = await SeedFileAsync(ownerId, DateTimeOffset.UtcNow, uploaded: true);

        var response = await owner.PostAsync(
            $"/desktop/files/{file.Id}",
            Form(await GetAntiforgeryTokenAsync(owner, $"/desktop/files/{file.Id}"), ("_handler", "delete-file")),
            Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(await blob.ExistsAsync(Ct));
        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        Assert.False(await db.Files.AnyAsync(f => f.Id == file.Id, Ct));
        Assert.False(await db.ShareLinks.AnyAsync(link => link.FileId == file.Id, Ct));
    }

    [Fact]
    public async Task Cleanup_DeletesExpiredContentAndAbandonedUploadsOnly()
    {
        var (_, ownerId) = await RegisterAsync();
        var now = DateTimeOffset.UtcNow;
        var expiredPasteId = await SeedPasteAsync(ownerId, "expired", now.AddMinutes(-1));
        var expiredThreadId = await SeedThreadAsync(expiredPasteId);
        await SeedShareLinkAsync(expiredPasteId, maxViews: null, expiredThreadId);
        var livePasteId = await SeedPasteAsync(ownerId, "live", now.AddHours(1));
        var expiredFile = await SeedFileAsync(ownerId, now.AddDays(-1), uploaded: true, expiresAt: now.AddMinutes(-1));
        var abandonedUpload = await SeedFileAsync(ownerId, now - FileStorage.UploadLifetime, uploaded: false);
        var pendingUpload = await SeedFileAsync(ownerId, now.AddMinutes(-1), uploaded: false);
        var liveFile = await SeedFileAsync(ownerId, now.AddDays(-1), uploaded: true);

        await using (var cleanupDb = await database.DbFactory.CreateDbContextAsync(Ct))
        {
            await ExpiredContentCleanup.RunAsync(
                cleanupDb, database.Factory.Services.GetRequiredService<FileStorage>(), now, Ct);
        }

        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        Assert.False(await db.Pastes.AnyAsync(paste => paste.Id == expiredPasteId, Ct));
        Assert.False(await db.ShareLinks.AnyAsync(link => link.PasteId == expiredPasteId, Ct));
        Assert.True(await db.Pastes.AnyAsync(paste => paste.Id == livePasteId, Ct));
        foreach (var (file, blob) in new[] { expiredFile, abandonedUpload })
        {
            Assert.False(await db.Files.AnyAsync(f => f.Id == file.Id, Ct));
            Assert.False(await db.ShareLinks.AnyAsync(link => link.FileId == file.Id, Ct));
            Assert.False(await blob.ExistsAsync(Ct));
        }

        foreach (var (file, blob) in new[] { pendingUpload, liveFile })
        {
            Assert.True(await db.Files.AnyAsync(f => f.Id == file.Id, Ct));
            Assert.True(await blob.ExistsAsync(Ct));
        }
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

    private async Task<Guid> SeedPasteAsync(string ownerId, string title, DateTimeOffset? expiresAt = null)
    {
        var paste = new Paste { OwnerId = ownerId, Title = title, CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = expiresAt };

        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        db.Pastes.Add(paste);
        await db.SaveChangesAsync(Ct);
        return paste.Id;
    }

    private async Task<Guid> SeedThreadAsync(Guid pasteId)
    {
        var thread = new PasteThread { PasteId = pasteId, Title = "thread", CreatedAt = DateTimeOffset.UtcNow };

        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        db.Threads.Add(thread);
        await db.SaveChangesAsync(Ct);
        return thread.Id;
    }

    private async Task<(FileItem File, BlobClient Blob)> SeedFileAsync(
        string ownerId,
        DateTimeOffset createdAt,
        bool uploaded,
        DateTimeOffset? expiresAt = null,
        string fileName = "seeded.txt",
        Guid? folderId = null)
    {
        var file = new FileItem
        {
            OwnerId = ownerId,
            FolderId = folderId,
            FileName = fileName,
            ContentType = "text/plain",
            Size = 5,
            CreatedAt = createdAt,
            UploadedAt = uploaded ? createdAt : null,
            ExpiresAt = expiresAt
        };
        await using (var db = await database.DbFactory.CreateDbContextAsync(Ct))
        {
            db.Files.Add(file);
            db.ShareLinks.Add(new ShareLink
            {
                FileId = file.Id,
                TokenHash = ShareToken.Hash(ShareToken.Generate()),
                CreatedAt = createdAt
            });
            await db.SaveChangesAsync(Ct);
        }

        var container = database.Factory.Services.GetRequiredService<BlobServiceClient>()
            .GetBlobContainerClient(FileStorage.ContainerName);
        await container.CreateIfNotExistsAsync(cancellationToken: Ct);
        var blob = container.GetBlobClient(file.BlobName);
        await blob.UploadAsync(BinaryData.FromString("hello"), Ct);
        return (file, blob);
    }

    private async Task<Guid?> GetFolderIdAsync(Guid fileId)
    {
        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        return await db.Files.Where(f => f.Id == fileId).Select(f => f.FolderId).SingleAsync(Ct);
    }

    private async Task<Guid> SeedFolderAsync(string ownerId)
    {
        var folder = new Folder { OwnerId = ownerId, Name = "folder", CreatedAt = DateTimeOffset.UtcNow };

        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        db.Folders.Add(folder);
        await db.SaveChangesAsync(Ct);
        return folder.Id;
    }

    private async Task<string> SeedShareLinkAsync(Guid pasteId, int? maxViews, Guid? threadId = null)
    {
        var token = ShareToken.Generate();

        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        db.ShareLinks.Add(new ShareLink
        {
            PasteId = pasteId,
            ThreadId = threadId,
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
