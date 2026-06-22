using System.Security.Claims;

namespace AlloyDbCrudApi.Application.Abstractions;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
    IReadOnlyList<string> Roles { get; }
    string? CorrelationId { get; }
}

public interface IAuditService
{
    Task LogAsync(string action, string entityName, string entityId, string? detail = null, CancellationToken ct = default);
}

public interface ITokenService
{
    (string accessToken, DateTime expiresAt) IssueAccessToken(Guid userId, string email, string fullName, IEnumerable<string> roles);
    string IssueRefreshToken();
    (string accessToken, string refreshToken, DateTime expiresAt) IssueTokens(Guid userId, string email, string fullName, IEnumerable<string> roles);
    ClaimsPrincipal? ParseAccessToken(string token);
    string HashRefreshToken(string token);
}
