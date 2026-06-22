using AlloyDbCrudApi.Application.Contracts.Returns;
using FluentValidation;

namespace AlloyDbCrudApi.Application.Validators.Returns;

public class CreateReturnRequestValidator : AbstractValidator<CreateReturnRequest>
{
    public CreateReturnRequestValidator()
    {
        RuleFor(x => x.TransactionId).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Reason).IsInEnum();
        RuleFor(x => x.Notes).MaximumLength(1000).When(x => x.Notes is not null);
    }
}
