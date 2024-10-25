using static VizInvoiceGeneratorWebAPI.Models.AuthDto;

namespace VizInvoiceGeneratorWebAPI.Interfaces
{
    public interface IAuthService
    {
        bool CheckLogin(string email, string password);
        UserDto GetUserByEmail(string email);
        UserTokenDto GetUserTokenDto(UserDto user);
    }
}
