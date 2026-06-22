using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AlloyDbCrudApi.Application.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AlloyDbCrudApi.Infrastructure.Identity;

public class JwtTokenService : ITokenService
{
    private readonly JwtOptions _opt;

    public JwtTokenService(IOptions<JwtOptions> opt)
    {
        _opt = opt.Value;
    }

    public (string accessToken, DateTime expiresAt) IssueAccessToken(Guid userId, string email, string fullName, IEnumerable<string> roles)
    {
        var expiresAt = DateTime.UtcNow.Add(_opt.AccessTokenLifetime);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new("name", fullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        foreach (var r in roles.Distinct())
            claims.Add(new Claim(ClaimTypes.Role, r));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public string IssueRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes);
    }

    public (string accessToken, string refreshToken, DateTime expiresAt) IssueTokens(Guid userId, string email, string fullName, IEnumerable<string> roles)
    {
        var (access, exp) = IssueAccessToken(userId, email, fullName, roles);
        var refresh = IssueRefreshToken();
        return (access, refresh, exp);
    }

    public ClaimsPrincipal? ParseAccessToken(string token)
    {
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.SigningKey));
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _opt.Issuer,
                ValidateAudience = true,
                ValidAudience = _opt.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
            }, out _);
            return principal;
        }
        catch
        {
            return null;
        }
    }

    public string HashRefreshToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}

public record JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "AlloyDbCrudApi";
    public string Audience { get; set; } = "AlloyDbCrudApi";
    public string SigningKey { get; set; } = string.Empty;
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);
}
