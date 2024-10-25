using MongoDB.Driver;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using VizInvoiceGeneratorWebAPI.Interfaces;
using VizInvoiceGeneratorWebAPI.Models;
using VizInvoiceGeneratorWebAPI.Services.Extensions;
using static VizInvoiceGeneratorWebAPI.Models.AuthDto;

namespace VizInvoiceGeneratorWebAPI.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly ITokenService _tokenService;
        private readonly IMongoCollection<User> _users;

        public AuthService(ITokenService tokenService, MongoDbContext context)
        {
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _users = context.Users;
        }

        public bool CheckLogin(string email, string password)
        {
            try
            {
                string encryptedPassword = DecryptWithUICipherAndEncryptItWithAPICipher(password);

                var filter = Builders<User>.Filter.And(
                 Builders<User>.Filter.Eq(u => u.Email, email),
                 Builders<User>.Filter.Eq(u => u.Password, encryptedPassword)
             );

                var userCount = _users.Find(filter).CountDocuments();

                return userCount > 0;
            }
            catch (Exception)
            {
                throw;
            }
         
        }

        public UserDto GetUserByEmail(string email)
        {
            UserDto result = new UserDto();

            try
            {
                var filter = Builders<User>.Filter.Eq(u => u.Email, email);
                var user = _users.Find(filter).FirstOrDefault();

                if (user != null)
                {
                    result = new UserDto
                    {
                        Id = user.Id,
                        DisplayName = user.Name ?? string.Empty,
                        Email = user.Email ?? string.Empty,
                        Role = user.Role
                    };
                }
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while fetching user by email", ex);
            }

            return result;
        }

        public UserTokenDto GetUserTokenDto(UserDto user)
        {
            var claims = PrepareClaims(user.Email, "Role");
            var accessToken = _tokenService.GenerateAccessToken(claims);
            //var userDto = SimpleMapper.Map<User, UserDto>(user);
            //userDto.DisplayName = user.DisplayName;
            // userDto.Role = _roleRightsService.GetRoleById(user.RoleId).Name;
            var userTokenDto = new UserTokenDto
            {
                AccessToken = accessToken,
                User = user
            };
            return userTokenDto;
        }
        private List<Claim> PrepareClaims(string email, string role)
        {
            return new List<Claim>
            {
                new Claim(ClaimTypes.Name, email),
                new Claim(ClaimTypes.Role, role),
               // new Claim("TenantId", tenantId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };
        }

        public static string DecryptWithUICipherAndEncryptItWithAPICipher(string UICipherText)
        {
            string decryptedUIText = CipherService.Decrypt(
                                        UICipherText,
                                        AppConstants.UICipherKey,
                                        AppConstants.UICipherIV);
            string encryptedText = CipherService.Encrypt(decryptedUIText, AppConstants.CipherKey, AppConstants.CipherIV);
            return encryptedText;
        }
    }
}
