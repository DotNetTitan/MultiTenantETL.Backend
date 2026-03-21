using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Infrastructure.Identity;
using MultiTenantETL.Infrastructure.Persistence;
using MultiTenantETL.Infrastructure.Services;
using NSubstitute;
using OpenIddict.Abstractions;

namespace MultiTenantETL.UnitTests.Services;

public class UserServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOpenIddictTokenManager _tokenManager;
    private readonly UserService _sut;

    public UserServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var tenantProvider = Substitute.For<ITenantProvider>();
        _context = new ApplicationDbContext(options, tenantProvider);

        var userStore = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            userStore, null, null, null, null, null, null, null, null);

        _tokenManager = Substitute.For<IOpenIddictTokenManager>();

        _sut = new UserService(_userManager, _context, _tokenManager);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task AdminResetPasswordAsync_Success_UsesResetPasswordFlowAndRevokesTokens()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "test@example.com",
            UserName = "test@example.com"
        };

        var token1 = new object();
        var token2 = new object();

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GeneratePasswordResetTokenAsync(user).Returns("reset-token");
        _userManager.ResetPasswordAsync(user, "reset-token", "NewPassword123!")
            .Returns(IdentityResult.Success);
        _tokenManager.FindBySubjectAsync(userId.ToString(), Arg.Any<CancellationToken>())
            .Returns(GetTokens(token1, token2));

        // Act
        var result = await _sut.AdminResetPasswordAsync(userId, "NewPassword123!");

        // Assert
        result.Success.Should().BeTrue();
        await _userManager.Received(1).GeneratePasswordResetTokenAsync(user);
        await _userManager.Received(1).ResetPasswordAsync(user, "reset-token", "NewPassword123!");
        await _userManager.DidNotReceive().RemovePasswordAsync(Arg.Any<ApplicationUser>());
        await _tokenManager.Received(1).TryRevokeAsync(token1, Arg.Any<CancellationToken>());
        await _tokenManager.Received(1).TryRevokeAsync(token2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateUserStatusAsync_DeactivateUser_RevokesTokens()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "test@example.com",
            UserName = "test@example.com",
            IsActive = true
        };

        var token = new object();

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        _tokenManager.FindBySubjectAsync(userId.ToString(), Arg.Any<CancellationToken>())
            .Returns(GetTokens(token));

        // Act
        var result = await _sut.UpdateUserStatusAsync(userId, false);

        // Assert
        result.Success.Should().BeTrue();
        user.IsActive.Should().BeFalse();
        await _tokenManager.Received(1).TryRevokeAsync(token, Arg.Any<CancellationToken>());
    }

    private static async IAsyncEnumerable<object> GetTokens(params object[] tokens)
    {
        foreach (var token in tokens)
        {
            yield return token;
            await Task.Yield();
        }
    }
}
