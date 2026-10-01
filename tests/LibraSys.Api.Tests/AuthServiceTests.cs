using LibraSys.Api.Configuration;
using LibraSys.Api.Features.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Xunit;

namespace LibraSys.Api.Tests;

public sealed class AuthServiceTests
{
    private const string Password = "correct-password";

    [Fact]
    public async Task LoginAsync_ReturnsTokenForValidCredentials_AndResetsFailures()
    {
        var repository = new FakeAuthRepository(UserWithPassword(failedLoginCount: 3));
        var service = CreateService(repository);

        var response = await service.LoginAsync(
            new LoginRequest("demo_member", Password), CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("demo_member", response.Username);
        Assert.Equal("Member", response.Role);
        Assert.Equal(42, repository.ResetUserId);
        Assert.Empty(repository.FailedAttempts);
    }

    [Fact]
    public async Task LoginAsync_ReturnsNullAndRecordsFailureForInvalidCredentials()
    {
        var repository = new FakeAuthRepository(UserWithPassword(failedLoginCount: 2));
        var service = CreateService(repository);

        var response = await service.LoginAsync(
            new LoginRequest("demo_member", "wrong-password"), CancellationToken.None);

        Assert.Null(response);
        Assert.Equal((42L, 3), Assert.Single(repository.FailedAttempts));
        Assert.Equal(0, repository.ResetUserId);
    }

    [Fact]
    public async Task LoginAsync_ReturnsNullWhenAccountIsLocked()
    {
        var repository = new FakeAuthRepository(UserWithPassword(
            failedLoginCount: 5, lockedUntil: DateTime.UtcNow.AddMinutes(10)));
        var service = CreateService(repository);

        var response = await service.LoginAsync(
            new LoginRequest("demo_member", Password), CancellationToken.None);

        Assert.Null(response);
        Assert.Empty(repository.FailedAttempts);
        Assert.Equal(0, repository.ResetUserId);
    }

    private static AuthService CreateService(FakeAuthRepository repository)
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "LibraSys.Tests",
            Audience = "LibraSys.Tests",
            SigningKey = "test-signing-key-with-at-least-32-characters",
            ExpirationMinutes = 30
        });
        return new AuthService(
            repository,
            options,
            new PasswordHasher<AuthUser>());
    }

    private static AuthUser UserWithPassword(int failedLoginCount, DateTime? lockedUntil = null)
    {
        var user = new AuthUser
        {
            UserId = 42,
            Username = "demo_member",
            RoleName = "Member",
            FailedLoginCount = failedLoginCount,
            LockedUntil = lockedUntil
        };
        user.PasswordHash = new PasswordHasher<AuthUser>()
            .HashPassword(user, Password);
        return user;
    }

    private sealed class FakeAuthRepository(AuthUser user) : IAuthRepository
    {
        public List<(long UserId, int Count)> FailedAttempts { get; } = [];
        public long ResetUserId { get; private set; }

        public Task<AuthUser?> FindActiveUserAsync(
            string username, CancellationToken cancellationToken) =>
            Task.FromResult<AuthUser?>(username == user.Username ? user : null);

        public Task RecordFailedLoginAsync(
            long userId, int failedLoginCount, CancellationToken cancellationToken)
        {
            FailedAttempts.Add((userId, failedLoginCount));
            return Task.CompletedTask;
        }

        public Task ResetFailedLoginsAsync(
            long userId, CancellationToken cancellationToken)
        {
            ResetUserId = userId;
            return Task.CompletedTask;
        }
    }
}
