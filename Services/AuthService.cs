using FaceCheck.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Data;
using Microsoft.Data.SqlClient;

namespace FaceCheck.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request);
    }

    public class AuthService : IAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IConfiguration configuration, ILogger<AuthService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Email and password are required."
                    };
                }

                // Get user from database using ADO.NET
                var user = await GetUserByEmailAsync(request.Email);
                if (user == null)
                {
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Invalid email or password."
                    };
                }

                // Verify password
                if (!BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
                {
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Invalid email or password."
                    };
                }

                // Generate JWT token
                var token = GenerateJwtToken(user);

                return new LoginResponse
                {
                    Success = true,
                    Message = "Login successful.",
                    Token = token,
                    User = new UserData
                    {
                        Id = user.Id,
                        FirstName = user.Firstname,
                        LastName = user.Lastname,
                        Email = user.Email
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Login error: {ex.Message}");
                return new LoginResponse
                {
                    Success = false,
                    Message = "An error occurred during login."
                };
            }
        }

        private async Task<Person?> GetUserByEmailAsync(string email)
        {
            const string query = @"
                SELECT Id, Firstname, Lastname, Email, Password, Status, CreatedAt
                FROM Persons
                WHERE Email = @Email
            ";

            using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Email", email);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new Person
                            {
                                Id = reader.GetInt32(0),
                                Firstname = reader.GetString(1),
                                Lastname = reader.GetString(2),
                                Email = reader.GetString(3),
                                Password = reader.GetString(4),
                                Status = reader.GetInt32(5),
                                CreatedAt = reader.GetDateTime(6)
                            };
                        }
                    }
                }
            }

            return null;
        }

        private string GenerateJwtToken(Person user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"] ?? ""));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.GivenName, user.Firstname),
                new Claim(ClaimTypes.Surname, user.Lastname)
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(int.Parse(_configuration["Jwt:ExpiryInHours"] ?? "24")),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
