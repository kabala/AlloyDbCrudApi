namespace AlloyDbCrudApi.Domain.Enums;

public enum SaleStatus
{
    Completed = 0,
    Returned = 1,
    PartiallyReturned = 2,
    Cancelled = 3,
}

public static class SaleStatusNames
{
    public const string Completed = "Completed";
    public const string Returned = "Returned";
    public const string PartiallyReturned = "PartiallyReturned";
    public const string Cancelled = "Cancelled";

    public static string Name(SaleStatus s) => s switch
    {
        SaleStatus.Returned => Returned,
        SaleStatus.PartiallyReturned => PartiallyReturned,
        SaleStatus.Cancelled => Cancelled,
        _ => Completed,
    };
}
