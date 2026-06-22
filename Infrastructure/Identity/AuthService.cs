using System.Security.Claims;
using AlloyDbCrudApi.Application.Abstractions;
using AlloyDbCrudApi.Application.Contracts.Auth;
using AlloyDbCrudApi.Domain.Entities;
using AlloyDbCrudApi.Domain.Enums;
using AlloyDbCrudApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AlloyDbCrudApi.Infrastructure.Identity;

public class AuthService : IAuthService
{
    private readonly UserManager<User> _users;
    private readonly SignInManager<User> _signIn;
    private readonly ITokenService _tokens;
    private readonly AppDbContext _db;
    private readonly RoleManager<IdentityRole<Guid>> _roles;

    public AuthService(UserManager<User> users, SignInManager<User> signIn, ITokenService tokens, AppDbContext db, RoleManager<IdentityRole<Guid>> roles)
    {
        _users = users; _signIn = signIn; _tokens = tokens; _db = db; _roles = roles;
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(req.Email);
        if (user is null || !user.IsActive)
            throw new UnauthorizedAccessException("Invalid credentials.");
        var res = await _signIn.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
        if (!res.Succeeded)
            throw new UnauthorizedAccessException("Invalid credentials.");

        var roles = await _users.GetRolesAsync(user);
        return await IssueAndPersistAsync(user, roles, ct);
    }

    public async Task<TokenResponse> RefreshAsync(RefreshRequest req, CancellationToken ct = default)
    {
        var hash = _tokens.HashRefreshToken(req.RefreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash, ct);
        if (stored is null || !stored.IsActive)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        var user = await _users.FindByIdAsync(stored.UserId.ToString());
        if (user is null || !user.IsActive)
            throw new UnauthorizedAccessException("User not active.");

        stored.RevokedAt = DateTime.UtcNow;
        var roles = await _users.GetRolesAsync(user);
        return await IssueAndPersistAsync(user, roles, ct);
    }

    private async Task<TokenResponse> IssueAndPersistAsync(User user, IList<string> roles, CancellationToken ct)
    {
        var (access, refresh, exp) = _tokens.IssueTokens(user.Id, user.Email!, user.FullName, roles);
        var rt = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _tokens.HashRefreshToken(refresh),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        };
        _db.RefreshTokens.Add(rt);
        await _db.SaveChangesAsync(ct);
        return new TokenResponse
        {
            AccessToken = access,
            RefreshToken = refresh,
            ExpiresAt = exp,
        };
    }
}
