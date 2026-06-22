using FluentValidation;

namespace AlloyDbCrudApi.Application.Validators.Auth;

public class LoginRequestValidator : AbstractValidator<AlloyDbCrudApi.Application.Contracts.Auth.LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
    }
}

public class RefreshRequestValidator : AbstractValidator<AlloyDbCrudApi.Application.Contracts.Auth.RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
