using AlloyDbCrudApi.Application.Contracts.Products;
using FluentValidation;

namespace AlloyDbCrudApi.Application.Validators.Products;

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().MaximumLength(50).NotEqual("???");
        RuleFor(x => x.Category).NotEmpty().MaximumLength(80).NotEqual("???");
        RuleFor(x => x.Color).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Size).MaximumLength(20);
        RuleFor(x => x.Season).MaximumLength(40);
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.ListPrice).GreaterThanOrEqualTo(0m);
    }
}

public class ProductListQueryValidator : AbstractValidator<ProductListQuery>
{
    public ProductListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}
