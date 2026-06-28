namespace AlloyDbCrudApi.Infrastructure.Seeding;

public sealed record SeedCommand(string Name)
{
    public const string RetailBiHistory = "retail-bi-history";

    public static SeedCommand? Parse(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--seed", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                    throw new InvalidOperationException("Missing seed name after '--seed'.");

                return new SeedCommand(args[i + 1].Trim());
            }

            const string prefix = "--seed=";
            if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return new SeedCommand(arg[prefix.Length..].Trim());
        }

        return null;
    }
}
