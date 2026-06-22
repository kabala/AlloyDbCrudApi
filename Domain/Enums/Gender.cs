namespace AlloyDbCrudApi.Domain.Enums;

public enum Gender
{
    Male = 0,
    Female = 1,
    Other = 2,
    Unspecified = 3,
}

public static class GenderNames
{
    public const string Male = "Male";
    public const string Female = "Female";
    public const string Other = "Other";
    public const string Unspecified = "Unspecified";

    public static readonly IReadOnlyDictionary<string, Gender> Map =
        new Dictionary<string, Gender>(StringComparer.OrdinalIgnoreCase)
        {
            [Male] = Gender.Male,
            [Female] = Gender.Female,
            [Other] = Gender.Other,
            [Unspecified] = Gender.Unspecified,
        };

    public static Gender Parse(string? raw)
        => string.IsNullOrWhiteSpace(raw) || !Map.TryGetValue(raw, out var g)
            ? Gender.Unspecified
            : g;

    public static string Name(Gender g) => g switch
    {
        Gender.Male => Male,
        Gender.Female => Female,
        Gender.Other => Other,
        _ => Unspecified,
    };
}
