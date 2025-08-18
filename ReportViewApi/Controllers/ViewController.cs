using Dapper;
using DynamicViewApi.Models.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text;
using System.Text.Json;

namespace DynamicViewApi.Controllers
{
    [ApiController]
    [Route("view")]
    public class ViewController(IConfiguration configuration) : ControllerBase
    {
        private readonly string? sqlPassword = Environment.GetEnvironmentVariable("__DB_PASSWORD__", EnvironmentVariableTarget.Machine);

        private static readonly Dictionary<string, string> OperatorMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "eq", "=" }, { "neq", "<>" }, { "lt", "<" }, { "lte", "<=" },
            { "gt", ">" }, { "gte", ">=" }, { "like", "LIKE" }
        };

        [HttpPost("query")]
        public async Task<IActionResult> QueryViewData([FromBody] Dictionary<string, object> request)
        {
            if (request == null || !request.TryGetValue("view_name", out var viewNameValue) || string.IsNullOrWhiteSpace(viewNameValue?.ToString()))
            {
                return BadRequest(new ApiResponse { Success = false, Message = "Invalid request. Please provide 'view_name'." });
            }

            var viewName = viewNameValue.ToString()!;
            var parameters = request.Where(p => p.Key != "view_name");

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
                var sqlBuilder = new StringBuilder($"SELECT * FROM {viewName} WHERE 1=1");
                var dynamicParameters = new DynamicParameters();
                var filterCriteria = new List<string>();

                foreach (var param in parameters)
                {
                    if (param.Value == null || string.IsNullOrWhiteSpace(param.Value.ToString())) continue;

                    var keyParts = param.Key.Split(["__"], 2, StringSplitOptions.None);
                    var fieldName = keyParts[0];
                    var opKey = keyParts.Length > 1 ? keyParts[1] : "eq";

                    if (!OperatorMap.TryGetValue(opKey, out var sqlOperator))
                    {
                        return BadRequest(new ApiResponse { Success = false, Message = $"Operator shorthand '{opKey}' is not allowed." });
                    }

                    var paramName = $"@{param.Key.Replace("__", "_")}";
                    sqlBuilder.Append($" AND [{fieldName}] {sqlOperator} {paramName}");
                    filterCriteria.Add($"{fieldName} {sqlOperator} {param.Value}");

                    var paramValue = param.Value is JsonElement jsonElement ? ConvertJsonElement(jsonElement) : param.Value;
                    dynamicParameters.Add(paramName, paramValue);
                }

                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // Lấy dữ liệu và metadata đồng thời
                var queryTask = connection.QueryAsync(sqlBuilder.ToString(), dynamicParameters);
                var metadataTask = GetViewMetadataAsync(connection, viewName, filterCriteria);

                await Task.WhenAll(queryTask, metadataTask);

                var result = await queryTask;
                var metadata = await metadataTask;

                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Query executed successfully.",
                    Data = result,
                    Metadata = metadata
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

        private static async Task<Metadata> GetViewMetadataAsync(SqlConnection connection, string viewName, List<string> filters)
        {
            var metadata = new Metadata();
            var sql = @"SELECT COLUMN_NAME, DATA_TYPE 
                        FROM INFORMATION_SCHEMA.COLUMNS 
                        WHERE TABLE_NAME = @ViewName";

            var columns = await connection.QueryAsync(sql, new { ViewName = viewName });

            foreach (var column in columns)
            {
                metadata.Fields[column.COLUMN_NAME] = new FieldInfo { Type = column.DATA_TYPE };
            }

            metadata.Summary = new SummaryInfo
            {
                Description = $"Metadata for view '{viewName}'",
                TotalFields = metadata.Fields.Count,
                DataSource = viewName,
                FilterCriteria = filters.Count != 0 ? string.Join(" AND ", filters) : "None"
            };

            return metadata;
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