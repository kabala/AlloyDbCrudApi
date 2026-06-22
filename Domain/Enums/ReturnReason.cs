namespace AlloyDbCrudApi.Domain.Enums;

public enum ReturnReason
{
    Defective = 0,
    WrongSize = 1,
    WrongItem = 2,
    CustomerChange = 3,
    Other = 99,
}

public enum ReturnStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
}

public static class ReturnReasonNames
{
    public const string Defective = "Defective";
    public const string WrongSize = "WrongSize";
    public const string WrongItem = "WrongItem";
    public const string CustomerChange = "CustomerChange";
    public const string Other = "Other";

    public static string Name(ReturnReason r) => r switch
    {
        ReturnReason.Defective => Defective,
        ReturnReason.WrongSize => WrongSize,
        ReturnReason.WrongItem => WrongItem,
        ReturnReason.CustomerChange => CustomerChange,
        _ => Other,
    };
}

public static class ReturnStatusNames
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    public static string Name(ReturnStatus s) => s switch
    {
        ReturnStatus.Approved => Approved,
        ReturnStatus.Rejected => Rejected,
        _ => Pending,
    };
}
