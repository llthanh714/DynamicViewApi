using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text;
using System.Text.Json;

namespace DynamicViewApi.Controllers
{
    [ApiController]
    [Route("api/view")]
    public class ViewController(IConfiguration configuration) : ControllerBase
    {
        private readonly string? sqlPassword = Environment.GetEnvironmentVariable("__DB_PASSWORD__", EnvironmentVariableTarget.Machine);

        // Ánh xạ từ viết tắt sang toán tử SQL để đảm bảo an toàn
        private static readonly Dictionary<string, string> OperatorMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "eq", "=" },
            { "neq", "<>" },
            { "lt", "<" },
            { "lte", "<=" },
            { "gt", ">" },
            { "gte", ">=" },
            { "like", "LIKE" }
        };

        [HttpPost("query")]
        public async Task<IActionResult> QueryViewData([FromBody] Dictionary<string, object> request)
        {
            // 1. Validate input and extract view_name
            if (request == null || !request.TryGetValue("view_name", out var viewNameValue) || viewNameValue == null || string.IsNullOrWhiteSpace(viewNameValue.ToString()))
            {
                return BadRequest("Invalid request. Please provide 'view_name'.");
            }

            var viewName = viewNameValue.ToString();
            // Lọc ra các tham số, loại bỏ view_name
            var parameters = request.Where(p => p.Key != "view_name");

            try
            {
                var sqlBuilder = new StringBuilder($"SELECT * FROM {viewName} WHERE 1=1");
                var dynamicParameters = new DynamicParameters();

                foreach (var param in parameters)
                {
                    if (param.Value == null || string.IsNullOrWhiteSpace(param.Value.ToString()))
                    {
                        continue;
                    }

                    var keyParts = param.Key.Split(["__"], 2, StringSplitOptions.None);
                    var fieldName = keyParts[0];
                    // Mặc định là 'eq' nếu không có toán tử được chỉ định
                    var opKey = keyParts.Length > 1 ? keyParts[1] : "eq";

                    if (!OperatorMap.TryGetValue(opKey, out var sqlOperator))
                    {
                        return BadRequest($"Operator shorthand '{opKey}' is not allowed for key '{param.Key}'.");
                    }

                    // Tạo tên tham số Dapper duy nhất để tránh xung đột
                    var paramName = $"@{param.Key.Replace("__", "_")}";

                    sqlBuilder.Append($" AND [{fieldName}] {sqlOperator} {paramName}");

                    object? parameterValue = param.Value;
                    if (parameterValue is JsonElement jsonElement)
                    {
                        parameterValue = ConvertJsonElement(jsonElement);
                    }

                    dynamicParameters.Add(paramName, parameterValue);
                }

                var sqlQuery = sqlBuilder.ToString();
                var rawConnectionString = configuration.GetConnectionString("DefaultConnection");
                if (rawConnectionString == null)
                {
                    return StatusCode(500, "Database connection string is not configured.");
                }
                if (sqlPassword == null)
                {
                    return StatusCode(500, "Database password environment variable is not set.");
                }
                var connectionString = rawConnectionString.Replace("__DB_PASSWORD__", sqlPassword);

                using var connection = new SqlConnection(connectionString);
                var result = await connection.QueryAsync(sqlQuery, dynamicParameters);
                return Ok(result);
            }
            catch (SqlException ex)
            {
                return BadRequest($"Database query error: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
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