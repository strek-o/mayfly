using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Mayfly.Web.Data;

public class MayflyDbContext(DbContextOptions<MayflyDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IDataProtectionKeyContext
{
    public DbSet<Paste> Pastes => Set<Paste>();

    public DbSet<PasteThread> Threads => Set<PasteThread>();

    public DbSet<ThreadEntry> Entries => Set<ThreadEntry>();

    public DbSet<ShareLink> ShareLinks => Set<ShareLink>();

    public DbSet<FileItem> Files => Set<FileItem>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Paste>(paste =>
        {
            paste.Property(p => p.Title).HasMaxLength(200);
            paste.HasIndex(p => p.ExpiresAt);
            paste.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.OwnerId);
            paste.HasMany(p => p.Threads).WithOne().HasForeignKey(t => t.PasteId);
        });

        modelBuilder.Entity<PasteThread>(thread =>
        {
            thread.Property(t => t.Title).HasMaxLength(200);
            thread.HasMany(t => t.Entries).WithOne().HasForeignKey(e => e.ThreadId);
        });

        modelBuilder.Entity<ThreadEntry>(entry =>
        {
            entry.Property(e => e.AuthorName).HasMaxLength(50);
        });

        modelBuilder.Entity<ShareLink>(link =>
        {
            link.Property(l => l.TokenHash).HasMaxLength(32).IsFixedLength();
            link.HasIndex(l => l.TokenHash).IsUnique();
            link.HasOne<Paste>().WithMany().HasForeignKey(l => l.PasteId);
            link.HasOne<PasteThread>().WithMany().HasForeignKey(l => l.ThreadId).OnDelete(DeleteBehavior.NoAction);
            link.ToTable(table => table.HasCheckConstraint("CK_ShareLinks_MaxViews", "[MaxViews] IS NULL OR [MaxViews] > 0"));
        });

        modelBuilder.Entity<FileItem>(file =>
        {
            file.Property(f => f.FileName).HasMaxLength(255);
            file.Property(f => f.ContentType).HasMaxLength(255);
            file.HasIndex(f => f.ExpiresAt);
            file.HasOne<ApplicationUser>().WithMany().HasForeignKey(f => f.OwnerId);
            file.ToTable(table => table.HasCheckConstraint("CK_Files_Size", "[Size] >= 0"));
        });
    }
}
