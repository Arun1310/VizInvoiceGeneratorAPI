using System.Security.Claims;

namespace VizInvoiceGeneratorWebAPI.Interfaces
{
    public interface ITokenService
    {
        string GenerateAccessToken(IEnumerable<Claim> claims);
        // User GetUserByEmail(string email);
        ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
    }
}
