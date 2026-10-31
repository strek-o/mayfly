using System.Net;
using System.Net.Http.Json;
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

    private static int clientCount;

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
    public async Task ThreadPage_ForThreadOfAnotherPaste_ReturnsNotFound()
    {
        var (owner, ownerId) = await RegisterAsync();
        var (_, otherId) = await RegisterAsync();
        var ownPasteId = await SeedPasteAsync(ownerId, "own paste");
        var ownThreadId = await SeedThreadAsync(ownPasteId);
        var otherThreadId = await SeedThreadAsync(await SeedPasteAsync(otherId, "not yours"));

        var ownResponse = await owner.GetAsync($"/pastes/{ownPasteId}/threads/{ownThreadId}", Ct);
        var mixedResponse = await owner.GetAsync($"/pastes/{ownPasteId}/threads/{otherThreadId}", Ct);

        Assert.Equal(HttpStatusCode.OK, ownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, mixedResponse.StatusCode);
    }

    [Fact]
    public async Task CreatePaste_WithUndefinedLifetime_ShowsValidationErrorAndSavesNothing()
    {
        var (owner, _) = await RegisterAsync();
        var title = $"invalid-{Guid.NewGuid():N}";
        var antiforgery = await GetAntiforgeryTokenAsync(owner, "/pastes");

        var response = await owner.PostAsync(
            "/pastes",
            Form(antiforgery, ("_handler", "create-paste"), ("PasteInput.Title", title), ("PasteInput.Lifetime", "999")),
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
    public async Task DeleteThread_RemovesItsEntriesAndLinksOnly()
    {
        var (owner, ownerId) = await RegisterAsync();
        var pasteId = await SeedPasteAsync(ownerId, "threads");
        var deletedId = await SeedThreadAsync(pasteId);
        var keptId = await SeedThreadAsync(pasteId);
        await SeedShareLinkAsync(pasteId, maxViews: null, deletedId);
        await SeedShareLinkAsync(pasteId, maxViews: null, keptId);
        await SeedShareLinkAsync(pasteId, maxViews: null);
        await using (var seed = await database.DbFactory.CreateDbContextAsync(Ct))
        {
            seed.Entries.Add(new ThreadEntry { ThreadId = deletedId, Content = "gone", CreatedAt = DateTimeOffset.UtcNow });
            seed.Entries.Add(new ThreadEntry { ThreadId = keptId, Content = "kept", CreatedAt = DateTimeOffset.UtcNow });
            await seed.SaveChangesAsync(Ct);
        }

        var threadUrl = $"/pastes/{pasteId}/threads/{deletedId}";
        var response = await owner.PostAsync(
            threadUrl,
            Form(await GetAntiforgeryTokenAsync(owner, threadUrl), ("_handler", "delete-thread")),
            Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/pastes/{pasteId}", response.Headers.Location?.AbsolutePath);
        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        Assert.Equal([keptId], await db.Threads.Where(thread => thread.PasteId == pasteId).Select(thread => thread.Id).ToListAsync(Ct));
        Assert.Equal(["kept"], await db.Entries.Where(entry => entry.ThreadId == deletedId || entry.ThreadId == keptId).Select(entry => entry.Content).ToListAsync(Ct));
        Assert.Equal(2, await db.ShareLinks.CountAsync(link => link.PasteId == pasteId, Ct));
        Assert.False(await db.ShareLinks.AnyAsync(link => link.ThreadId == deletedId, Ct));
    }

    [Fact]
    public async Task DeleteThread_ByAnotherUser_KeepsThread()
    {
        var (_, ownerId) = await RegisterAsync();
        var (other, _) = await RegisterAsync();
        var pasteId = await SeedPasteAsync(ownerId, "not yours");
        var threadId = await SeedThreadAsync(pasteId);

        var response = await other.PostAsync(
            $"/pastes/{pasteId}/threads/{threadId}",
            Form(await GetAntiforgeryTokenAsync(other, "/pastes"), ("_handler", "delete-thread")),
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        Assert.True(await db.Threads.AnyAsync(thread => thread.Id == threadId, Ct));
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
        Assert.Contains("1 item", desktop);
        Assert.Contains("outside.txt", desktop);
        Assert.DoesNotContain("inside.png", desktop);

        var inFolder = await owner.GetStringAsync($"/desktop/folders/{folderId}", Ct);
        Assert.Contains("inside.png", inFolder);
        Assert.DoesNotContain("outside.txt", inFolder);
    }

    [Fact]
    public async Task CreateFolder_InsideAFolder_NestsIt()
    {
        var (owner, ownerId) = await RegisterAsync();
        var parentId = await SeedFolderAsync(ownerId);
        var parentUrl = $"/desktop/folders/{parentId}";

        var created = await owner.PostAsync(
            parentUrl,
            Form(await GetAntiforgeryTokenAsync(owner, parentUrl), ("_handler", "create-folder"), ("FolderInput.Name", "Nested")),
            Ct);

        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        Assert.Equal(parentUrl, created.Headers.Location?.AbsolutePath);
        Guid childId;
        await using (var db = await database.DbFactory.CreateDbContextAsync(Ct))
        {
            childId = await db.Folders.Where(f => f.OwnerId == ownerId && f.ParentId == parentId && f.Name == "Nested").Select(f => f.Id).SingleAsync(Ct);
        }

        Assert.DoesNotContain("Nested", await owner.GetStringAsync("/desktop", Ct));
        Assert.Contains($"href=\"desktop/folders/{childId}\"", await owner.GetStringAsync(parentUrl, Ct));
        Assert.Contains($"href=\"desktop/folders/{parentId}\"", await owner.GetStringAsync($"/desktop/folders/{childId}", Ct));
    }

    [Fact]
    public async Task CreateFolder_InAnotherUsersFolder_CreatesNothing()
    {
        var (_, ownerId) = await RegisterAsync();
        var (other, _) = await RegisterAsync();
        var folderId = await SeedFolderAsync(ownerId);

        var response = await other.PostAsync(
            $"/desktop/folders/{folderId}",
            Form(await GetAntiforgeryTokenAsync(other, "/desktop"), ("_handler", "create-folder"), ("FolderInput.Name", "Intruder")),
            Ct);

        Assert.False(response.IsSuccessStatusCode);
        Assert.NotEqual(HttpStatusCode.Redirect, response.StatusCode);
        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        Assert.False(await db.Folders.AnyAsync(f => f.Name == "Intruder", Ct));
    }

    [Fact]
    public async Task FolderEndpoint_CreatesFoldersOnlyInOwnFolders()
    {
        var (owner, ownerId) = await RegisterAsync();
        var (_, otherId) = await RegisterAsync();
        var foreignId = await SeedFolderAsync(otherId);
        var antiforgery = await GetAntiforgeryTokenAsync(owner, "/desktop");

        var top = await owner.PostAsync("/folders", Form(antiforgery, ("Name", " Photos ")), Ct);
        var topId = (await top.Content.ReadFromJsonAsync<FolderCreateEndpoint.FolderCreated>(Ct))!.Id;
        var nested = await owner.PostAsync("/folders", Form(antiforgery, ("Name", "2026"), ("ParentId", topId.ToString())), Ct);
        var foreign = await owner.PostAsync("/folders", Form(antiforgery, ("Name", "Intruder"), ("ParentId", foreignId.ToString())), Ct);
        var blank = await owner.PostAsync("/folders", Form(antiforgery, ("Name", "  ")), Ct);

        Assert.Equal(HttpStatusCode.OK, top.StatusCode);
        Assert.Equal(HttpStatusCode.OK, nested.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        Assert.Equal("Photos", await db.Folders.Where(f => f.Id == topId && f.OwnerId == ownerId && f.ParentId == null).Select(f => f.Name).SingleAsync(Ct));
        Assert.True(await db.Folders.AnyAsync(f => f.OwnerId == ownerId && f.ParentId == topId && f.Name == "2026", Ct));
        Assert.False(await db.Folders.AnyAsync(f => f.Name == "Intruder", Ct));
        Assert.Equal(2, await db.Folders.CountAsync(f => f.OwnerId == ownerId, Ct));
    }

    [Fact]
    public async Task FolderEndpoint_GivesFoldersTheChosenLifetime()
    {
        var (owner, ownerId) = await RegisterAsync();
        var expiredId = await SeedFolderAsync(ownerId, expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        var antiforgery = await GetAntiforgeryTokenAsync(owner, "/desktop");
        var before = DateTimeOffset.UtcNow;

        var expiring = await owner.PostAsync("/folders", Form(antiforgery, ("Name", "Temp"), ("Lifetime", "OneHour")), Ct);
        var underExpired = await owner.PostAsync(
            "/folders",
            Form(antiforgery, ("Name", "Late"), ("ParentId", expiredId.ToString())),
            Ct);

        Assert.Equal(HttpStatusCode.OK, expiring.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, underExpired.StatusCode);
        var expiringId = (await expiring.Content.ReadFromJsonAsync<FolderCreateEndpoint.FolderCreated>(Ct))!.Id;
        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        var expiresAt = await db.Folders.Where(f => f.Id == expiringId).Select(f => f.ExpiresAt).SingleAsync(Ct);
        Assert.InRange(expiresAt!.Value, before.AddHours(1), DateTimeOffset.UtcNow.AddHours(1));
        Assert.False(await db.Folders.AnyAsync(f => f.OwnerId == ownerId && f.Name == "Late", Ct));
    }

    [Fact]
    public async Task NewFolders_NeverOutliveTheirParent()
    {
        var (owner, ownerId) = await RegisterAsync();
        var now = DateTimeOffset.UtcNow;
        var hourParentId = await SeedFolderAsync(ownerId, expiresAt: now.AddHours(1));
        var weekParentId = await SeedFolderAsync(ownerId, expiresAt: now.AddDays(7));
        var antiforgery = await GetAntiforgeryTokenAsync(owner, "/desktop");
        var parentUrl = $"/desktop/folders/{hourParentId}";

        await owner.PostAsync("/folders", Form(antiforgery, ("Name", "Uploaded"), ("ParentId", hourParentId.ToString())), Ct);
        await owner.PostAsync(
            "/folders",
            Form(antiforgery, ("Name", "Shorter"), ("Lifetime", "OneHour"), ("ParentId", weekParentId.ToString())),
            Ct);
        await owner.PostAsync(
            parentUrl,
            Form(await GetAntiforgeryTokenAsync(owner, parentUrl), ("_handler", "create-folder"), ("FolderInput.Name", "Manual")),
            Ct);

        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        var parentExpiresAt = await db.Folders.Where(f => f.Id == hourParentId).Select(f => f.ExpiresAt).SingleAsync(Ct);
        var expiry = await db.Folders
            .Where(f => f.OwnerId == ownerId && f.ParentId != null)
            .ToDictionaryAsync(f => f.Name, f => f.ExpiresAt, Ct);
        Assert.Equal(parentExpiresAt, expiry["Uploaded"]);
        Assert.Equal(parentExpiresAt, expiry["Manual"]);
        Assert.InRange(expiry["Shorter"]!.Value, now.AddHours(1), DateTimeOffset.UtcNow.AddHours(1));
    }

    [Fact]
    public async Task FilesInExpiredFolders_StopCountingAndCannotBeDownloaded()
    {
        var (owner, ownerId) = await RegisterAsync();
        var now = DateTimeOffset.UtcNow;
        var expiredId = await SeedFolderAsync(ownerId, expiresAt: now.AddMinutes(-1));
        var (inside, _) = await SeedFileAsync(ownerId, now, uploaded: true, folderId: expiredId);
        await SeedFileAsync(ownerId, now, uploaded: true);
        var token = ShareToken.Generate();
        await using (var db = await database.DbFactory.CreateDbContextAsync(Ct))
        {
            db.ShareLinks.Add(new ShareLink { FileId = inside.Id, TokenHash = ShareToken.Hash(token), CreatedAt = now });
            await db.SaveChangesAsync(Ct);
            Assert.Equal(5, await db.GetUsedBytesAsync(ownerId, DateTimeOffset.UtcNow, Ct));
        }

        Assert.Equal(HttpStatusCode.NotFound, (await CreateClient().GetAsync($"/s/{token}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/files/{inside.Id}/download", Ct)).StatusCode);
    }

    [Fact]
    public async Task DesktopLink_StopsServingAFolderThatExpiresDuringTheVisit()
    {
        var (_, ownerId) = await RegisterAsync();
        var now = DateTimeOffset.UtcNow;
        var folderId = await SeedFolderAsync(ownerId, expiresAt: now.AddHours(1));
        var (inside, _) = await SeedFileAsync(ownerId, now, uploaded: true, folderId: folderId);
        var token = await SeedDesktopLinkAsync(ownerId);
        var visitor = CreateClient();
        await visitor.PostAsync(
            $"/s/{token}",
            Form(await GetAntiforgeryTokenAsync(visitor, $"/s/{token}"), ("_handler", "reveal")),
            Ct);
        var window = await visitor.GetStringAsync($"/s/{token}?folder={folderId}&file={inside.Id}", Ct);
        var antiforgery = AntiforgeryField.Match(window).Groups[1].Value;
        await using (var db = await database.DbFactory.CreateDbContextAsync(Ct))
        {
            await db.Folders.Where(f => f.Id == folderId).ExecuteUpdateAsync(set => set.SetProperty(f => f.ExpiresAt, now.AddMinutes(-1)), Ct);
        }

        var download = await visitor.PostAsync($"/s/{token}/download", Form(antiforgery, ("FileId", inside.Id.ToString())), Ct);

        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
    }

    [Fact]
    public async Task ExpiredFolder_HidesItsContentsUntilCleanupDeletesThem()
    {
        var (owner, ownerId) = await RegisterAsync();
        var now = DateTimeOffset.UtcNow;
        var expiredId = await SeedFolderAsync(ownerId, expiresAt: now.AddMinutes(-1));
        var nestedId = await SeedFolderAsync(ownerId, expiredId);
        var liveId = await SeedFolderAsync(ownerId, expiresAt: now.AddHours(1));
        var (nested, nestedBlob) = await SeedFileAsync(ownerId, now, uploaded: true, folderId: nestedId);
        var (kept, keptBlob) = await SeedFileAsync(ownerId, now, uploaded: true, folderId: liveId);
        var expiredToken = await SeedFolderLinkAsync(expiredId);
        var nestedToken = await SeedFolderLinkAsync(nestedId);

        var desktop = await owner.GetStringAsync("/desktop", Ct);
        Assert.DoesNotContain($"desktop/folders/{expiredId}", desktop);
        Assert.Contains($"desktop/folders/{liveId}", desktop);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/desktop/folders/{expiredId}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/desktop/folders/{nestedId}", Ct)).StatusCode);

        var visitor = CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/s/{expiredToken}", Ct)).StatusCode);
        await visitor.PostAsync(
            $"/s/{nestedToken}",
            Form(await GetAntiforgeryTokenAsync(visitor, $"/s/{nestedToken}"), ("_handler", "reveal")),
            Ct);
        var nestedView = await visitor.GetAsync($"/s/{nestedToken}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, nestedView.StatusCode);
        Assert.DoesNotContain(nested.FileName, await nestedView.Content.ReadAsStringAsync(Ct));

        await using (var cleanupDb = await database.DbFactory.CreateDbContextAsync(Ct))
        {
            await ExpiredContentCleanup.RunAsync(
                cleanupDb, database.Factory.Services.GetRequiredService<FileStorage>(), now, Ct);
        }

        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        Assert.False(await db.Folders.AnyAsync(f => f.Id == expiredId || f.Id == nestedId, Ct));
        Assert.False(await db.Files.AnyAsync(f => f.Id == nested.Id, Ct));
        Assert.False(await nestedBlob.ExistsAsync(Ct));
        Assert.False(await db.ShareLinks.AnyAsync(link => link.FolderId == expiredId || link.FolderId == nestedId, Ct));
        Assert.True(await db.Folders.AnyAsync(f => f.Id == liveId, Ct));
        Assert.True(await db.Files.AnyAsync(f => f.Id == kept.Id, Ct));
        Assert.True(await keptBlob.ExistsAsync(Ct));
    }

    [Fact]
    public async Task DeleteFolder_AlsoRemovesExpiredSubfolders()
    {
        var (owner, ownerId) = await RegisterAsync();
        var now = DateTimeOffset.UtcNow;
        var parentId = await SeedFolderAsync(ownerId);
        var expiredChildId = await SeedFolderAsync(ownerId, parentId, now.AddMinutes(-1));
        var (file, blob) = await SeedFileAsync(ownerId, now, uploaded: true, folderId: expiredChildId);
        var url = $"/desktop/folders/{parentId}";

        var response = await owner.PostAsync(
            url,
            Form(await GetAntiforgeryTokenAsync(owner, url), ("_handler", "delete-current-folder")),
            Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(await blob.ExistsAsync(Ct));
        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        Assert.False(await db.Folders.AnyAsync(f => f.Id == parentId || f.Id == expiredChildId, Ct));
        Assert.False(await db.Files.AnyAsync(f => f.Id == file.Id, Ct));
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
    public async Task DeleteFolder_RemovesItsFilesBlobsAndLinksOnly()
    {
        var (owner, ownerId) = await RegisterAsync();
        var folderId = await SeedFolderAsync(ownerId);
        var now = DateTimeOffset.UtcNow;
        var (inside, insideBlob) = await SeedFileAsync(ownerId, now, uploaded: true, folderId: folderId);
        var (outside, outsideBlob) = await SeedFileAsync(ownerId, now, uploaded: true);
        await SeedFolderLinkAsync(folderId);
        var url = $"/desktop/folders/{folderId}";

        var response = await owner.PostAsync(
            url,
            Form(await GetAntiforgeryTokenAsync(owner, url), ("_handler", "delete-current-folder")),
            Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(await insideBlob.ExistsAsync(Ct));
        Assert.True(await outsideBlob.ExistsAsync(Ct));
        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        Assert.False(await db.Folders.AnyAsync(f => f.Id == folderId, Ct));
        Assert.False(await db.ShareLinks.AnyAsync(link => link.FolderId == folderId, Ct));
        Assert.False(await db.Files.AnyAsync(f => f.Id == inside.Id, Ct));
        Assert.False(await db.ShareLinks.AnyAsync(link => link.FileId == inside.Id, Ct));
        Assert.True(await db.Files.AnyAsync(f => f.Id == outside.Id, Ct));
        Assert.True(await db.ShareLinks.AnyAsync(link => link.FileId == outside.Id, Ct));
    }

    [Fact]
    public async Task DeleteFolder_RemovesNestedFoldersAndReturnsToTheParent()
    {
        var (owner, ownerId) = await RegisterAsync();
        var topId = await SeedFolderAsync(ownerId);
        var middleId = await SeedFolderAsync(ownerId, topId);
        var bottomId = await SeedFolderAsync(ownerId, middleId);
        var siblingId = await SeedFolderAsync(ownerId, topId);
        var now = DateTimeOffset.UtcNow;
        var (deep, deepBlob) = await SeedFileAsync(ownerId, now, uploaded: true, folderId: bottomId);
        var (kept, keptBlob) = await SeedFileAsync(ownerId, now, uploaded: true, folderId: siblingId);
        await SeedFolderLinkAsync(bottomId);
        await SeedFolderLinkAsync(siblingId);
        var url = $"/desktop/folders/{middleId}";

        var response = await owner.PostAsync(
            url,
            Form(await GetAntiforgeryTokenAsync(owner, url), ("_handler", "delete-current-folder")),
            Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/desktop/folders/{topId}", response.Headers.Location?.AbsolutePath);
        Assert.False(await deepBlob.ExistsAsync(Ct));
        Assert.True(await keptBlob.ExistsAsync(Ct));
        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        Assert.False(await db.Folders.AnyAsync(f => f.Id == middleId || f.Id == bottomId, Ct));
        Assert.False(await db.Files.AnyAsync(f => f.Id == deep.Id, Ct));
        Assert.False(await db.ShareLinks.AnyAsync(link => link.FolderId == bottomId || link.FileId == deep.Id, Ct));
        Assert.True(await db.Folders.AnyAsync(f => f.Id == topId, Ct));
        Assert.True(await db.Folders.AnyAsync(f => f.Id == siblingId, Ct));
        Assert.True(await db.Files.AnyAsync(f => f.Id == kept.Id, Ct));
        Assert.True(await db.ShareLinks.AnyAsync(link => link.FolderId == siblingId, Ct));
    }

    [Fact]
    public async Task DeleteFolder_ByAnotherUser_KeepsFolder()
    {
        var (_, ownerId) = await RegisterAsync();
        var (other, _) = await RegisterAsync();
        var folderId = await SeedFolderAsync(ownerId);

        var response = await other.PostAsync(
            $"/desktop/folders/{folderId}",
            Form(await GetAntiforgeryTokenAsync(other, "/desktop"), ("_handler", "delete-current-folder")),
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        Assert.True(await db.Folders.AnyAsync(f => f.Id == folderId, Ct));
    }

    [Fact]
    public async Task DesktopMenu_DeletesOnlyOwnFolder()
    {
        var (owner, ownerId) = await RegisterAsync();
        var (other, _) = await RegisterAsync();
        var deletedId = await SeedFolderAsync(ownerId);
        var keptId = await SeedFolderAsync(ownerId);
        await SeedFolderLinkAsync(keptId);

        var ownResponse = await owner.PostAsync(
            "/desktop",
            Form(await GetAntiforgeryTokenAsync(owner, "/desktop"), ("_handler", "delete-folder"), ("DeleteFolderId", deletedId.ToString())),
            Ct);
        var foreignResponse = await other.PostAsync(
            "/desktop",
            Form(await GetAntiforgeryTokenAsync(other, "/desktop"), ("_handler", "delete-folder"), ("DeleteFolderId", keptId.ToString())),
            Ct);

        Assert.Equal(HttpStatusCode.Redirect, ownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);
        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        Assert.False(await db.Folders.AnyAsync(f => f.Id == deletedId, Ct));
        Assert.True(await db.Folders.AnyAsync(f => f.Id == keptId, Ct));
        Assert.True(await db.ShareLinks.AnyAsync(link => link.FolderId == keptId, Ct));
    }

    [Fact]
    public async Task DesktopMenu_DeletesOnlyOwnFile()
    {
        var (owner, ownerId) = await RegisterAsync();
        var (other, _) = await RegisterAsync();
        var now = DateTimeOffset.UtcNow;
        var (deleted, deletedBlob) = await SeedFileAsync(ownerId, now, uploaded: true);
        var (kept, keptBlob) = await SeedFileAsync(ownerId, now, uploaded: true);

        var ownResponse = await owner.PostAsync(
            "/desktop",
            Form(await GetAntiforgeryTokenAsync(owner, "/desktop"), ("_handler", "delete-desktop-file"), ("DeleteFileId", deleted.Id.ToString())),
            Ct);
        var foreignResponse = await other.PostAsync(
            "/desktop",
            Form(await GetAntiforgeryTokenAsync(other, "/desktop"), ("_handler", "delete-desktop-file"), ("DeleteFileId", kept.Id.ToString())),
            Ct);

        Assert.Equal(HttpStatusCode.Redirect, ownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);
        Assert.False(await deletedBlob.ExistsAsync(Ct));
        Assert.True(await keptBlob.ExistsAsync(Ct));
        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        Assert.False(await db.Files.AnyAsync(f => f.Id == deleted.Id, Ct));
        Assert.True(await db.Files.AnyAsync(f => f.Id == kept.Id, Ct));
        Assert.True(await db.ShareLinks.AnyAsync(link => link.FileId == kept.Id, Ct));
    }

    [Fact]
    public async Task FolderWindow_CreatesFolderLink()
    {
        var (owner, ownerId) = await RegisterAsync();
        var folderId = await SeedFolderAsync(ownerId);
        var url = $"/desktop/folders/{folderId}?share=true";

        var response = await owner.PostAsync(
            url,
            Form(await GetAntiforgeryTokenAsync(owner, url),
                ("_handler", "create-share-link"),
                ("LinkInput.Lifetime", "OneDay")),
            Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        Assert.True(await db.ShareLinks.AnyAsync(link => link.FolderId == folderId && link.FileId == null && link.PasteId == null, Ct));
    }

    [Fact]
    public async Task FolderLink_ShowsAndServesOnlyFilesInTheFolder()
    {
        var (_, ownerId) = await RegisterAsync();
        var folderId = await SeedFolderAsync(ownerId);
        var now = DateTimeOffset.UtcNow;
        var (inside, _) = await SeedFileAsync(ownerId, now, uploaded: true, fileName: "inside.txt", folderId: folderId);
        var (outside, _) = await SeedFileAsync(ownerId, now, uploaded: true, fileName: "outside.txt");
        var token = await SeedFolderLinkAsync(folderId);
        var visitor = CreateClient();

        var revealed = await visitor.PostAsync(
            $"/s/{token}",
            Form(await GetAntiforgeryTokenAsync(visitor, $"/s/{token}"), ("_handler", "reveal")),
            Ct);
        Assert.Equal(HttpStatusCode.Redirect, revealed.StatusCode);

        var page = await visitor.GetStringAsync($"/s/{token}", Ct);
        Assert.Contains("inside.txt", page);
        Assert.DoesNotContain("outside.txt", page);

        var antiforgery = AntiforgeryField.Match(page).Groups[1].Value;
        var insideDownload = await visitor.PostAsync($"/s/{token}/download", Form(antiforgery, ("FileId", inside.Id.ToString())), Ct);
        var outsideDownload = await visitor.PostAsync($"/s/{token}/download", Form(antiforgery, ("FileId", outside.Id.ToString())), Ct);

        Assert.Equal(HttpStatusCode.Redirect, insideDownload.StatusCode);
        Assert.Contains(inside.BlobName, insideDownload.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.NotFound, outsideDownload.StatusCode);
    }

    [Fact]
    public async Task FolderLink_OpensSubfoldersOnlyInsideTheSharedFolder()
    {
        var (_, ownerId) = await RegisterAsync();
        var sharedId = await SeedFolderAsync(ownerId);
        var childId = await SeedFolderAsync(ownerId, sharedId);
        var asideId = await SeedFolderAsync(ownerId);
        var now = DateTimeOffset.UtcNow;
        var (nested, _) = await SeedFileAsync(ownerId, now, uploaded: true, fileName: "nested.txt", folderId: childId);
        var (aside, _) = await SeedFileAsync(ownerId, now, uploaded: true, fileName: "aside.txt", folderId: asideId);
        var token = await SeedFolderLinkAsync(sharedId);
        var visitor = CreateClient();
        await visitor.PostAsync(
            $"/s/{token}",
            Form(await GetAntiforgeryTokenAsync(visitor, $"/s/{token}"), ("_handler", "reveal")),
            Ct);

        var root = await visitor.GetStringAsync($"/s/{token}", Ct);
        Assert.Contains($"?folder={childId}", root);
        Assert.DoesNotContain("nested.txt", root);

        var child = await visitor.GetStringAsync($"/s/{token}?folder={childId}", Ct);
        Assert.Contains("nested.txt", child);
        Assert.Contains($"?folder={sharedId}", child);

        var outside = await visitor.GetStringAsync($"/s/{token}?folder={asideId}", Ct);
        Assert.DoesNotContain("aside.txt", outside);
        Assert.Contains($"?folder={childId}", outside);

        var antiforgery = AntiforgeryField.Match(child).Groups[1].Value;
        var nestedDownload = await visitor.PostAsync($"/s/{token}/download", Form(antiforgery, ("FileId", nested.Id.ToString())), Ct);
        var asideDownload = await visitor.PostAsync($"/s/{token}/download", Form(antiforgery, ("FileId", aside.Id.ToString())), Ct);

        Assert.Equal(HttpStatusCode.Redirect, nestedDownload.StatusCode);
        Assert.Contains(nested.BlobName, nestedDownload.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.NotFound, asideDownload.StatusCode);
    }

    [Fact]
    public async Task DesktopWindow_CreatesALinkOnlyTheOwnerCanRevoke()
    {
        var (owner, ownerId) = await RegisterAsync();
        var (other, _) = await RegisterAsync();
        const string url = "/desktop?share=true";

        var created = await owner.PostAsync(
            url,
            Form(await GetAntiforgeryTokenAsync(owner, url), ("_handler", "create-share-link"), ("LinkInput.Lifetime", "OneDay")),
            Ct);

        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        Guid linkId;
        await using (var db = await database.DbFactory.CreateDbContextAsync(Ct))
        {
            linkId = await db.ShareLinks
                .Where(link => link.DesktopOwnerId == ownerId && link.PasteId == null && link.FileId == null && link.FolderId == null)
                .Select(link => link.Id)
                .SingleAsync(Ct);
        }

        await other.PostAsync(
            url,
            Form(await GetAntiforgeryTokenAsync(other, url), ("_handler", "revoke-share-link"), ("RevokeId", linkId.ToString())),
            Ct);
        await using (var db = await database.DbFactory.CreateDbContextAsync(Ct))
        {
            Assert.True(await db.ShareLinks.AnyAsync(link => link.Id == linkId && link.RevokedAt == null, Ct));
        }

        await owner.PostAsync(
            url,
            Form(await GetAntiforgeryTokenAsync(owner, url), ("_handler", "revoke-share-link"), ("RevokeId", linkId.ToString())),
            Ct);
        await using (var db = await database.DbFactory.CreateDbContextAsync(Ct))
        {
            Assert.True(await db.ShareLinks.AnyAsync(link => link.Id == linkId && link.RevokedAt != null, Ct));
        }
    }

    [Fact]
    public async Task DesktopLink_ShowsOnlyTheOwnersDesktopInTheirLayout()
    {
        var (_, ownerId) = await RegisterAsync();
        var (_, otherId) = await RegisterAsync();
        var folderId = await SeedFolderAsync(ownerId);
        var foreignFolderId = await SeedFolderAsync(otherId);
        var now = DateTimeOffset.UtcNow;
        var (onDesktop, _) = await SeedFileAsync(ownerId, now, uploaded: true, fileName: "on-desktop.txt");
        var (inFolder, _) = await SeedFileAsync(ownerId, now, uploaded: true, fileName: "in-folder.txt", folderId: folderId);
        var (foreign, _) = await SeedFileAsync(otherId, now, uploaded: true, fileName: "someone-elses.txt");
        await using (var db = await database.DbFactory.CreateDbContextAsync(Ct))
        {
            await db.Files.Where(f => f.Id == onDesktop.Id).ExecuteUpdateAsync(set => set.SetProperty(f => f.GridColumn, 3).SetProperty(f => f.GridRow, 2), Ct);
        }

        var token = await SeedDesktopLinkAsync(ownerId);
        var visitor = CreateClient();
        await visitor.PostAsync(
            $"/s/{token}",
            Form(await GetAntiforgeryTokenAsync(visitor, $"/s/{token}"), ("_handler", "reveal")),
            Ct);

        var desktop = await visitor.GetStringAsync($"/s/{token}", Ct);
        Assert.Contains("on-desktop.txt", desktop);
        Assert.Contains("grid-area: 2 / 3", desktop);
        Assert.Contains($"?folder={folderId}", desktop);
        Assert.DoesNotContain("in-folder.txt", desktop);
        Assert.DoesNotContain("someone-elses.txt", desktop);
        Assert.DoesNotContain("data-file-id", desktop);

        var inside = await visitor.GetStringAsync($"/s/{token}?folder={folderId}", Ct);
        Assert.Contains("in-folder.txt", inside);

        var forged = await visitor.GetStringAsync($"/s/{token}?folder={foreignFolderId}&file={foreign.Id}", Ct);
        Assert.Contains("on-desktop.txt", forged);
        Assert.DoesNotContain("someone-elses.txt", forged);

        var window = await visitor.GetStringAsync($"/s/{token}?folder={folderId}&file={inFolder.Id}", Ct);
        Assert.Contains($"name=\"FileId\" value=\"{inFolder.Id}\"", window);

        var antiforgery = AntiforgeryField.Match(window).Groups[1].Value;
        var ownDownload = await visitor.PostAsync($"/s/{token}/download", Form(antiforgery, ("FileId", inFolder.Id.ToString())), Ct);
        var foreignDownload = await visitor.PostAsync($"/s/{token}/download", Form(antiforgery, ("FileId", foreign.Id.ToString())), Ct);

        Assert.Equal(HttpStatusCode.Redirect, ownDownload.StatusCode);
        Assert.Contains(inFolder.BlobName, ownDownload.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.NotFound, foreignDownload.StatusCode);
    }

    [Fact]
    public async Task GuestEntry_LandsInTheChosenThread()
    {
        var (_, ownerId) = await RegisterAsync();
        var pasteId = await SeedPasteAsync(ownerId, "open notes");
        await SeedThreadAsync(pasteId);
        var secondId = await SeedThreadAsync(pasteId);
        var token = await SeedShareLinkAsync(pasteId, maxViews: null, canAppend: true);
        var visitor = CreateClient();
        await visitor.PostAsync(
            $"/s/{token}",
            Form(await GetAntiforgeryTokenAsync(visitor, $"/s/{token}"), ("_handler", "reveal")),
            Ct);
        var secondThreadUrl = $"/s/{token}?thread={secondId}";

        var response = await visitor.PostAsync(
            $"/s/{token}/entries",
            Form(
                await GetAntiforgeryTokenAsync(visitor, secondThreadUrl),
                ("ThreadId", secondId.ToString()),
                ("Content", "**hello** from a guest"),
                ("AuthorName", "Marta")),
            Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith($"{secondThreadUrl}#entry-", response.Headers.Location?.OriginalString);
        var secondThread = await visitor.GetStringAsync(secondThreadUrl, Ct);
        Assert.Contains("<strong>hello</strong> from a guest", secondThread);
        Assert.Contains("Marta", secondThread);
        Assert.DoesNotContain("from a guest", await visitor.GetStringAsync($"/s/{token}", Ct));
    }

    [Fact]
    public async Task DesktopLayout_SavesOnlyOwnItemsAndPlacesThem()
    {
        var (owner, ownerId) = await RegisterAsync();
        var (_, otherId) = await RegisterAsync();
        var folderId = await SeedFolderAsync(ownerId);
        var now = DateTimeOffset.UtcNow;
        var (file, _) = await SeedFileAsync(ownerId, now, uploaded: true);
        var (foreign, _) = await SeedFileAsync(otherId, now, uploaded: true);
        var antiforgery = await GetAntiforgeryTokenAsync(owner, "/desktop");

        var saved = await owner.PostAsync(
            "/desktop/layout",
            Form(antiforgery,
                ("Items[0].Id", file.Id.ToString()), ("Items[0].Column", "3"), ("Items[0].Row", "2"),
                ("Items[1].Id", folderId.ToString()), ("Items[1].Column", "1"), ("Items[1].Row", "4"),
                ("Items[2].Id", foreign.Id.ToString()), ("Items[2].Column", "5"), ("Items[2].Row", "5")),
            Ct);
        var outOfRange = await owner.PostAsync(
            "/desktop/layout",
            Form(antiforgery, ("Items[0].Id", file.Id.ToString()), ("Items[0].Column", "0"), ("Items[0].Row", "1")),
            Ct);

        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, outOfRange.StatusCode);
        await using (var db = await database.DbFactory.CreateDbContextAsync(Ct))
        {
            Assert.True(await db.Files.AnyAsync(f => f.Id == file.Id && f.GridColumn == 3 && f.GridRow == 2, Ct));
            Assert.True(await db.Folders.AnyAsync(f => f.Id == folderId && f.GridColumn == 1 && f.GridRow == 4, Ct));
            Assert.True(await db.Files.AnyAsync(f => f.Id == foreign.Id && f.GridColumn == null && f.GridRow == null, Ct));
        }

        var desktop = await owner.GetStringAsync("/desktop", Ct);
        Assert.Contains("grid-area: 2 / 3", desktop);
        Assert.Contains("grid-area: 4 / 1", desktop);
    }

    [Fact]
    public async Task MoveFile_IntoOwnFolderAndBackToTheDesktop()
    {
        var (owner, ownerId) = await RegisterAsync();
        var folderId = await SeedFolderAsync(ownerId);
        var (file, _) = await SeedFileAsync(ownerId, DateTimeOffset.UtcNow, uploaded: true);
        var antiforgery = await GetAntiforgeryTokenAsync(owner, "/desktop");
        await using (var db = await database.DbFactory.CreateDbContextAsync(Ct))
        {
            await db.Files.Where(f => f.Id == file.Id).ExecuteUpdateAsync(set => set.SetProperty(f => f.GridColumn, 2).SetProperty(f => f.GridRow, 3), Ct);
        }

        var intoFolder = await owner.PostAsync($"/files/{file.Id}/move", Form(antiforgery, ("FolderId", folderId.ToString())), Ct);
        Assert.Equal(HttpStatusCode.NoContent, intoFolder.StatusCode);
        Assert.Equal(folderId, await GetFolderIdAsync(file.Id));
        await using (var db = await database.DbFactory.CreateDbContextAsync(Ct))
        {
            Assert.False(await db.Files.AnyAsync(f => f.Id == file.Id && (f.GridColumn != null || f.GridRow != null), Ct));
        }

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

    private HttpClient CreateClient()
    {
        var client = database.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var number = Interlocked.Increment(ref clientCount);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"198.18.{number / 256}.{number % 256}");
        return client;
    }

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

    private async Task<Guid> SeedFolderAsync(string ownerId, Guid? parentId = null, DateTimeOffset? expiresAt = null)
    {
        var folder = new Folder { OwnerId = ownerId, ParentId = parentId, Name = "folder", CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = expiresAt };

        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        db.Folders.Add(folder);
        await db.SaveChangesAsync(Ct);
        return folder.Id;
    }

    private async Task<string> SeedDesktopLinkAsync(string ownerId)
    {
        var token = ShareToken.Generate();

        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        db.ShareLinks.Add(new ShareLink
        {
            DesktopOwnerId = ownerId,
            TokenHash = ShareToken.Hash(token),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(Ct);
        return token;
    }

    private async Task<string> SeedFolderLinkAsync(Guid folderId)
    {
        var token = ShareToken.Generate();

        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        db.ShareLinks.Add(new ShareLink
        {
            FolderId = folderId,
            TokenHash = ShareToken.Hash(token),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(Ct);
        return token;
    }

    private async Task<string> SeedShareLinkAsync(Guid pasteId, int? maxViews, Guid? threadId = null, bool canAppend = false)
    {
        var token = ShareToken.Generate();

        await using var db = await database.DbFactory.CreateDbContextAsync(Ct);
        db.ShareLinks.Add(new ShareLink
        {
            PasteId = pasteId,
            ThreadId = threadId,
            TokenHash = ShareToken.Hash(token),
            CanAppend = canAppend,
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
