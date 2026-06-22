using AlloyDbCrudApi.Application.Contracts.Customers;
using FluentValidation;

namespace AlloyDbCrudApi.Application.Validators.Customers;

public class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Age).InclusiveBetween(0, 130);
        RuleFor(x => x.Gender).IsInEnum();
        RuleFor(x => x.City).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Email).MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public class CustomerListQueryValidator : AbstractValidator<CustomerListQuery>
{
    public CustomerListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}
