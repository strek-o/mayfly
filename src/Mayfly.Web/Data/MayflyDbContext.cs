using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Mayfly.Web.Data;

public class MayflyDbContext(DbContextOptions<MayflyDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Paste> Pastes => Set<Paste>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Paste>(paste =>
        {
            paste.Property(p => p.Title).HasMaxLength(200);
        });
    }
}
