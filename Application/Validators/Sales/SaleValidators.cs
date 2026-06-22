using AlloyDbCrudApi.Application.Contracts.Sales;
using FluentValidation;

namespace AlloyDbCrudApi.Application.Validators.Sales;

public class CreateSaleRequestValidator : AbstractValidator<CreateSaleRequest>
{
    public CreateSaleRequestValidator()
    {
        RuleFor(x => x.TransactionId).NotEmpty().MaximumLength(50);
        RuleFor(x => x.StoreId).NotEmpty().MaximumLength(50);
        RuleFor(x => x.CustomerId).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Items).NotEmpty().WithMessage("Sale must contain at least one item.");
        RuleForEach(x => x.Items).SetValidator(new SaleItemRequestValidator());
    }
}

public class SaleItemRequestValidator : AbstractValidator<SaleItemRequest>
{
    public SaleItemRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Discount).InclusiveBetween(0m, 1m);
    }
}

public class SaleListQueryValidator : AbstractValidator<SaleListQuery>
{
    public SaleListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}
