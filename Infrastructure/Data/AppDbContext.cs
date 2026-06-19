using CallbackListener.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CallbackListener.Infrastructure.Data;

public sealed class AppDbContext : IdentityDbContext<AppUser>
{
    public DbSet<Listener> Listeners => Set<Listener>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Listener>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasOne(x => x.User)
             .WithMany(u => u.Listeners)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ApiKey>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.KeyHash);
            e.HasOne(x => x.User)
             .WithMany(u => u.ApiKeys)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Listener)
             .WithMany(l => l.ApiKeys)
             .HasForeignKey(x => x.ListenerId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
