using DonationHub.Application.Abstractions.Persistence;
using DonationHub.Domain.Entities;
using DonationHub.Infrastructure.Data;
using DonationHub.Infrastructure.Data.Interceptors;
using DonationHub.Infrastructure.Identity;
using DonationHub.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using DonationHub.Application.Abstractions.Authentication;
using DonationHub.Application.Features.Auth;
using System.Security.Cryptography;

namespace DonationHub.Tests;

public sealed class PersistenceTests
{
    [Fact]
    public async Task Identity_account_and_role_are_committed_with_exactly_one_save()
    {
        await using var fixture = new Fixture();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => fixture.Context());
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<AppDbContext>();
        services.AddScoped<AppUserStore>();
        services.AddScoped<IUserStore<ApplicationUser>>(provider => provider.GetRequiredService<AppUserStore>());
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IIdentityGateway, IdentityGateway>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        db.Roles.Add(new IdentityRole<Guid>("Donor") { Id = Guid.NewGuid(), NormalizedName = "DONOR" });
        await db.SaveChangesAsync();
        fixture.Counter.Saves = 0;
        var password = "Aa9!" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var result = await scope.ServiceProvider.GetRequiredService<IIdentityGateway>().CreateAsync(
            new RegisterRequest("new@example.test", password, "New User"), "Donor", fixture.Clock.Now, default);
        Assert.Null(result.Error);
        Assert.Equal(1, fixture.Counter.Saves);
        var user = await db.Users.SingleAsync();
        Assert.NotEqual(password, user.PasswordHash);
        Assert.Equal(user.Id, (await db.UserRoles.SingleAsync()).UserId);
        Assert.True(scope.ServiceProvider.GetRequiredService<AppUserStore>().AutoSaveChanges);
    }

    [Fact]
    public async Task Soft_delete_keeps_row_and_created_time_and_hides_it_from_reads()
    {
        await using var fixture = new Fixture();
        await using var db = fixture.Context();
        await db.Database.EnsureCreatedAsync();
        var category = Category.Create("الصحة", "Health", "الصحة", "Health");
        var creator = new ApplicationUser { Id = Guid.NewGuid(), FullName = "Creator", CreatedAt = fixture.Clock.Now };
        var campaign = Campaign.Create(100m, fixture.Clock.Now.AddDays(10), fixture.Clock.Now,
            "حملة", "Campaign", "الوصف", "Description", category.Id, creator.Id);
        db.AddRange(creator, category, campaign);
        await db.SaveChangesAsync();
        var createdAt = campaign.CreatedAt;
        fixture.Clock.Now = fixture.Clock.Now.AddHours(1);
        db.Remove(campaign);
        await db.SaveChangesAsync();
        Assert.True(campaign.IsDeleted);
        Assert.Equal(fixture.Clock.Now, campaign.DeletedAtUtc);
        Assert.Equal(createdAt, campaign.CreatedAt);
        Assert.Equal(fixture.Clock.Now, campaign.UpdatedAt);
        Assert.Empty(await db.Campaigns.ToListAsync());
        Assert.Null(await new EfRepository<Campaign>(db).GetByIdAsync(campaign.Id));
        db.ChangeTracker.Clear();
        var retained = await db.Campaigns.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(DateTimeKind.Utc, retained.DeletedAtUtc!.Value.Kind);
        Assert.True(await new CategoryQueries(db).HasCampaignsAsync(category.Id, default));
    }

    [Fact]
    public async Task Campaign_creator_foreign_key_restricts_user_deletion()
    {
        await using var fixture = new Fixture();
        await using var db = fixture.Context();
        await db.Database.EnsureCreatedAsync();
        var creator = new ApplicationUser { Id = Guid.NewGuid(), FullName = "Creator", CreatedAt = fixture.Clock.Now };
        var category = Category.Create("الصحة", "Health", "الصحة", "Health");
        var campaign = Campaign.Create(100m, fixture.Clock.Now.AddDays(10), fixture.Clock.Now,
            "حملة", "Campaign", "الوصف", "Description", category.Id, creator.Id);
        db.AddRange(creator, category, campaign);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        creator = await db.Users.SingleAsync();
        db.Remove(creator);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Category_reads_project_dtos_without_tracking_entities()
    {
        await using var fixture = new Fixture();
        await using var db = fixture.Context();
        await db.Database.EnsureCreatedAsync();
        var category = Category.Create("الصحة", "Health", "الصحة", "Health");
        db.Add(category);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var queries = new CategoryQueries(db);
        Assert.Equal(category.Id, Assert.Single(await queries.GetAllAsync(default)).Id);
        Assert.Equal(category.Id, (await queries.GetByIdAsync(category.Id, default))!.Id);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Racing_refresh_rolls_back_losing_replacement()
    {
        await using var fixture = new Fixture();
        Guid tokenId;
        await using (var setup = fixture.Context())
        {
            await setup.Database.EnsureCreatedAsync();
            var user = new ApplicationUser { Id = Guid.NewGuid(), FullName = "User", CreatedAt = fixture.Clock.Now };
            var token = RefreshToken.Create(user.Id, "original-hash", fixture.Clock.Now, fixture.Clock.Now.AddDays(7));
            setup.AddRange(user, token);
            await setup.SaveChangesAsync();
            tokenId = token.Id;
        }
        await using var first = fixture.Context();
        await using var second = fixture.Context();
        var oldFirst = await first.RefreshTokens.SingleAsync(t => t.Id == tokenId);
        var oldSecond = await second.RefreshTokens.SingleAsync(t => t.Id == tokenId);
        var winner = RefreshToken.Create(oldFirst.UserId, "winner-hash", fixture.Clock.Now, fixture.Clock.Now.AddDays(7));
        var loser = RefreshToken.Create(oldSecond.UserId, "loser-hash", fixture.Clock.Now, fixture.Clock.Now.AddDays(7));
        oldFirst.Revoke(fixture.Clock.Now, winner.Id);
        oldSecond.Revoke(fixture.Clock.Now, loser.Id);
        first.Add(winner);
        second.Add(loser);
        await new EfUnitOfWork(first).SaveChangesAsync();
        await Assert.ThrowsAsync<PersistenceConcurrencyException>(() => new EfUnitOfWork(second).SaveChangesAsync());
        await using var verify = fixture.Context();
        Assert.Equal(2, await verify.RefreshTokens.CountAsync());
        Assert.False(await verify.RefreshTokens.AnyAsync(t => t.Id == loser.Id));
        Assert.Equal(winner.Id, (await verify.RefreshTokens.SingleAsync(t => t.Id == tokenId)).ReplacedByTokenId);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        public TestClock Clock { get; } = new();
        public SaveCounter Counter { get; } = new();
        public Fixture() => _connection.Open();
        public AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection).AddInterceptors(new AuditingInterceptor(Clock), Counter).Options);
        public ValueTask DisposeAsync() => _connection.DisposeAsync();
    }

    private sealed class SaveCounter : SaveChangesInterceptor
    {
        public int Saves { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Saves++;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTime Now { get; set; } = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
}
