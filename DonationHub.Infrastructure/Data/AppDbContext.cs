using DonationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DonationHub.Infrastructure.Data;

public class AppDbContext: DbContext
{
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Category> Categories => Set<Category>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}