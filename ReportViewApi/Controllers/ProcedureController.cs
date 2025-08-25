using Dapper;
using DynamicViewApi.Models.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

namespace DynamicViewApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("sp")]
    public class ProcedureController(IConfiguration configuration) : ControllerBase
    {
        private readonly string? sqlPassword = Environment.GetEnvironmentVariable("__DB_PASSWORD__", EnvironmentVariableTarget.Machine);

        [HttpPost("execute")]
        public async Task<IActionResult> ExecuteStoredProcedure([FromBody] Dictionary<string, object> request)
        {
            if (request == null || !request.TryGetValue("sp_name", out var spNameValue) || string.IsNullOrWhiteSpace(spNameValue?.ToString()))
            {
                return BadRequest(new ApiResponse { Success = false, Message = "Invalid request. Please provide 'sp_name'." });
            }

            var spName = spNameValue.ToString()!;
            var parameters = request.Where(p => p.Key != "sp_name");

            var rawConnectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(rawConnectionString))
            {
                return StatusCode(500, new ApiResponse { Success = false, Message = "Database connection string is not configured." });
            }
            if (string.IsNullOrEmpty(sqlPassword))
            {
                return StatusCode(500, new ApiResponse { Success = false, Message = "Database password environment variable is not set." });
            }
            var connectionString = rawConnectionString.Replace("__DB_PASSWORD__", sqlPassword);

            try
            {
                var dynamicParameters = new DynamicParameters();
                foreach (var param in parameters)
                {
                    var paramValue = param.Value is JsonElement jsonElement ? ConvertJsonElement(jsonElement) : param.Value;
                    dynamicParameters.Add($"@{param.Key}", paramValue);
                }

                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var result = await connection.QueryAsync(spName, dynamicParameters, commandType: CommandType.StoredProcedure);

                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Stored procedure executed successfully.",
                    Data = result
                });
            }
            catch (SqlException ex)
            {
                return BadRequest(new ApiResponse { Success = false, Message = $"Database query error: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse { Success = false, Message = $"Internal server error: {ex.Message}" });
            }
        }

        private static object? ConvertJsonElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.TryGetDateTime(out var dateTime) ? dateTime : element.GetString(),
                JsonValueKind.Number => element.GetDecimal(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => element.ToString(),
            };
        }
    }
}