using DynamicViewApi.Models.Response;
using DynamicViewApi.Models.Config; // Thêm dòng này
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace DynamicViewApi.Controllers
{
    [ApiController]
    [Route("proxy")]
    public class ProxyController(IHttpClientFactory httpClientFactory, IConfiguration configuration) : ControllerBase
    {
        [HttpPost("forward")]
        public async Task<IActionResult> ForwardRequest([FromBody] Dictionary<string, object> request)
        {
            // 1. Kiểm tra các tham số bắt buộc
            if (request == null
                || !request.TryGetValue("orguid", out var orguidValue) || string.IsNullOrWhiteSpace(orguidValue?.ToString())
                || !request.TryGetValue("endpoint", out var endpointValue) || string.IsNullOrWhiteSpace(endpointValue?.ToString()))
            {
                return BadRequest(new ApiResponse { Success = false, Message = "Invalid request. Please provide 'orguid' and 'endpoint'." });
            }

            var orguid = orguidValue.ToString()!;
            var endpointPath = endpointValue.ToString()!;
            var payload = request.TryGetValue("payload", out object? value) ? value : null;

            // 2. Lấy danh sách cấu hình Endpoints
            var proxySettings = configuration.GetSection("ProxySettings").Get<ProxySettings>();
            if (proxySettings == null || proxySettings.Endpoints.Count == 0)
            {
                return StatusCode(500, new ApiResponse { Success = false, Message = "Proxy endpoints are not configured." });
            }

            // 3. Tìm BaseUrl phù hợp dựa trên orguid
            var endpointConfig = proxySettings.Endpoints.FirstOrDefault(e => e.Orguid.Equals(orguid, StringComparison.OrdinalIgnoreCase))
                                 ?? proxySettings.Endpoints.FirstOrDefault(e => e.Orguid.Equals("default", StringComparison.OrdinalIgnoreCase));

            if (endpointConfig == null || string.IsNullOrEmpty(endpointConfig.BaseUrl))
            {
                return BadRequest(new ApiResponse { Success = false, Message = $"No proxy endpoint configured for orguid '{orguid}' or a default endpoint." });
            }

            var baseUrl = endpointConfig.BaseUrl;

            // 4. Gửi request (phần logic này giữ nguyên)
            try
            {
                var client = httpClientFactory.CreateClient();
                var fullUrl = new Uri(new Uri(baseUrl), endpointPath);
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, fullUrl);

                if (payload != null)
                {
                    var jsonPayload = JsonSerializer.Serialize(payload);
                    requestMessage.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                }

                var response = await client.SendAsync(requestMessage);

                if (response.IsSuccessStatusCode)
                {
                    var responseData = await response.Content.ReadFromJsonAsync<object>();
                    return Ok(responseData);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, new ApiResponse
                    {
                        Success = false,
                        Message = $"Failed to forward request to {fullUrl}. Status code: {response.StatusCode}. Details: {errorContent}"
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse { Success = false, Message = $"Internal server error: {ex.Message}" });
            }
        }
    }
}