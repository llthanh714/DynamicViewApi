using Dapper;
using DynamicViewApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text;

namespace DynamicViewApi.Controllers
{
    [ApiController]
    [Route("api/view")]
    public class ViewController(IConfiguration configuration) : ControllerBase
    {
        private readonly string? sqlPassword = Environment.GetEnvironmentVariable("__DB_PASSWORD__", EnvironmentVariableTarget.Machine);

        // List of allowed views to query for security
        // IMPORTANT: Only add views here that you want to expose via the API
        private readonly HashSet<string> _allowedViews = new(StringComparer.OrdinalIgnoreCase)
        {
            "v_patients"
        };

        [HttpPost("query")]
        public async Task<IActionResult> QueryViewData([FromBody] ViewQueryRequest request)
        {
            // 1. Validate input
            if (request == null || string.IsNullOrWhiteSpace(request.ViewName))
            {
                return BadRequest("Invalid request. Please provide 'viewName'.");
            }

            // 2. SECURITY CHECK: Ensure viewName is in the allowed list
            if (!_allowedViews.Contains(request.ViewName))
            {
                return BadRequest($"Access to view '{request.ViewName}' is not permitted.");
            }

            try
            {
                var sqlBuilder = new StringBuilder($"SELECT * FROM {request.ViewName} WHERE 1=1");
                var dynamicParameters = new DynamicParameters();

                // 3. Build dynamic WHERE clause from parameters
                if (request.Parameters != null && request.Parameters.Count != 0)
                {
                    foreach (var param in request.Parameters)
                    {
                        // Only add condition if value is not empty
                        if (param.Value != null && !string.IsNullOrWhiteSpace(param.Value.ToString()))
                        {
                            // Add AND condition to SQL statement
                            sqlBuilder.Append($" AND [{param.Key}] = @{param.Key}");

                            // Add parameter to Dapper's DynamicParameters
                            // to prevent SQL Injection
                            dynamicParameters.Add($"@{param.Key}", param.Value);
                        }
                    }
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

                // 4. Execute the query
                using var connection = new SqlConnection(connectionString);
                var result = await connection.QueryAsync(sqlQuery, dynamicParameters);
                return Ok(result);
            }
            catch (SqlException ex)
            {
                // Return error if there's an SQL issue (e.g., invalid column name in parameters)
                return BadRequest($"Database query error: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Catch other general errors
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}