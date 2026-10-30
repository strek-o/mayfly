using Microsoft.EntityFrameworkCore;

namespace Mayfly.Core.Data;

public static class FolderTree
{
    public static async Task<List<Folder>> LoadTreeAsync(
        this IQueryable<Folder> folders,
        Guid rootId,
        CancellationToken cancellationToken = default)
    {
        var ownerFolders = await folders
            .AsNoTracking()
            .Where(folder => folders.Any(root => root.Id == rootId && root.OwnerId == folder.OwnerId))
            .ToListAsync(cancellationToken);

        return ownerFolders.Subtree(rootId);
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
