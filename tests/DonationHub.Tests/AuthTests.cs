using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using DonationHub.Application.Abstractions.Authentication;
using DonationHub.Application.Abstractions.Persistence;
using DonationHub.Application.Features.Auth;
using DonationHub.Application.Options;
using DonationHub.Domain.Entities;
using DonationHub.Infrastructure.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DonationHub.Tests;

public sealed class AuthTests
{
    [Fact]
    public async Task Login_issues_signed_claims_and_stores_only_hash_with_one_save()
    {
        var fixture = new Fixture();
        var result = await fixture.Login();
        Assert.Null(result.Error);
        var pair = result.Value!;
        var stored = Assert.Single(fixture.Repository.Items);
        Assert.NotEqual(pair.RefreshToken, stored.TokenHash);
        Assert.Equal(fixture.Tokens.HashRefreshToken(pair.RefreshToken), stored.TokenHash);
        Assert.Equal(fixture.Clock.Now.AddDays(7), stored.ExpiresAt);
        Assert.Equal(1, fixture.UnitOfWork.Saves);
        var principal = new JwtSecurityTokenHandler().ValidateToken(pair.AccessToken, new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = fixture.Options.Issuer,
            ValidateAudience = true, ValidAudience = fixture.Options.Audience,
            ValidateIssuerSigningKey = true, ValidateLifetime = false,
            IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(fixture.Options.SigningKey))
        }, out var token);
        Assert.Equal(fixture.Identity.User.Id.ToString(), principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(fixture.Identity.User.Email, principal.FindFirstValue(ClaimTypes.Email));
        Assert.True(principal.IsInRole("Donor"));
        Assert.Equal(fixture.Clock.Now.AddMinutes(15), token.ValidTo);
    }

    [Fact]
    public async Task Invalid_email_and_password_have_identical_errors()
    {
        var fixture = new Fixture();
        var unknown = await fixture.Service.LoginAsync(new("unknown@example.test", fixture.Identity.Password), default);
        var wrong = await fixture.Service.LoginAsync(new(fixture.Identity.User.Email, "wrong"), default);
        Assert.Equal(unknown.Error, wrong.Error);
        Assert.Equal("Auth.InvalidCredentials", unknown.Error!.Code);
        Assert.Equal(0, fixture.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Disabled_user_requires_correct_password_before_deactivation_error()
    {
        var fixture = new Fixture();
        fixture.Identity.User = fixture.Identity.User with { IsActive = false };
        var correct = await fixture.Login();
        Assert.Equal("Auth.AccountDeactivated", correct.Error!.Code);
        Assert.Equal(AuthErrorKind.Forbidden, correct.Error.Kind);
        var incorrect = await fixture.Service.LoginAsync(new(fixture.Identity.User.Email, "wrong"), default);
        Assert.Equal("Auth.InvalidCredentials", incorrect.Error!.Code);
        Assert.Empty(fixture.Repository.Items);
    }

    [Fact]
    public async Task Rotation_links_tokens_and_replay_cannot_save_again()
    {
        var fixture = new Fixture();
        var login = (await fixture.Login()).Value!;
        var refreshed = await fixture.Service.RefreshAsync(new(login.RefreshToken), default);
        Assert.Null(refreshed.Error);
        Assert.NotEqual(login.RefreshToken, refreshed.Value!.RefreshToken);
        var old = fixture.Repository.Items[0];
        var replacement = fixture.Repository.Items[1];
        Assert.Equal(replacement.Id, old.ReplacedByTokenId);
        Assert.Equal(fixture.Clock.Now, old.RevokedAt);
        Assert.Equal(2, fixture.UnitOfWork.Saves);
        var replay = await fixture.Service.RefreshAsync(new(login.RefreshToken), default);
        Assert.Equal("Auth.RefreshTokenInvalid", replay.Error!.Code);
        Assert.Equal(2, fixture.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Expired_and_unknown_refresh_tokens_are_rejected()
    {
        var fixture = new Fixture();
        var login = (await fixture.Login()).Value!;
        fixture.Clock.Now = fixture.Clock.Now.AddDays(7);
        Assert.Equal("Auth.RefreshTokenInvalid", (await fixture.Service.RefreshAsync(new(login.RefreshToken), default)).Error!.Code);
        Assert.Equal("Auth.RefreshTokenInvalid", (await fixture.Service.RefreshAsync(new("unknown"), default)).Error!.Code);
    }

    [Fact]
    public async Task Disabled_user_cannot_refresh()
    {
        var fixture = new Fixture();
        var login = (await fixture.Login()).Value!;
        fixture.Identity.User = fixture.Identity.User with { IsActive = false };
        Assert.Equal("Auth.AccountDeactivated", (await fixture.Service.RefreshAsync(new(login.RefreshToken), default)).Error!.Code);
        Assert.Equal(1, fixture.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Concurrency_conflict_is_reported_as_invalid_refresh()
    {
        var fixture = new Fixture();
        var login = (await fixture.Login()).Value!;
        fixture.UnitOfWork.Conflict = true;
        var result = await fixture.Service.RefreshAsync(new(login.RefreshToken), default);
        Assert.Equal("Auth.RefreshTokenInvalid", result.Error!.Code);
    }

    [Fact]
    public async Task Logout_revokes_owned_token_and_is_idempotent()
    {
        var fixture = new Fixture();
        var login = (await fixture.Login()).Value!;
        var wrongOwner = await fixture.Service.LogoutAsync(Guid.NewGuid(), new(login.RefreshToken), default);
        Assert.Equal("Auth.RefreshTokenInvalid", wrongOwner.Error!.Code);
        Assert.Null((await fixture.Service.LogoutAsync(fixture.Identity.User.Id, new(login.RefreshToken), default)).Error);
        Assert.Null((await fixture.Service.LogoutAsync(fixture.Identity.User.Id, new(login.RefreshToken), default)).Error);
        Assert.Equal(2, fixture.UnitOfWork.Saves);
        Assert.Equal("Auth.RefreshTokenInvalid", (await fixture.Service.RefreshAsync(new(login.RefreshToken), default)).Error!.Code);
    }

    [Fact]
    public async Task Registration_and_staff_creation_choose_only_allowed_roles()
    {
        var fixture = new Fixture();
        var request = new RegisterRequest("new@example.test", fixture.Identity.Password, "New User");
        Assert.Null((await fixture.Service.RegisterAsync(request, default)).Error);
        Assert.Equal("Donor", fixture.Identity.CreatedRole);
        Assert.Null((await fixture.Service.CreateStaffAsync(new(request.Email, request.Password, request.FullName, "en", "CampaignManager"), default)).Error);
        Assert.Equal("CampaignManager", fixture.Identity.CreatedRole);
        Assert.NotNull((await fixture.Service.CreateStaffAsync(new(request.Email, request.Password, request.FullName, "en", "Donor"), default)).Error);
    }

    [Fact]
    public async Task Invalid_profiles_are_rejected_at_application_boundary()
    {
        var fixture = new Fixture();
        Assert.Equal("Validation.Failed", (await fixture.Service.RegisterAsync(new("x@example.test", "x", " ", "ar"), default)).Error!.Code);
        Assert.Equal("Validation.Failed", (await fixture.Service.RegisterAsync(new("", "x", "Name", "en"), default)).Error!.Code);
    }

    private sealed class Fixture
    {
        public TestClock Clock { get; } = new();
        public FakeIdentity Identity { get; } = new();
        public FakeRepository Repository { get; } = new();
        public FakeUnitOfWork UnitOfWork { get; } = new();
        public JwtOptions Options { get; } = new() { Issuer = "tests", Audience = "tests", SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) };
        public JwtTokenService Tokens { get; }
        public AuthService Service { get; }
        public Fixture()
        {
            Tokens = new(OptionsWrapper(), Clock);
            Service = new(Identity, Tokens, Repository, Repository, UnitOfWork, OptionsWrapper(), Clock);
        }
        private IOptions<JwtOptions> OptionsWrapper() => Microsoft.Extensions.Options.Options.Create(Options);
        public Task<AuthResult<TokenResponse>> Login() => Service.LoginAsync(new(Identity.User.Email, Identity.Password), default);
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTime Now { get; set; } = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
    private sealed class FakeIdentity : IIdentityGateway
    {
        public string Password { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        public AccountProfile User { get; set; } = new(Guid.NewGuid(), "user@example.test", "User", "en", true, ["Donor"]);
        public string? CreatedRole { get; private set; }
        public Task<AccountProfile?> VerifyCredentialsAsync(string email, string password) => Task.FromResult(
            email == User.Email && password == Password ? User : null);
        public Task<AccountProfile?> FindAsync(Guid userId) => Task.FromResult(userId == User.Id ? User : null);
        public Task<AuthResult<AccountResponse>> CreateAsync(RegisterRequest request, string role, DateTime nowUtc, CancellationToken ct)
        {
            CreatedRole = role;
            return Task.FromResult(AuthResult<AccountResponse>.Success(new(Guid.NewGuid(), request.Email, request.FullName)));
        }
    }
    private sealed class FakeRepository : IGenericRepository<RefreshToken>, IRefreshTokenReader
    {
        public List<RefreshToken> Items { get; } = [];
        public Task<RefreshToken?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(t => t.Id == id));
        public IQueryable<RefreshToken> Query() => Items.AsQueryable();
        public Task AddAsync(RefreshToken entity, CancellationToken ct) { Items.Add(entity); return Task.CompletedTask; }
        public void Remove(RefreshToken entity) => Items.Remove(entity);
        public Task<RefreshToken?> FindAsync(string hash, Guid? userId, CancellationToken ct) => Task.FromResult(
            Items.SingleOrDefault(t => t.TokenHash == hash && (!userId.HasValue || t.UserId == userId)));
    }
    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int Saves { get; private set; }
        public bool Conflict { get; set; }
        public Task<int> SaveChangesAsync(CancellationToken ct)
        {
            Saves++;
            if (Conflict) throw new PersistenceConcurrencyException(new InvalidOperationException("Test race"));
            return Task.FromResult(1);
        }
    }
}
