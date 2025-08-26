using DynamicViewApi.Models.Config; // Thêm dòng này
using DynamicViewApi.Models.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace DynamicViewApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("proxy")]
    public class ProxyController(IHttpClientFactory httpClientFactory, IOptions<ProxySettings> proxySettingsOptions) : ControllerBase
    {
        private readonly ProxySettings _proxySettings = proxySettingsOptions.Value;

        [HttpPost("forward")]
        public async Task<IActionResult> ForwardRequest([FromBody] Dictionary<string, object> request)
        {
            // 1. Kiểm tra các tham số bắt buộc
            if (request == null
                || !request.TryGetValue("orgcode", out var orgcodeValue) || string.IsNullOrWhiteSpace(orgcodeValue?.ToString())
                || !request.TryGetValue("endpoint", out var endpointValue) || string.IsNullOrWhiteSpace(endpointValue?.ToString()))
            {
                return BadRequest(new ApiResponse { Success = false, Message = "Invalid request. Please provide 'orgcode' and 'endpoint'." });
            }

            var orgcode = orgcodeValue.ToString()!;
            var endpointPath = endpointValue.ToString()!;
            var payload = request.TryGetValue("payload", out object? value) ? value : null;

            // 2. Lấy danh sách cấu hình Endpoints
            if (_proxySettings == null || _proxySettings.Endpoints.Count == 0)
            {
                return StatusCode(500, new ApiResponse { Success = false, Message = "Proxy endpoints are not configured." });
            }

            // 3. Tìm BaseUrl phù hợp dựa trên orgcode
            var endpointConfig = _proxySettings.Endpoints.FirstOrDefault(e => e.OrgCode.Equals(orgcode, StringComparison.OrdinalIgnoreCase))
                                 ?? _proxySettings.Endpoints.FirstOrDefault(e => e.OrgCode.Equals("default", StringComparison.OrdinalIgnoreCase));

            if (endpointConfig == null || string.IsNullOrEmpty(endpointConfig.BaseUrl))
            {
                return BadRequest(new ApiResponse { Success = false, Message = $"No proxy endpoint configured for orguid '{orgcode}' or a default endpoint." });
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

                // Chuyển tiếp header Authorization nếu có
                if (Request.Headers.TryGetValue("Authorization", out var authHeaderValue))
                {
                    requestMessage.Headers.Add("Authorization", authHeaderValue.ToString());
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