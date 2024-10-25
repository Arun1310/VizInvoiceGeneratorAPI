using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VizInvoiceGeneratorWebAPI.Interfaces;
using static VizInvoiceGeneratorWebAPI.Models.AuthDto;

namespace VizInvoiceGeneratorWebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TokenController : ControllerBase
    {
        readonly ITokenService _tokenService;
        readonly IAuthService _authService;
        public TokenController(ITokenService tokenService, IAuthService authService)
        {
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        [HttpPost]
        [Route("refresh")]
        public IActionResult Refresh(TokenDto tokenDto)
        {
            if (tokenDto is null)
            {
                return BadRequest("Invalid client request");
            }
            var principal = _tokenService.GetPrincipalFromExpiredToken(tokenDto.AccessToken);
            var email = principal.Identity.Name;
            UserDto user = _authService.GetUserByEmail(email);
            return Ok(_authService.GetUserTokenDto(user));
        }
    }
}
