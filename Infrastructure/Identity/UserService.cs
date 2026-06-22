using AlloyDbCrudApi.Application.Abstractions;
using AlloyDbCrudApi.Application.Contracts.Common;
using AlloyDbCrudApi.Application.Contracts.Users;
using AlloyDbCrudApi.Domain.Entities;
using AlloyDbCrudApi.Domain.Enums;
using AlloyDbCrudApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AlloyDbCrudApi.Infrastructure.Identity;

public class UserService : IUserService
{
    private readonly UserManager<User> _users;
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public UserService(UserManager<User> users, AppDbContext db, IAuditService audit)
    {
        _users = users; _db = db; _audit = audit;
    }

    public async Task<PagedResult<UserDto>> ListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var q = _users.Users.AsNoTracking();
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(u => u.FullName)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new UserDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email!,
                Role = u.Role,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
            })
            .ToListAsync(ct);
        return new PagedResult<UserDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var u = await _users.FindByIdAsync(id.ToString());
        if (u is null) return null;
        return new UserDto
        {
            Id = u.Id, FullName = u.FullName, Email = u.Email!, Role = u.Role, IsActive = u.IsActive, CreatedAt = u.CreatedAt,
        };
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest req, CancellationToken ct = default)
    {
        var user = new User { FullName = req.FullName, Email = req.Email, UserName = req.Email, Role = req.Role };
        var res = await _users.CreateAsync(user, req.Password);
        if (!res.Succeeded)
            throw new InvalidOperationException(string.Join("; ", res.Errors.Select(e => e.Description)));
        await _users.AddToRoleAsync(user, RoleNames.Name(req.Role));
        await _audit.LogAsync("user.create", "User", user.Id.ToString(), $"Created {req.Email} as {req.Role}", ct);
        return new UserDto
        {
            Id = user.Id, FullName = user.FullName, Email = user.Email!, Role = user.Role, IsActive = user.IsActive, CreatedAt = user.CreatedAt,
        };
    }
}
