using Mayfly.Core.Time;
using Microsoft.EntityFrameworkCore;

namespace Mayfly.Core.Data;

public static class FolderTree
{
    public static async Task<List<Folder>> LoadTreeAsync(
        this IQueryable<Folder> folders,
        Guid rootId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var ownerFolders = await folders
            .AsNoTracking()
            .Where(folder => folders.Any(root => root.Id == rootId && root.OwnerId == folder.OwnerId))
            .ToListAsync(cancellationToken);

        return ownerFolders.Live(now).Subtree(rootId);
    }

    public static IQueryable<FileItem> WhereFolderNotExpired(
        this IQueryable<FileItem> files,
        IQueryable<Folder> folders,
        DateTimeOffset now)
    {
        var liveFolders = folders.WhereNotExpired(now);
        return files.Where(file => file.FolderId == null || liveFolders.Any(folder => folder.Id == file.FolderId));
    }

    public static List<Folder> Subtree(this IReadOnlyList<Folder> folders, Guid rootId)
    {
        var subtree = folders.Where(folder => folder.Id == rootId).ToList();
        for (var i = 0; i < subtree.Count; i++)
        {
            subtree.AddRange(folders.Where(folder => folder.ParentId == subtree[i].Id));
        }

        return subtree;
    }

    public static List<Folder> Expired(this IReadOnlyList<Folder> folders, DateTimeOffset now) =>
        folders
            .Where(folder => folder.ExpiresAt <= now)
            .SelectMany(folder => folders.Subtree(folder.Id))
            .DistinctBy(folder => folder.Id)
            .ToList();

    public static List<Folder> Live(this IReadOnlyList<Folder> folders, DateTimeOffset now)
    {
        var expired = folders.Expired(now).Select(folder => folder.Id).ToHashSet();
        return folders.Where(folder => !expired.Contains(folder.Id)).ToList();
    }

    public static List<Folder> Ancestors(this IReadOnlyList<Folder> folders, Folder child)
    {
        var chain = new List<Folder>();
        var parent = folders.SingleOrDefault(folder => folder.Id == child.ParentId);
        while (parent is not null)
        {
            chain.Insert(0, parent);
            parent = folders.SingleOrDefault(folder => folder.Id == parent.ParentId);
        }

        return chain;
    }
}
