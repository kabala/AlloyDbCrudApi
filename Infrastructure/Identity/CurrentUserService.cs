using System.Security.Claims;
using AlloyDbCrudApi.Application.Abstractions;

namespace AlloyDbCrudApi.Infrastructure.Identity;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _ctx;

    public CurrentUserService(IHttpContextAccessor ctx) => _ctx = ctx;

    public Guid? UserId
    {
        get
        {
            var id = _ctx.HttpContext?.User?.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
                     ?? _ctx.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(id, out var g) ? g : null;
        }
    }

    public string? Email => _ctx.HttpContext?.User?.FindFirstValue(ClaimTypes.Email)
                            ?? _ctx.HttpContext?.User?.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email);

    public bool IsAuthenticated => _ctx.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => _ctx.HttpContext?.User?.IsInRole(role) ?? false;

    public IReadOnlyList<string> Roles =>
        _ctx.HttpContext?.User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

    public string? CorrelationId =>
        _ctx.HttpContext?.Items.TryGetValue("CorrelationId", out var v) == true ? v?.ToString() : null;
}
