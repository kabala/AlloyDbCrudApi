using AlloyDbCrudApi.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace AlloyDbCrudApi.Domain.Entities;

public class User : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeactivatedAt { get; set; }

    public bool Can(string permission)
        => PermissionCatalog.RolePermissions.TryGetValue(Role, out var perms) && perms.Contains(permission);

    private Role _role = Role.Vendedor;
    public Role Role
    {
        get => _role;
        set => _role = value;
    }
}

public static class PermissionCatalog
{
    public const string ManageUsers = "users.manage";
    public const string ManageCatalog = "catalog.manage";
    public const string ManageInventory = "inventory.manage";
    public const string RegisterSales = "sales.register";
    public const string RegisterReturns = "returns.register";
    public const string ViewCustomers = "customers.view";
    public const string ManageCustomers = "customers.manage";
    public const string ViewReports = "reports.view";
    public const string ManageDiscounts = "discounts.manage";

    public static readonly IReadOnlyDictionary<Role, IReadOnlySet<string>> RolePermissions =
        new Dictionary<Role, IReadOnlySet<string>>
        {
            [Role.Superadmin] = new HashSet<string>
            {
                ManageUsers, ManageCatalog, ManageInventory, RegisterSales, RegisterReturns,
                ViewCustomers, ManageCustomers, ViewReports, ManageDiscounts,
            },
            [Role.Vendedor] = new HashSet<string>
            {
                RegisterSales, RegisterReturns, ViewCustomers, ManageCustomers,
            },
            [Role.Visualizador] = new HashSet<string>
            {
                ViewCustomers, ViewReports,
            },
        }.ToDictionary(kv => kv.Key, kv => (IReadOnlySet<string>)kv.Value);
}
