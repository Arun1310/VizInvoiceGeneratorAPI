using FluentValidation;
using Microsoft.Extensions.Localization;
using VizInvoiceGeneratorWebAPI.Interfaces;
using static VizInvoiceGeneratorWebAPI.Models.AuthDto;

namespace VizInvoiceGeneratorWebAPI.Validator.Auth
{
    public class LoginValidator : AbstractValidator<LoginRequest>
    {
        public LoginValidator(IStringLocalizer<LoginRequest> localizer, IAuthService authService)
        {
            RuleLevelCascadeMode = CascadeMode.Stop;
            RuleFor(x => x.Email).NotEmpty().WithMessage(m => localizer["LOGIN00001"]);
            RuleFor(x => x.Password).NotEmpty().WithMessage(m => localizer["LOGIN00002"]);
            RuleFor(x => x.Email).Must((obj, Email) => authService.CheckLogin(Email, obj.Password))
                .WithMessage(m => localizer["LOGIN00003"]);
        }
    }
}
