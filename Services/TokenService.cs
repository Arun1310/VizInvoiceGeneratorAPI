using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using VizInvoiceGeneratorWebAPI.Interfaces;


namespace VizInvoiceGeneratorWebAPI.Services.Auth
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public string GenerateAccessToken(IEnumerable<Claim> claims)
        {
            var secret = _configuration["JWTSettings:Secret"];

            if (string.IsNullOrEmpty(secret))
            {
                throw new ArgumentException("JWT secret is not configured.");
            }

            var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var signingCredentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);

            var tokenOptions = new JwtSecurityToken(
                issuer: _configuration["JWTSettings:ValidIssuer"],
                audience: _configuration["JWTSettings:ValidAudience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(Convert.ToInt32(_configuration["JWTSettings:MinutesToExpiration"])),
                signingCredentials: signingCredentials
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(tokenOptions);
            return tokenString;
        }


        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
                return Convert.ToBase64String(randomNumber);
            }
        }

        public ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
        {
            var secret = _configuration["JWTSettings:Secret"];

            if (string.IsNullOrEmpty(secret))
            {
                throw new ArgumentException("JWT secret is not configured.");
            }

            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false, //you might want to validate the audience and issuer depending on your use case
                ValidateIssuer = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                ValidateLifetime = false //here we are saying that we don't care about the token's expiration date

            };

            var tokenHandler = new JwtSecurityTokenHandler();
            SecurityToken securityToken;
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out securityToken);
            var jwtSecurityToken = securityToken as JwtSecurityToken;
            if (jwtSecurityToken == null || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                throw new SecurityTokenException("Invalid token");

            return principal;
        }


        //public User GetUserByEmail(string email)
        //{
        //    return _dbContext.Users.SingleOrDefault(u => u.Email == email);
        //}
    }
}
