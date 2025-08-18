using System.Net;

namespace DynamicViewApi.Middleware
{
    public class IPWhitelistMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly List<IPAddress> _whitelist;

        public IPWhitelistMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            var ips = configuration.GetSection("HttpIpWhitelist").Get<string[]>();
            _whitelist = ips?.Select(IPAddress.Parse).ToList() ?? [];
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Chỉ kiểm tra IP nếu kết nối không phải là HTTPS
            if (!context.Request.IsHttps)
            {
                var remoteIp = context.Connection.RemoteIpAddress;

                if (remoteIp == null || !_whitelist.Contains(remoteIp))
                {
                    // Nếu IP không có trong danh sách trắng, trả về lỗi 403 Forbidden
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    await context.Response.WriteAsync("Forbidden: Your IP address is not allowed for HTTP access.");
                    return;
                }
            }

            // Nếu IP hợp lệ hoặc là kết nối HTTPS, cho phép đi tiếp
            await _next(context);
        }
    }
}