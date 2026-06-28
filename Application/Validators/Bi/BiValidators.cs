using AlloyDbCrudApi.Application.Contracts.Bi;
using FluentValidation;

namespace AlloyDbCrudApi.Application.Validators.Bi;

public class BiDashboardQueryValidator : AbstractValidator<BiDashboardQuery>
{
    public BiDashboardQueryValidator()
    {
        RuleFor(x => x).Must(HaveValidRange).WithMessage("fromDate must be before or equal to toDate.");
    }

    private static bool HaveValidRange(BiDashboardQuery query)
        => !query.FromDate.HasValue || !query.ToDate.HasValue || query.FromDate <= query.ToDate;
}

public class BiProductAbcQueryValidator : AbstractValidator<BiProductAbcQuery>
{
    public BiProductAbcQueryValidator()
    {
        Include(new BiDashboardQueryValidator());
        RuleFor(x => x.Take).InclusiveBetween(1, 500);
        RuleFor(x => x.AbcClass)
            .Must(x => string.IsNullOrWhiteSpace(x) || x is "A" or "B" or "C")
            .WithMessage("abcClass must be A, B, or C.");
    }
}

public class BiCustomerRfmQueryValidator : AbstractValidator<BiCustomerRfmQuery>
{
    public BiCustomerRfmQueryValidator()
    {
        Include(new BiDashboardQueryValidator());
        RuleFor(x => x.Take).InclusiveBetween(1, 500);
    }
}

public class BiBreakdownQueryValidator : AbstractValidator<BiBreakdownQuery>
{
    public BiBreakdownQueryValidator()
    {
        Include(new BiDashboardQueryValidator());
        RuleFor(x => x.Take).InclusiveBetween(1, 200);
    }
}
