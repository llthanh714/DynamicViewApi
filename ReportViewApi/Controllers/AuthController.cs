using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DynamicViewApi.Controllers
{
    [ApiController]
    [Route("auth")]
    public class AuthController(IConfiguration configuration) : ControllerBase
    {
        private readonly string? sqlPassword = Environment.GetEnvironmentVariable("__DB_PASSWORD__", EnvironmentVariableTarget.Machine);

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            var rawConnectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(rawConnectionString) || string.IsNullOrEmpty(sqlPassword))
            {
                return StatusCode(500, "Database connection is not configured properly.");
            }
            var connectionString = rawConnectionString.Replace("__DB_PASSWORD__", sqlPassword);

            await using var connection = new SqlConnection(connectionString);

            // Tìm người dùng trong cơ sở dữ liệu
            var sql = @"SELECT 
                            u.id AS Guid, 
                            u.code AS Username,
                            u.password AS PasswordHash, 
                            r.name AS Role 
                        FROM
	                        users u
	                        JOIN users_roles r ON u.id = r.users_id
                        WHERE
	                        u.code = @Username
	                        AND u.islocked = 0
	                        AND r.name = 'SYSADMIN'";
            var user = await connection.QuerySingleOrDefaultAsync<UserModel>(sql, new { model.Username });

            if (user == null)
            {
                return Unauthorized("Invalid username or password.");
            }

            // Xác minh mật khẩu
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash);

            if (!isPasswordValid)
            {
                return Unauthorized("Invalid username or password.");
            }

            // Nếu thông tin hợp lệ, tạo token
            var token = GenerateJwtToken(user);
            return Ok(new { token });
        }

        private string GenerateJwtToken(UserModel user)
        {
            var jwtKey = configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey))
            {
                throw new InvalidOperationException("JWT key is not configured.");
            }
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Username ?? string.Empty),
                new(JwtRegisteredClaimNames.Jti, user.Guid ?? string.Empty)
            };

            if (!string.IsNullOrEmpty(user.Role))
            {
                claims.Add(new Claim(ClaimTypes.Role, user.Role));
            }

            var token = new JwtSecurityToken(
                issuer: configuration["Jwt:Issuer"],
                audience: configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(120),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    // Model để nhận dữ liệu từ DB
    public class UserModel
    {
        public string? Guid { get; set; }
        public string? Username { get; set; }
        public string? PasswordHash { get; set; }
        public string? Role { get; set; }
    }

    public class LoginModel
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}