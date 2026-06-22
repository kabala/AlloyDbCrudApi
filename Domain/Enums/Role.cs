namespace AlloyDbCrudApi.Domain.Enums;

public enum Role
{
    Superadmin = 0,
    Vendedor = 1,
    Visualizador = 2,
}

public static class RoleNames
{
    public const string Superadmin = "Superadmin";
    public const string Vendedor = "Vendedor";
    public const string Visualizador = "Visualizador";

    public static readonly IReadOnlyDictionary<Role, string> Map = new Dictionary<Role, string>
    {
        [Role.Superadmin] = Superadmin,
        [Role.Vendedor] = Vendedor,
        [Role.Visualizador] = Visualizador,
    };

    public static string Name(Role role) => Map[role];
}
