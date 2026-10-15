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
    }
}
