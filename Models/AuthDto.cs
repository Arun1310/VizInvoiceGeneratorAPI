namespace VizInvoiceGeneratorWebAPI.Models
{
    public class AuthDto
    {
        public class TokenDto
        {
            public string AccessToken { get; set; } = string.Empty;
            public string RefreshToken { get; set; } = string.Empty;
        }

        public class UserDto
        {
            public string Id { get; set; }
            public string DisplayName { get; set; } = string.Empty;
            public string? Username { get; set; }
            public string Email { get; set; } = string.Empty;
            public string? Role { get; set; }
        }

        public class UserTokenDto
        {
            public string AccessToken { get; set; } = string.Empty;
            public UserDto? User { get; set; }
        }

        public class LoginRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }
    }
}
