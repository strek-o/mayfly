using Mayfly.Core.Data;
using Mayfly.Core.Time;
using Microsoft.EntityFrameworkCore;

namespace Mayfly.Web.Files;

public sealed record DesktopContent(
    List<Folder> AllFolders,
    Folder? Current,
    List<(Folder Folder, int Count)> Folders,
    List<FileItem> Files)
{
    public static async Task<DesktopContent> LoadAsync(MayflyDbContext db, string ownerId, Guid? folderId, DateTimeOffset now)
    {
        var allFolders = await db.Folders
            .AsNoTracking()
            .Where(f => f.OwnerId == ownerId)
            .OrderBy(f => f.Name)
            .ToListAsync();
        var current = allFolders.Find(f => f.Id == folderId);
        var currentId = current?.Id;

        var fileCounts = await db.Files
            .Where(file => file.OwnerId == ownerId && file.UploadedAt != null && file.FolderId != null)
            .WhereNotExpired(now)
            .GroupBy(file => file.FolderId!.Value)
            .Select(group => new { FolderId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.FolderId, item => item.Count);

        var folders = allFolders
            .Where(f => f.ParentId == currentId)
            .Select(f => (f, fileCounts.GetValueOrDefault(f.Id) + allFolders.Count(child => child.ParentId == f.Id)))
            .ToList();

        var files = await db.Files
            .AsNoTracking()
            .Where(file => file.OwnerId == ownerId && file.UploadedAt != null && file.FolderId == currentId)
            .WhereNotExpired(now)
            .OrderBy(file => file.ExpiresAt == null)
            .ThenBy(file => file.ExpiresAt)
            .ThenByDescending(file => file.UploadedAt)
            .ToListAsync();

        return new DesktopContent(allFolders, current, folders, files);
    }
}
