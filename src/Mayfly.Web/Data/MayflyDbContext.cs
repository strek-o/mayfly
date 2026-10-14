using Microsoft.EntityFrameworkCore;

namespace Mayfly.Web.Data;

public class MayflyDbContext(DbContextOptions<MayflyDbContext> options) : DbContext(options)
{
    public DbSet<Paste> Pastes => Set<Paste>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Paste>(paste =>
        {
            paste.Property(p => p.Title).HasMaxLength(200);
        });
    }
}
