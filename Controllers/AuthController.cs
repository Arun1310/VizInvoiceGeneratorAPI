using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using VizInvoiceGeneratorWebAPI.Interfaces;
using VizInvoiceGeneratorWebAPI.Services.Extensions;
using VizInvoiceGeneratorWebAPI.Validator.Auth;
using static VizInvoiceGeneratorWebAPI.Models.AuthDto;

namespace VizInvoiceGeneratorWebAPI.Controllers.Auth
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        readonly IAuthService _authService;
        readonly ITokenService _tokenService;
        private readonly IStringLocalizer<LoginRequest> _loginLocalizer;
        public AuthController(
           IAuthService authService,
           ITokenService tokenService,
           IStringLocalizer<LoginRequest> loginLocalizer
           )
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _loginLocalizer = loginLocalizer ?? throw new ArgumentNullException(nameof(loginLocalizer));
        }


        [HttpPost, Route("sign-in")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            LoginValidator validator = new(_loginLocalizer, _authService);
            ValidationResult result = validator.Validate(request);
            if (result.IsValid)
            {
                UserDto userResult = _authService.GetUserByEmail(request.Email);
                return Ok(_authService.GetUserTokenDto(userResult));
            }
            return BadRequest(ErrorService.GetErrorMessage(result));
        }
    }
}
